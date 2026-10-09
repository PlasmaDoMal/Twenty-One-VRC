using System.Threading.Tasks;
using MenSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TarotVisuals : MenSharpBehaviour
{
    public CardDealer dealer;
    public GameObject[] slotObjects;
    public GameObject tarotPrefab;
    public BoxCollider[] spawnAreas;
    public BoxCollider tableTrigger;
    public Transform[] tableRows;
    public Material[] tarotMaterials;
    [Tooltip("Local language preference from MenuSystem. Optional; defaults to English.")]
    public UdonBehaviour languageSource;
    public int language;
    public int appliedLanguage = -1;
    public int cardsPerRow = 8;
    public float handSpacing = 0.07f;
    public float tableSpacing = 0.09f;
    public float rowSpacing = 0.12f;

    // One manager per table. Arrays are local presentation, never synchronized.
    public TarotPickup[] handVisuals;
    public TarotPickup[] tableVisuals;
    public TarotPickup lastDroppedCard;
    public TarotPickup incomingDroppedCard;


    public TarotPickup pendingCard;
    public int pendingSequence, pendingEpoch, pendingOwner;
    public float pendingSince;
    public float acknowledgementTimeout = 5f;

    public void ResolvePendingUse()
    {
        if (pendingCard == null) return;
        bool processed = dealer.processedActionSeq[pendingOwner] == pendingSequence
            && dealer.processedActionEpoch[pendingOwner] == pendingEpoch;
        bool accepted = processed && dealer.acceptedActionSeq[pendingOwner] == pendingSequence;
        if (accepted && dealer.usedTrumpCount > 0)
        {
            lastDropPosition = pendingCard.cardTransform.position;
            lastDropType = pendingCard.tarotType;
            lastDropOwner = pendingOwner;
            lastDropTime = Time.time;
            handVisuals[pendingOwner * dealer.maxTrumpsPerPlayer + pendingCard.handIndex] = null;
            Destroy(pendingCard.cardObject);
            pendingCard = null;
            lastFingerprint = int.MinValue;
            return;
        }
        if (!processed && dealer.actionEpoch == pendingEpoch && IsLocalOwner(pendingOwner)
            && dealer.matchStarted && Time.time - pendingSince < acknowledgementTimeout) return;
        int rejectedOwner = pendingCard.ownerPlayer;
        bool rejectedOwnedLocally = IsLocalOwner(rejectedOwner);
        pendingCard.ownedLocally = rejectedOwnedLocally;
        pendingCard.awaitingUse = false;
        pendingCard.pendingUse = false;
        pendingCard.onTable = false;
        pendingCard.ReturnHome();
        pendingCard = null;
        lastFingerprint = int.MinValue;
    }

    public float nextPoll;
    public Vector3 lastDropPosition;
    public int lastDropType;
    public int lastDropOwner;
    public float lastDropTime;
    public int lastFingerprint = int.MinValue;
    public int lastRoundVisualized = -1;

    public void Start()
    {
        // VRChat resolves serialized null Udon references to this behaviour.
        // These references are runtime state, so initialize them explicitly.
        pendingCard = null;
        incomingDroppedCard = null;
        lastDroppedCard = null;
        if (dealer == null || tarotPrefab == null || spawnAreas == null
            || spawnAreas.Length < 2 || tableRows == null || tableRows.Length < 2)
        {
            Debug.LogError("TarotVisuals: configure dealer, prefab, dois spawns e duas posicoes de mesa.");
            return;
        }
        handVisuals = new TarotPickup[dealer.maxTrumpsPerPlayer * 2];
        tableVisuals = new TarotPickup[64];

    }

    public void Update()
    {
            if (handVisuals == null || handVisuals.Length == 0 || Time.time < nextPoll) return;
            nextPoll = Time.time + 0.15f;
            int selectedLanguage = 0;
            if (languageSource != null && languageSource.gameObject != gameObject)
            {
                object selectedValue = languageSource.GetProgramVariable("language");
                if (selectedValue != null) selectedLanguage = (int)selectedValue;
            }
            language = selectedLanguage == 1 ? 1 : 0;
            if (appliedLanguage != language)
            {
                appliedLanguage = language;
                RefreshDescriptions();
            }
            ResolvePendingUse();
            for (int i = 0; i < handVisuals.Length; i++)
            {
                TarotPickup card = handVisuals[i];
                if (card == null) continue;
                int viewerOwner = card.ownerPlayer;
                bool viewerOwns = IsLocalOwner(viewerOwner);
                card.ownedLocally = viewerOwns;
                if (card.pickup != null && !card.held && !card.moving && !card.awaitingUse)
                    card.pickup.pickupable = card.ownedLocally;
            }
            for (int i = 0; i < tableVisuals.Length; i++)
            {
                TarotPickup card = tableVisuals[i];
                if (card == null) continue;
                int viewerOwner = card.ownerPlayer;
                bool viewerOwns = IsLocalOwner(viewerOwner);
                card.ownedLocally = viewerOwns;
                if (card.pickup != null && !card.held && !card.moving && !card.awaitingUse)
                    card.pickup.pickupable = card.ownedLocally;
            }
            int fingerprint = dealer.roundNumber * 31 + dealer.trumpCount;
            fingerprint = fingerprint * 31 + dealer.usedTrumpCount;
            fingerprint = fingerprint * 31 + (dealer.matchOver ? 1 : 0);
            for (int i = 0; i < dealer.trumpCount; i++)
            {
                fingerprint = fingerprint * 31 + dealer.trumpType[i];
                fingerprint = fingerprint * 31 + dealer.trumpOwner[i];
            }
            for (int i = 0; i < dealer.usedTrumpCount; i++)
            {
                fingerprint = fingerprint * 31 + dealer.usedTrumpType[i];
                fingerprint = fingerprint * 31 + dealer.usedTrumpOwner[i];
            }
            if (fingerprint != lastFingerprint)
            {
                lastFingerprint = fingerprint;
                RefreshVisuals();
            }
    }

    public void RefreshVisuals()
    {
        if (dealer == null || handVisuals == null || tableVisuals == null) return;
        if (lastRoundVisualized != dealer.roundNumber)
        {


            // Unused hand cards persist across rounds when accumulation is enabled.
            if (dealer.clearTrumpsEachRound)
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
            int count = CountInHand(player);
            int capacity = dealer.maxTrumpsPerPlayer;
            for (int index = 0; index < capacity; index++)
            {
                int key = player * capacity + index;
                int type = index < count ? TypeInHand(player, index) : 0;
                TarotPickup existing = handVisuals[key];
                if (existing != null && existing.awaitingUse) continue;
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
        int countOnTable = Mathf.Min(dealer.usedTrumpCount, tableVisuals.Length);
        for (int i = 0; i < tableVisuals.Length; i++)
        {
            int type = i < countOnTable ? dealer.usedTrumpType[i] : 0;
            int owner = i < countOnTable ? dealer.usedTrumpOwner[i] : 0;
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
                bool fromDrop = lastDropType == type
                    && lastDropOwner == owner && Time.time - lastDropTime < 2f;
                Vector3 start = fromDrop ? lastDropPosition : destination;
                if (fromDrop && lastDroppedCard != null)
                {
                    tableVisuals[i] = lastDroppedCard;
                    lastDroppedCard = null;
                    continue;
                }
                TarotPickup created = CreateCard(owner, ownerPosition, type, true, start, rotation);
                tableVisuals[i] = created;
                if (created != null && fromDrop) MoveCard(created, destination, rotation, false);
            }
            else if (!existing.moving && !existing.held)
            {
                existing.homePosition = destination;
                existing.homeRotation = rotation;
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
        pickup.visuals = this;
        pickup.ownerPlayer = owner; pickup.handIndex = index; pickup.tarotType = type;
        pickup.onTable = placed; pickup.face = face;
        pickup.homePosition = position; pickup.homeRotation = rotation;
        string createdSymbol = TrumpSymbol(type);
        string createdDescription = TrumpDescription(type);
        pickup.symbol = createdSymbol;
        pickup.description = createdDescription;
        bool createdOwnedLocally = IsLocalOwner(owner);
        pickup.ownedLocally = createdOwnedLocally;
        pickup.Configure();
        card.name = (placed ? "TarotTable_" : "TarotHand_") + owner + "_" + index + "_" + type;
        return pickup;
    }

    public bool IsLocalOwner(int player)
    {
        return slotObjects != null && player >= 0 && player < slotObjects.Length
            && slotObjects[player] != null && Utilities.IsValid(Networking.LocalPlayer)
            && Networking.IsOwner(Networking.LocalPlayer, slotObjects[player]);
    }

    public void CardDropped()
    {
        TarotPickup card = incomingDroppedCard;
        if (card == null || card.onTable || card.pendingUse || card.awaitingUse) return;
        if (pendingCard != null) { card.ReturnHome(); return; }
        if (!IsLocalOwner(card.ownerPlayer) || !dealer.CanLocalPlayerAct()
            || dealer.turnIndex != card.ownerPlayer
            || TypeInHand(card.ownerPlayer, card.handIndex) != card.tarotType)
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
        pendingCard = card;
        pendingOwner = card.ownerPlayer;
        pendingEpoch = dealer.actionEpoch;
        pendingSince = Time.time;
        card.awaitingUse = true;
        card.moving = false;
        if (card.pickup != null) card.pickup.pickupable = false;
        if (card.cardCollider != null) card.cardCollider.enabled = false;
        dealer.incomingTrumpHandIndex = card.handIndex;
        dealer.RequestUseTrumpFromCard();
        PlayerSlot slot = slotObjects[pendingOwner].GetComponent<PlayerSlot>();
        pendingSequence = slot != null ? slot.ActionSeq : -1;
    }

    private void MoveCard(TarotPickup card, Vector3 destination, Quaternion rotation, bool consumed)
    {
        card.incomingDestination = destination;
        card.incomingRotation = rotation;
        card.incomingConsumed = consumed;
        card.ApplyMove();
    }
    private int CountInHand(int player)
    {
        int count = 0;
        for (int i = 0; i < dealer.trumpCount; i++) if (dealer.trumpOwner[i] == player) count++;
        return count;
    }
    private int TypeInHand(int player, int index)
    {
        int seen = 0;
        for (int i = 0; i < dealer.trumpCount; i++)
        {
            if (dealer.trumpOwner[i] != player) continue;
            if (seen == index) return dealer.trumpType[i];
            seen++;
        }
        return 0;
    }

    private Vector3 HandPosition(int player, int index)
    {
        BoxCollider spawn = spawnAreas[player];
        // Disabled colliders report empty bounds at the pivot, inside the stand.
        // Use the configured box geometry even when physics is disabled.
        Vector3 center = spawn.transform.TransformPoint(spawn.center);
        Vector3 halfX = spawn.transform.TransformVector(Vector3.right * spawn.size.x * 0.5f);
        Vector3 halfY = spawn.transform.TransformVector(Vector3.up * spawn.size.y * 0.5f);
        Vector3 halfZ = spawn.transform.TransformVector(Vector3.forward * spawn.size.z * 0.5f);
        float surfaceY = center.y + Mathf.Abs(halfX.y) + Mathf.Abs(halfY.y) + Mathf.Abs(halfZ.y);
        BoxCollider prefabCollider = tarotPrefab.GetComponent<BoxCollider>();
        float halfCardHeight = prefabCollider != null
            ? prefabCollider.size.y * tarotPrefab.transform.localScale.y * 0.5f : 0.005f;
        int column = index % 4;
        int row = index / 4;
        return new Vector3(center.x + (column - 1.5f) * handSpacing,
            surfaceY + halfCardHeight + 0.001f,
            center.z + (row - 0.5f) * 0.11f);
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
        if (language == 1) return PortugueseDescription(type);
        string name = TrumpSymbol(type);
        string effect = "";
        if (type >= 1 && type <= 6) effect = "Draw " + (type + 1) + " if it is still in the deck.";
        else if (type == 7) effect = "Set the target to 17.";
        else if (type == 8) effect = "Set the target to 24.";
        else if (type == 9) effect = "Set the target to 27.";
        else if (type == 10) effect = "Increase the stake by 1.";
        else if (type == 11) effect = "Increase the stake by 2.";
        else if (type == 12) effect = "Reduce the stake by 1.";
        else if (type == 13) effect = "Reduce the stake by 2.";
        else if (type == 14) effect = "Survive lethal round damage with 1 life.";
        else if (type == 15) effect = "Stake +1. Draw a trump card.";
        else if (type == 16) effect = "Destroy your opponent's latest active trump.";
        else if (type == 17) effect = "Destroy your opponent's latest active trump. If destroyed, draw a trump.";
        else if (type == 18) effect = "Both players draw 2 trump cards.";
        else if (type == 19) effect = "Draw a hidden number card.";
        else if (type == 20) effect = "Draw the number needed to reach the target, if available.";
        else if (type == 21) effect = "Remove your opponent's last number card. Keep their last hidden card.";
        else if (type == 22) effect = "Return your last number card. Keep your last hidden card.";
        else if (type == 23) effect = "Swap both players' last number cards. Keep each last hidden card.";
        else if (type == 24) effect = "Your opponent draws a number card. No bonus trump.";
        else if (type == 25) effect = "Replace your number cards with 2 new cards, the first hidden.";
        return name + "\n" + effect;
    }

    public void RefreshDescriptions()
    {
        for (int i = 0; handVisuals != null && i < handVisuals.Length; i++)
        {
            TarotPickup card = handVisuals[i];
            if (card == null) continue;
            int type = card.tarotType;
            string translated = TrumpDescription(type);
            card.description = translated;
            if (card.descriptionText != null) card.descriptionText.text = translated;
        }
        for (int i = 0; tableVisuals != null && i < tableVisuals.Length; i++)
        {
            TarotPickup card = tableVisuals[i];
            if (card == null) continue;
            int type = card.tarotType;
            string translated = TrumpDescription(type);
            card.description = translated;
            if (card.descriptionText != null) card.descriptionText.text = translated;
        }
    }

    private string PortugueseDescription(int type)
    {
        string effect = "";
        if (type >= 1 && type <= 6) effect = "Compre " + (type + 1) + " se ainda estiver no baralho.";
        else if (type == 7) effect = "Mude o alvo para 17.";
        else if (type == 8) effect = "Mude o alvo para 24.";
        else if (type == 9) effect = "Mude o alvo para 27.";
        else if (type == 10) effect = "Aumente a aposta em 1.";
        else if (type == 11) effect = "Aumente a aposta em 2.";
        else if (type == 12) effect = "Reduza a aposta em 1.";
        else if (type == 13) effect = "Reduza a aposta em 2.";
        else if (type == 14) effect = "Sobreviva ao dano fatal da rodada com 1 de vida.";
        else if (type == 15) effect = "Aposta +1. Compre uma carta trump.";
        else if (type == 16) effect = "Destrua a última trump ativa do adversário.";
        else if (type == 17) effect = "Destrua a última trump ativa do adversário. Se destruída, compre uma trump.";
        else if (type == 18) effect = "Ambos os jogadores compram 2 cartas trump.";
        else if (type == 19) effect = "Compre uma carta numérica oculta.";
        else if (type == 20) effect = "Compre o número necessário para alcançar o alvo, se disponível.";
        else if (type == 21) effect = "Remova a última carta numérica do adversário. Preserve a última carta oculta dele.";
        else if (type == 22) effect = "Devolva sua última carta numérica. Preserve sua última carta oculta.";
        else if (type == 23) effect = "Troque as últimas cartas numéricas dos jogadores. Preserve a última carta oculta de cada um.";
        else if (type == 24) effect = "O adversário compra uma carta numérica, sem bônus de trump.";
        else if (type == 25) effect = "Substitua suas cartas numéricas por 2 novas, sendo a primeira oculta.";
        return TrumpSymbol(type) + "\n" + effect;
    }
}

