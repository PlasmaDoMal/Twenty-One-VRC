using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TwentyOneUIAnimator : UdonSharpBehaviour
{
    public CanvasGroup[] groups;
    public RectTransform[] elements;
    public float duration = 0.45f;
    public float stagger = 0.08f;
    public float startScale = 0.92f;
    public float rise = 14f;
    public bool reduceMotion;
    public bool showOnStart = true;
    private Vector3[] restPositions;
    private Vector3[] restScales;
    private float[] startAlphas;
    private float elapsed;
    private bool showing;
    private bool playing;
    private bool initialized;

    private void Initialize()
    {
        if (initialized) return;
        int count = groups == null ? 0 : groups.Length;
        restPositions = new Vector3[count];
        restScales = new Vector3[count];
        startAlphas = new float[count];
        for (int i = 0; i < count; i++)
        {
            if (elements != null && i < elements.Length && elements[i] != null)
            {
                restPositions[i] = elements[i].anchoredPosition3D;
                restScales[i] = elements[i].localScale;
            }
        }
        initialized = true;
    }
    private void Start() { Initialize(); if (showOnStart) Show(); else HideInstant(); }
    public void Show() { Begin(true); }
    public void Hide() { Begin(false); }
    public void HideInstant()
    {
        Initialize(); playing = false; showing = false;
        for (int i = 0; i < groups.Length; i++)
            if (groups[i] != null) { groups[i].alpha = 0f; groups[i].interactable = false; groups[i].blocksRaycasts = false; }
    }
    private void Begin(bool value)
    {
        Initialize(); showing = value; elapsed = 0f; playing = true;
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] == null) continue;
            startAlphas[i] = groups[i].alpha;
            groups[i].interactable = false; groups[i].blocksRaycasts = false;
        }
    }
    private void Update()
    {
        if (!playing) return;
        elapsed += Time.unscaledDeltaTime;
        float step = reduceMotion ? 0.1f : Mathf.Max(0.01f, duration);
        float delay = reduceMotion ? 0f : stagger;
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] == null) continue;
            float t = Mathf.Clamp01((elapsed - i * delay) / step);
            float e = showing ? 1f - Mathf.Pow(1f - t, 3f) : t * t * t;
            groups[i].alpha = Mathf.Lerp(startAlphas[i], showing ? 1f : 0f, e);
            if (elements != null && i < elements.Length && elements[i] != null)
            {
                float amount = reduceMotion ? 0f : (showing ? 1f - e : e);
                elements[i].localScale = restScales[i] * Mathf.Lerp(1f, startScale, amount);
                elements[i].anchoredPosition3D = restPositions[i] - new Vector3(0f, rise * amount, 0f);
            }
            groups[i].interactable = showing && t >= 1f;
            groups[i].blocksRaycasts = showing && t >= 1f;
        }
        if (elapsed >= step + delay * Mathf.Max(0, groups.Length - 1)) playing = false;
    }
}
