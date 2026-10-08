using System.Threading.Tasks;
using MenSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class IntroMenuController : MenSharpBehaviour
{
    public Transform introSpawn;
    public Transform warehouseSpawn;
    public GameObject introMenuRoot;
    public GameObject blackoutRoot;
    public Renderer fadeRenderer;
    public Button playButton;
    public CanvasGroup menuGroup;
    public GameObject selectionIndicator;
    public AudioSource hoverAudio;
    public AudioSource clickAudio;
    public AudioSource transitionAudio;
    public bool immobilizePlayer = false;
    public bool immobilizeDuringTransition = true;
    public bool useFullscreenBackdrop = false;
    public bool followPlayer = false;
    public Transform fixedMenuAnchor;
    public float followTurnThreshold = 25f;
    public float followTurnSpeed = 8f;
    [HideInInspector] public float menuYaw;
    [HideInInspector] public float targetYaw;
    [HideInInspector] public bool movementLocked;
    public float fadeToBlackDuration = 0.8f;
    public float blackHoldDuration = 0.2f;
    public float postTeleportDelay = 0.1f;
    public float fadeFromBlackDuration = 1.0f;
    public float menuRevealDuration = 0.4f;
    public LogoIntroAnimator logoAnimator;
    public float menuDistance = 1f;
    [HideInInspector] public bool introReady;
    [HideInInspector] public bool introCompleted;
    // 0 Waiting, 1 Closing, 2 BlackHold, 3 TeleportPending, 4 Opening, 5 Complete.
    [HideInInspector] public int state;
    [HideInInspector] public VRCPlayerApi localPlayer;
    [HideInInspector] public Material fadeMaterial;
    [HideInInspector] public float elapsed;
    [HideInInspector] public float revealElapsed;
    [HideInInspector] public bool initialized;
    [HideInInspector] public bool respawnPending;

    public void Start()
    {
        initialized = false;
        introCompleted = false;
        state = 0;
        elapsed = 0f;
        revealElapsed = 0f;
        movementLocked = false;
        respawnPending = false;
        fadeMaterial = fadeRenderer.material;
        SetFade(0f);
        introMenuRoot.SetActive(true);
        blackoutRoot.SetActive(useFullscreenBackdrop);
        playButton.interactable = false;
        if (menuGroup != null) menuGroup.alpha = logoAnimator != null ? 1f : 0f;

    }

    public void Update()
    {
        TickMenu();
    }

    private void TickMenu()
    {
        if (!initialized)
        {
            localPlayer = Networking.LocalPlayer;
            if (!Utilities.IsValid(localPlayer)) return;
            if (introSpawn == null || warehouseSpawn == null || fadeRenderer == null) return;
            initialized = true;
            if (immobilizePlayer) { localPlayer.Immobilize(true); movementLocked = true; }
            AlignMenu();
            introReady = logoAnimator == null;
            playButton.interactable = introReady;
            if (logoAnimator != null) logoAnimator.BeginIntro();
        }
        if (state == 0)
        {
            if (logoAnimator != null)
            {
                introReady = logoAnimator.readyForPlay;
                playButton.interactable = introReady;
            }
            else
            {
                revealElapsed += Time.deltaTime;
                if (menuGroup != null) menuGroup.alpha = Ease(revealElapsed, menuRevealDuration);
            }
            return;
        }
        if (state == 5) return;
        elapsed += Time.deltaTime;
        if (state == 1)
        {
            SetFade(Ease(elapsed, fadeToBlackDuration));
            if (elapsed >= Mathf.Max(0f, fadeToBlackDuration))
            {
                SetFade(1f);
                elapsed = 0f;
                state = 2;
            }
        }
        else if (state == 2)
        {
            // Separate frame plus hold ensures a fully black frame was rendered before teleport.
            if (elapsed >= Mathf.Max(0.15f, blackHoldDuration))
            {
                state = 3;
                elapsed = 0f;

            }
        }
        else if (state == 3)
        {
            TeleportUnderBlack();
        }
        else if (state == 4)
        {
            if (elapsed < Mathf.Max(0.1f, postTeleportDelay)) return;
            float opening = elapsed - Mathf.Max(0.1f, postTeleportDelay);
            SetFade(1f - Ease(opening, fadeFromBlackDuration));
            if (opening >= Mathf.Max(0f, fadeFromBlackDuration))
            {
                SetFade(0f);
                fadeRenderer.enabled = false;
                state = 5;
                if (movementLocked) { localPlayer.Immobilize(false); movementLocked = false; }
            }
        }
    }

    public void Play()
    {
        if (!initialized || !introReady || state != 0 || warehouseSpawn == null) return;
        if (immobilizeDuringTransition) { localPlayer.Immobilize(true); movementLocked = true; }
        if (logoAnimator != null) logoAnimator.StopIntro();
        playButton.interactable = false;
        if (selectionIndicator != null && selectionIndicator != gameObject) selectionIndicator.SetActive(false);
        if (clickAudio != null && clickAudio.clip != null) clickAudio.Play();
        if (transitionAudio != null && transitionAudio.clip != null) transitionAudio.Play();
        introCompleted = true;
        if (menuGroup != null) { menuGroup.alpha = 1f; menuGroup.interactable = false; }
        fadeRenderer.enabled = true;
        elapsed = 0f;
        state = 1;
    }

    public void TeleportUnderBlack()
    {
        if (state != 3 || !Utilities.IsValid(localPlayer)) return;
        if (warehouseSpawn == null)
        {
            // Recover safely if a destination was removed at runtime.
            introCompleted = false;
            if (movementLocked && !immobilizePlayer) { localPlayer.Immobilize(false); movementLocked = false; }
            if (logoAnimator != null) logoAnimator.BeginIntro();
            state = 0;
            SetFade(0f);
            playButton.interactable = true;
            if (menuGroup != null) menuGroup.interactable = true;
            return;
        }
        SetFade(1f);
        localPlayer.TeleportTo(warehouseSpawn.position, warehouseSpawn.rotation);
        introMenuRoot.SetActive(false);
        blackoutRoot.SetActive(false);
        elapsed = 0f;
        state = 4;
    }

    public void PlayHover()
    {
        if (state != 0 || !initialized || !introReady) return;
        if (logoAnimator != null) logoAnimator.HoverOn();
        if (selectionIndicator != null && selectionIndicator != gameObject) selectionIndicator.SetActive(true);
        if (hoverAudio != null && hoverAudio.clip != null) hoverAudio.Play();
    }
    public void PlayExit()
    {
        if (logoAnimator != null) logoAnimator.HoverOff();
        else if (selectionIndicator != null && selectionIndicator != gameObject) selectionIndicator.SetActive(false);
    }
    public void OnPlayerRespawn(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player) || !player.isLocal || !initialized) return;
        if (introCompleted)
        {
            // Defer beyond the SDK respawn operation; never reopen the intro.
            respawnPending = true;
            Scheduler.Run(() => RedirectAfterFrame());
        }
        else
        {
            if (immobilizePlayer) { player.Immobilize(true); movementLocked = true; }
            Scheduler.Run(() => AlignAfterFrame());
        }
    }
    public void RedirectRespawn()
    {
        if (!respawnPending || !Utilities.IsValid(localPlayer)) return;
        respawnPending = false;
        if (state == 1 || state == 2 || state == 3) return;
        if (warehouseSpawn != null) localPlayer.TeleportTo(warehouseSpawn.position, warehouseSpawn.rotation);
    }
    private async Task TeleportAfterFrame()
    {
        await Scheduler.DelayFrames(1);
        TeleportUnderBlack();
    }
    private async Task RedirectAfterFrame()
    {
        await Scheduler.DelayFrames(1);
        RedirectRespawn();
    }
    private async Task AlignAfterFrame()
    {
        await Scheduler.DelayFrames(1);
        AlignMenu();
    }
    public void AlignMenu()
    {
        if (!Utilities.IsValid(localPlayer) || introSpawn == null) return;
        if (!followPlayer && fixedMenuAnchor != null)
        {
            introMenuRoot.transform.SetPositionAndRotation(fixedMenuAnchor.position, fixedMenuAnchor.rotation);
            return;
        }
        Vector3 head = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
        menuYaw = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation.eulerAngles.y;
        targetYaw = menuYaw;
        Quaternion yaw = Quaternion.Euler(0f, menuYaw, 0f);
        introMenuRoot.transform.SetPositionAndRotation(head + yaw * Vector3.forward * menuDistance, yaw);
    }
    public void PostLateUpdate()
    {
        if (!initialized || state == 5 || !Utilities.IsValid(localPlayer)) return;
        Vector3 head = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
        if (followPlayer && state == 0)
        {
            float headYaw = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation.eulerAngles.y;
            if (Mathf.Abs(Mathf.DeltaAngle(targetYaw, headYaw)) > followTurnThreshold) targetYaw = headYaw;
            menuYaw = Mathf.LerpAngle(menuYaw, targetYaw, 1f - Mathf.Exp(-followTurnSpeed * Time.deltaTime));
            Quaternion facing = Quaternion.Euler(0f, menuYaw, 0f);
            introMenuRoot.transform.SetPositionAndRotation(head + facing * Vector3.forward * menuDistance, facing);
        }
        // Only the transition fade covers the eye viewport in the walkable room.
        blackoutRoot.transform.position = head;
        fadeRenderer.transform.position = head;
    }
    [HideInInspector] public float Ease(float time, float duration)
    {
        float t = Mathf.Clamp01(time / Mathf.Max(0.001f, duration));
        return t * t * (3f - 2f * t);
    }
    private void SetFade(float alpha)
    {
        if (fadeMaterial != null) fadeMaterial.SetFloat("_Alpha", alpha);
    }
    public void OnDisable()
    {
        if (initialized && Utilities.IsValid(localPlayer) && movementLocked)
            localPlayer.Immobilize(false);
    }
}
