using MenSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// Animacao de entrada e saida do menu de escolha: sobe enquanto aparece, desce
/// enquanto some.
///
/// Os dois sentidos usam o mesmo <see cref="Drive"/>, entao sao simetricos por
/// construcao — mudar a curva ou a duracao muda os dois juntos.
///
/// Campos de instancia sao publicos de proposito: no MenSharp um campo privado
/// vira estatico, e um estatico seria compartilhado pelos botoes da mesa. Ha um
/// FadeRise por botao, entao <see cref="restPosition"/> e <see cref="running"/>
/// precisam ser um de cada.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FadeRise : MenSharpBehaviour
{
    [Header("Rise")]
    [Min(0f)] public float duration = 0.6f;
    [Tooltip("How far below the resting position the object starts.")]
    public float offsetY = 0.25f;
    [Tooltip("Exponent applied to the curve. 1 = as authored, 2 = slower start.")]
    [Range(0.01f, 4f)] public float smoothness = 1f;
    public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Targets")]
    public CanvasGroup[] canvasGroups;

    [Tooltip("Comeca escondido e parado, sem esperar a vez chegar. Marque no botao que so vale depois de certa jogada.")]
    public bool startHidden = false;

    [HideInInspector] public Vector3 restPosition;
    [HideInInspector] public bool running;

    public void Start()
    {
        // A posicao de repouso e lida uma vez, aqui. Os metodos de animacao nao
        // releem: depois de um HideNow o objeto ja esta deslocado, e reler daria
        // a posicao escondida como se fosse a de repouso.
        restPosition = transform.localPosition;
        if (startHidden)
        {
            HideNow();
        }
    }

    /// <summary>
    /// Some com o botao na hora, sem animar. E o que <see cref="startHidden"/>
    /// chama no Start.
    /// </summary>
    public void HideNow()
    {
        running = false;
        transform.localPosition = restPosition + Vector3.down * offsetY;
        SetAlpha(0f);
    }

    /// <summary>
    /// Aparece subindo. E o que o dono da mesa dispara quando a vez chega, e
    /// tambem o que traz de volta o botao marcado como
    /// <see cref="startHidden"/>.
    /// </summary>
    public void FadeInAndRise()
    {
        if (running) return;
        Scheduler.Run(() => RunIn());
    }

    /// <summary>
    /// O contrario: some com o menu, descendo o mesmo caminho. E o que toca
    /// depois que o jogador escolheu, para o botao nao ficar em cima da tela
    /// enquanto o dono do baralho processa a jogada e a vez vira.
    ///
    /// Ao terminar, o objeto fica transparente e na posicao final — a proxima vez
    /// (<see cref="FadeInAndRise"/>) recomeca do inicio de novo, porque a
    /// posicao de repouso continua a mesma.
    /// </summary>
    public void FadeOutAndDrop()
    {
        if (running) return;
        Scheduler.Run(() => RunOut());
    }

    private async System.Threading.Tasks.Task RunIn()
    {
        running = true;
        Vector3 from = restPosition + Vector3.down * offsetY;
        transform.localPosition = from;
        SetAlpha(0f);

        await Drive(from, restPosition, 0f, 1f);

        transform.localPosition = restPosition;
        SetAlpha(1f);
        running = false;
    }

    private async System.Threading.Tasks.Task RunOut()
    {
        running = true;

        await Drive(restPosition, restPosition + Vector3.down * offsetY, 1f, 0f);

        transform.localPosition = restPosition + Vector3.down * offsetY;
        SetAlpha(0f);
        running = false;
    }

    /// <summary>
    /// Anda de <paramref name="from"/> ate <paramref name="to"/> enquanto
    /// <paramref name="alphaFrom"/> vira <paramref name="alphaTo"/>, no mesmo
    /// relogio e na mesma curva. Compartilhado pelos dois sentidos, para o
    /// fade in e o fade out sao sempre simetricos.
    /// </summary>
    private async System.Threading.Tasks.Task Drive(Vector3 from, Vector3 to, float alphaFrom, float alphaTo)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
            float e = curve.Evaluate(t);
            if (Mathf.Abs(smoothness - 1f) > 0.001f) e = Mathf.Pow(e, smoothness);
            transform.localPosition = Vector3.LerpUnclamped(from, to, e);
            SetAlpha(Mathf.LerpUnclamped(alphaFrom, alphaTo, e));
            await Scheduler.NextFrame();
        }
    }

    private void SetAlpha(float value)
    {
        if (canvasGroups == null) return;
        for (int i = 0; i < canvasGroups.Length; i++)
            if (canvasGroups[i] != null) canvasGroups[i].alpha = value;
    }
}