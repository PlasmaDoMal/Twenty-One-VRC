using MenSharp;
using TMPro;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// No hover, troca o vertex color do texto e aumenta o fontSize. O botao em si
/// nao muda de tamanho.
///
/// O HitArea encaminha PointerEnter e PointerExit por EventTrigger persistente
/// para HoverOn e HoverExit no UdonBehaviour compilado. O motivo e o input:
///
/// - No desktop, quem aponta e o cursor do ClientSim, que raycasta pela
///   GraphicRaycaster e entrega PointerEnter/PointerExit pelo EventSystem.
/// - Em VR, o laser do controle usa o mesmo EventSystem do botao.
///
/// Os dois caminhos passam pelos mesmos metodos, entao nao ha duas animacoes
/// para manter em sincronia.
///
/// Importante: o componente fica no <b>HitArea</b> (o retangulo invisivel que
/// cobre o texto), nao no botao inteiro. O Raycast precisa bater na area
/// cliquavel — se ficar no pai do canvas, o raio atravessa o painel inteiro em
/// vez de acertar o texto.
///
/// Corrigido tambem o estado preso: <see cref="hovered"/> e reavaliado em cada
/// transicao e no Start, para um hover que nao recebeu o PointerExit nao deixar
/// o texto amarelo para sempre. Era o que mantinha os dois botoes amarelos ao
/// mesmo tempo.
/// </summary>
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
    public bool logHover = false;

    // Publicos de proposito: no MenSharp um campo privado vira estatico, e um
    // estatico seria compartilhado pelos quatro HoverText da mesa.
    [HideInInspector] public float restFontSize;
    [HideInInspector] public bool hovered;
    [HideInInspector] public bool running;
    [HideInInspector] public int animationVersion;

    public void Start()
    {
        CacheRest();
        // Comeca no estado de repouso, nunca no de hover. Sem isto, um texto que
        // ficou marcado de uma sessao anterior abre a partida amarelo.
        hovered = false;
        running = false;
        animationVersion++;
        ApplyRest();
    }

    /// <summary>Atalho para o Inspector e para eventos do Udon.</summary>
    public void HoverOn()
    {
        SetHovered(true);
    }

    /// <summary>Atalho para o Inspector e para eventos do Udon.</summary>
    public void HoverExit()
    {
        SetHovered(false);
    }

    /// <summary>
    /// Aplica a troca de estado e so anima quando o estado realmente mudou.
    /// Sem esta guarda, dois PointerEnter seguidos reiniciariam a animacao e o
    /// texto nunca chegaria no fim.
    /// </summary>
    private void SetHovered(bool value)
    {
        if (logHover && hovered != value)
            Debug.Log("HoverText: " + gameObject.name + (value ? " entrou" : " saiu"));
        if (hovered == value) return;
        hovered = value;
        int version = ++animationVersion;
        running = true;
        Scheduler.Run(() => Run(version));
    }

    private void CacheRest()
    {
        if (labels == null || labels.Length == 0) return;
        if (labels[0] == null) return;
        restColor = labels[0].color;
        restFontSize = labels[0].fontSize;
    }

    private void ApplyRest()
    {
        if (labels == null) return;
        for (int i = 0; i < labels.Length; i++)
        {
            TextMeshProUGUI label = labels[i];
            if (label == null) continue;
            label.color = restColor;
            label.fontSize = restFontSize;
        }
    }

    private async System.Threading.Tasks.Task Run(int version)
    {
        if (version != animationVersion) return;
        Color colorFrom = labels != null && labels.Length > 0 && labels[0] != null
            ? labels[0].color : restColor;
        float sizeFrom = labels != null && labels.Length > 0 && labels[0] != null
            ? labels[0].fontSize : restFontSize;
        Color colorTo = hovered ? hoverColor : restColor;
        float sizeTo = hovered ? restFontSize + fontSizeBoost : restFontSize;

        float elapsed = 0f;
        while (elapsed < duration && version == animationVersion)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
            float e = curve.Evaluate(t);
            if (Mathf.Abs(smoothness - 1f) > 0.001f) e = Mathf.Pow(e, smoothness);
            Apply(e, colorFrom, colorTo, sizeFrom, sizeTo);
            await Scheduler.NextFrame();
        }

        if (version != animationVersion) return;
        Apply(1f, colorFrom, colorTo, sizeFrom, sizeTo);
        running = false;
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
