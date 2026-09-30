using System.Collections.Generic;
using System.Threading.Tasks;
using MenSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

/// <summary>
/// Compra as cartas da maquina e as entrega na mao dos jogadores.
///
/// O fluxo tem duas fases: a carta primeiro nasce na maquina
/// (<see cref="InstantiateBatch"/>) e so depois e interpolada ate a mao que
/// lhe cabe (<see cref="FlyToHand"/>). Uma compra e sempre um lote: o lote
/// inteiro nasce de uma vez e as cartas saem uma a uma.
///
/// A mao de destino e um array, e o jogo alterna: a carta 0 vai para o
/// jogador 0, a 1 para o 1, a 2 para o 0 de novo. Como cada mao guarda as
/// cartas na ordem em que recebeu, comprar 10 cartas nao manda tudo para o
/// mesmo ponto — cada uma ocupa a proxima casa do seu leque, e as que se
/// sobrepoem ficam empilhadas uma sobre a outra.
///
/// A rodada comeca com <see cref="openingCards"/> cartas (<see cref="StartRound"/>)
/// e depois cada jogador compra uma por vez com <see cref="Hit"/> ou fica com
/// o que tem com <see cref="Stay"/>. A mao que recebe o Hit e indicada no
/// parametro do metodo: cada botao chama <see cref="Hit(int)"/> com o seu
/// jogador, e o estado de <see cref="currentPlayer"/> e atualizado nessa hora.
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

    [Header("Round")]
    [Tooltip("Cartas compradas na abertura da rodada. So na primeira: depois e hit ou stay.")]
    public int openingCards = 3;

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

    [Tooltip("Embaralha o baralho antes de comecar a rodada.")]
    public bool shuffleDeck = true;

    [Tooltip("Loga no console o baralho montado e cada carta comprada.")]
    public bool logDeals = true;

    [Header("Turn")]
    [Tooltip("Quem pediu a jogada. So de quem e o turno, para a UI destacar a mao; a mao que recebe a carta e sempre a do parametro de Hit/Stay.")]
    public int currentPlayer = 0;

    [Tooltip("Jogador que vai pedir a jogada. Escreva aqui e chame Hit() ou Stay() sem parametro, que e o jeito de chamar evento no Udon.")]
    public int requestedPlayer = 0;

    [Header("Hand layout")]
    [Tooltip("Cartas por fileira antes de comecar a proxima.")]
    public int cardsPerRow = 5;

    [Tooltip("Espaco entre cartas vizinhas, medido na largura da carta. 0 = todas uma sobre a outra. 0.5 = metade da carta de fora. 1 = cada uma da largura dela de folga, nenhuma encosta na outra. Acima de 1 abre ainda mais.")]
    public float cardGap = 1f;

    [Tooltip("Distancia entre fileiras, medida no comprimento da carta. 1 = uma fileira de cartao de distancia.")]
    public float rowGap = 1.2f;

    [Tooltip("Altura de cada carta sobre a anterior, para nao haver z-fighting.")]
    public float layerThickness = 0.002f;

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

    [Tooltip("Separacao com que o lote sai da maquina, antes de voar.")]
    public float machineSpread = 0.06f;

    private List<GameObject> hand = new List<GameObject>();
    private List<int> handOf = new List<int>();
    private List<int> slotOf = new List<int>();
    private List<int> cardValue = new List<int>();
    private List<bool> special = new List<bool>();
    private List<int> pendingTargets = new List<int>();
    private int[] deck = new int[0];
    private int dealIndex = 0;
    private uint randomState = 0;
    private bool hasBuilt = false;
    private int lastBuiltSeed = 0;
    private bool openingDealt = false;
    private bool dealing = false;
    private int flying = 0;

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

    // ------------------------------------------------------------------ api

    /// <summary>
    /// Abre a rodada. Quem nao tem o baralho so pede: quem tem sorteia a
    /// semente nova, manda pela rede e <see cref="OnDeserialization"/> roda em
    /// todas as maquinas, para as tres comprarem exatamente as mesmas cartas.
    /// Offline (editor, single player) nao ha dono, entao segue direto.
    /// </summary>
    public void StartRound()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local != null && !Networking.IsOwner(local, gameObject))
        {
            SendCustomNetworkEvent(NetworkEventTarget.Owner, "StartRound");
            return;
        }

        NewSeed();
        if (local != null)
        {
            RequestSerialization();
        }
        ApplySeed();
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

    /// <summary>Chegou estado novo da rede: refaz o baralho se a semente mudou.</summary>
    public void OnDeserialization()
    {
        if (hasBuilt && lastBuiltSeed == deckSeed)
        {
            return;
        }
        ApplySeed();
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
            QueueCard(i % hands);
        }
        StartDraining();
    }

    /// <summary>
    /// O jogador <paramref name="playerIndex"/> compra uma carta da maquina.
    /// E o "hit".
    ///
    /// Quem compra e quem o chamador disser, nao o revezamento interno: o
    /// jogador da vez e decidido por quem receber o botao, e registrado aqui
    /// na hora da chamada. Assim o mesmo botao dos dois lados do baralho serve
    /// para as duas maos, e o jogo nao precisa adivinhar de quem e a vez.
    /// </summary>
    public void Hit(int playerIndex)
    {
        if (!IsReady())
        {
            return;
        }
        int player = ClampPlayer(playerIndex);
        EnsureDeck();
        if (DeckEmpty)
        {
            if (logDeals)
            {
                Debug.Log("CardDealer: baralho acabou, o hit do jogador " + player + " foi ignorado.");
            }
            return;
        }
        currentPlayer = player;
        QueueCard(player);
        StartDraining();
    }

    /// <summary>Hit para <see cref="requestedPlayer"/>. Use este nos eventos Udon.</summary>
    public void Hit()
    {
        Hit(requestedPlayer);
    }

    /// <summary>
    /// O jogador <paramref name="playerIndex"/> fica com o que tem, sem comprar
    /// nada. E o "stay". Registra a jogada da mesma forma que o
    /// <see cref="Hit(int)"/>, para a UI e para o placar saberem de quem foi.
    /// </summary>
    public void Stay(int playerIndex)
    {
        currentPlayer = ClampPlayer(playerIndex);
    }

    /// <summary>Stay para <see cref="requestedPlayer"/>. Use este nos eventos Udon.</summary>
    public void Stay()
    {
        Stay(requestedPlayer);
    }

    /// <summary>Some com todas as cartas da mesa e abre uma rodada nova.</summary>
    public void ClearHand()
    {
        pendingTargets.Clear();
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
            card.transform.localPosition = SlotPosition(slot);
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

    private void QueueCard(int target)
    {
        pendingTargets.Add(target);
    }

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
            while (pendingTargets.Count > 0)
            {
                // fase 1: o lote inteiro nasce na maquina
                int count = pendingTargets.Count;
                int[] created = InstantiateBatch(count);
                // fase 2: uma a uma, cada carta voa para a mao que lhe cabe
                for (int i = 0; i < created.Length; i++)
                {
                    if (i > 0 && delayBetweenCards > 0f)
                    {
                        await Scheduler.Delay(delayBetweenCards);
                    }
                    await FlyToHand(created[i]);
                }
            }
        }
        finally
        {
            dealing = false;
            flying = 0;
        }
    }

    /// <summary>Fase 1: cria as cartas do lote, todas ainda na maquina.</summary>
    private int[] InstantiateBatch(int count)
    {
        Transform machine = machineAnchor != null ? machineAnchor : transform;
        Vector3 scale = CardScale() * startScale;
        int[] created = new int[count];
        int made = 0;
        for (int i = 0; i < count; i++)
        {
            // uma carta por instanciamento: cada compra sai do baralho e nunca
            // volta, entao nenhuma se repete
            int value = TakeNextValue();
            if (value <= 0)
            {
                if (logDeals)
                {
                    Debug.Log("CardDealer: baralho acabou. Faltou " + (count - made) + " carta(s) do lote.");
                }
                break;
            }

            int target = ClampPlayer(pendingTargets[i]);
            Transform anchor = handAnchors[target];
            int index = hand.Count;
            int slot = CountInHand(target);
            Vector3 to = SlotPosition(slot);
            Quaternion toRotation = SlotRotation(slot);

            GameObject card = Instantiate(cardPrefab, anchor);
            Transform cardTransform = card.transform;
            cardTransform.localPosition =
                anchor.InverseTransformPoint(machine.position) + MachineSpread(i, count, anchor);
            cardTransform.localRotation = toRotation * Quaternion.Euler(0f, flipAngle, 0f);
            cardTransform.localScale = scale;

            // entra na mesa antes de voar, e a casa fica registrada agora: se a
            // casa fosse recalculada no voo, as cartas do mesmo lote — que ja
            // estariam todas na lista — cairiam todas na ultima casa
            hand.Add(card);
            handOf.Add(target);
            slotOf.Add(slot);
cardValue.Add(value);

            // slot == 0 e a primeira carta daquele jogador, e nao a primeira da mesa:
            // na abertura alternada, a carta 1 e do jogador 0 e a carta 2 do jogador 1,
            // e as duas sao especiais.
            bool isSpecial = ShouldMarkSpecial(slot == 0);
            special.Add(isSpecial);
            if (isSpecial && hideSpecialCards && hiddenMaterial != null)
            {
                Renderer cardRenderer = card.GetComponent<Renderer>();
                if (cardRenderer != null)
                {
                    cardRenderer.sharedMaterial = hiddenMaterial;
                }
            }
            if (logDeals)
            {
                Debug.Log("CardDealer: carta " + value + " -> jogador " + target
                    + " (casa " + slot + (isSpecial ? ", especial" : "") + "). Restam " + DeckRemaining + ".");
            }
            created[made] = index;
            made++;
        }
        pendingTargets.Clear();
        if (made == count)
        {
            return created;
        }
        // baralho acabou no meio do lote: devolve so o que deu
        int[] trimmed = new int[made];
        for (int i = 0; i < made; i++)
        {
            trimmed[i] = created[i];
        }
        return trimmed;
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
        Vector3 from = card.transform.localPosition;
        Quaternion fromRotation = card.transform.localRotation;
        Vector3 fromScale = card.transform.localScale;
        Vector3 to = SlotPosition(slot);
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
            cardTransform.localPosition =
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
            card.transform.localPosition = to;
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
    private Vector3 SlotPosition(int slot)
    {
        int perRow = RowSize();
        int column = slot % perRow;
        int row = slot / perRow;
        Vector3 card = CardScale();
        return new Vector3(
            (column - (perRow - 1) * 0.5f) * card.x * cardGap,
            slot * layerThickness,
            row * card.z * rowGap);
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

    private Vector3 MachineSpread(int i, int count, Transform anchor)
    {
        if (count < 2 || machineSpread == 0f)
        {
            return Vector3.zero;
        }
        // o lote sai da maquina lado a lado, sempre na mesma ordem do mundo,
        // mesmo com as duas maos viradas uma para a outra
        Vector3 world = new Vector3((i - (count - 1) * 0.5f) * machineSpread, 0f, 0f);
        return anchor.InverseTransformDirection(world);
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
