using MenSharp;
using TMPro;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MatchHUD : MenSharpBehaviour
{
    public CardDealer dealer;
    public TextMeshPro[] displays;
    public float nextUpdate;
    public string lastDisplay;

    public void Update()
    {
        if (dealer == null || displays == null || Time.time < nextUpdate) return;
        nextUpdate = Time.time + 0.1f;
        string status = !dealer.matchStarted ? "WAITING FOR PLAYERS"
            : dealer.matchOver ? "MATCH FINISHED"
            : dealer.roundResolving ? "ROUND FINISHED"
            : "PLAYER " + (dealer.turnIndex + 1) + " TURN  /  " + dealer.turnSecondsLeft + "s";
        string result = "";
        if (dealer.roundResolving || dealer.matchOver)
        {
            result = dealer.lastRoundWinner < 0 ? "DRAW"
                : "PLAYER " + (dealer.lastRoundWinner + 1) + " WINS";
            if (dealer.lastTimeoutPlayer >= 0) result += "  /  TIME OUT";
        }
        string lives = dealer.life != null && dealer.life.Length >= 2
            ? "P1 LIFE " + dealer.life[0] + "   /   P2 LIFE " + dealer.life[1] : "";
        int bet = dealer.roundResolving || dealer.matchOver ? dealer.resolvedBet : dealer.CurrentBet();
        string value = "<size=80%>ROUND " + dealer.roundNumber + "   /   BET " + bet
            + "</size>\n" + lives + "\n" + status + (result.Length > 0 ? "\n" + result : "");
        if (value == lastDisplay) return;
        lastDisplay = value;
        for (int i = 0; i < displays.Length; i++)
            if (displays[i] != null) displays[i].text = value;
    }
}
