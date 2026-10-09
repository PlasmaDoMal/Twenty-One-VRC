using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class SpectatorDoorTeleport : UdonSharpBehaviour
{
    public Transform destination;
    public UdonBehaviour matchSession;
    [Min(0f)] public float heightOffset = 0.05f;

    public override void Interact()
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        if (!Utilities.IsValid(player) || destination == null || matchSession == null) return;
        int host = (int)matchSession.GetProgramVariable("hostId");
        int guest = (int)matchSession.GetProgramVariable("guestId");
        if (player.playerId == host || player.playerId == guest
            || (int)matchSession.GetProgramVariable("transitionState") != 0) return;
        matchSession.SendCustomEvent("_BeginSpectating");
        player.TeleportTo(destination.position + Vector3.up * heightOffset,
            Quaternion.Euler(0f, destination.eulerAngles.y, 0f));
    }
}
