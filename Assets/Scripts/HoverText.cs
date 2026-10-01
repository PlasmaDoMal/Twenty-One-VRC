using MenSharp;
using TMPro;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class HoverText : MenSharpBehaviour
{
    [Header("Targets")]
    public TextMeshProUGUI[] labels;

    [Header("Cores (vertex color)")]
    public Color restColor = Color.white;
    public Color hoverColor = new Color32(0xFF, 0xC8, 0x5A, 0xFF);

    [Header("Tamanho")]
    [Tooltip("Quanto o fontSize cresce no hover. O botao em si nao muda de tamanho.")]
    public float fontSizeBoost = 8f;

    [Header("Timing")]
    [Min(0f)] public float duration = 0.12f;
    [Range(0.01f, 4f)] public float smoothness = 1f;
    public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // Publicos de proposito: no MenSharp um campo privado vira estatico, e um
    // estatico seria compartilhado pelos quatro HoverText da mesa.
    [HideInInspector] public float restFontSize;
    [HideInInspector] public bool hovered;
    [HideInInspector] public bool running;

    public void Start()
    {
        CacheRest();
        Apply(0f);
    }

    public void HoverOn()
    {
        hovered = true;
        Begin();
    }

    public void HoverExit()
    {
        hovered = false;
        Begin();
    }

    private void CacheRest()
    {
        if (labels == null || labels.Length == 0) return;
        if (labels[0] == null) return;
        restColor = labels[0].color;
        restFontSize = labels[0].fontSize;
    }

    private void Begin()
    {
        if (running) return;
        Scheduler.Run(() => Run());
    }

    private async System.Threading.Tasks.Task Run()
    {
        running = true;

        Color colorFrom = hovered ? restColor : hoverColor;
        float sizeFrom = hovered ? restFontSize : restFontSize + fontSizeBoost;
        Color colorTo = hovered ? hoverColor : restColor;
        float sizeTo = hovered ? restFontSize + fontSizeBoost : restFontSize;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
            float e = curve.Evaluate(t);
            if (Mathf.Abs(smoothness - 1f) > 0.001f) e = Mathf.Pow(e, smoothness);
            Apply(e, colorFrom, colorTo, sizeFrom, sizeTo);
            await Scheduler.NextFrame();
        }

        Apply(1f, colorFrom, colorTo, sizeFrom, sizeTo);
        running = false;
    }

    private void Apply(float e)
    {
        Apply(e,
            hovered ? restColor : hoverColor,
            hovered ? hoverColor : restColor,
            hovered ? restFontSize : restFontSize + fontSizeBoost,
            hovered ? restFontSize + fontSizeBoost : restFontSize);
    }

    private void Apply(float e, Color colorFrom, Color colorTo, float sizeFrom, float sizeTo)
    {
        if (labels == null) return;
        Color c = Color.LerpUnclamped(colorFrom, colorTo, e);
        float size = Mathf.LerpUnclamped(sizeFrom, sizeTo, e);
        for (int i = 0; i < labels.Length; i++)
        {
            TextMeshProUGUI label = labels[i];
            if (label == null) continue;
            label.color = c;
            label.fontSize = size;
        }
    }
}