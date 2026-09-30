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
    [Header("References")]
    [Tooltip("Prefab instanciado a cada carta.")]
    public GameObject cardPrefab;

    [Tooltip("De onde a carta sai. Se vazio, usa a posicao deste objeto.")]
    public Transform machineAnchor;

    [Tooltip("Centro da mao de cada jogador. As cartas viram filha da maa que lhes cabe.")]
    public Transform[] handAnchors;

    [Tooltip("Textos dos ScoreCubes, na mesma ordem das maos: Player1 = 0, Player2 = 1.")]
    public TMP_Text[] scoreTexts;

    [Header("Round")]
    [Tooltip("Cartas da abertura no total. Com dois jogadores, 6 da 1 secreta e 2 normais para cada um.")]
    public int openingCards = 6;

    [Header("Special card")]
    [Tooltip("Marca como especial a primeira carta que cada jogador recebe na abertura. Como o baralho esta embaralhado, o numero delas tambem e sorteado.")]
    public bool markFirstCardSpecial = true;

    [Tooltip("Material das cartas especiais. Vazio = nao troca nada.")]
    public Material hiddenMaterial;

    [Tooltip("Esconde a face especial trocando o material. Deixa desligado ate o baralho de trumps entrar.")]
    public bool hideSpecialCards = false;

    [Header("Deck")]
    [Tooltip("Menor numero do baralho. No Twenty One comeca em 1.")]
    public int deckMinValue = 1;

    [Tooltip("Maior numero do baralho, e tambem o limite de cartas da rodada. No Twenty One vai ate 11.")]
    public int deckMaxValue = 11;

    [Tooltip("Semente do embaralhamento. E sincronizada, entao todo mundo monta a mesma ordem; o dono sorteia uma nova por rodada.")]
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

    [Tooltip("Altura Y no mundo para nascer e pousar. Baseada no Card da cena (1.0825), sem consultar esse objeto em runtime. O arco do voo e somado apenas durante a animacao.")]
    public float cardWorldY = 1.0825f;

    [Header("Round rules")]
    [Tooltip("Alvo para ganhar a rodada. No original e 21.")]
    public int targetScore = 21;

    [Tooltip("Quanto de vida o perdedor da rodada perde.")]
    public int roundDamage = 1;

    [Tooltip("Vida inicial de cada jogador. A partida acaba quando a vida de alguem chega a zero.")]
    public int startingLife = 3;

    [Tooltip("Numero maximo de rodadas. 0 = sem limite, e a partida so acaba quando a vida de alguem zera.")]
    public int maxRounds = 0;

    [Tooltip("Duas passadas seguidas encerram a rodada. E a regra do original.")]
    public bool twoStaysEndRound = true;

    [Tooltip("Cartas de tarot que cada jogador recebe por rodada.")]
    public int trumpCardsPerRound = 2;

    [Header("Network")]
    [Tooltip("Via de entrada de cada jogador na mesa. Dono do baralho preenche sozinho; e aqui que o turno e validado.")]
    public PlayerSlot[] slots;

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
    private int logApplied = 0;

    // Estado da partida. Tudo synced, e so o dono do baralho escreve, entao os
    // tres clientes veem a mesma vez, a mesma rodada e a mesma vida.
    [UdonSynced] public int turnIndex = 0;
    [UdonSynced] public bool matchStarted = false;
    [UdonSynced] public int roundNumber = 1;
    [UdonSynced] public int matchStarterPlayerId = 0;
    [UdonSynced] public int consecutiveStays = 0;
    [UdonSynced] public int[] life;
    [UdonSynced] public int[] trumpUsed;

    // Ultima jogada processada de cada Slot. E o que impede a mesma intencao de
    // ser executada duas vezes, sem precisar limpar nada no Slot.
    private int[] slotSeqSeen = new int[0];
    private bool matchOver = false;
    private int roundWinner = -1;

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

    public void Start()
    {
        RefreshScores();
        Scheduler.Run(() => WatchSlots());
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
        if (scoreTexts == null)
        {
            return;
        }
        for (int i = 0; i < scoreTexts.Length; i++)
        {
            TMP_Text scoreText = scoreTexts[i];
            if (scoreText != null)
            {
                int total = i < HandCount() ? HandTotal(i) : 0;
                scoreText.text = total + "/" + targetScore;
            }
        }
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
        if (logValue != null && logValue.Length == logCapacity)
        {
            return;
        }
        logValue = new int[logCapacity];
        logOwner = new int[logCapacity];
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
            // uma entrada = uma carta consumida do baralho. O dono ja consomeu
            // em OwnerDeal, entao aqui so mantenemos o contador em dia nas outras
            // maquinas.
            EnsureDeck();
            TakeNextValue();
            if (!InstantiateFromEntry(logApplied))
            {
                break;
            }
            logApplied++;
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
    private bool OwnerDeal(int target)
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
        logCount++;

        // o dono aplica na hora e ja fica com o cursor no fim
        logApplied = logCount;
        InstantiateFromEntry(logCount - 1);
        RequestSerialization();
        return true;
    }

    /// <summary>
    /// Abre a rodada. Quem nao tem o baralho so pede: quem tem sorteia a
    /// semente nova, zera o historico e manda pela rede, e o historico de
    /// compras que faz as tres maquinas comprarem as mesmas cartas, na mesma
    /// ordem. Offline (editor, single player) nao ha dono, entao segue direto.
    /// </summary>
    public void StartRound()
    {
        if (!IsDeckOwner())
        {
            SendCustomNetworkEvent(NetworkEventTarget.Owner, "StartRound");
            return;
        }
        if (!matchStarted || matchOver)
        {
            return;
        }

        NewSeed();
        EnsureLogBuffers();
        logCount = 0;
        logApplied = 0;
        consecutiveStays = 0;
        roundWinner = -1;
        // a rodada contada em diante: StartMatch zera antes de chamar, e
        // FinishRound chama este metodo no fim de cada rodada, entao o numero
        // que aparece no log e sempre o da rodada que esta começando
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
    public void StartMatch()
    {
        if (!IsDeckOwner())
        {
            SendCustomNetworkEvent(NetworkEventTarget.Owner, "StartMatch");
            return;
        }
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
        EnsureLife();
        for (int i = 0; i < life.Length; i++)
        {
            life[i] = startingLife;
        }
        EnsureTrumpUsed();
        for (int i = 0; i < trumpUsed.Length; i++)
        {
            trumpUsed[i] = 0;
        }
        if (logTurns)
        {
            Debug.Log("CardDealer: partida comecada por " + matchStarterPlayerId
                + ". Vida " + startingLife + ", dano " + roundDamage
                + " por rodada, alvo " + targetScore + ".");
        }
        StartRound();
    }

    /// <summary>
    /// A UI chama isto. So o jogador que started a partida consegue reiniciar,
    /// e o dono do baralho confere isso de novo antes de obedecer.
    /// </summary>
    public void RequestStartMatch()
    {
        if (IsMatchStarter())
        {
            StartMatch();
        }
        else if (logTurns)
        {
            Debug.Log("CardDealer: pedido de iniciar partida recusado, so quem started pode.");
        }
    }

    /// <summary>
    /// Este jogador pode iniciar ou reiniciar a partida?
    ///
    /// Antes da primeira jogada nao existe "quem started", entao ninguem fica de
    /// fora: qualquer um pode pedir o inicio. Isso nao abre brecha, porque quem
    /// executa e o dono do baralho e ele confere tudo de novo em
    /// <see cref="MatchCanStart"/> — dois Slots configurados, dois jogadores
    /// dentro. O pedido de um cliente so chega a um dono de Slot legitimo.
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
            return true;
        }
        return local.playerId == matchStarterPlayerId;
    }

    private bool MatchCanStart()
    {
        if (slots == null || slots.Length < HandCount() || HandCount() < 2)
        {
            if (logTurns)
            {
                Debug.Log("CardDealer: nao da para comecar, configure um Slot por jogador.");
            }
            return false;
        }
        if (VRCPlayerApi.GetPlayerCount() < HandCount())
        {
            if (logTurns)
            {
                Debug.Log("CardDealer: aguardando os dois jogadores para iniciar a partida.");
            }
            return false;
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
        if (slots == null)
        {
            return;
        }
        // GetPlayers segue a ordem de entrada, que e a ordem da mesa
        VRCPlayerApi[] online = VRCPlayerApi.GetPlayers();
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
    private void NewSeed()
    {
        deckSeed = Random.Range(1, int.MaxValue);
    }

    /// <summary>
    /// Roda aqui depois que a semente chegou. Monta o baralho, limpa a mesa e
    /// compra a abertura. Todo mundo passa por este mesmo caminho, entao as
    /// tres maquinas veem a mesma rodada.
    ///
    /// Publico porque e o mesmo caminho de quem entra atras: da para chamar
    /// direto ao reconectar, sem esperar a rede mandar a semente de novo.
    /// </summary>
    public void ApplySeed()
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
        if (!hasBuilt || lastBuiltSeed != deckSeed)
        {
            logApplied = 0;
            EnsureLogBuffers();
            ClearHand();
            EnsureDeck();
        }
        ApplyLog();
        RefreshScores();
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
    public void DealOpeningHand()
    {
        if (!matchStarted)
        {
            return;
        }
        if (openingDealt)
        {
            return;
        }
        if (!IsDeckOwner())
        {
            SendCustomNetworkEvent(NetworkEventTarget.Owner, "DealOpeningHand");
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
            OwnerDeal(i % hands);
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
        if (!matchStarted || matchOver || !IsReady())
        {
            return;
        }
        if (OwnerDeal(player) && pendingFly.Count > 0)
        {
            StartDraining();
        }
        if (CheckBust(player))
        {
            return;
        }
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

    /// <summary>
    /// Carta de tarot so no turno de quem ela e. Uma vez usada, ela nao volta:
    /// o dono do baralho guarda o uso em <c>trumpUsed</c>.
    /// </summary>
    private void AcceptTrump(int player, int cardIndex)
    {
        if (matchOver)
        {
            return;
        }
        if (cardIndex < 0 || cardIndex >= hand.Count)
        {
            if (logTurns)
            {
                Debug.Log("CardDealer: tarot recusada, a carta " + cardIndex + " nao existe na mesa.");
            }
            return;
        }
        if (handOf[cardIndex] != player)
        {
            if (logTurns)
            {
                Debug.Log("CardDealer: tarot recusada, a carta " + cardIndex + " nao e do jogador " + player + ".");
            }
            return;
        }
        if (!IsSpecialCard(cardIndex))
        {
            if (logTurns)
            {
                Debug.Log("CardDealer: tarot recusada, a carta " + cardIndex + " nao e de tarot.");
            }
            return;
        }
        EnsureTrumpUsed();
        int word = player >> 5;
        int bit = 1 << (player & 31);
        if ((trumpUsed[word] & bit) != 0)
        {
            if (logTurns)
            {
                Debug.Log("CardDealer: tarot recusada, o jogador " + player + " ja usou a carta " + cardIndex + ".");
            }
            return;
        }
        trumpUsed[word] |= bit;
        if (logTurns)
        {
            Debug.Log("CardDealer: jogador " + player + " usou a carta de tarot " + CardValue(cardIndex)
                + " (carta " + cardIndex + " da mesa). Usar tarot nao conta como comprar nem passar.");
        }
        // o efeito da carta entra aqui. O uso ja foi marcado acima, entao a
        // mesma carta nao pode ser usada de novo mesmo depois que o efeito rodar.
        RequestSerialization();
    }

    /// <summary>Passa a vez. So o dono do baralho chama, e so depois de uma jogada aceita.</summary>
    private void AdvanceTurn(int player)
    {
        if (HandCount() > 0)
        {
            turnIndex = (player + 1) % HandCount();
        }
        currentPlayer = turnIndex;
        RequestSerialization();
    }

    /// <summary>Estourou o alvo? A rodada acaba e o outro jogador ganha.</summary>
    private bool CheckBust(int player)
    {
        int total = HandTotal(player);
        if (total <= targetScore)
        {
            return false;
        }
        if (logTurns)
        {
            Debug.Log("CardDealer: jogador " + player + " estourou com " + total
                + " (alvo " + targetScore + "), perde a rodada.");
        }
        consecutiveStays = 0;
        FinishRound(1 - player);
        return true;
    }

    /// <summary>Duas passadas seguidas: vence quem parou mais perto do alvo, sem estourar.</summary>
    private void EndRoundByStays()
    {
        int a = HandTotal(0);
        int b = HandTotal(1);
        bool overA = a > targetScore;
        bool overB = b > targetScore;
        int winner;
        if (overA && overB)
        {
            // os dois estouraram: perde quem tiver o numero maior
            winner = a > b ? 1 : 0;
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
                + a + " x " + b + " (alvo " + targetScore + ")."
                + (winner < 0 ? " Empatou." : " Venceu o jogador " + winner + "."));
        }
        FinishRound(winner);
    }

    /// <summary>
    /// Fecha a rodada, aplica o dano no perdedor e ve se a partida acabou. Com
    /// <c>winner</c> menor que zero a rodada empatou e ninguem toma dano.
    /// </summary>
    private void FinishRound(int winner)
    {
        roundWinner = winner;
        consecutiveStays = 0;
        EnsureLife();

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
            life[loser] -= roundDamage;
            if (life[loser] < 0)
            {
                life[loser] = 0;
            }
            if (logTurns)
            {
                Debug.Log("CardDealer: rodada " + roundNumber + " — jogador " + winner + " venceu, jogador "
                    + loser + " perdeu " + roundDamage + " de vida (resta " + life[loser] + ").");
            }
        }

        RequestSerialization();
        if (MatchFinished())
        {
            EndMatch(winner);
            return;
        }
        StartRound();
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
        if (logTurns)
        {
            Debug.Log("CardDealer: partida encerrada na rodada " + roundNumber
                + (winner < 0 ? ", empate." : ", venceu o jogador " + winner + "."));
        }
        RequestSerialization();
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

    private void EnsureTrumpUsed()
    {
        int hands = HandCount();
        int words = (hands + 31) / 32;
        if (words < 1)
        {
            words = 1;
        }
        if (trumpUsed == null || trumpUsed.Length != words)
        {
            trumpUsed = new int[words];
        }
    }

    /// <summary>
    /// API para a UI. Cada botao chama estes daqui; nao ha parametro de jogador
    /// porque o dono do baralho ja sabe de quem e a vez.
    /// </summary>
    public void RequestHit()
    {
        if (!matchStarted)
        {
            return;
        }
        int index = MySlotIndex();
        if (index < 0)
        {
            if (logTurns)
            {
                Debug.Log("CardDealer: hit pedido sem Slot do jogador local.");
            }
            return;
        }
        slots[index].RequestHit();
    }

    public void RequestStay()
    {
        if (!matchStarted)
        {
            return;
        }
        int index = MySlotIndex();
        if (index < 0)
        {
            if (logTurns)
            {
                Debug.Log("CardDealer: stay pedido sem Slot do jogador local.");
            }
            return;
        }
        slots[index].RequestStay();
    }

    /// <summary>
    /// Usa a carta de tarot <paramref name="cardIndex"/>. O dono do baralho
    /// recusa se nao for a vez de quem jogou.
    /// </summary>
    public void RequestUseTrump(int cardIndex)
    {
        int index = MySlotIndex();
        if (index < 0)
        {
            if (logTurns)
            {
                Debug.Log("CardDealer: tarot pedida sem Slot do jogador local.");
            }
            return;
        }
        slots[index].RequestUseTrump(cardIndex);
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
        if (!IsDeckOwner() || !matchStarted || matchOver)
        {
            return;
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
            if (i != turnIndex)
            {
                if (logTurns)
                {
                    Debug.Log("CardDealer: jogada do jogador " + i
                        + " recusada, a vez e do jogador " + turnIndex + ".");
                }
                continue;
            }
            int type = slot.ActionType;
            int arg = slot.ActionArg;
            if (type == PlayerSlot.ActionHit)
            {
                AcceptHit(i);
            }
            else if (type == PlayerSlot.ActionStay)
            {
                AcceptStay(i);
            }
            else if (type == PlayerSlot.ActionTrump)
            {
                AcceptTrump(i, arg);
            }
        }
    }

    /// <summary>Some com todas as cartas da mesa e abre uma rodada nova.</summary>
    public void ClearHand()
    {
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
        currentPlayer = 0;
        RefreshScores();
    }

    /// <summary>Some com a ultima carta comprada e fecha o leque.</summary>
    public void RemoveLastCard()
    {
        Prune();
        int last = hand.Count - 1;
        if (last < 0)
        {
            return;
        }
        // o historico precisa perder a mesma carta, senao o dono voltaria a
        // mandar um estado que ja nao existe na mesa dos outros
        if (logCount > 0)
        {
            logCount--;
            logValue[logCount] = 0;
            logOwner[logCount] = 0;
            if (logApplied > logCount)
            {
                logApplied = logCount;
            }
            if (dealIndex > 0)
            {
                dealIndex--;
            }
            RequestSerialization();
        }
        if (hand[last] != null)
        {
            Destroy(hand[last]);
        }
        hand.RemoveAt(last);
        handOf.RemoveAt(last);
        slotOf.RemoveAt(last);
        cardValue.RemoveAt(last);
        special.RemoveAt(last);
        RepositionHand();
        RefreshScores();
    }

    /// <summary>
    /// Recalcula a casa de cada carta que ja pousou, sem animacao. As cartas em
    /// voo sao deixadas como estao: elas recebem a casa final ao aterrissar.
    /// </summary>
    public void RepositionHand()
    {
        Prune();
        int settled = hand.Count - flying;
        if (settled < 0)
        {
            settled = 0;
        }
        Vector3 scale = CardScale();
        for (int i = 0; i < settled; i++)
        {
            GameObject card = hand[i];
            if (card == null)
            {
                continue;
            }
            int slot = slotOf[i];
            card.transform.localPosition = SlotPosition(slot, card.transform.parent);
            card.transform.localRotation = SlotRotation(slot);
            card.transform.localScale = scale;
        }
    }

    /// <summary>
    /// Cada jogador ganha uma carta especial na abertura: a primeira que ele
    /// recebe. Como o baralho ja vem embaralhado, o numero de cada uma tambem
    /// e sorteado — dois jogadores podem virar com cartas diferentes.
    ///
    /// <paramref name="firstOfThatPlayer"/> e o que decide, e nao o numero: a
    /// marca viaja com a carta, nao com a casa nem com a ordem da mesa.
    ///
    /// No jogo original uma das cartas da abertura e a oculta (Hush), que o
    /// oponente nao pode ver. Hoje a compra e a mesma de qualquer outra carta;
    /// o que falta e so o efeito — virar a carta para baixo e esconder o valor
    /// dela do oponente — que entra junto com o baralho de trumps.
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
        Scheduler.Run(() => DrainQueue());
    }

    private async Task DrainQueue()
    {
        try
        {
            while (pendingFly.Count > 0)
            {
                int cardIndex = pendingFly[0];
                pendingFly.RemoveAt(0);
                if (pendingFly.Count > 0 && delayBetweenCards > 0f)
                {
                    await Scheduler.Delay(delayBetweenCards);
                }
                await FlyToHand(cardIndex);
            }
        }
        finally
        {
            dealing = false;
            flying = 0;
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
    private bool InstantiateFromEntry(int entry)
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
        Vector3 spawnPosition = machine.position;
        spawnPosition.y = cardWorldY;
        cardTransform.position = spawnPosition;
        cardTransform.localRotation = toRotation * Quaternion.Euler(0f, flipAngle, 0f);
        cardTransform.localScale = CardScale() * startScale;

        // entra na mesa antes de voar, e a casa fica registrada agora: se a casa
        // fosse recalculada no voo, as cartas do mesmo lote, que ja estariam
        // todas na lista, cairiam todas na ultima casa
        hand.Add(card);
        handOf.Add(target);
        slotOf.Add(slot);
        cardValue.Add(value);
        RefreshScores();

        // slot == 0 e a primeira carta daquele jogador, e nao a primeira da mesa:
        // na abertura alternada, a carta 1 e do jogador 0 e a carta 2 do jogador 1,
        // e as duas sao especiais.
        bool isSpecial = ShouldMarkSpecial(slot == 0);
        special.Add(isSpecial);
        ApplyCardMaterial(card, value, isSpecial);
        if (logDeals)
        {
            Debug.Log("CardDealer: carta " + value + " -> jogador " + target
                + " (casa " + slot + (isSpecial ? ", especial" : "") + "). Restam " + DeckRemaining + ".");
        }
        pendingFly.Add(index);
        return true;
    }

    /// <summary>
    /// Escolhe o material da carta pelo numero que ela tem: o material do valor
    /// <paramref name="value"/> fica na posicao <c>value - deckMinValue</c> de
    /// <see cref="cardMaterials"/>. Uma carta de tarot oculta, se
    /// <see cref="hideSpecialCards"/> estiver ligado, troca esse material pelo
    /// <see cref="hiddenMaterial"/>.
    ///
    /// O Renderer e procurado em profundidade porque o mesh do prefab fica num
    /// filho, e nao no objeto raiz da carta.
    /// </summary>
    private void ApplyCardMaterial(GameObject card, int value, bool isSpecial)
    {
        Renderer cardRenderer = card.GetComponentInChildren<Renderer>(true);
        if (cardRenderer == null)
        {
            return;
        }
        if (isSpecial && hideSpecialCards && hiddenMaterial != null)
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
    private async Task FlyToHand(int cardIndex)
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
        Vector3 to = anchor.TransformPoint(SlotPosition(slot, anchor));
        Quaternion toRotation = SlotRotation(slot);
        Vector3 toScale = CardScale();

        flying++;
        float duration = dealDuration > 0.01f ? dealDuration : 0.01f;
        float startedAt = Time.time;
        while (card != null)
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
        if (card != null)
        {
            card.transform.position = to;
            card.transform.localRotation = toRotation;
            card.transform.localScale = toScale;
        }
        flying--;
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
        Vector3 world = anchor.TransformPoint(local);
        world.y = cardWorldY + slot * layerThickness;
        return anchor.InverseTransformPoint(world);
    }

    /// <summary>O leque abre e fecha por fileira, e nao pelo total da mao.</summary>
    private Quaternion SlotRotation(int slot)
    {
        int perRow = RowSize();
        int column = slot % perRow;
        return Quaternion.Euler(0f, (column - (perRow - 1) * 0.5f) * fanAngle, 0f);
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

    /// <summary>Tira da mesa as cartas que foram destruidas por outra via.</summary>
    private void Prune()
    {
        for (int i = hand.Count - 1; i >= 0; i--)
        {
            if (hand[i] == null)
            {
                hand.RemoveAt(i);
                handOf.RemoveAt(i);
                slotOf.RemoveAt(i);
                cardValue.RemoveAt(i);
                special.RemoveAt(i);
            }
        }
        RefreshScores();
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
}
