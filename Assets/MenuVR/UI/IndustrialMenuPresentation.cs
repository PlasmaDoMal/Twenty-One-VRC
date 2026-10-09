using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRC.Udon;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class IndustrialMenuPresentation : UdonSharpBehaviour
{
    public Text[] sources;
    public TextMeshProUGUI[] labels;
    public UdonBehaviour session;
    public UdonBehaviour router;
    public UdonBehaviour[] screens;
    public UdonBehaviour[] buttons;
    public TwentyOneUIAnimator animator;
    public CanvasGroup menuGroup;
    public TextMeshProUGUI seatOne;
    public TextMeshProUGUI seatTwo;
    public TextMeshProUGUI motionLabel;
    public TextMeshProUGUI title;
    public TextMeshProUGUI warning;
    public TextMeshProUGUI terms;
    public UdonBehaviour dealer;
    public Toggle motionToggle;
    public GameObject readyButton;
    public bool reduceMotion;
    private float nextPoll;
    private bool visible;

    private void Start()
    {
        ApplyMotion();
    }
    public void ToggleReducedMotion() { reduceMotion = !reduceMotion; ApplyMotion(); }
    public void SetReducedMotion() { if (motionToggle != null) reduceMotion = motionToggle.isOn; ApplyMotion(); }
    private void ApplyMotion()
    {
        if (animator != null) animator.reduceMotion = reduceMotion;
        if (motionLabel != null) motionLabel.text = "Steady view";
        if (router != null)
        {
            router.SetProgramVariable("idleAmplitude", 0f);
            router.SetProgramVariable("introScale", reduceMotion ? 1f : 0.92f);
            router.SetProgramVariable("introRise", reduceMotion ? 0f : 14f);
            router.SetProgramVariable("introDuration", reduceMotion ? 0.1f : 0.45f);
        }
        for (int i = 0; screens != null && i < screens.Length; i++)
        {
            if (screens[i] == null) continue;
            screens[i].SetProgramVariable("slideDistance", reduceMotion ? 0f : 12f);
            screens[i].SetProgramVariable("childSlide", reduceMotion ? 0f : 10f);
            screens[i].SetProgramVariable("stagger", reduceMotion ? 0f : 0.08f);
            screens[i].SetProgramVariable("duration", reduceMotion ? 0.1f : 0.22f);
        }
        for (int i = 0; buttons != null && i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            buttons[i].SetProgramVariable("lift", reduceMotion ? 0f : 2f);
            buttons[i].SetProgramVariable("hoverScale", 1f);
            buttons[i].SetProgramVariable("pressScale", 1f);
        }
    }
    private void Update()
    {
        float now = Time.unscaledTime;
        if (now >= nextPoll)
        {
            nextPoll = now + 0.1f;
            for (int i = 0; sources != null && labels != null && i < sources.Length && i < labels.Length; i++)
            {
                if (sources[i] == null || labels[i] == null) continue;
                if (labels[i].text != sources[i].text) labels[i].text = sources[i].text;
                if (labels[i] != title) labels[i].color = sources[i].color;
            }
            if (session != null)
            {
                int host = (int)session.GetProgramVariable("hostId");
                int guest = (int)session.GetProgramVariable("guestId");
                string first = host >= 0 ? "Seat 1\nOccupied." : "Seat 1\nSit down.";
                string second = guest >= 0 ? "Seat 2\nOccupied." : host >= 0 ? "Seat 2\nSit down." : "Seat 2\nEmpty.";
                if (seatOne != null && seatOne.text != first) seatOne.text = first;
                if (seatTwo != null && seatTwo.text != second) seatTwo.text = second;
                Color open = new Color(0.93f, 0.93f, 0.93f, 1f);
                Color taken = new Color(0.6f, 0.6f, 0.6f, 1f);
                if (seatOne != null) seatOne.color = host >= 0 ? taken : open;
                if (seatTwo != null) seatTwo.color = guest >= 0 || host < 0 ? taken : open;
                if (router != null && Utilities.IsValid(Networking.LocalPlayer)
                    && (bool)router.GetProgramVariable("ready"))
                {
                    int localId = Networking.LocalPlayer.playerId;
                    string wanted = host == localId ? "create" : guest == localId ? "join" : "main";
                    if ((string)router.GetProgramVariable("currentScreen") != wanted)
                    {
                        router.SetProgramVariable("incomingScreen", wanted);
                        router.SendCustomEvent("ApplyNavigation");
                    }
                }
            }
            if (terms != null && dealer != null)
            {
                string value = "Life: " + (int)dealer.GetProgramVariable("startingLife")
                    + "\nStake: " + (int)dealer.GetProgramVariable("roundDamage")
                    + " (+" + (int)dealer.GetProgramVariable("roundDamageGrowth") + " each round)";
                if (terms.text != value) terms.text = value;
            }
            bool show = menuGroup != null && menuGroup.alpha > 0.01f;
            if (show != visible) { visible = show; if (animator != null) { if (show) animator.Show(); else animator.Hide(); } }
        }
    }
}
