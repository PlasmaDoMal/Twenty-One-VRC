using MenSharp;
using TMPro;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MatchHUD : MenSharpBehaviour
{
    public CardDealer dealer;
    public TextMeshPro[] displays;
    [Min(0.05f)] public float fadeInSeconds = 0.32f;
    [Min(0.05f)] public float fadeOutSeconds = 0.22f;
    [Min(0f)] public float slideDistance = 0.035f;
    [Min(0f)] public float visibleSeconds = 2.5f;
    [HideInInspector] public float hideAt;
    [HideInInspector] public bool fadingToHidden;
    [HideInInspector] public string contentKey;
    [HideInInspector] public int pendingTurn = -1;
    [HideInInspector] public int announcedRound = -1;
    [HideInInspector] public int announcedTurnEpoch = -1;
    [HideInInspector] public bool roundAnnouncement;
    [HideInInspector] public Vector3[] restPositions;
    [HideInInspector] public Color[] textColors;
    [HideInInspector] public bool initialized;
    [HideInInspector] public int animationState;
    [HideInInspector] public float animationTime;
    [HideInInspector] public float exitAlpha = 1f;
    [HideInInspector] public float exitOffset;
    [HideInInspector] public float currentAlpha;
    [HideInInspector] public float currentOffset;
    public float nextUpdate;
    public string lastDisplay;
    public string pendingDisplay;
    public int hudLifeOne, hudLifeTwo, hudBet, hudTarget, hudTurn, hudSeconds, hudWinner;
    public bool hudResolving, hudMatchOver;

    public void Start() { Initialize(); }
    public void Initialize()
    {
        if (initialized || displays == null) return;
        restPositions = new Vector3[displays.Length];
        textColors = new Color[displays.Length];
        for (int i = 0; i < displays.Length; i++)
        {
            if (displays[i] == null) continue;
            restPositions[i] = displays[i].transform.localPosition;
            textColors[i] = displays[i].color;
            displays[i].text = "";
        }
        initialized = true;
        lastDisplay = "";
        pendingDisplay = "";
        contentKey = "";
        ApplyVisual(0f, -slideDistance);
    }
    public void OnEnable() { nextUpdate = 0f; }
    public void OnDisable()
    {
        if (!initialized) return;
        ApplyVisual(0f, 0f);
        lastDisplay = "";
        pendingDisplay = "";
        animationState = 0;
        contentKey = "";
        fadingToHidden = false;
        announcedRound = -1;
        announcedTurnEpoch = -1;
        roundAnnouncement = false;
    }
    public void ApplyVisual(float alpha, float offset)
    {
        currentAlpha = alpha;
        currentOffset = offset;
        for (int i = 0; i < displays.Length; i++)
        {
            if (displays[i] == null) continue;
            Color color = textColors[i];
            color.a *= alpha;
            displays[i].color = color;
            displays[i].transform.localPosition = restPositions[i] + Vector3.up * offset;
        }
    }
    public void BeginEntry()
    {
        lastDisplay = pendingDisplay;
        for (int i = 0; i < displays.Length; i++)
            if (displays[i] != null) displays[i].text = pendingTurn < 0 || i == pendingTurn ? lastDisplay : "";
        animationState = 2;
        fadingToHidden = false;
        animationTime = 0f;
        ApplyVisual(0f, -slideDistance);
    }
    public void QueueMessage(string value, int player)
    {
        string key = value + ":" + player;
        if (key == contentKey) return;
        contentKey = key;
        pendingDisplay = value;
        pendingTurn = player;
        fadingToHidden = value == "";
        if (currentAlpha <= 0f && value != "") BeginEntry();
        else if (animationState != 1)
        {
            exitAlpha = currentAlpha;
            exitOffset = currentOffset;
            animationTime = 0f;
            animationState = 1;
        }
    }
    public void Update()
    {
        Initialize();
        if (!initialized || dealer == null) return;
        if (Time.time >= nextUpdate)
        {
            nextUpdate = Time.time + 0.1f;
            hudLifeOne = dealer.life != null && dealer.life.Length > 0 ? dealer.life[0] : dealer.startingLife;
            hudLifeTwo = dealer.life != null && dealer.life.Length > 1 ? dealer.life[1] : dealer.startingLife;
            hudBet = dealer.roundResolving || dealer.matchOver ? dealer.resolvedBet : dealer.CurrentBet();
            hudTarget = dealer.EffectiveTarget();
            hudTurn = dealer.turnIndex; hudSeconds = dealer.turnSecondsLeft;
            hudWinner = dealer.lastRoundWinner; hudResolving = dealer.roundResolving; hudMatchOver = dealer.matchOver;
            if (!dealer.matchStarted || dealer.matchOver || dealer.roundResolving)
            {
                roundAnnouncement = false;
                if (!dealer.matchStarted) { announcedRound = -1; announcedTurnEpoch = -1; }
                QueueMessage("", -1);
            }
            else if (announcedRound != dealer.roundNumber)
            {
                announcedRound = dealer.roundNumber;
                announcedTurnEpoch = -1;
                roundAnnouncement = true;
                QueueMessage("ROUND " + dealer.roundNumber, -1);
            }
            else if (!roundAnnouncement)
            {
                if (dealer.CanLocalPlayerAct() && announcedTurnEpoch != dealer.actionEpoch)
                {
                    announcedTurnEpoch = dealer.actionEpoch;
                    QueueMessage("YOUR TURN", dealer.turnIndex);
                }
                else if (!dealer.IsLocalPlayer(dealer.turnIndex)) QueueMessage("", -1);
            }
        }
        if (animationState == 0 && currentAlpha > 0f && Time.time >= hideAt)
        {
            if (roundAnnouncement && dealer.CanLocalPlayerAct())
            {
                roundAnnouncement = false;
                announcedTurnEpoch = dealer.actionEpoch;
                QueueMessage("YOUR TURN", dealer.turnIndex);
            }
            else
            {
                roundAnnouncement = false;
                QueueMessage("", -1);
            }
        }
        if (animationState == 0) return;
        animationTime += Time.deltaTime;
        float duration = animationState == 1 ? fadeOutSeconds : fadeInSeconds;
        float t = Mathf.Clamp01(animationTime / Mathf.Max(0.05f, duration));
        float eased = t * t * (3f - 2f * t);
        if (animationState == 1)
        {
            ApplyVisual(exitAlpha * (1f - eased), Mathf.Lerp(exitOffset, slideDistance, eased));
            if (t >= 1f)
            {
                if (fadingToHidden)
                {
                    animationState = 0;
                    ApplyVisual(0f, 0f);
                    for (int i = 0; i < displays.Length; i++)
                        if (displays[i] != null) displays[i].text = "";
                }
                else BeginEntry();
            }
        }
        else
        {
            ApplyVisual(eased, -slideDistance * (1f - eased));
            if (t >= 1f) { animationState = 0; hideAt = Time.time + visibleSeconds; ApplyVisual(1f, 0f); }
        }
    }
}
