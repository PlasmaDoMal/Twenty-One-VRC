using System.Collections.Generic;
using System.Threading.Tasks;
using MenSharp;
using UnityEngine;

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

    [Tooltip("Qual das cartas da abertura e a especial. -1 = nenhuma.")]
    public int specialCardIndex = 2;

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
    private List<bool> special = new List<bool>();
    private List<int> pendingTargets = new List<int>();
    private List<int> pendingRounds = new List<int>();
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

    // ------------------------------------------------------------------ api

    /// <summary>
    /// Abre a rodada: limpa a mesa e compra as cartas de abertura, alternando
    /// entre as maos. E o unico momento em que as 3 cartas saem de uma vez.
    /// </summary>
    public void StartRound()
    {
        ClearHand();
        DealOpeningHand();
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
        openingDealt = true;
        currentPlayer = 0;
        int hands = HandCount();
        for (int i = 0; i < openingCards; i++)
        {
            // alterna: 0, 1, 0, 1...
            QueueCard(i % hands, i);
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
        currentPlayer = player;
        QueueCard(player, -1);
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
        pendingRounds.Clear();
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
    /// A carta <paramref name="cardIndex"/> da mesa (na ordem em que foi
    /// comprada) e a carta especial da abertura?
    ///
    /// No jogo original uma das cartas da abertura e a oculta (Hush), que o
    /// oponente nao pode ver. Hoje a compra e a mesma de qualquer outra carta;
    /// o que falta e so o efeito — virar a carta para baixo e esconder o valor
    /// dela do oponente — que entra junto com o baralho de trumps. A marca em
    /// <see cref="specialCardIndex"/> e o indice da carta dentro da abertura.
    /// </summary>
    public bool IsSpecialCard(int cardIndex)
    {
        return cardIndex >= 0 && cardIndex < special.Count && special[cardIndex];
    }

    // -------------------------------------------------------------- dealing

    private void QueueCard(int target, int roundIndex)
    {
        pendingTargets.Add(target);
        pendingRounds.Add(roundIndex);
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
        for (int i = 0; i < count; i++)
        {
            int target = ClampPlayer(pendingTargets[i]);
            int roundIndex = pendingRounds[i];
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
            special.Add(roundIndex == specialCardIndex);
            created[i] = index;
        }
        pendingTargets.Clear();
        pendingRounds.Clear();
        return created;
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
