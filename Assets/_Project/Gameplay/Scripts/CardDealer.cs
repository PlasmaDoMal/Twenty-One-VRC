using System.Collections.Generic;
using System.Threading.Tasks;
using MenSharp;
using TMPro;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

/// <summary>
/// Compra as cartas da maquina e as entrega na mao dos jogadores.
///
/// O fluxo tem duas fases: a carta primeiro nasce na maquina
/// (<see cref="InstantiateFromEntry"/>) e so depois e interpolada ate a mao que
/// lhe cabe (<see cref="FlyToHand"/>). Uma compra por vez, com uma pausa entre
/// elas.
///
/// O dono do baralho e o unico que compra. Cada compra vira uma entrada no
/// historico sincronizado, e as outras maquinas refazem a mesa a partir dele em
/// <see cref="OnDeserialization"/>. O Udon nao instancia objeto em rede, entao a
/// carta viaja como dado e cada maquina cria a sua visualizacao, no mesmo lugar.
///
/// A mao de destino e um array, e o jogo alterna: a carta 0 vai para o
/// jogador 0, a 1 para o 1, a 2 para o 0 de novo. Como cada mao guarda as
/// cartas na ordem em que recebeu, comprar 10 cartas nao manda tudo para o
/// mesmo ponto — cada uma ocupa a proxima casa do seu leque, e as que se
/// sobrepoem ficam empilhadas uma sobre a outra.
///
/// A rodada comeca com <see cref="openingCards"/> cartas (<see cref="StartRound"/>)
/// e depois cada jogador, na sua vez, compra uma carta com
/// <see cref="RequestHit"/> ou fica com o que tem com <see cref="RequestStay"/>.
/// Quem esta na vez e <see cref="turnIndex"/>, que o dono do baralho controla e
/// ninguem consegue furar: a jogada chega pelo <see cref="PlayerSlot"/> do
/// jogador, e so o dono de um Slot escreve nos campos dele.
///
/// Tudo que e visual fica em campo publico: para trocar o visual da carta, o
/// lugar de cada mao ou de onde a carta sai, basta arrastar outro objeto no
/// Inspector.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class CardDealer : MenSharpBehaviour
{
    public bool preserveLobbySlots;

    public void PrepareLobbySession()
    {
        if (!IsDeckOwner()) return;
        AbortMatch();
        matchStarterPlayerId = 0;
    }
    public void AbortMatch()
    {
        if (!IsDeckOwner()) return;
        actionEpoch++;
        ClearHand();
        logCount = 0; logApplied = 0;
        logStructureRevision++;
        trumpCount = 0; tableTrumpCount = 0; usedTrumpCount = 0;
        matchStarted = false; matchOver = false; roundResolving = false;
        turnSecondsLeft = 0; turnDeadline = 0f; turnReadyAt = 0d;
        RequestSerialization();
        NotifyTurnChanged();
    }

    public void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (!IsDeckOwner()) return;
        slotSeqSeen = new int[slots.Length];
        for (int i = 0; i < slots.Length; i++) slotSeqSeen[i] = processedActionSeq[i];
        turnDeadline = turnSecondsLeft > 0 ? Time.time + turnSecondsLeft : 0f;
        NotifyTurnChanged();
    }

    // IDs estaveis para UI e rede. 1..6 representam as cartas numericas 2..7.
    public const int TrumpGo17 = 7, TrumpGo24 = 8, TrumpGo27 = 9;
    public const int TrumpOneUp = 10, TrumpTwoUp = 11, TrumpShield = 12;
    public const int TrumpShieldPlus = 13, TrumpBless = 14, TrumpBloodshed = 15;
    public const int TrumpDestroy = 16, TrumpReincarnation = 17, TrumpFriendship = 18;
    public const int TrumpHush = 19, TrumpPerfectDraw = 20, TrumpRemove = 21;
    public const int TrumpReturn = 22, TrumpExchange = 23, TrumpDisservice = 24;
    public const int TrumpRefresh = 25;

    [Header("References")]
    [Tooltip("Prefab instanciado a cada carta.")]
    public GameObject cardPrefab;

    [Tooltip("De onde a carta sai. Se vazio, usa a posicao deste objeto.")]
    public Transform machineAnchor;

    [Tooltip("Centro da mao de cada jogador. As cartas viram filha da maa que lhes cabe.")]
    public Transform[] handAnchors;

    [Tooltip("Textos dos ScoreCubes, na mesma ordem das maos: Player1 = 0, Player2 = 1.")]
    public TMP_Text[] scoreTexts;
    [Tooltip("Opponent-facing scores, in the same player order as scoreTexts.")]
    public TMP_Text[] enemyScoreTexts;

    [Header("Round")]
    [Tooltip("Cartas da abertura no total. Com dois jogadores, 4 da 1 secreta e 1 normal para cada um.")]
    public int openingCards = 4;

    [Header("Special card")]
    [Tooltip("Marca como especial a primeira carta que cada jogador recebe na abertura. Como o baralho esta embaralhado, o numero delas tambem e sorteado.")]
    public bool markFirstCardSpecial = true;

    [Tooltip("Material das cartas especiais. Vazio = nao troca nada.")]
    public Material hiddenMaterial;

    [Tooltip("Esconde a face da primeira carta numerica de cada jogador. A carta oculta nao e uma trump.")]
    public bool hideSpecialCards = true;

    [Header("Deck")]
    [Tooltip("Menor numero do baralho. No Twenty One comeca em 1.")]
    public int deckMinValue = 1;

    [Tooltip("Maior numero do baralho, e tambem o limite de cartas da rodada. No Twenty One vai ate 11.")]
    public int deckMaxValue = 11;

    [Tooltip("Semente do embaralhamento. E sincronizada, entao todo mundo monta a mesma ordem; o dono sorteia uma nova por rodada.")]
    [UdonSynced] public int actionEpoch;
    [UdonSynced] public int hitSoundSequence;
    [UdonSynced] public int[] processedActionSeq = new int[2];
    [UdonSynced] public int[] processedActionEpoch = new int[2];
    [UdonSynced] public int[] acceptedActionSeq = new int[2];
    [UdonSynced] public int deckSeed = 12345;

    [Header("Network")]
    [Tooltip("Quantas compras cabem no historico que vai pela rede. Uma rodada do Twenty One usa 11, entao sobram folga.")]
    public int logCapacity = 32;

    [Tooltip("Embaralha o baralho antes de comecar a rodada.")]
    public bool shuffleDeck = true;

    [Tooltip("Loga no console o baralho montado e cada carta comprada.")]
    public bool logDeals = true;

    [Header("Turn")]
    [Tooltip("De quem e a vez, espelhado de turnIndex para a UI destacar a mao. Nao e a fonte da verdade: quem decide o turno e turnIndex.")]
    [UdonSynced] public int currentPlayer = 0;

    [Header("Hand layout")]
    [Tooltip("Cartas por fileira antes de comecar a proxima.")]
    public int cardsPerRow = 5;

    [Tooltip("Espaco entre cartas vizinhas, medido na largura da carta. 0 = todas uma sobre a outra. 0.5 = metade da carta de fora. 1 = cada uma da largura dela de folga, nenhuma encosta na outra. Acima de 1 abre ainda mais.")]
    public float cardGap = 1f;

    [Tooltip("Distancia entre fileiras, medida no comprimento da carta. 1 = uma fileira de cartao de distancia.")]
    public float rowGap = 1.2f;

    [Tooltip("Altura adicional por carta, em metros no mundo. 0 = todas na mesma altura. Aumente apenas se houver sobreposicao.")]
    public float layerThickness = 0f;

    [Tooltip("Abertura do leque, em graus por carta.")]
    public float fanAngle = 3f;

    [Header("Animation")]
    [Tooltip("Duracao do voo de uma carta.")]
    public float dealDuration = 0.35f;

    [Tooltip("Pausa entre uma carta pousar e a proxima sair.")]
    public float delayBetweenCards = 0.1f;

    [Tooltip("Altura do arco do voo.")]
    public float arcHeight = 0.3f;

    [Tooltip("Vira a carta durante o voo, em graus. 180 = vira de costas.")]
    public float flipAngle = 180f;

    [Tooltip("Escala da carta no inicio do voo, em fracao do tamanho final.")]
    public float startScale = 0.6f;

    [Header("Card look")]
    [Tooltip("Material de cada carta, na ordem dos valores do baralho: o primeiro material e o deckMinValue, e assim por diante. A carta comprada recebe o material do proprio numero.")]
    public Material[] cardMaterials;

    // Legacy serialized field; placement now follows each hand anchor.
    [HideInInspector]
    public float cardWorldY = 1.0825f;

    [Header("Round rules")]
    [Tooltip("Alvo para ganhar a rodada. No original e 21.")]
    public int targetScore = 21;

    [Tooltip("Quanto de vida o perdedor da rodada perde.")]
    public int roundDamage = 1;

    [Tooltip("Aumento da aposta a cada rodada. 1 segue a progressao descrita nas regras; 0 mantem o dano fixo.")]
    public int roundDamageGrowth = 1;

    [Tooltip("Vida inicial de cada jogador. A partida acaba quando a vida de alguem chega a zero.")]
    public int startingLife = 20;

    [Tooltip("Numero maximo de rodadas. 0 = sem limite, e a partida so acaba quando a vida de alguem zera.")]
    public int maxRounds = 0;

    [Tooltip("Duas passadas seguidas encerram a rodada. E a regra do original.")]
    public bool twoStaysEndRound = true;

    [Tooltip("Segundos para agir antes de perder a rodada por tempo. 0 desliga o timeout. A TV CRT mostra esta contagem.")]
    public float turnTimeoutSeconds = 60f;

    [Tooltip("Cartas de tarot que cada jogador recebe por rodada.")]
    public int trumpCardsPerRound = 2;

    [Tooltip("Numero maximo de trumps na mao de cada jogador.")]
    public int maxTrumpsPerPlayer = 8;

    [Tooltip("Chance percentual de ganhar uma trump ao comprar com Hit. Nao vale para Disservice.")]
    public int bonusTrumpChancePercent = 20;

    [Tooltip("Limpa as trumps nao usadas no inicio de cada rodada.")]
    public bool clearTrumpsEachRound = false;

    [Tooltip("Capacidade do historico de trumps colocadas na mesa.")]
    public int tableTrumpCapacity = 32;

    [Header("Network")]
    [Tooltip("Via de entrada de cada jogador na mesa. Dono do baralho preenche sozinho; e aqui que o turno e validado.")]
    public PlayerSlot[] slots;

    [Header("Turn events")]
    [Tooltip("Menus que reagem a virada da vez. Cada um e avisado por OnTurnChanged, e decide sozinho se a vez e dele.")]
    public TurnFadeIn[] turnListeners;

    [Tooltip("Loga cada vez que a vez muda e para quem ela foi.")]
    public bool logTurnEvents = false;

    [Tooltip("Loga no console as jogadas aceitas e as recusadas por vez errada.")]
    public bool logTurns = true;

    private List<GameObject> hand = new List<GameObject>();
    private List<int> handOf = new List<int>();
    private List<int> slotOf = new List<int>();
    private List<int> cardValue = new List<int>();
    private List<bool> special = new List<bool>();
    private List<int> pendingFly = new List<int>();
    private int[] deck = new int[0];
    private int dealIndex = 0;
    private uint randomState = 0;
    private bool hasBuilt = false;
    private int lastBuiltSeed = 0;
    private bool openingDealt = false;
    private bool dealing = false;
    private int flying = 0;
    private int dealGeneration = 0;

    // Historico de compras da rodada. O dono escreve aqui e manda pela rede; os
    // outros leem e refazem a mesa na mesma ordem. E o que mantem as tres
    // maquinas com a mesma carta na mao, mesmo com o atraso de ida e volta da
    // compra. O Udon nao instancia objeto em rede, entao a carta viaja como
    // dado e cada maquina cria a sua visualizacao.
    //
    // Publicos de proposito: no MenSharp um campo privado vira estatico
    // compartilhado. Ha um CardDealer so na mesa, entao aqui nao mudaria nada,
    // mas assim o campo nao vira armadilha se alguem colocar um segundo.
    [UdonSynced] public int logCount = 0;
    [UdonSynced] public int[] logValue;
    [UdonSynced] public int[] logOwner;
    [UdonSynced] public int[] logActive;
    [UdonSynced] public int[] logHidden;
    [UdonSynced] public int logStructureRevision = 0;
    private int logApplied = 0;
    private int lastStructureRevision = 0;

    // Trumps sao dados, sem prefab por enquanto. A UI futura le os tipos por
    // TrumpAt e usa RequestUseTrump com o indice na propria mao.
    [UdonSynced] public int[] trumpType;
    [UdonSynced] public int[] trumpOwner;
    [UdonSynced] public int trumpCount = 0;
    [UdonSynced] public int[] tableTrumpType;
    [UdonSynced] public int[] tableTrumpOwner;
    [UdonSynced] public int tableTrumpCount = 0;
    // Presentation history is separate from the trumps whose effects remain active.
    [UdonSynced] public int[] usedTrumpType = new int[64];
    [UdonSynced] public int[] usedTrumpOwner = new int[64];
    [UdonSynced] public int usedTrumpCount;
    [UdonSynced] public int baseBet = 1;
    [UdonSynced] public int hookMask = 0;

    // Estado da partida. Tudo synced, e so o dono do baralho escreve, entao os
    // tres clientes veem a mesma vez, a mesma rodada e a mesma vida.
    [UdonSynced] public int turnIndex = 0;
    [UdonSynced] public bool matchStarted = false;
    [UdonSynced] public int roundNumber = 1;
    [UdonSynced] public int matchStarterPlayerId = 0;
    [UdonSynced] public int consecutiveStays = 0;
    [UdonSynced] public int[] life;
    [UdonSynced] public bool matchOver = false;

    // Segundos inteiros que faltam no turno, para os mostradores locais (as TVs
    // CRT). So o dono escreve, junto da contagem; 0 = fora de uma contagem
    // valendo. Como o valor viaja pela rede, todos os clientes desenham o mesmo
    // MM:SS sem depender do proprio relogio.
    [UdonSynced] public int turnSecondsLeft = 0;

    // Vencedor da ultima rodada (0 ou 1), para a TV mostrar YouWon/YouLost a
    // partir de quem venceu. -1 = sem resultado (inicio da partida, empate ou
    // espectador). Diferente de roundWinner, este NAO e limpo no StartRound, so
    // no proximo FinishRound (ou no StartMatch), entao o resultado continua a
    // vista enquanto a rodada seguinte ainda distribui as cartas.
    [UdonSynced] public int lastRoundWinner = -1;
    [UdonSynced] public int machineTargetPlayer = -1;
    [UdonSynced] public bool roundResolving;
    [UdonSynced] public double roundEndAt;
    [UdonSynced] public int lastTimeoutPlayer = -1;
    [UdonSynced] public int resolvedBet;
    public float resultDisplaySeconds = 5f;
    public float turnTransitionSeconds = 2f;
    [UdonSynced] public double turnReadyAt;

    // Ultima jogada processada de cada Slot. E o que impede a mesma intencao de
    // ser executada duas vezes, sem precisar limpar nada no Slot.
    private int[] slotSeqSeen = new int[0];
    private int roundWinner = -1;
    private float turnDeadline = 0f;

    // Ultima vez transmitida aos ouvintes, para o aviso sair so na mudanca.
    // Publicos de proposito: no MenSharp um campo privado vira estatico.
    [HideInInspector] public bool turnEventActionable = false;
    [HideInInspector] public int turnEventPlayer = -1;

    // ------------------------------------------------------------------ eventos

    /// <summary>
    /// True quando ha jogada para fazer: a partida comecou, a abertura ja saiu,
    /// nenhuma carta esta no ar e a partida nao acabou.
    ///
    /// E o mesmo portao que a UI usava antes de virar evento — sem ele o menu
    /// apareceria na abertura, com a mao ainda vazia, e de novo a cada carta
    /// que estivesse voando.
    /// </summary>
    public bool TurnIsActionable()
    {
        if (!matchStarted || matchOver || roundResolving)
        {
            return false;
        }
        if (Networking.GetServerTimeInSeconds() < turnReadyAt) return false;
        if (!openingDealt)
        {
            return false;
        }
        if (dealing || pendingFly.Count > 0)
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// Avisa os menus que a vez mudou. E o unico lugar que dispara evento: ele
    /// guarda o ultimo estado transmitido e so fala quando ele muda de fato,
    /// entao os menus nao precisam deduplicar nem ficam sondando por quadro.
    ///
    /// O aviso sai para os <i>dois</i> lados: o dono do baralho chama aqui
    /// direto, e os outros clientes quando o estado chega por
    /// <see cref="OnDeserialization"/>. Por isso nao e
    /// <c>SendCustomNetworkEvent</c>: o estado da vez ja viaja nos campos
    /// sincronizados, entao so falta reactsar localmente quando ele muda.
    /// </summary>
    public void NotifyTurnChanged()
    {
        bool actionable = TurnIsActionable();
        int player = actionable ? turnIndex : -1;
        // O log vem antes da deduplicacao de proposito: e o unico registro de
        // que o portao foi avaliado. Depois do return, um estado que nao mudou
        // nao deixa rastro, e nao da para distinguir "avaliado e ja estava
        // certo" de "nunca chegou a avaliar".
        if (logTurnEvents)
        {
            Debug.Log("CardDealer: TurnIsActionable=" + actionable + " (matchStarted=" + matchStarted
                + ", matchOver=" + matchOver + ", openingDealt=" + openingDealt
                + ", dealing=" + dealing + ", pendingFly=" + pendingFly.Count
                + ", turno=" + turnIndex + ")");
        }
        if (actionable == turnEventActionable && player == turnEventPlayer)
        {
            return;
        }
        turnEventActionable = actionable;
        turnEventPlayer = player;
        if (logTurnEvents)
        {
            Debug.Log("CardDealer: vez mudou para o jogador " + player
                + (actionable ? "" : " (sem jogada no momento)."));
        }
        if (turnListeners == null)
        {
            return;
        }
        bool solo = IsLocalPlayer(0) && IsLocalPlayer(1);
        for (int i = 0; i < turnListeners.Length; i++)
        {
            TurnFadeIn listener = turnListeners[i];
            if (listener == null)
            {
                continue;
            }
            // Chamada direta, nao SendCustomEvent: o Udon so exporta como evento
            // os metodos sem argumentos, e um OnTurnChanged(bool, int) nunca
            // chegaria ao menu. O estado vai em dois campos e o ApplyTurn(), sem
            // parametros, so aplica.
            listener.incomingActionable = actionable;
            listener.incomingTurnPlayer = player;
            listener.incomingSolo = solo;
            listener.ApplyTurn();
        }
    }

    /// <summary>Quantas cartas estao na mesa, somando as duas maos.</summary>
    public int CardCount
    {
        get { return hand.Count; }
    }

    /// <summary>Quantas maos existem, isto e, quantos jogadores.</summary>
    public int PlayerCount
    {
        get { return HandCount(); }
    }

    /// <summary>True enquanto ainda tem carta na fila ou no ar.</summary>
    public bool IsDealing
    {
        get { return dealing; }
    }

    /// <summary>As cartas de abertura desta rodada ja foram compradas.</summary>
    public bool OpeningHandDealt
    {
        get { return openingDealt; }
    }

    /// <summary>Quantas cartas tem no baralho desta rodada.</summary>
    public int DeckCount
    {
        get { return deck != null ? deck.Length : 0; }
    }

    /// <summary>
    /// Quantas cartas ainda faltam sair. E o limite da rodada: somando as duas
    /// maos, nunca passa de <see cref="DeckCount"/>.
    /// </summary>
    public int DeckRemaining
    {
        get { return DeckCount - dealIndex; }
    }

    /// <summary>True quando nao ha mais carta nenhuma para comprar.</summary>
    public bool DeckEmpty
    {
        get { return DeckRemaining <= 0; }
    }

    /// <summary>
    /// O numero da carta <paramref name="cardIndex"/> da mesa, na ordem em que
    /// foi comprada. Vale o que saiu do baralho, nao a posicao no leque.
    /// </summary>
    public int CardValue(int cardIndex)
    {
        return cardIndex >= 0 && cardIndex < cardValue.Count ? cardValue[cardIndex] : 0;
    }

    /// <summary>Quantas cartas o jogador <paramref name="playerIndex"/> tem na mao.</summary>
    public int CardsInHand(int playerIndex)
    {
        int player = ClampPlayer(playerIndex);
        int count = 0;
        for (int i = 0; i < handOf.Count; i++)
        {
            if (handOf[i] == player)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>Soma dos numeros na mao do jogador. E o placar do 21.</summary>
    public int HandTotal(int playerIndex)
    {
        int player = ClampPlayer(playerIndex);
        int sum = 0;
        for (int i = 0; i < handOf.Count; i++)
        {
            if (handOf[i] == player)
            {
                sum += cardValue[i];
            }
        }
        return sum;
    }

    [HideInInspector] public float nextCardVisibilityRefresh;

    public float nextSlotPoll;
    public void Update()
    {
        if (Time.time >= nextSlotPoll)
        {
            nextSlotPoll = Time.time + 0.05f;
            ProcessSlots();
            NotifyTurnChanged();
        }
        if (Time.time < nextCardVisibilityRefresh) return;
        nextCardVisibilityRefresh = Time.time + 0.2f;
        RefreshCardVisibility();
    }

    /// <summary>Only the local hand owner sees a hidden face before the result.</summary>
    public void RefreshCardVisibility()
    {
        for (int i = 0; i < hand.Count; i++)
            if (hand[i] != null)
                ApplyCardMaterial(hand[i], cardValue[i], special[i], handOf[i]);
    }

    public void Start()
    {
        nextCardVisibilityRefresh = 0f;
        RefreshScores();

        // Fecha a partida que ja estava valendo quando este cliente entrou, para
        // um menu que ficou aceso no outro cliente nao sobreviver a carga.
        NotifyTurnChanged();
    }

    private async Task WatchSlots()
    {
        while (true)
        {
            ProcessSlots();
            await Scheduler.NextFrame();
        }
    }

    /// <summary>Atualiza cada placar a partir da mao reconstruida neste cliente.</summary>
    public void RefreshScores()
    {
        for (int i = 0; scoreTexts != null && i < scoreTexts.Length; i++)
        {
            TMP_Text scoreText = scoreTexts[i];
            if (scoreText != null)
            {
                int total = i < HandCount() ? HandTotal(i) : 0;
                scoreText.text = total + "/" + EffectiveTarget();
            }
        }
        for (int i = 0; enemyScoreTexts != null && i < enemyScoreTexts.Length; i++)
        {
            if (enemyScoreTexts[i] != null) enemyScoreTexts[i].text = EnemyHandScore(i);
        }
    }

    /// <summary>Public total excludes all still-hidden cards, including Hush.</summary>
    public string EnemyHandScore(int playerIndex)
    {
        int visibleTotal = 0;
        bool concealed = false;
        bool reveal = !hideSpecialCards || roundResolving || matchOver;
        for (int i = 0; i < handOf.Count; i++)
        {
            if (handOf[i] != playerIndex) continue;
            if (!reveal && i < special.Count && special[i]) concealed = true;
            else visibleTotal += cardValue[i];
        }
        return (concealed ? "?" : "") + visibleTotal + "/" + EffectiveTarget();
    }

    // ------------------------------------------------------------------ api

    /// <summary>
    /// Quem manda no baralho. No editor e no single player nao ha dono de rede,
    /// entao quem chama ja manda.
    /// </summary>
    private bool IsDeckOwner()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        return local == null || Networking.IsOwner(local, gameObject);
    }

    /// <summary>
    /// Cria os buffers do historico se ainda nao existirem. So o dono escreve
    /// neles; nas outras maquinas eles ficam em zero, e tanto faz.
    /// </summary>
    private void EnsureLogBuffers()
    {
        if (logValue != null && logOwner != null && logActive != null && logHidden != null
            && logValue.Length == logCapacity && logOwner.Length == logCapacity
            && logActive.Length == logCapacity && logHidden.Length == logCapacity)
        {
            return;
        }
        logValue = new int[logCapacity];
        logOwner = new int[logCapacity];
        logActive = new int[logCapacity];
        logHidden = new int[logCapacity];
    }

    /// <summary>
    /// Refaz na mesa tudo que ainda nao foi aplicado. O dono ja aplicou na
    /// hora, entao o cursor dele esta no fim e nao ha nada a fazer; nas outras
    /// maquinas e aqui que as cartas aparecem.
    /// </summary>
    private void ApplyLog()
    {
        EnsureLogBuffers();
        while (logApplied < logCount)
        {
            if (logValue[logApplied] <= 0)
            {
                Debug.LogWarning("CardDealer: entrada invalida no historico: " + logApplied + ".");
                break;
            }
            // Espelha tambem a troca feita por cartas numericas de tarot. Isso
            // mantem as cartas restantes iguais se a posse do baralho mudar.
            if (!TakeLoggedValue(logValue[logApplied]))
            {
                Debug.LogWarning("CardDealer: valor repetido ou ausente no historico: "
                    + logValue[logApplied] + ".");
                break;
            }
            if (logActive[logApplied] != 0 && !InstantiateFromEntry(logApplied, true))
            {
                break;
            }
            logApplied++;
        }
        if (matchStarted && !matchOver && logApplied >= openingCards)
        {
            openingDealt = true;
        }
        if (pendingFly.Count > 0)
        {
            StartDraining();
        }
    }

    /// <summary>
    /// Compra uma carta e escreve no historico. So o dono chama isto; a carta
    /// sai para todo mundo por <see cref="OnDeserialization"/>, e nao por
    /// instancia de objeto em rede, que o Udon nao faz.
    /// </summary>
    private bool OwnerDeal(int target, bool hidden)
    {
        EnsureDeck();
        if (DeckEmpty)
        {
            if (logDeals)
            {
                Debug.Log("CardDealer: baralho acabou, o hit do jogador " + target + " foi ignorado.");
            }
            return false;
        }
        EnsureLogBuffers();
        if (logCount >= logCapacity)
        {
            if (logDeals)
            {
                Debug.Log("CardDealer: historico cheio (" + logCapacity + "), a compra foi ignorada.");
            }
            return false;
        }

        logValue[logCount] = TakeNextValue();
        logOwner[logCount] = target;
        logActive[logCount] = 1;
        logHidden[logCount] = hidden ? 1 : 0;
        logCount++;

        // o dono aplica na hora e ja fica com o cursor no fim
        logApplied = logCount;
        InstantiateFromEntry(logCount - 1, true);
        RequestSerialization();
        return true;
    }

    private bool OwnerDealNumber(int target, int number, bool hidden)
    {
        EnsureDeck();
        for (int i = dealIndex; i < deck.Length; i++)
        {
            if (deck[i] == number)
            {
                int swap = deck[dealIndex];
                deck[dealIndex] = deck[i];
                deck[i] = swap;
                return OwnerDeal(target, hidden);
            }
        }
        return false;
    }

    /// <summary>
    /// Abre a rodada. Quem nao tem o baralho so pede: quem tem sorteia a
    /// semente nova, zera o historico e manda pela rede, e o historico de
    /// compras que faz as tres maquinas comprarem as mesmas cartas, na mesma
    /// ordem. Offline (editor, single player) nao ha dono, entao segue direto.
    /// </summary>
    private void StartRound()
    {
        if (!matchStarted || matchOver || roundResolving)
        {
            return;
        }

        actionEpoch++;
        if (!NewSeed())
        {
            EndMatch(-1);
            return;
        }
        EnsureLogBuffers();
        logCount = 0;
        logApplied = 0;
        logStructureRevision++;
        lastStructureRevision = logStructureRevision;
        consecutiveStays = 0;
        hookMask = 0;
        tableTrumpCount = 0;
        usedTrumpCount = 0;
        EnsureTrumpBuffers();
        if (clearTrumpsEachRound)
        {
            trumpCount = 0;
        }
        roundWinner = -1;
        // a rodada contada em diante: StartMatch zera antes de chamar, e
        // FinishRound chama este metodo no fim de cada rodada, entao o numero
        // que aparece no log e sempre o da rodada que esta começando
        roundResolving = false;
        turnReadyAt = 0d;
        roundNumber++;
        turnIndex = 0;
        currentPlayer = 0;
        // a vez recomeca no primeiro Slot com jogador, para o turno nunca ficar
        // preso num lugar vazio
        int first = FirstOccupiedSlot();
        if (first >= 0)
        {
            turnIndex = first;
            currentPlayer = first;
        }
        RequestSerialization();
        ApplySeed();
        for (int i = 0; i < trumpCardsPerRound; i++)
        {
            DrawTrump(0);
            DrawTrump(1);
        }
        RequestSerialization();
        ResetTurnDeadline();
        // A rodada abriu e a vez foi definida. O evento vai aqui e nao so no fim
        // do voo das cartas porque TurnIsActionable ainda e false neste momento
        // (openingDealt acaba de virar, o baralho pode ainda estar saindo), e
        // quem fecha o portao e o DrainQueue.
        NotifyTurnChanged();
    }

    /// <summary>
    /// Comeca a partida. E o dono do baralho quem sorteia a semente e quem
    /// guarda o estado inteiro, entao a semente nova vai pela rede como parte do
    /// mesmo estado — nao ha segunda mensagem nem janela em que um cliente pode
    /// montar um baralho diferente.
    ///
    /// So quem started a partida pode comecar de novo. Quem started e o primeiro
    /// jogador da mesa, e o dono do baralho guarda o id dele; todo mundo le o
    /// mesmo valor, entao os tres clientes concordam em quem started.
    /// </summary>
    private void StartMatch()
    {
        if (!MatchCanStart())
        {
            return;
        }

        AssignSlots();
        if (slotSeqSeen == null || slotSeqSeen.Length != slots.Length)
        {
            slotSeqSeen = new int[slots.Length];
        }
        for (int i = 0; i < slots.Length; i++)
        {
            slotSeqSeen[i] = slots[i] != null ? slots[i].ActionSeq : 0;
        }
        VRCPlayerApi starter = FirstSlotPlayer();
        if (starter != null)
        {
            matchStarterPlayerId = starter.playerId;
        }
        matchOver = false;
        matchStarted = true;
        // StartRound incrementa roundNumber, entao comeca em zero aqui para a
        // primeira rodada ser a 1
        roundNumber = 0;
        roundWinner = -1;
        lastRoundWinner = -1;
        machineTargetPlayer = -1;
        lastTimeoutPlayer = -1;
        roundResolving = false;
        EnsureLife();
        for (int i = 0; i < life.Length; i++)
        {
            life[i] = startingLife;
        }
        EnsureTrumpBuffers();
        trumpCount = 0;
        baseBet = roundDamage;
        if (logTurns)
        {
            Debug.Log("CardDealer: partida comecada por " + matchStarterPlayerId
                + ". Vida " + startingLife + ", aposta inicial " + roundDamage
                + ", alvo " + targetScore + ".");
        }
        StartRound();
    }

    /// <summary>
    /// A UI chama isto. So o jogador que started a partida consegue reiniciar,
    /// e o dono do baralho confere isso de novo antes de obedecer.
    /// </summary>
    public void RequestStartMatch()
    {
        if (!IsMatchStarter())
        {
            if (logTurns)
            {
                Debug.Log("CardDealer: pedido de iniciar partida recusado, so o dono do Slot 0 pode.");
            }
            return;
        }
        if (slots != null && slots.Length > 0 && slots[0] != null)
        {
            slots[0].RequestStartMatch();
        }
    }

    /// <summary>
    /// Este jogador pode iniciar ou reiniciar a partida?
    ///
    /// Antes da primeira jogada, o dono do Slot 0 pode iniciar. O pedido passa
    /// pelo Slot, que autentica o jogador antes de o dono do baralho processar.
    ///
    /// Depois que a partida comeca, so quem started reinicia. Todo mundo le o
    /// mesmo <see cref="matchStarterPlayerId"/>, entao os tres clientes concordam
    /// em quem started.
    /// </summary>
    public bool IsMatchStarter()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null)
        {
            return true;
        }
        if (!matchStarted)
        {
            // Antes da primeira jogada vale o dono do baralho, e nao o dono do
            // Slot 0.
            //
            // A Razão e a posse: os Slots pertencem ao master ate
            // <see cref="AssignSlots"/> rodar, e AssignSlots só roda dentro de
            // <see cref="StartMatch"/>. Se o teste fosse slots[0].IsMine(),
            // nenhum jogador passaria — o Slot 0 é do master, o master não
            // pode iniciar porque não é dono de si mesmo no Slot, e a partida
            // nunca começaria. O dono do baralho é sempre alguém.
            return IsDeckOwner();
        }
        return local.playerId == matchStarterPlayerId;
    }

    /// <summary>
    /// O jogador local manda no baralho? E o que separa "criar partida" de
    /// "entrar partida" no menu: quem tem o baralho arbitra a partida inteira,
    /// e os demais so ocupam uma vaga.
    ///
    /// Publico porque o menu vive em outro programa Udon, e um programa so
    /// alcanca o outro pelos membros publicos — o <c>gameObject</c> de outro
    /// comportamento nao e acessivel de la.
    /// </summary>
    public bool LocalPlayerOwnsDeck()
    {
        return IsDeckOwner();
    }

    private bool MatchCanStart()
    {
        if (!IsReady())
        {
            if (logTurns)
            {
                Debug.Log("CardDealer: a mesa ainda nao esta pronta (IsReady false).");
            }
            return false;
        }
        if (slots == null || slots.Length < HandCount() || HandCount() != 2)
        {
            if (logTurns)
            {
                Debug.Log("CardDealer: configure exatamente duas maos e um Slot por jogador.");
            }
            return false;
        }
        int deckSize = Mathf.Abs(deckMaxValue - deckMinValue) + 1;
        if (openingCards < HandCount() * 2 || openingCards > deckSize
            || logCapacity < deckSize || maxTrumpsPerPlayer < trumpCardsPerRound
            || maxTrumpsPerPlayer < 1 || trumpCardsPerRound < 0
            || tableTrumpCapacity < 1)
        {
            Debug.LogWarning("CardDealer: confira abertura, baralho, historico e capacidade das trumps.");
            return false;
        }
        for (int i = 0; i < HandCount(); i++)
        {
            if (slots[i] == null)
            {
                Debug.LogWarning("CardDealer: Slot " + i + " nao esta configurado.");
                return false;
            }
        }
        if (VRCPlayerApi.GetPlayerCount() < HandCount() && !(preserveLobbySlots && slots[0].IsMine() && slots[1].IsMine()))
        {
            if (logTurns)
            {
                Debug.Log("CardDealer: aguardando os dois jogadores para iniciar a partida.");
            }
            return false;
        }
        if (logTurns)
        {
            // A partida vai comecar. Este log e o que distingue "recusado" de
            // "nao chegou a tentar": sem ele, uma partida que nao sobe nao deixa
            // rastro nenhum no console.
            Debug.Log("CardDealer: MatchCanStart aprovado, a partida pode comecar.");
        }
        return true;
    }

    /// <summary>Primeiro Slot com jogador na mesa, ou -1.</summary>
    private int FirstOccupiedSlot()
    {
        if (slots == null)
        {
            return -1;
        }
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].HasPlayer())
            {
                return i;
            }
        }
        return -1;
    }

    private VRCPlayerApi FirstSlotPlayer()
    {
        int index = FirstOccupiedSlot();
        if (index < 0)
        {
            return null;
        }
        return slots[index].OwnerPlayer();
    }

    /// <summary>
    /// Da a mesa ao primeiro jogador que entrou, o segundo, e assim por diante,
    /// Define a posicao de cada Slot e passa a posse dele para o jogador. E o
    /// que amarra a mao 0 a um jogador especifico: sem isso o primeiro botao a
    /// apertar seria o dono.
    /// </summary>
    private void AssignSlots()
    {
        if (preserveLobbySlots) return;
        if (slots == null)
        {
            return;
        }
        // Usa a mesma ordem de playerId que OwnerOnlyUIVisibility. Assim o
        // menu visivel e sempre o do Slot que recebe a jogada.
        VRCPlayerApi[] online = VRCPlayerApi.GetPlayers();
        for (int i = 1; i < online.Length; i++)
        {
            VRCPlayerApi key = online[i];
            int j = i - 1;
            while (j >= 0 && online[j].playerId > key.playerId)
            {
                online[j + 1] = online[j];
                j--;
            }
            online[j + 1] = key;
        }
        VRCPlayerApi[] players = new VRCPlayerApi[slots.Length];
        int count = 0;
        for (int i = 0; i < online.Length && count < slots.Length; i++)
        {
            if (online[i] != null)
            {
                players[count] = online[i];
                count++;
            }
        }
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
            {
                continue;
            }
            if (i >= count)
            {
                slots[i].Assign(i, "");
                continue;
            }
            slots[i].GiveOwnershipTo(players[i]);
            slots[i].Assign(i, players[i].displayName);
            if (logTurns)
            {
                Debug.Log("CardDealer: Slot " + i + " e de " + players[i].displayName + ".");
            }
        }
    }


    /// <summary>
    /// Monta o baralho da rodada: uma carta de cada numero entre
    /// <see cref="deckMinValue"/> e <see cref="deckMaxValue"/>, embaralhado. E
    /// daqui que sai o limite de cartas: <see cref="deckMaxValue"/> - a
    /// abertura ja tira algumas, entao o resto se divide entre os dois ate
    /// acabar.
    /// </summary>
    private void BuildDeck()
    {
        int min = deckMinValue;
        int max = deckMaxValue;
        if (max < min)
        {
            int swap = min;
            min = max;
            max = swap;
        }

        deck = new int[max - min + 1];
        for (int i = 0; i < deck.Length; i++)
        {
            deck[i] = min + i;
        }
        if (shuffleDeck)
        {
            for (int i = deck.Length - 1; i > 0; i--)
            {
                int j = NextRandomBelow(i + 1);
                int taken = deck[i];
                deck[i] = deck[j];
                deck[j] = taken;
            }
        }
        dealIndex = 0;
        lastBuiltSeed = deckSeed;
        hasBuilt = true;

        if (logDeals)
        {
            Debug.Log("CardDealer: baralho de " + deck.Length + " cartas (" + min + " a " + max
                + ", semente " + deckSeed + "): " + DeckString());
        }
    }

    /// <summary>
    /// Guarda o estado do gerador. O xorshift32 anda nos mesmos numeros em
    /// qualquer cliente: e o que faz todo mundo sortear a mesma ordem a partir
    /// da mesma <see cref="deckSeed"/>. O <c>UnityEngine.Random</c> nao serve,
    /// porque cada cliente tem o proprio.
    /// </summary>
    private void SeedRandom(int seed)
    {
        uint state = (uint)seed;
        if (state == 0u)
        {
            state = 2463534242u;
        }
        randomState = state;
    }

    private uint NextRandom()
    {
        uint x = randomState;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        randomState = x;
        return x;
    }

    private int NextRandomBelow(int limit)
    {
        if (limit <= 1)
        {
            return 0;
        }
        return (int)(NextRandom() % (uint)limit);
    }

    /// <summary>
    /// Garante que o baralho da mesa bate com a semente sincronizada. Chamado
    /// antes de qualquer compra, para ninguem comprar de um baralho velho.
    /// </summary>
    private void EnsureDeck()
    {
        if (!hasBuilt || lastBuiltSeed != deckSeed)
        {
            SeedRandom(deckSeed);
            BuildDeck();
        }
    }

    /// <summary>
    /// Sorteia a semente da rodada. So quem tem o baralho faz isso; o resto
    /// recebe pelo <see cref="OnDeserialization"/>.
    /// </summary>
    private bool NewSeed()
    {
        // A semente e a unica coisa que precisa viajar pela rede. Antes de
        // distribuir, experimenta baralhos deterministas ate que nenhuma mao
        // de abertura comece estourada.
        bool previousLogDeals = logDeals;
        logDeals = false;
        int attempts = shuffleDeck ? 4096 : 1;
        int limit = Mathf.Min(21, targetScore);
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            deckSeed = Random.Range(1, int.MaxValue);
            SeedRandom(deckSeed);
            BuildDeck();
            int[] totals = new int[HandCount()];
            int count = Mathf.Min(openingCards, deck.Length);
            for (int i = 0; i < count; i++)
            {
                totals[i % totals.Length] += deck[i];
            }
            bool safe = true;
            for (int i = 0; i < totals.Length; i++)
            {
                if (totals[i] > limit)
                {
                    safe = false;
                    break;
                }
            }
            if (safe)
            {
                logDeals = previousLogDeals;
                if (logTurns && attempt > 0)
                {
                    Debug.Log("CardDealer: abertura segura encontrada apos " + (attempt + 1)
                        + " embaralhamentos.");
                }
                return true;
            }
        }
        logDeals = previousLogDeals;
        Debug.LogError("CardDealer: nao foi possivel formar uma abertura sem estourar "
            + limit + " pontos; a rodada nao sera distribuida.");
        return false;
    }

    /// <summary>
    /// Roda aqui depois que a semente chegou. Monta o baralho, limpa a mesa e
    /// compra a abertura. Todo mundo passa por este mesmo caminho, entao as
    /// tres maquinas veem a mesma rodada.
    ///
    /// Clientes que chegam depois usam OnDeserialization para montar o estado
    /// recebido, sem executar este metodo do dono do baralho.
    /// </summary>
    private void ApplySeed()
    {
        hasBuilt = false;
        EnsureDeck();
        ClearHand();
        DealOpeningHand();
    }

    /// <summary>
    /// Chegou estado novo da rede. Se a semente mudou, e rodada nova: limpa a
    /// mesa e recomeca o cursor do historico. Depois aplica tudo que chegou,
    /// que e o que faz a carta aparecer nas outras maquinas.
    /// </summary>
    public void OnDeserialization()
    {
        bool newDeck = !hasBuilt || lastBuiltSeed != deckSeed;
        if (newDeck || lastStructureRevision != logStructureRevision || logApplied > logCount)
        {
            RebuildFromLog();
        }
        else
        {
            ApplyLog();
        }
        RefreshCardVisibility();
        RefreshScores();
        NotifyTurnChanged();
    }

    private void RebuildFromLog()
    {
        EnsureLogBuffers();
        ClearHand();
        hasBuilt = false;
        EnsureDeck();
        int rebuilt = 0;
        for (int i = 0; i < logCount && i < logCapacity; i++)
        {
            if (!TakeLoggedValue(logValue[i]))
            {
                Debug.LogWarning("CardDealer: nao foi possivel reconstruir o baralho na entrada " + i + ".");
                break;
            }
            if (logActive[i] != 0 && logValue[i] > 0)
            {
                InstantiateFromEntry(i, false);
            }
            rebuilt++;
        }
        logApplied = rebuilt;
        if (rebuilt == logCount)
        {
            lastStructureRevision = logStructureRevision;
        }
        openingDealt = matchStarted && !matchOver && logCount >= openingCards;
        RefreshScores();
    }

    private void StructureChanged()
    {
        logStructureRevision++;
        RebuildFromLog();
        RequestSerialization();
    }

    /// <summary>
    /// Pega a proxima carta do baralho. Devolve 0 quando nao ha mais nenhuma, o
    /// que e o limite: uma carta so sai uma vez, entao ninguem pode repetir o
    /// que o outro ja pegou.
    /// </summary>
    private int TakeNextValue()
    {
        if (deck == null || dealIndex < 0 || dealIndex >= deck.Length)
        {
            return 0;
        }
        int value = deck[dealIndex];
        dealIndex++;
        return value;
    }

    private bool TakeLoggedValue(int value)
    {
        EnsureDeck();
        for (int i = dealIndex; i < deck.Length; i++)
        {
            if (deck[i] != value) continue;
            int swap = deck[dealIndex];
            deck[dealIndex] = deck[i];
            deck[i] = swap;
            TakeNextValue();
            return true;
        }
        return false;
    }

    private string DeckString()
    {
        string text = "[";
        for (int i = dealIndex; i < deck.Length; i++)
        {
            if (i > dealIndex)
            {
                text += ", ";
            }
            text += deck[i];
        }
        return text + "]";
    }

    /// <summary>Compra as cartas de abertura. So funciona uma vez por rodada.</summary>
    private void DealOpeningHand()
    {
        if (!matchStarted)
        {
            return;
        }
        if (openingDealt)
        {
            return;
        }
        if (!IsReady())
        {
            return;
        }
        EnsureDeck();
        if (DeckEmpty)
        {
            if (logDeals)
            {
                Debug.Log("CardDealer: baralho vazio, a abertura nao saiu.");
            }
            return;
        }
        openingDealt = true;
        currentPlayer = 0;
        int hands = HandCount();
        int wanted = openingCards < DeckRemaining ? openingCards : DeckRemaining;
        for (int i = 0; i < wanted; i++)
        {
            // alterna: 0, 1, 0, 1...
            int target = i % hands;
            OwnerDeal(target, ShouldMarkSpecial(i < hands));
        }
        if (pendingFly.Count > 0)
        {
            StartDraining();
        }
    }

    /// <summary>
    /// Compra uma carta da maquina para a mao de quem esta na vez.
    ///
    /// Quem compra e o <see cref="turnIndex"/>, nunca o indice que o chamador
    /// disser: o dono do baralho decide a mao, olhando o proprio estado. E o
    /// Slot que garante que a jogada veio mesmo daquele jogador.
    /// </summary>
    private void AcceptHit(int player)
    {
        if (!matchStarted || matchOver || !IsReady() || HandTotal(player) > EffectiveTarget())
        {
            return;
        }
        if (!OwnerDeal(player, false))
        {
            AcceptStay(player);
            return;
        }
        hitSoundSequence++;
        consecutiveStays = 0;
        if (bonusTrumpChancePercent > 0
            && Random.Range(0, 100) < bonusTrumpChancePercent)
        {
            DrawTrump(player);
        }
        if (pendingFly.Count > 0)
        {
            StartDraining();
        }
        // Busts stay hidden until round resolution; only further Hit is blocked.
        AdvanceTurn(player);
    }

    /// <summary>Fica com o que tem. So conta como rodada se vierem duas seguidas.</summary>
    private void AcceptStay(int player)
    {
        if (matchOver)
        {
            return;
        }
        currentPlayer = player;
        consecutiveStays++;
        if (logTurns)
        {
            Debug.Log("CardDealer: jogador " + player + " passou ("
                + consecutiveStays + " seguida(s)). Total " + HandTotal(player) + ".");
        }
        if (twoStaysEndRound && consecutiveStays >= 2)
        {
            EndRoundByStays();
            return;
        }
        AdvanceTurn(player);
    }

    public int TrumpCountInHand(int player)
    {
        int count = 0;
        for (int i = 0; i < trumpCount; i++)
        {
            if (trumpOwner[i] == player)
            {
                count++;
            }
        }
        return count;
    }

    public int TrumpAt(int player, int handIndex)
    {
        int seen = 0;
        for (int i = 0; i < trumpCount; i++)
        {
            if (trumpOwner[i] != player)
            {
                continue;
            }
            if (seen == handIndex)
            {
                return trumpType[i];
            }
            seen++;
        }
        return 0;
    }

    public string TrumpName(int type)
    {
        if (type >= 1 && type <= 6) return (type + 1) + "-Card";
        if (type == TrumpGo17) return "Go For 17";
        if (type == TrumpGo24) return "Go For 24";
        if (type == TrumpGo27) return "Go For 27";
        if (type == TrumpOneUp) return "One-Up";
        if (type == TrumpTwoUp) return "Two-Up";
        if (type == TrumpShield) return "Shield";
        if (type == TrumpShieldPlus) return "Shield+";
        if (type == TrumpBless) return "Bless";
        if (type == TrumpBloodshed) return "Bloodshed";
        if (type == TrumpDestroy) return "Destroy";
        if (type == TrumpReincarnation) return "Reincarnation";
        if (type == TrumpFriendship) return "Friendship";
        if (type == TrumpHush) return "Hush";
        if (type == TrumpPerfectDraw) return "Perfect Draw";
        if (type == TrumpRemove) return "Remove";
        if (type == TrumpReturn) return "Return";
        if (type == TrumpExchange) return "Exchange";
        if (type == TrumpDisservice) return "Disservice";
        if (type == TrumpRefresh) return "Refresh";
        return "";
    }

    private bool DrawTrump(int player)
    {
        EnsureTrumpBuffers();
        if (TrumpCountInHand(player) >= maxTrumpsPerPlayer || trumpCount >= trumpType.Length)
        {
            return false;
        }
        trumpType[trumpCount] = Random.Range(1, 26);
        trumpOwner[trumpCount] = player;
        if (logDeals)
        {
            Debug.Log("CardDealer: trump " + TrumpName(trumpType[trumpCount])
                + " -> jogador " + player + ".");
        }
        trumpCount++;
        RequestSerialization();
        return true;
    }

    private int TrumpGlobalIndex(int player, int handIndex)
    {
        if (handIndex < 0) return -1;
        int seen = 0;
        for (int i = 0; i < trumpCount; i++)
        {
            if (trumpOwner[i] != player) continue;
            if (seen == handIndex) return i;
            seen++;
        }
        return -1;
    }

    private void RemoveTrumpFromHand(int index)
    {
        for (int i = index; i < trumpCount - 1; i++)
        {
            trumpType[i] = trumpType[i + 1];
            trumpOwner[i] = trumpOwner[i + 1];
        }
        trumpCount--;
        trumpType[trumpCount] = 0;
        trumpOwner[trumpCount] = 0;
    }

    private bool IsGoFor(int type)
    {
        return type >= TrumpGo17 && type <= TrumpGo27;
    }

    private void RemoveTableTrumpAt(int index)
    {
        for (int i = index; i < tableTrumpCount - 1; i++)
        {
            tableTrumpType[i] = tableTrumpType[i + 1];
            tableTrumpOwner[i] = tableTrumpOwner[i + 1];
        }
        tableTrumpCount--;
        tableTrumpType[tableTrumpCount] = 0;
        tableTrumpOwner[tableTrumpCount] = 0;
        RefreshScores();
    }

    private void PlaceTrump(int player, int type)
    {
        if (IsGoFor(type))
        {
            for (int i = tableTrumpCount - 1; i >= 0; i--)
            {
                if (IsGoFor(tableTrumpType[i])) RemoveTableTrumpAt(i);
            }
        }
        if (tableTrumpCount >= tableTrumpCapacity) return;
        tableTrumpType[tableTrumpCount] = type;
        tableTrumpOwner[tableTrumpCount] = player;
        tableTrumpCount++;
        RefreshScores();
    }

    public int EffectiveTarget()
    {
        for (int i = tableTrumpCount - 1; i >= 0; i--)
        {
            int type = tableTrumpType[i];
            if (type == TrumpGo17) return 17;
            if (type == TrumpGo24) return 24;
            if (type == TrumpGo27) return 27;
        }
        return targetScore;
    }

    public int CurrentBet()
    {
        int value = baseBet;
        for (int i = 0; i < tableTrumpCount; i++)
        {
            int type = tableTrumpType[i];
            if (type == TrumpOneUp || type == TrumpBloodshed) value++;
            else if (type == TrumpTwoUp) value += 2;
            else if (type == TrumpShield) value--;
            else if (type == TrumpShieldPlus) value -= 2;
        }
        return value < 0 ? 0 : value;
    }

    private bool HasBless()
    {
        for (int i = 0; i < tableTrumpCount; i++)
        {
            if (tableTrumpType[i] == TrumpBless) return true;
        }
        return false;
    }

    private bool DestroyOpponentTrump(int player)
    {
        for (int i = tableTrumpCount - 1; i >= 0; i--)
        {
            if (tableTrumpOwner[i] == 1 - player)
            {
                RemoveTableTrumpAt(i);
                return true;
            }
        }
        return false;
    }

    private int LatestCardEntry(int player)
    {
        for (int i = logCount - 1; i >= 0; i--)
        {
            if (logActive[i] != 0 && logOwner[i] == player) return i;
        }
        return -1;
    }

    private bool LastHiddenIsProtected(int player, int entry)
    {
        if (entry < 0 || logHidden[entry] == 0) return false;
        int hiddenCount = 0;
        for (int i = 0; i < logCount; i++)
        {
            if (logActive[i] != 0 && logOwner[i] == player && logHidden[i] != 0)
                hiddenCount++;
        }
        return hiddenCount <= 1;
    }

    private void RemoveLatestCard(int player)
    {
        int entry = LatestCardEntry(player);
        if (entry < 0 || LastHiddenIsProtected(player, entry)) return;
        logActive[entry] = 0;
        StructureChanged();
    }

    private void ExchangeLatestCards(int player)
    {
        int own = LatestCardEntry(player);
        int other = LatestCardEntry(1 - player);
        if (own < 0 || other < 0 || LastHiddenIsProtected(player, own)
            || LastHiddenIsProtected(1 - player, other)) return;
        logOwner[own] = 1 - player;
        logOwner[other] = player;
        StructureChanged();
    }

    private void RefreshHand(int player)
    {
        for (int i = 0; i < logCount; i++)
        {
            if (logOwner[i] == player) logActive[i] = 0;
        }
        StructureChanged();
        OwnerDeal(player, true);
        OwnerDeal(player, false);
    }

    /// <summary>Consome uma trump da mao do jogador, sem encerrar seu turno.</summary>
    private void AcceptTrump(int player, int handIndex)
    {
        int globalIndex = TrumpGlobalIndex(player, handIndex);
        if (globalIndex < 0) return;
        int type = trumpType[globalIndex];
        if (usedTrumpType == null || usedTrumpType.Length != 64) usedTrumpType = new int[64];
        if (usedTrumpOwner == null || usedTrumpOwner.Length != 64) usedTrumpOwner = new int[64];
        if (usedTrumpCount >= 64)
        {
            for (int i = 1; i < 64; i++) { usedTrumpType[i - 1] = usedTrumpType[i]; usedTrumpOwner[i - 1] = usedTrumpOwner[i]; }
            usedTrumpCount = 63;
        }
        usedTrumpType[usedTrumpCount] = type;
        usedTrumpOwner[usedTrumpCount++] = player;
        RemoveTrumpFromHand(globalIndex);
        consecutiveStays = 0;
        if (type >= 1 && type <= 6) OwnerDealNumber(player, type + 1, false);
        else if (IsGoFor(type) || type == TrumpOneUp || type == TrumpTwoUp
            || type == TrumpShield || type == TrumpShieldPlus || type == TrumpBless)
            PlaceTrump(player, type);
        else if (type == TrumpBloodshed)
        {
            PlaceTrump(player, type);
            DrawTrump(player);
        }
        else if (type == TrumpDestroy) DestroyOpponentTrump(player);
        else if (type == TrumpReincarnation)
        {
            if (DestroyOpponentTrump(player)) DrawTrump(player);
        }
        else if (type == TrumpFriendship)
        {
            DrawTrump(player); DrawTrump(player);
            DrawTrump(1 - player); DrawTrump(1 - player);
        }
        else if (type == TrumpHush) OwnerDeal(player, true);
        else if (type == TrumpPerfectDraw)
        {
            int needed = EffectiveTarget() - HandTotal(player);
            if (needed > 0) OwnerDealNumber(player, needed, false);
        }
        else if (type == TrumpRemove)
        {
            if ((hookMask & (1 << (1 - player))) != 0)
                hookMask &= ~(1 << (1 - player));
            else RemoveLatestCard(1 - player);
        }
        else if (type == TrumpReturn) RemoveLatestCard(player);
        else if (type == TrumpExchange)
        {
            if (hookMask != 0) hookMask = 0;
            else ExchangeLatestCards(player);
        }
        else if (type == TrumpDisservice) OwnerDeal(1 - player, false);
        else if (type == TrumpRefresh) RefreshHand(player);
        if (pendingFly.Count > 0) StartDraining();
        RequestSerialization();
        if (logTurns) Debug.Log("CardDealer: jogador " + player + " usou " + TrumpName(type) + ".");
    }

    /// <summary>Passa a vez. So o dono do baralho chama, e so depois de uma jogada aceita.</summary>
    private void AdvanceTurn(int player)
    {
        if (HandCount() > 0)
        {
            turnIndex = (player + 1) % HandCount();
        }
        currentPlayer = turnIndex;
        actionEpoch++; // Invalidate requests from the previous turn.
        turnReadyAt = Networking.GetServerTimeInSeconds() + Mathf.Max(0f, turnTransitionSeconds);
        turnSecondsLeft = 0;
        ResetTurnDeadline();
        RequestSerialization();
        NotifyTurnChanged();
    }

    private void ResetTurnDeadline()
    {
        turnDeadline = turnTimeoutSeconds > 0f && !dealing && pendingFly.Count == 0
            && Networking.GetServerTimeInSeconds() >= turnReadyAt
            ? Time.time + turnTimeoutSeconds : 0f;
    }

    /// <summary>
    /// Mantem <see cref="turnSecondsLeft"/> com o numero inteiro de segundos que
    /// faltam no turno. Roda so no dono (ProcessSlots ja barrou os outros) e
    /// reenvia quando o valor muda, entao os mostradores locais leem a mesma
    /// contagem. Fora de uma contagem valendo vale 0, que e o "sem tempo".
    /// </summary>
    private void SyncTurnSecondsLeft()
    {
        int target = 0;
        if (matchStarted && !matchOver && turnTimeoutSeconds > 0f
            && openingDealt && !dealing && pendingFly.Count == 0
            && turnDeadline > 0f && Networking.GetServerTimeInSeconds() >= turnReadyAt)
        {
            float remain = turnDeadline - Time.time;
            target = remain > 0f ? Mathf.CeilToInt(remain) : 0;
        }
        if (target != turnSecondsLeft)
        {
            turnSecondsLeft = target;
            RequestSerialization();
        }
    }

    /// <summary>Duas passadas seguidas: vence quem parou mais perto do alvo, sem estourar.</summary>
    private void EndRoundByStays()
    {
        int a = HandTotal(0);
        int b = HandTotal(1);
        int target = EffectiveTarget();
        bool overA = a > target;
        bool overB = b > target;
        int winner;
        if (hookMask == 1) winner = 1;
        else if (hookMask == 2) winner = 0;
        else if (hookMask == 3) winner = -1;
        else if (overA && overB)
        {
            // Ambos ultrapassaram o alvo: empate, independentemente dos totais.
            winner = -1;
        }
        else if (overA)
        {
            winner = 1;
        }
        else if (overB)
        {
            winner = 0;
        }
        else
        {
            winner = a == b ? -1 : (a > b ? 0 : 1);
        }
        if (logTurns)
        {
            Debug.Log("CardDealer: rodada encerrada por duas passadas. "
                + a + " x " + b + " (alvo " + target + ")."
                + (winner < 0 ? " Empatou." : " Venceu o jogador " + winner + "."));
        }
        lastTimeoutPlayer = -1;
        FinishRound(winner);
    }

    /// <summary>
    /// Fecha a rodada, aplica o dano no perdedor e ve se a partida acabou. Com
    /// <c>winner</c> menor que zero a rodada empatou e ninguem toma dano.
    /// </summary>
    private void FinishRound(int winner)
    {
        if (roundResolving || matchOver) return;
        actionEpoch++;
        roundWinner = winner;
        lastRoundWinner = winner;
        consecutiveStays = 0;
        EnsureLife();
        int damage = CurrentBet();
        resolvedBet = damage;
        bool blessSaved = false;

        if (winner < 0)
        {
            if (logTurns)
            {
                Debug.Log("CardDealer: rodada " + roundNumber + " empatada, ninguem levou dano.");
            }
        }
        else
        {
            int loser = 1 - winner;
            machineTargetPlayer = loser;
            if (HasBless() && life[loser] <= damage)
            {
                life[loser] = 1;
                blessSaved = true;
            }
            else
            {
                life[loser] -= damage;
                if (life[loser] < 0) life[loser] = 0;
            }
            if (logTurns)
            {
                Debug.Log("CardDealer: rodada " + roundNumber + " — jogador " + winner + " venceu, jogador "
                    + loser + " perdeu " + damage + " de vida (resta " + life[loser] + ").");
            }
        }

        baseBet = blessSaved ? Mathf.Max(0, baseBet - 1)
            : Mathf.Max(0, baseBet + roundDamageGrowth);

        turnDeadline = 0f;
        roundResolving = true;
        roundEndAt = Networking.GetServerTimeInSeconds() + Mathf.Max(3f, resultDisplaySeconds);
        RevealRoundCards();
        turnSecondsLeft = 0;
        RequestSerialization();
        NotifyTurnChanged();
    }

    /// <summary>A partida acabou por vida ou por limite de rodadas?</summary>
    private bool MatchFinished()
    {
        EnsureLife();
        for (int i = 0; i < life.Length; i++)
        {
            if (life[i] <= 0)
            {
                return true;
            }
        }
        return maxRounds > 0 && roundNumber >= maxRounds;
    }

    private void EndMatch(int winner)
    {
        matchOver = true;
        ClearHand();
        logCount = 0;
        logApplied = 0;
        trumpCount = 0;
        tableTrumpCount = 0;
        hookMask = 0;
        RefreshScores();
        if (logTurns)
        {
            Debug.Log("CardDealer: partida encerrada na rodada " + roundNumber
                + (winner < 0 ? ", empate." : ", venceu o jogador " + winner + "."));
        }
        RequestSerialization();
        // A partida acabou: some com os dois menus de uma vez, sem esperar
        // qualquer virada de turno.
        NotifyTurnChanged();
    }

    private void RevealRoundCards()
    {
        for (int i = 0; i < hand.Count; i++)
            if (hand[i] != null) ApplyCardMaterial(hand[i], cardValue[i], false, handOf[i]);
    }

    private void EnsureLife()
    {
        int hands = HandCount();
        if (life == null || life.Length != hands)
        {
            life = new int[hands];
            for (int i = 0; i < hands; i++)
            {
                life[i] = startingLife;
            }
        }
    }

    private void EnsureTrumpBuffers()
    {
        int handCapacity = maxTrumpsPerPlayer * 2;
        if (trumpType == null || trumpOwner == null || trumpType.Length != handCapacity
            || trumpOwner.Length != handCapacity)
        {
            trumpType = new int[handCapacity];
            trumpOwner = new int[handCapacity];
            trumpCount = 0;
        }
        if (tableTrumpType == null || tableTrumpOwner == null
            || tableTrumpType.Length != tableTrumpCapacity
            || tableTrumpOwner.Length != tableTrumpCapacity)
        {
            tableTrumpType = new int[tableTrumpCapacity];
            tableTrumpOwner = new int[tableTrumpCapacity];
            tableTrumpCount = 0;
        }
    }

    /// <summary>
    /// API para a UI. Cada botao chama estes daqui; nao ha parametro de jogador
    /// porque o dono do baralho ja sabe de quem e a vez.
    /// </summary>
    public void RequestHit()
    {
        if (!CanLocalPlayerAct())
        {
            if (logTurns)
            {
                Debug.Log("CardDealer: hit ignorado, o jogador local nao pode agir nesta vez.");
            }
            return;
        }
        slots[turnIndex].RequestHit();
    }

    public void RequestStay()
    {
        if (!CanLocalPlayerAct())
        {
            if (logTurns)
            {
                Debug.Log("CardDealer: stay ignorado, o jogador local nao pode agir nesta vez.");
            }
            return;
        }
        slots[turnIndex].RequestStay();
    }

    /// <summary>
    /// Usa a trump na posicao <paramref name="cardIndex"/> da mao local. O dono
    /// do baralho recusa se nao for a vez de quem jogou.
    /// </summary>
    public int incomingTrumpHandIndex;
    public void RequestUseTrumpFromCard() { RequestUseTrump(incomingTrumpHandIndex); }
public void RequestUseTrump(int cardIndex)
    {
        if (!CanLocalPlayerAct())
        {
            if (logTurns) Debug.Log("CardDealer: tarot ignorada fora da vez do jogador local.");
            return;
        }
        int expectedIndex = TrumpGlobalIndex(turnIndex, cardIndex);
        if (expectedIndex < 0) return;
        slots[turnIndex].incomingTrumpType = trumpType[expectedIndex];
        slots[turnIndex].incomingTrumpHandIndex = cardIndex;
        slots[turnIndex].RequestUseTrumpFromCard();
    }

    /// <summary>Qual Slot e o do jogador local.</summary>
    private int MySlotIndex()
    {
        if (slots == null)
        {
            return -1;
        }
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].IsMine())
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// O dono do baralho le os Slots a cada quadro. Como so o dono de um Slot
    /// escreve nos campos dele, a jogada e autenticada pelo proprio objeto, e o
    /// numero de sequencia faz cada uma valer uma vez so.
    /// </summary>
    private void ProcessSlots()
    {
        if (!IsDeckOwner())
        {
            return;
        }
        if (roundResolving)
        {
            if (Networking.GetServerTimeInSeconds() >= roundEndAt)
            {
                if (MatchFinished()) { roundResolving = false; EndMatch(lastRoundWinner); }
                else { roundResolving = false; StartRound(); }
            }
            return;
        }
        SyncTurnSecondsLeft();
        if (matchStarted && !matchOver && turnTimeoutSeconds > 0f
            && openingDealt && !dealing && pendingFly.Count == 0
            && Networking.GetServerTimeInSeconds() >= turnReadyAt)
        {
            if (turnDeadline <= 0f)
            {
                ResetTurnDeadline();
            }
            else if (Time.time >= turnDeadline && !dealing)
            {
                if (logTurns)
                {
                    Debug.Log("CardDealer: jogador " + turnIndex + " perdeu a rodada por tempo.");
                }
                lastTimeoutPlayer = turnIndex;
                FinishRound(1 - turnIndex);
                return;
            }
        }
        if (slots == null || slots.Length == 0)
        {
            return;
        }
        if (slotSeqSeen == null || slotSeqSeen.Length != slots.Length)
        {
            slotSeqSeen = new int[slots.Length];
        }
        for (int i = 0; i < slots.Length; i++)
        {
            PlayerSlot slot = slots[i];
            if (slot == null || !slot.HasPlayer())
            {
                continue;
            }
            if (slot.ActionSeq == slotSeqSeen[i])
            {
                continue;
            }
            // a jogada e do dono deste Slot, por construcao: ele e quem pode ter
            // escrito no campo sincronizado
            slotSeqSeen[i] = slot.ActionSeq;
            if (logTurns)
            {
                Debug.Log("CardDealer: jogador " + i + " jogou " + slot.ActionType
                    + " na vez " + turnIndex + ".");
            }
            processedActionSeq[i] = slot.ActionSeq;
            processedActionEpoch[i] = slot.actionEpoch;
            acceptedActionSeq[i] = -1;
            RequestSerialization();
            VRCPlayerApi sender = slot.OwnerPlayer();
            if (slot.actionEpoch != actionEpoch || sender == null
                || sender.playerId != slot.actionPlayerId) continue;
            int type = slot.ActionType;
            if (type == PlayerSlot.ActionStartMatch)
            {
                if (i == 0)
                {
                    StartMatch();
                    return;
                }
                continue;
            }
            if (!TurnIsActionable())
            {
                continue;
            }
            if (i != turnIndex)
            {
                if (logTurns)
                {
                    Debug.Log("CardDealer: jogada do jogador " + i
                        + " recusada, a vez e do jogador " + turnIndex + ".");
                }
                continue;
            }
            int arg = slot.ActionArg;
            if (type == PlayerSlot.ActionHit)
            {
                if (HandTotal(i) > EffectiveTarget()) continue;
                AcceptHit(i);
            }
            else if (type == PlayerSlot.ActionStay)
            {
                AcceptStay(i);
            }
            else if (type == PlayerSlot.ActionTrump)
            {
                int index = TrumpGlobalIndex(i, arg);
                if (index < 0 || trumpType[index] != slot.actionTrumpType) continue;
                AcceptTrump(i, arg);
                acceptedActionSeq[i] = slot.ActionSeq;
                RequestSerialization();
            }
        }
    }

    /// <summary>Some com todas as cartas da mesa e abre uma rodada nova.</summary>
    private void ClearHand()
    {
        // Invalida qualquer animacao suspensa de uma rodada anterior.
        dealGeneration++;
        dealing = false;
        flying = 0;
        pendingFly.Clear();
        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i] != null)
            {
                Destroy(hand[i]);
            }
        }
        hand.Clear();
        handOf.Clear();
        slotOf.Clear();
        cardValue.Clear();
        special.Clear();
        openingDealt = false;
        RefreshScores();
    }

    /// <summary>
    /// Cada jogador ganha uma carta especial na abertura: a primeira que ele
    /// recebe. Como o baralho ja vem embaralhado, o numero de cada uma tambem
    /// e sorteado — dois jogadores podem virar com cartas diferentes.
    ///
    /// <paramref name="firstOfThatPlayer"/> e o que decide, e nao o numero: a
    /// marca viaja com a carta, nao com a casa nem com a ordem da mesa.
    ///
    /// A face da primeira carta de cada mao usa hiddenMaterial quando
    /// hideSpecialCards esta ligado. O valor continua participando da soma.
    /// </summary>
    private bool ShouldMarkSpecial(bool firstOfThatPlayer)
    {
        return markFirstCardSpecial && firstOfThatPlayer;
    }

    /// <summary>
    /// A carta <paramref name="cardIndex"/> da mesa (na ordem em que foi
    /// comprada) e a especial? Use <see cref="CardValue"/> para saber qual e o
    /// numero dela.
    ///
    /// E o que a mecanica de tarot vai consultar antes de mexer numa carta: a
    /// especial fica de fora do efeito.
    /// </summary>
    public bool IsSpecialCard(int cardIndex)
    {
        return cardIndex >= 0 && cardIndex < special.Count && special[cardIndex];
    }

    // -------------------------------------------------------------- dealing

    private void StartDraining()
    {
        if (dealing)
        {
            return;
        }
        dealing = true;
        int generation = dealGeneration;
        Scheduler.Run(() => DrainQueue(generation));
    }

    private async Task DrainQueue(int generation)
    {
        try
        {
            while (generation == dealGeneration && pendingFly.Count > 0)
            {
                int cardIndex = pendingFly[0];
                pendingFly.RemoveAt(0);
                if (pendingFly.Count > 0 && delayBetweenCards > 0f)
                {
                    await Scheduler.Delay(delayBetweenCards);
                }
                if (generation != dealGeneration)
                {
                    break;
                }
                await FlyToHand(cardIndex, generation);
            }
        }
        finally
        {
            if (generation == dealGeneration)
            {
                dealing = false;
                flying = 0;
                ResetTurnDeadline();
                // O portao de jogada so abre quando a ultima carta pousa. Sem
                // este aviso, o menu do proximo jogador ficaria esperando o
                // evento seguinte, que so viria com a jogada dele — e ele
                // nunca teria como jogar.
                NotifyTurnChanged();
            }
        }
    }

    /// <summary>
    /// Cria a carta da entrada <paramref name="entry"/> do historico, ainda na
    /// maquina, e a coloca na fila de voo. O valor vem do historico, e nao do
    /// baralho local, para que todas as maquinas criem exatamente a mesma
    /// carta.
    ///
    /// Nao consome o baralho: quem sorteia e o dono, em
    /// <see cref="OwnerDeal"/>. Nos outros maquinas quem adianta o contador de
    /// "restam N" e <see cref="ApplyLog"/>, uma vez por entrada.
    /// </summary>
    private bool InstantiateFromEntry(int entry, bool animate)
    {
        if (entry < 0 || entry >= logCount || logValue == null)
        {
            return false;
        }
        int value = logValue[entry];
        if (value <= 0)
        {
            return false;
        }

        int target = ClampPlayer(logOwner[entry]);
        Transform anchor = handAnchors[target];
        int index = hand.Count;
        int slot = CountInHand(target);
        Quaternion toRotation = SlotRotation(slot);

        Transform machine = machineAnchor != null ? machineAnchor : transform;
        GameObject card = Instantiate(cardPrefab, anchor);
        Transform cardTransform = card.transform;
        if (animate)
        {
            Vector3 spawnPosition = machine.position;
            spawnPosition = new Vector3(spawnPosition.x, anchor.position.y, spawnPosition.z);
            cardTransform.position = spawnPosition;
            cardTransform.localRotation = toRotation * Quaternion.Euler(0f, flipAngle, 0f);
            cardTransform.localScale = CardScale() * startScale;
        }
        else
        {
            cardTransform.localPosition = SlotPosition(slot, anchor);
            cardTransform.localRotation = toRotation;
            cardTransform.localScale = CardScale();
        }

        // entra na mesa antes de voar, e a casa fica registrada agora: se a casa
        // fosse recalculada no voo, as cartas do mesmo lote, que ja estariam
        // todas na lista, cairiam todas na ultima casa
        hand.Add(card);
        handOf.Add(target);
        slotOf.Add(slot);
        cardValue.Add(value);

        // A marca de oculta pertence a entrada do historico, inclusive Hush e
        // Refresh; nao depende da casa atual depois de Remove ou Exchange.
        bool isSpecial = logHidden[entry] != 0;
        special.Add(isSpecial);
        RefreshScores();
        ApplyCardMaterial(card, value, isSpecial, target);
        if (logDeals)
        {
            Debug.Log("CardDealer: carta " + value + " -> jogador " + target
                + " (casa " + slot + (isSpecial ? ", especial" : "") + "). Restam " + DeckRemaining + ".");
        }
        if (animate)
        {
            pendingFly.Add(index);
        }
        return true;
    }

    /// <summary>
    /// Escolhe o material da carta pelo numero que ela tem: o material do valor
    /// <paramref name="value"/> fica na posicao <c>value - deckMinValue</c> de
    /// <see cref="cardMaterials"/>. Uma carta numerica oculta, se
    /// <see cref="hideSpecialCards"/> estiver ligado, troca esse material pelo
    /// <see cref="hiddenMaterial"/>.
    ///
    /// O Renderer e procurado em profundidade porque o mesh do prefab fica num
    /// filho, e nao no objeto raiz da carta.
    /// </summary>
    private void ApplyCardMaterial(GameObject card, int value, bool isSpecial, int ownerPlayer)
    {
        Renderer cardRenderer = card.GetComponentInChildren<Renderer>(true);
        if (cardRenderer == null)
        {
            return;
        }
        if (isSpecial && hideSpecialCards && !roundResolving
            && !IsLocalPlayer(ownerPlayer) && hiddenMaterial != null)
        {
            cardRenderer.sharedMaterial = hiddenMaterial;
            return;
        }
        if (cardMaterials == null || cardMaterials.Length == 0)
        {
            return;
        }
        int slotMaterial = value - deckMinValue;
        if (slotMaterial < 0 || slotMaterial >= cardMaterials.Length)
        {
            return;
        }
        Material material = cardMaterials[slotMaterial];
        if (material != null)
        {
            cardRenderer.sharedMaterial = material;
        }
    }

    /// <summary>Fase 2: a carta vai da maquina ate a casa dela, interpolando.</summary>
    private async Task FlyToHand(int cardIndex, int generation)
    {
        if (cardIndex < 0 || cardIndex >= hand.Count)
        {
            return;
        }
        GameObject card = hand[cardIndex];
        if (card == null)
        {
            return;
        }

        int slot = slotOf[cardIndex];
        Vector3 from = card.transform.position;
        Quaternion fromRotation = card.transform.localRotation;
        Vector3 fromScale = card.transform.localScale;
        Transform anchor = card.transform.parent;
        Vector3 toLocal = SlotPosition(slot, anchor);
        Vector3 to = anchor.TransformPoint(toLocal);
        Quaternion toRotation = SlotRotation(slot);
        Vector3 toScale = CardScale();

        flying++;
        float duration = dealDuration > 0.01f ? dealDuration : 0.01f;
        float startedAt = Time.time;
        while (generation == dealGeneration && card != null)
        {
            float t = Mathf.Clamp01((Time.time - startedAt) / duration);
            float eased = EaseOutCubic(t);
            Transform cardTransform = card.transform;
            cardTransform.position =
                Vector3.Lerp(from, to, eased) + Vector3.up * (Mathf.Sin(eased * Mathf.PI) * arcHeight);
            cardTransform.localRotation = Quaternion.Slerp(fromRotation, toRotation, eased);
            cardTransform.localScale = Vector3.Lerp(fromScale, toScale, eased);
            if (t >= 1f)
            {
                break;
            }
            await Scheduler.NextFrame();
        }

        // garante a casa exata, independente do ultimo frame
        if (generation == dealGeneration && card != null)
        {
            card.transform.localPosition = toLocal;
            card.transform.localRotation = toRotation;
            card.transform.localScale = toScale;
        }
        if (generation == dealGeneration)
        {
            flying--;
        }
    }

    // -------------------------------------------------------------- layout

    private int HandCount()
    {
        return handAnchors != null ? handAnchors.Length : 0;
    }

    private int ClampPlayer(int player)
    {
        int hands = HandCount();
        if (hands < 1)
        {
            return 0;
        }
        return player < 0 ? 0 : (player >= hands ? hands - 1 : player);
    }

    /// <summary>Casa <paramref name="slot"/> da mao, no espaco local do anchor.</summary>
    private Vector3 SlotPosition(int slot, Transform anchor)
    {
        int perRow = RowSize();
        int column = slot % perRow;
        int row = slot / perRow;
        Vector3 card = CardScale();
        Vector3 local = new Vector3(
            (column - (perRow - 1) * 0.5f) * card.x * cardGap,
            0f,
            row * card.z * rowGap);
        if (layerThickness == 0f) return local;
        Vector3 world = anchor.TransformPoint(local);
        world = new Vector3(world.x, world.y + slot * layerThickness, world.z);
        return anchor.InverseTransformPoint(world);
    }

    /// <summary>O leque abre e fecha por fileira, e nao pelo total da mao.</summary>
    private Quaternion SlotRotation(int slot)
    {
        int perRow = RowSize();
        int column = slot % perRow;
        // Match Card-Placeholder: its 180-degree roll presents the readable face.
        return Quaternion.Euler(0f, (column - (perRow - 1) * 0.5f) * fanAngle, 180f);
    }

    /// <summary>Espaco que a carta ocupa dentro da sua mao, contando as de antes.</summary>
    private int CountInHand(int target)
    {
        int count = 0;
        for (int i = 0; i < handOf.Count; i++)
        {
            if (handOf[i] == target)
            {
                count++;
            }
        }
        return count;
    }

    private int RowSize()
    {
        return cardsPerRow < 1 ? 1 : cardsPerRow;
    }

    private Vector3 CardScale()
    {
        return cardPrefab != null ? cardPrefab.transform.localScale : Vector3.one;
    }

    private float EaseOutCubic(float t)
    {
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }

    private bool IsReady()
    {
        if (cardPrefab == null)
        {
            Debug.LogWarning("CardDealer: cardPrefab esta vazio.");
            return false;
        }
        if (HandCount() == 0)
        {
            Debug.LogWarning("CardDealer: handAnchors esta vazio.");
            return false;
        }
        for (int i = 0; i < handAnchors.Length; i++)
        {
            if (handAnchors[i] == null)
            {
                Debug.LogWarning("CardDealer: handAnchors[" + i + "] esta vazio.");
                return false;
            }
        }
        return true;
    }


    public bool CanLocalPlayerAct()
    {
        if (!TurnIsActionable())
        {
            return false;
        }
        int index = slots != null && turnIndex >= 0 && turnIndex < slots.Length && slots[turnIndex] != null && slots[turnIndex].IsMine() ? turnIndex : MySlotIndex();
        return index >= 0 && index == turnIndex && slots[index] != null
            && slots[index].HasPlayer();
    }


public bool IsLocalPlayer(int player)
    {
        return slots != null && player >= 0 && player < slots.Length
            && slots[player] != null && slots[player].IsMine();
    }
}
