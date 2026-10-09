using UdonSharp;
using TMPro;
using UnityEngine;
using VRC.Udon;
using VRC.SDKBase;

// Read-only example view: bind snapshot to the existing MatchHUD program.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TwentyOneHUDView : UdonSharpBehaviour
{
    public UdonBehaviour snapshot;
    public TMP_Text lifeOne, lifeTwo, bet, target, turn, result;
    public RectTransform blade;
    public CanvasGroup resultGroup;
    public bool reduceMotion;
    private float nextPoll;
    private float revealAt;
    private bool wasResolving;
    private int lastBet = -1;
    private float shakeUntil;
    private Vector3 bladeRest;
    private void Start() { if (blade != null) bladeRest = blade.anchoredPosition3D; }
    private void Update()
    {
        if (snapshot == null) return;
        float now = Time.unscaledTime;
        if (now >= nextPoll)
        {
            nextPoll = now + 0.1f;
            int a = (int)snapshot.GetProgramVariable("hudLifeOne");
            int b = (int)snapshot.GetProgramVariable("hudLifeTwo");
            int stake = (int)snapshot.GetProgramVariable("hudBet");
            int goal = (int)snapshot.GetProgramVariable("hudTarget");
            int player = (int)snapshot.GetProgramVariable("hudTurn");
            int seconds = (int)snapshot.GetProgramVariable("hudSeconds");
            bool resolving = (bool)snapshot.GetProgramVariable("hudResolving");
            bool over = (bool)snapshot.GetProgramVariable("hudMatchOver");
            int winner = (int)snapshot.GetProgramVariable("hudWinner");
            if (lifeOne != null) lifeOne.text = "01 / LIFE " + a;
            if (lifeTwo != null) lifeTwo.text = "02 / LIFE " + b;
            if (bet != null) bet.text = "TALLY " + stake;
            if (target != null) target.text = "TARGET " + goal;
            if (turn != null) turn.text = "SEAT " + (player + 1) + " / " + seconds + "s";
            if (stake != lastBet) { if (lastBet >= 0) shakeUntil = now + 0.18f; lastBet = stake; }
            if ((resolving || over) && !wasResolving) revealAt = now + (reduceMotion ? 0f : 0.6f);
            wasResolving = resolving || over;
            if (result != null) result.text = winner < 0 ? "NO BLOOD. DRAW." : over ? "SEAT " + (winner + 1) + " SURVIVES." : "SEAT " + (2 - winner) + " / DAMAGE " + stake;
        }
        if (resultGroup != null) resultGroup.alpha = !wasResolving || now < revealAt ? 0f : Mathf.Clamp01((now - revealAt) / 0.25f);
        if (blade != null)
        {
            Vector3 p = bladeRest + new Vector3(Mathf.Clamp(lastBet, 0, 3) * 80f, 0f, 0f);
            if (!reduceMotion && now < shakeUntil) p.x += Mathf.Sin(now * 95f) * 2f;
            blade.anchoredPosition3D = p;
        }
    }
}
