using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.SDK3.UdonNetworkCalling;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class WorldMatchSession : UdonSharpBehaviour
{
    public GameObject gameRoot;
    public GameObject[] lobbyRoots;
    public GameObject[] introVisualRoots;
    public UdonBehaviour dealer;
    public GameObject[] playerSlots;
    public Transform play1;
    public Transform play2;
    public Transform lobbySpawn;
    public UdonBehaviour menuRouter;
    public UdonBehaviour introController;
    public Renderer fadeRenderer;
    public float fadeOutSeconds = 0.35f;
    public float blackHoldSeconds = 0.12f;
    public float fadeInSeconds = 0.45f;
    [UdonSynced] public int hostId = -1;
    [UdonSynced] public int guestId = -1;
    [UdonSynced] public int launchEpoch;
    [UdonSynced] public bool launching;
    [UdonSynced] public double startAt;
    public int transitionState;
    public int localSeat = -1;
    public bool teleported;
    public bool initialized;
    private int seenEpoch;
    private int requestedEpoch;
    private float elapsed;
    private bool returning;
    private Material fadeMaterial;
    private bool wasParticipant;

    private void Start()
    {
        if (fadeRenderer != null) { fadeMaterial = fadeRenderer.material; fadeMaterial.SetFloat("_Alpha", 0f); fadeRenderer.enabled = false; }
        initialized = true;
        ApplyWorldState();
    }

    private VRCPlayerApi Caller()
    {
        return NetworkCalling.InNetworkCall ? NetworkCalling.CallingPlayer : Networking.LocalPlayer;
    }
    private bool IntroDone()
    {
        return introController == null || (bool)introController.GetProgramVariable("introCompleted");
    }
    public void _RequestCreate() { if (IntroDone()) SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(CreateTable)); }
    public void _RequestJoin() { if (IntroDone()) SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(JoinTable)); }
    public void _RequestStart() { SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(StartTable)); }
    public void _RequestLeave() { SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(LeaveTable)); }

    [NetworkCallable]
    public void CreateTable()
    {
        if (!Networking.IsOwner(gameObject)) return;
        VRCPlayerApi player = Caller();
        if (!Utilities.IsValid(player) || hostId >= 0 || launching) return;
        hostId = player.playerId;
        guestId = -1;
        ApplyWorldState();
        Networking.SetOwner(player, dealer.gameObject);
        Networking.SetOwner(player, playerSlots[0]);
        RequestSerialization();
    }
    // Explicit diagnostic mode: one player controls both seats, no artificial guest.
    public void _RequestSoloStart() { if (IntroDone()) SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(StartSoloTable)); }
    [NetworkCallable]
    public void StartSoloTable()
    {
        if (!Networking.IsOwner(gameObject)) return;
        VRCPlayerApi player = Caller();
        if (!Utilities.IsValid(player) || launching || hostId != player.playerId || guestId >= 0) return;
        guestId = hostId;
        Networking.SetOwner(player, dealer.gameObject);
        Networking.SetOwner(player, playerSlots[0]);
        Networking.SetOwner(player, playerSlots[1]);
        launching = true;
        startAt = Networking.GetServerTimeInSeconds() + 1.5;
        launchEpoch++;
        RequestSerialization();
        ApplyWorldState();
    }
    [NetworkCallable]
    public void JoinTable()
    {
        if (!Networking.IsOwner(gameObject)) return;
        VRCPlayerApi player = Caller();
        if (!Utilities.IsValid(player) || hostId < 0 || guestId >= 0 || launching || player.playerId == hostId) return;
        guestId = player.playerId;
        ApplyWorldState();
        Networking.SetOwner(player, playerSlots[1]);
        RequestSerialization();
    }
    [NetworkCallable]
    public void StartTable()
    {
        if (!Networking.IsOwner(gameObject)) return;
        VRCPlayerApi player = Caller();
        if (!Utilities.IsValid(player) || player.playerId != hostId || launching || guestId < 0) return;
        VRCPlayerApi host = VRCPlayerApi.GetPlayerById(hostId);
        VRCPlayerApi guest = VRCPlayerApi.GetPlayerById(guestId);
        if (!Utilities.IsValid(host) || !Utilities.IsValid(guest) || hostId == guestId) return;
        Networking.SetOwner(host, dealer.gameObject);
        Networking.SetOwner(host, playerSlots[0]);
        Networking.SetOwner(guest, playerSlots[1]);
        launching = true;
        startAt = Networking.GetServerTimeInSeconds() + 1.5;
        launchEpoch++;
        RequestSerialization();
        ApplyWorldState();
    }
    [NetworkCallable]
    public void LeaveTable()
    {
        if (!Networking.IsOwner(gameObject)) return;
        VRCPlayerApi player = Caller();
        if (!Utilities.IsValid(player)) return;
        if (player.playerId == hostId) { hostId = -1; guestId = -1; launching = false; }
        else if (player.playerId == guestId) { guestId = -1; launching = false; }
        else return;
        RequestSerialization();
        ApplyWorldState();
    }
    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (player.playerId == hostId) { hostId = -1; guestId = -1; launching = false; }
        else if (player.playerId == guestId) { guestId = -1; launching = false; }
        else return;
        RequestSerialization();
    }
    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (hostId >= 0 && !Utilities.IsValid(VRCPlayerApi.GetPlayerById(hostId)))
        { hostId = -1; guestId = -1; launching = false; }
        else if (guestId >= 0 && !Utilities.IsValid(VRCPlayerApi.GetPlayerById(guestId)))
        { guestId = -1; launching = false; }
        RequestSerialization();
        ApplyWorldState();
    }
    public override void OnDeserialization() { ApplyWorldState(); }
    private void ApplyWorldState()
    {
        bool visible = launching || teleported || (transitionState != 0 && !returning);
        if (gameRoot != null && gameRoot.activeSelf != visible) gameRoot.SetActive(visible);
        // State 3 is entered under black before the intro teleports to the basement.
        // The destination must be active before the move and throughout fade-in.
        bool lobbyReady = introController == null || (int)introController.GetProgramVariable("state") >= 3;
        bool lobbyVisible = lobbyReady && (!teleported || (transitionState != 0 && returning));
        if (lobbyRoots != null) for (int i = 0; i < lobbyRoots.Length; i++)
            if (lobbyRoots[i] != null && lobbyRoots[i].activeSelf != lobbyVisible) lobbyRoots[i].SetActive(lobbyVisible);
        bool introVisible = introController != null && (int)introController.GetProgramVariable("state") < 4;
        if (introVisualRoots != null) for (int i = 0; i < introVisualRoots.Length; i++)
            if (introVisualRoots[i] != null && introVisualRoots[i].activeSelf != introVisible) introVisualRoots[i].SetActive(introVisible);
    }
    private void Update()
    {
        if (!initialized || !Utilities.IsValid(Networking.LocalPlayer)) return;
        if (!launching && Networking.IsOwner(dealer.gameObject)
            && (bool)dealer.GetProgramVariable("matchStarted")) dealer.SendCustomEvent("AbortMatch");
        ApplyWorldState();
        int id = Networking.LocalPlayer.playerId;
        localSeat = id == hostId ? 0 : (id == guestId ? 1 : -1);
        bool participant = localSeat >= 0;
        if (wasParticipant && (!participant || !launching) && teleported && transitionState == 0) BeginTransition(true);
        wasParticipant = participant;
        if (launching && launchEpoch != seenEpoch && participant && IntroDone())
        {
            seenEpoch = launchEpoch;
            BeginTransition(false);
        }
        if (transitionState != 0) TickTransition();
        if (launching && localSeat == 0 && teleported && transitionState == 0 && requestedEpoch != launchEpoch && Networking.GetServerTimeInSeconds() >= startAt && Networking.IsOwner(dealer.gameObject) && Networking.IsOwner(playerSlots[0]))
        {
            requestedEpoch = launchEpoch;
            dealer.SendCustomEvent("RequestStartMatch");
        }
        if (launching && localSeat == 0 && requestedEpoch == launchEpoch && (bool)dealer.GetProgramVariable("matchOver")) _RequestLeave();
    }
    private void BeginTransition(bool toLobby)
    {
        if (fadeRenderer == null || fadeMaterial == null) return;
        // Clear the previous table under the fade, before revealing the destination.
        if (!toLobby && localSeat == 0 && requestedEpoch != launchEpoch && Networking.IsOwner(dealer.gameObject))
            dealer.SendCustomEvent("PrepareLobbySession");
        returning = toLobby;
        elapsed = 0f;
        transitionState = 1;
        fadeRenderer.enabled = true;
        Networking.LocalPlayer.Immobilize(true);
        PositionFade();
    }
    private void TickTransition()
    {
        elapsed += Time.deltaTime;
        if (transitionState == 1)
        {
            fadeMaterial.SetFloat("_Alpha", Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeOutSeconds))));
            if (elapsed >= fadeOutSeconds) { transitionState = 2; elapsed = 0f; fadeMaterial.SetFloat("_Alpha", 1f); }
        }
        else if (transitionState == 2 && elapsed >= blackHoldSeconds)
        {
            if (!launching || localSeat < 0) returning = true;
            Transform destination = returning ? lobbySpawn : (localSeat == 0 ? play1 : play2);
            if (destination != null)
            {
                Networking.LocalPlayer.TeleportTo(destination.position, destination.rotation);
                teleported = !returning;
                Debug.Log("WorldMatchSession: teleported local player to " + destination.name);
            }
            transitionState = 3; elapsed = 0f;
        }
        else if (transitionState == 3)
        {
            fadeMaterial.SetFloat("_Alpha", 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeInSeconds))));
            if (elapsed >= fadeInSeconds)
            {
                fadeMaterial.SetFloat("_Alpha", 0f); fadeRenderer.enabled = false;
                Networking.LocalPlayer.Immobilize(false); transitionState = 0;
            }
        }
        PositionFade();
    }
    private void PositionFade() { fadeRenderer.transform.position = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position; }
    public override void PostLateUpdate() { if (transitionState != 0 && Utilities.IsValid(Networking.LocalPlayer)) PositionFade(); }
    public override void OnPlayerRespawn(VRCPlayerApi player)
    {
        if (player.isLocal && launching && localSeat >= 0) SendCustomEventDelayedFrames(nameof(_RespawnAtTable), 3);
    }
    public void _RespawnAtTable() { if (launching && localSeat >= 0) BeginTransition(false); }
    private void OnDisable()
    {
        if (fadeRenderer != null) fadeRenderer.enabled = false;
        if (Utilities.IsValid(Networking.LocalPlayer) && transitionState != 0) Networking.LocalPlayer.Immobilize(false);
    }
}


