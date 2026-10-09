using MenSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MachineKillerMotion : MenSharpBehaviour
{
    public CardDealer dealer;
    public Transform machine;
    public Transform playerOne;
    public Transform playerTwo;
    [Tooltip("Maximum fraction of the original path before the match ends.")]
    [Range(0f, 0.5f)] public float approachLimit = 0.5f;
    [Min(0.1f)] public float moveDuration = 1.2f;
    [HideInInspector] public Vector3 homeLocalPosition;
    [HideInInspector] public Vector3 moveStart;
    [HideInInspector] public Vector3 destination;
    [HideInInspector] public float moveStartedAt;
    [HideInInspector] public float nextPoll;
    [HideInInspector] public float progress;
    [HideInInspector] public int targetPlayer = -1;
    [HideInInspector] public int seenLife = -1;
    [HideInInspector] public bool initialized;
    [HideInInspector] public bool moving;
    [HideInInspector] public bool seenDefeat;
    [HideInInspector] public bool finalArrivalComplete;

    public void Start()
    {
        if (machine == null) machine = transform;
        homeLocalPosition = machine.localPosition;
        initialized = true;
        ResetMachine();
    }
    public void OnDisable() { if (initialized) ResetMachine(); }
    public void ResetMachine()
    {
        if (machine == null || !initialized) return;
        machine.localPosition = homeLocalPosition;
        moving = false;
        targetPlayer = -1;
        seenLife = -1;
        seenDefeat = false;
        finalArrivalComplete = false;
        progress = 0f;
        nextPoll = 0f;
    }
    public void Update()
    {
        if (!initialized || dealer == null || machine == null) return;
        Vector3 home = machine.parent != null ? machine.parent.TransformPoint(homeLocalPosition) : homeLocalPosition;
        if (!dealer.matchStarted)
        {
            if (targetPlayer >= 0 || moving) ResetMachine();
            return;
        }
        if (Time.time >= nextPoll)
        {
            nextPoll = Time.time + 0.05f;
            int player = dealer.machineTargetPlayer;
            if (player < 0)
            {
                if (targetPlayer >= 0 || moving) ResetMachine();
                finalArrivalComplete = dealer.matchOver;
                return;
            }
            if (player > 1 || dealer.life == null || dealer.life.Length <= player) { finalArrivalComplete = dealer.matchOver; return; }
            Transform target = player == 0 ? playerOne : playerTwo;
            if (target == null) { finalArrivalComplete = dealer.matchOver; return; }
            int remaining = dealer.life[player];
            // The final advance starts only after the dealer ends the entire match.
            bool defeated = dealer.matchOver && dealer.lastRoundWinner == 1 - player;
            if (!defeated) finalArrivalComplete = dealer.matchOver;
            if (player != targetPlayer || remaining != seenLife || defeated != seenDefeat)
            {
                targetPlayer = player;
                seenLife = remaining;
                seenDefeat = defeated;
                if (defeated) finalArrivalComplete = false;
                float lostFraction = 1f - (float)remaining / Mathf.Max(1, dealer.startingLife);
                progress = defeated ? 1f : Mathf.Clamp01(lostFraction) * Mathf.Clamp(approachLimit, 0f, 0.5f);
                Vector3 targetPosition = target.position;
                targetPosition.y = home.y;
                destination = Vector3.Lerp(home, targetPosition, progress);
                moveStart = machine.position;
                moveStart.y = home.y;
                moveStartedAt = Time.time;
                moving = true;
            }
        }
        if (!moving) return;
        float duration = Mathf.Max(0.1f, moveDuration);
        float t = Mathf.Clamp01((Time.time - moveStartedAt) / duration);
        float eased = t * t * (3f - 2f * t);
        Vector3 position = Vector3.Lerp(moveStart, destination, eased);
        position.y = home.y;
        machine.position = position;
        if (t >= 1f) { moving = false; if (seenDefeat) finalArrivalComplete = true; }
    }
}
