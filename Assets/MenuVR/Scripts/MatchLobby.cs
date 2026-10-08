using MenSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MatchLobby : MenSharpBehaviour
{
    public const int StateIdle = 0, StateHosting = 1, StateJoined = 2;
    public CardDealer dealer;
    public UdonBehaviour session;
    public bool previewOnly = true;
    public MenuRouter router;
    public float pollInterval = 0.1f;
    [HideInInspector] public int lobbyState;
    [HideInInspector] public bool isHost;
    [HideInInspector] public int playersPresent;
    [HideInInspector] public bool matchRunning;
    [HideInInspector] public bool canStart;
    [HideInInspector] public bool occupied;
    [HideInInspector] public int previousState = -1;
    [HideInInspector] public bool previousRunning;
    [HideInInspector] public int previousCount = -1;

    [HideInInspector] public float nextPoll;
    public void Update() { if (Time.unscaledTime < nextPoll) return; nextPoll = Time.unscaledTime + Mathf.Max(0.05f, pollInterval); Poll(); }
    public int HostId() { return previewOnly ? -1 : (int)session.GetProgramVariable("hostId"); }
    public int GuestId() { return previewOnly ? -1 : (int)session.GetProgramVariable("guestId"); }
    public bool IsHost() { return previewOnly ? lobbyState == StateHosting : Utilities.IsValid(Networking.LocalPlayer) && HostId() == Networking.LocalPlayer.playerId; }
    public int PlayersPresent() { return previewOnly ? 0 : (HostId() >= 0 ? 1 : 0) + (GuestId() >= 0 ? 1 : 0); }
    public bool MatchRunning() { return !previewOnly && (bool)session.GetProgramVariable("launching"); }
    public bool CanStartMatch() { return !previewOnly && IsHost() && GuestId() >= 0 && !MatchRunning(); }
    public bool Occupied() { return lobbyState != StateIdle; }
    public bool CanCreate() { return previewOnly ? !Occupied() : HostId() < 0 && !MatchRunning(); }
    public bool CanJoin() { return previewOnly ? !Occupied() : HostId() >= 0 && GuestId() < 0 && !Occupied() && !MatchRunning(); }
    public void RequestCreate()
    {
        if (!CanCreate()) { Say("A table is already open."); return; }
        if (previewOnly) { lobbyState = StateHosting; Publish(); }
        else session.SendCustomEvent("_RequestCreate");
    }
    public void RequestJoin()
    {
        if (!CanJoin()) { Say(HostId() < 0 ? "Create a table first." : "The table is full."); return; }
        if (previewOnly) { lobbyState = StateJoined; Publish(); }
        else session.SendCustomEvent("_RequestJoin");
    }
    public void RequestStart()
    {
        if (CanStartMatch()) session.SendCustomEvent("_RequestStart");
        else Say("Waiting for the second player.");
    }
    public void RequestLeave()
    {
        if (previewOnly) { lobbyState = StateIdle; Publish(); }
        else session.SendCustomEvent("_RequestLeave");
    }
    private void Poll()
    {

            if (!previewOnly && session != null && Utilities.IsValid(Networking.LocalPlayer))
            {
                int id = Networking.LocalPlayer.playerId;
                lobbyState = HostId() == id ? StateHosting : (GuestId() == id ? StateJoined : StateIdle);
            }
            isHost = IsHost(); playersPresent = PlayersPresent(); matchRunning = MatchRunning(); canStart = CanStartMatch(); occupied = Occupied();
            if (previousState != lobbyState || previousRunning != matchRunning || previousCount != playersPresent)
            {
                bool changedScreen = previousState != lobbyState;
                previousState = lobbyState; previousRunning = matchRunning; previousCount = playersPresent;
                if (changedScreen && router != null && router.ready)
                {
                    router.incomingScreen = lobbyState == StateHosting ? "create" : (lobbyState == StateJoined ? "join" : "main");
                    router.ApplyNavigation();
                }
                Say(matchRunning ? "Entering the game..." : (canStart ? "Both players are ready." : (occupied ? "Waiting for the second player." : "")));
                Publish();
            }
    }
    private void Publish() { if (router != null) router.Refresh(); }
    private void Say(string message) { if (router != null) { router.incomingStatus = message; router.ApplyStatus(); } }
}


