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
    [Header("Local menu click SFX")]
    public AudioSource menuClickAudio;
    [Range(0.1f, 3f)] public float menuPitchMin = 0.9f;
    [Range(0.1f, 3f)] public float menuPitchMax = 1.1f;
    [Header("Local language preference: 0 = English, 1 = Portuguese")]
    public int language;
    public GameObject settingsOverlay;
    public TextMeshProUGUI settingsEntryLabel;
    public TextMeshProUGUI settingsTitle;
    public TextMeshProUGUI languageLabel;
    public TextMeshProUGUI settingsBackLabel;
    public Image englishChoice;
    public Image portugueseChoice;
    private float nextPoll;
    private bool visible;

    private void Start()
    {
        if (settingsOverlay != null) settingsOverlay.SetActive(false);
        ApplyMotion();
        ApplyLanguageUI();
    }
    public void PlayMenuClick()
    {
        if (menuClickAudio == null || menuClickAudio.clip == null) return;
        menuClickAudio.pitch = Random.Range(Mathf.Min(menuPitchMin, menuPitchMax), Mathf.Max(menuPitchMin, menuPitchMax));
        menuClickAudio.Play();
    }
    public void ShowSettings()
    {
        ApplyLanguageUI();
        if (settingsOverlay != null) settingsOverlay.SetActive(true);
    }
    public void HideSettings() { if (settingsOverlay != null) settingsOverlay.SetActive(false); }
    public void SetEnglish() { language = 0; ApplyLanguageUI(); nextPoll = 0f; }
    public void SetPortuguese() { language = 1; ApplyLanguageUI(); nextPoll = 0f; }
    private void ApplyLanguageUI()
    {
        bool pt = language == 1;
        if (settingsEntryLabel != null) settingsEntryLabel.text = pt ? "Configurações" : "Settings";
        if (settingsTitle != null) settingsTitle.text = pt ? "CONFIGURAÇÕES" : "SETTINGS";
        if (languageLabel != null) languageLabel.text = pt ? "Idioma das cartas tarot" : "Tarot card language";
        if (settingsBackLabel != null) settingsBackLabel.text = pt ? "Voltar" : "Back";
        if (motionLabel != null) motionLabel.text = pt ? "Reduzir movimento" : "Steady view";
        Color selected = new Color(0.3f, 0.3f, 0.3f, 1f);
        Color unselected = new Color(0.08f, 0.08f, 0.08f, 1f);
        if (englishChoice != null) englishChoice.color = pt ? unselected : selected;
        if (portugueseChoice != null) portugueseChoice.color = pt ? selected : unselected;
    }
    private string MenuLabel(string value)
    {
        if (language != 1) return value;
        if (value == "Play") return "Jogar";
        if (value == "Ready") return "Pronto";
        if (value == "Leave table") return "Sair da mesa";
        if (value == "Solo test") return "Teste solo";
        return value;
    }
    public void ToggleReducedMotion() { reduceMotion = !reduceMotion; ApplyMotion(); }
    public void SetReducedMotion() { if (motionToggle != null) reduceMotion = motionToggle.isOn; ApplyMotion(); }
    private void ApplyMotion()
    {
        if (animator != null) animator.reduceMotion = reduceMotion;
        if (motionLabel != null) motionLabel.text = language == 1 ? "Reduzir movimento" : "Steady view";
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
                string translated = MenuLabel(sources[i].text);
                if (labels[i].text != translated) labels[i].text = translated;
                if (labels[i] != title) labels[i].color = sources[i].color;
            }
            if (session != null)
            {
                int host = (int)session.GetProgramVariable("hostId");
                int guest = (int)session.GetProgramVariable("guestId");
                string first = host >= 0 ? "Seat 1\nOccupied." : "Seat 1\nSit down.";
                string second = guest >= 0 ? "Seat 2\nOccupied." : host >= 0 ? "Seat 2\nSit down." : "Seat 2\nEmpty.";
                if (language == 1)
                {
                    first = host >= 0 ? "Lugar 1\nOcupado." : "Lugar 1\nSentar.";
                    second = guest >= 0 ? "Lugar 2\nOcupado." : host >= 0 ? "Lugar 2\nSentar." : "Lugar 2\nVazio.";
                }
                if (host >= 0)
                {
                    VRCPlayerApi occupant = VRCPlayerApi.GetPlayerById(host);
                    if (Utilities.IsValid(occupant)) first += "\n" + occupant.displayName;
                }
                if (guest >= 0)
                {
                    VRCPlayerApi occupant = VRCPlayerApi.GetPlayerById(guest);
                    if (Utilities.IsValid(occupant)) second += "\n" + occupant.displayName;
                }
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
                if (language == 1)
                    value = "Vida: " + (int)dealer.GetProgramVariable("startingLife")
                        + "\nAposta: " + (int)dealer.GetProgramVariable("roundDamage")
                        + " (+" + (int)dealer.GetProgramVariable("roundDamageGrowth") + " por rodada)";
                if (terms.text != value) terms.text = value;
            }
            bool show = menuGroup != null && menuGroup.alpha > 0.01f;
            if (show != visible) { visible = show; if (animator != null) { if (show) animator.Show(); else animator.Hide(); } }
        }
    }
}
