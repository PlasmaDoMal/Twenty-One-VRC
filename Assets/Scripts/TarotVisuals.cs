using System.Threading.Tasks;
using MenSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TarotVisuals : MenSharpBehaviour
{
    public CardDealer dealer;
    public GameObject tarotPrefab;
    public BoxCollider[] spawnAreas;
    public BoxCollider tableTrigger;
    public Transform[] tableRows;
    public Material[] tarotMaterials;
    public int cardsPerRow = 8;
    public float handSpacing = 0.07f;
    public float tableSpacing = 0.09f;
    public float rowSpacing = 0.12f;

    // One manager per table. Arrays are local presentation, never synchronized.
    public TarotPickup[] handVisuals;
    public TarotPickup[] tableVisuals;
    public TarotPickup lastDroppedCard;
    public Vector3 lastDropPosition;
    public int lastDropType;
    public int lastDropOwner;
    public float lastDropTime;
    public int lastFingerprint = int.MinValue;
    public int lastRoundVisualized = -1;

    public void Start()
    {
        if (dealer == null || tarotPrefab == null || spawnAreas == null
            || spawnAreas.Length < 2 || tableRows == null || tableRows.Length < 2)
        {
            Debug.LogError("TarotVisuals: configure dealer, prefab, dois spawns e duas posicoes de mesa.");
            return;
        }
        handVisuals = new TarotPickup[dealer.maxTrumpsPerPlayer * 2];
        tableVisuals = new TarotPickup[dealer.tableTrumpCapacity];
        Scheduler.Run(() => WatchState());
    }

private async Task WatchState()
    {
        while (true)
        {
            int fingerprint = dealer.roundNumber * 31 + dealer.trumpCount;
            fingerprint = fingerprint * 31 + dealer.tableTrumpCount;
            fingerprint = fingerprint * 31 + (dealer.matchOver ? 1 : 0);
            for (int i = 0; i < dealer.trumpCount; i++)
            {
                fingerprint = fingerprint * 31 + dealer.trumpType[i];
                fingerprint = fingerprint * 31 + dealer.trumpOwner[i];
            }
            for (int i = 0; i < dealer.tableTrumpCount; i++)
            {
                fingerprint = fingerprint * 31 + dealer.tableTrumpType[i];
                fingerprint = fingerprint * 31 + dealer.tableTrumpOwner[i];
            }
            if (fingerprint != lastFingerprint)
            {
                lastFingerprint = fingerprint;
                RefreshVisuals();
            }
            await Scheduler.Delay(0.15f);
        }
    }

    public void RefreshVisuals()
    {
        if (dealer == null || handVisuals == null || tableVisuals == null) return;
        if (lastRoundVisualized != dealer.roundNumber)
        {
            for (int i = 0; i < handVisuals.Length; i++)
            {
                if (handVisuals[i] != null) Destroy(handVisuals[i].cardObject);
                handVisuals[i] = null;
            }
            for (int i = 0; i < tableVisuals.Length; i++)
            {
                if (tableVisuals[i] != null) Destroy(tableVisuals[i].cardObject);
                tableVisuals[i] = null;
            }
            lastRoundVisualized = dealer.roundNumber;
        }
        for (int player = 0; player < 2; player++)
        {
            int count = dealer.TrumpCountInHand(player);
            int capacity = dealer.maxTrumpsPerPlayer;
            for (int index = 0; index < capacity; index++)
            {
                int key = player * capacity + index;
                int type = index < count ? dealer.TrumpAt(player, index) : 0;
                TarotPickup existing = handVisuals[key];
                if (existing != null && existing.tarotType != type)
                {
                    if (!existing.pendingUse) Destroy(existing.cardObject);
                    handVisuals[key] = null;
                    existing = null;
                }
                if (type > 0 && existing == null)
                {
                    Vector3 home = HandPosition(player, index);
                    Quaternion rotation = HandRotation(player);
                    handVisuals[key] = CreateCard(player, index, type, false, home, rotation);
                }
            }
        }

        int[] positions = new int[2];
        int countOnTable = Mathf.Min(dealer.tableTrumpCount, tableVisuals.Length);
        for (int i = 0; i < tableVisuals.Length; i++)
        {
            int type = i < countOnTable ? dealer.tableTrumpType[i] : 0;
            int owner = i < countOnTable ? dealer.tableTrumpOwner[i] : 0;
            TarotPickup existing = tableVisuals[i];
            if (existing != null && (existing.tarotType != type || existing.ownerPlayer != owner))
            {
                Destroy(existing.cardObject);
                tableVisuals[i] = null;
                existing = null;
            }
            if (type <= 0) continue;
            int ownerPosition = positions[owner]++;
            Vector3 destination = TablePosition(owner, ownerPosition);
            Quaternion rotation = HandRotation(owner);
            if (existing == null)
            {
                bool fromDrop = lastDroppedCard != null && lastDropType == type
                    && lastDropOwner == owner && Time.time - lastDropTime < 2f;
                Vector3 start = fromDrop ? lastDropPosition : destination;
                if (fromDrop)
                {
                    Destroy(lastDroppedCard.cardObject);
                    lastDroppedCard = null;
                }
                TarotPickup created = CreateCard(owner, ownerPosition, type, true, start, rotation);
                tableVisuals[i] = created;
                if (created != null && fromDrop) created.MoveTo(destination, rotation, false);
            }
            else if (!existing.moving)
            {
                existing.cardTransform.position = destination;
                existing.cardTransform.rotation = rotation;
            }
        }
    }

    private TarotPickup CreateCard(int owner, int index, int type, bool placed,
        Vector3 position, Quaternion rotation)
    {
        GameObject card = Instantiate(tarotPrefab, position, rotation);
        TarotPickup pickup = card.GetComponent<TarotPickup>();
        if (pickup == null)
        {
            Debug.LogError("TarotVisuals: prefab sem TarotPickup.");
            Destroy(card);
            return null;
        }
        Material face = tarotMaterials != null && type > 0 && type <= tarotMaterials.Length
            ? tarotMaterials[type - 1] : null;
        pickup.Configure(this, owner, index, type, placed, face, position, rotation,
            TrumpSymbol(type), TrumpDescription(type), IsLocalOwner(owner));
        card.name = (placed ? "TarotTable_" : "TarotHand_") + owner + "_" + index + "_" + dealer.TrumpName(type);
        return pickup;
    }

    public bool IsLocalOwner(int player)
    {
        return dealer != null && dealer.IsLocalPlayer(player);
    }

    public void CardDropped(TarotPickup card)
    {
        if (card == null || card.onTable || card.pendingUse) return;
        if (!IsLocalOwner(card.ownerPlayer) || !dealer.CanLocalPlayerAct()
            || dealer.turnIndex != card.ownerPlayer
            || dealer.TrumpAt(card.ownerPlayer, card.handIndex) != card.tarotType)
        {
            card.ReturnHome();
            return;
        }
        bool overTable = tableTrigger != null
            && (tableTrigger.bounds.Contains(card.cardTransform.position)
                || (card.cardCollider != null
                    && tableTrigger.bounds.Intersects(card.cardCollider.bounds)));
        if (!overTable)
        {
            card.ReturnHome();
            return;
        }
        lastDroppedCard = card;
        lastDropPosition = card.cardTransform.position;
        lastDropType = card.tarotType;
        lastDropOwner = card.ownerPlayer;
        lastDropTime = Time.time;
        int ownerPosition = 0;
        for (int i = 0; i < dealer.tableTrumpCount; i++)
            if (dealer.tableTrumpOwner[i] == card.ownerPlayer) ownerPosition++;
        card.MoveTo(TablePosition(card.ownerPlayer, ownerPosition),
            HandRotation(card.ownerPlayer), true);
        dealer.RequestUseTrump(card.handIndex);
    }

    private Vector3 HandPosition(int player, int index)
    {
        Bounds bounds = spawnAreas[player].bounds;
        int column = index % 4;
        int row = index / 4;
        return new Vector3(bounds.center.x + (column - 1.5f) * handSpacing,
            bounds.max.y + 0.025f, bounds.center.z + (row - 0.5f) * 0.11f);
    }

    private Quaternion HandRotation(int player)
    {
        return Quaternion.Euler(0f, player == 0 ? 0f : 180f, 0f);
    }

    private Vector3 TablePosition(int player, int index)
    {
        int perRow = Mathf.Max(1, cardsPerRow);
        int column = index % perRow;
        int row = index / perRow;
        Vector3 basePosition = tableRows[player].position;
        return basePosition + new Vector3(column * tableSpacing,
            row * 0.002f, row * rowSpacing * (player == 0 ? 1f : -1f));
    }

    public string TrumpSymbol(int type)
    {
        if (type >= 1 && type <= 6) return (type + 1) + "C";
        if (type == 7) return "G17";
        if (type == 8) return "G24";
        if (type == 9) return "G27";
        if (type == 10) return "1UP";
        if (type == 11) return "2UP";
        if (type == 12) return "SH";
        if (type == 13) return "SH+";
        if (type == 14) return "BL";
        if (type == 15) return "BLO";
        if (type == 16) return "DST";
        if (type == 17) return "RIN";
        if (type == 18) return "FR";
        if (type == 19) return "HSH";
        if (type == 20) return "PD";
        if (type == 21) return "RM";
        if (type == 22) return "RT";
        if (type == 23) return "EX";
        if (type == 24) return "DS";
        if (type == 25) return "RF";
        return "?";
    }

    public string TrumpDescription(int type)
    {
        string name = dealer.TrumpName(type);
        string effect = "";
        if (type >= 1 && type <= 6) effect = "Compra o numero " + (type + 1) + " se estiver no baralho.";
        else if (type == 7) effect = "Muda o alvo para 17.";
        else if (type == 8) effect = "Muda o alvo para 24.";
        else if (type == 9) effect = "Muda o alvo para 27.";
        else if (type == 10) effect = "Aumenta a aposta em 1.";
        else if (type == 11) effect = "Aumenta a aposta em 2.";
        else if (type == 12) effect = "Reduz a aposta em 1.";
        else if (type == 13) effect = "Reduz a aposta em 2.";
        else if (type == 14) effect = "Evita uma derrota fatal.";
        else if (type == 15) effect = "Aposta +1 e compra uma tarot.";
        else if (type == 16) effect = "Destroi a ultima tarot do oponente.";
        else if (type == 17) effect = "Destroi uma tarot e compra outra.";
        else if (type == 18) effect = "Ambos compram duas tarots.";
        else if (type == 19) effect = "Compra uma carta numerica oculta.";
        else if (type == 20) effect = "Compra o numero que completa o alvo.";
        else if (type == 21) effect = "Remove a ultima carta do oponente.";
        else if (type == 22) effect = "Devolve sua ultima carta numerica.";
        else if (type == 23) effect = "Troca as ultimas cartas das maos.";
        else if (type == 24) effect = "O oponente compra uma carta.";
        else if (type == 25) effect = "Troca sua mao por duas cartas novas.";
        return name + "\n" + effect;
    }
}