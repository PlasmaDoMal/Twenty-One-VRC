using System.Threading.Tasks;
using MenSharp;
using UnityEngine;
using UnityEngine.UI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class LogoIntroAnimator : MenSharpBehaviour
{
    [Header("Reference timeline (seconds from local entry)")]
    public float loadingStart = 2.266667f;
    public float loadingEnd = 5.166667f;
    public float logoStart = 5.166667f;
    public float logoRelocationStart = 7.733333f;
    public float menuStart = 8.3f;
    public float interactiveTime = 13.333333f;
    [Min(0.1f)] public float playbackSpeed = 1f;
    public bool includeLoadingText = true;
    [Header("Measured 2560 x 1080 coordinates, origin at top left")]
    public AnimationCurve logoX;
    public AnimationCurve logoY;
    public AnimationCurve logoScale;
    public AnimationCurve playX;
    public AnimationCurve loadingBrightness;
    public float playTop = 573f;
    public float playHeight = 30f;
    public GameObject tablePreview;
    public float tableRevealTime = 8.3f;
    [Header("Temporal echoes - earlier poses, never fixed offsets")]
    public float echoInterval = 0.04f;
    [Range(0f,1f)] public float echoOpacity = 0.65f;
    [Range(0f,1f)] public float echoFalloff = 0.79f;
    public RawImage logo;
    public RawImage[] echoes;
    public Text loadingHeading;
    public Text loadingCaption;
    public RectTransform playRoot;
    public CanvasGroup playGroup;
    public Image indicator;
    public bool alwaysSelected = true;
    public float indicatorOpacity = 0.72f;
    [Header("Optional sounds; no source audio is required")]
    public AudioSource logoEntryAudio;
    public AudioSource logoMoveAudio;
    [HideInInspector] public bool readyForPlay;
    [HideInInspector] public float currentTime;
    private bool running;
    private bool playedEntry;
    private bool playedMove;
    private bool hovered;
    private Color white = Color.white;

    public void BeginIntro()
    {
        currentTime = 0f;
        running = true;
        readyForPlay = false;
        playedEntry = false;
        playedMove = false;
        Sample(0f);
    }

    public void Start()
    {
        Scheduler.Run(() => RunAnimation());
    }

    private async Task RunAnimation()
    {
        while (true)
        {
            TickAnimation();
            await Scheduler.NextFrame();
        }
    }

    private void TickAnimation()
    {
        if (!running) return;
        currentTime += Time.deltaTime * Mathf.Max(0.1f, playbackSpeed);
        Sample(currentTime);
        if (!playedEntry && currentTime >= logoStart)
        {
            playedEntry = true;
            if (logoEntryAudio != null && logoEntryAudio.clip != null) logoEntryAudio.Play();
        }
        if (!playedMove && currentTime >= logoRelocationStart)
        {
            playedMove = true;
            if (logoMoveAudio != null && logoMoveAudio.clip != null) logoMoveAudio.Play();
        }
        readyForPlay = currentTime >= interactiveTime;
    }

    public void Sample(float referenceTime)
    {
        float t = referenceTime;
        if (tablePreview != null) tablePreview.SetActive(t >= tableRevealTime);
        bool loading = includeLoadingText && t >= loadingStart && t < loadingEnd;
        float headingAlpha = loading ? loadingBrightness.Evaluate(t) : 0f;
        loadingHeading.color = new Color(1f,1f,1f,headingAlpha);
        loadingCaption.color = new Color(1f,1f,1f,loading ? 0.915f : 0f);
        float visible = t >= logoStart ? 1f : 0f;
        SetLogoPose(logo, t, visible);
        for (int i=0; i<echoes.Length; i++)
        {
            float delay = (i+1)*Mathf.Max(0.001f,echoInterval);
            float oldTime = t-delay;
            // The first recorded frame already contains echoes: curves include measured-direction pre-roll.
            float dx=logoX.Evaluate(t)-logoX.Evaluate(oldTime);
            float dy=logoY.Evaluate(t)-logoY.Evaluate(oldTime);
            float ds=(logoScale.Evaluate(t)-logoScale.Evaluate(oldTime))*1080f;
            float motion=Mathf.Clamp01((Mathf.Abs(dx)+Mathf.Abs(dy)+Mathf.Abs(ds))/7f);
            float opacity=visible*echoOpacity*Mathf.Pow(echoFalloff,i)*motion;
            SetLogoPose(echoes[i],oldTime,opacity);
        }
        float px=playX.Evaluate(t);
        playRoot.anchoredPosition=new Vector2(px-1280f,540f-playTop-playHeight*0.5f);
        playGroup.alpha=t>=menuStart ? 1f : 0f;
        float selected=(alwaysSelected || hovered) && t>=interactiveTime ? indicatorOpacity : 0f;
        indicator.color=new Color(1f,1f,1f,selected);
    }

    private void SetLogoPose(RawImage image,float t,float opacity)
    {
        float s=logoScale.Evaluate(t);
        image.rectTransform.anchoredPosition=new Vector2(logoX.Evaluate(t)-1280f,540f-logoY.Evaluate(t));
        image.rectTransform.localScale=new Vector3(s,s,1f);
        white.a=opacity;
        image.color=white;
    }

    public void HoverOn() { hovered = true; }
    public void HoverOff() { hovered = false; }
    public void StopIntro()
    {
        running=false;
        readyForPlay=false;
        indicator.color=new Color(1f,1f,1f,0f);
    }
}
