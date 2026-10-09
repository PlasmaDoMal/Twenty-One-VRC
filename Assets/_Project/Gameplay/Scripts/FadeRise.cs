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
/// FadeRise por botao, entao <see cref="restY"/> e <see cref="running"/>
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

    // Publicos de proposito: no MenSharp um campo privado vira estatico, e um
    // estatico seria compartilhado pelos botoes da mesa. Ha um FadeRise por
    // botao, entao restY e running precisam ser um de cada.
    //
    // So a altura, nunca a posicao inteira: ver o Start().
    [HideInInspector] public float restY;
    [HideInInspector] public bool running;
    [HideInInspector] public int animationVersion;

    /// <summary>Marca que o botao esta no estado escondido, para o repouso ser derivado.</summary>
    [HideInInspector] public bool hidden;

    public void Start()
    {
        // A posicao de repouso e derivada da altura atual mais o offset, e nao
        // lida uma unica vez.
        //
        // Ler uma vez era fragil: o Start do botao e do TurnFadeIn correm em
        // ordem indefinida, entao o HideNow podia ter deslocado o botao antes
        // do Start dele. Aí o Hit lia 0 e ficava com repouso 0, e o Stay lia
        // -0.06 e ficava com repouso -0.06 — os dois escondidos em alturas
        // diferentes, e o Stay subindo de baixo de mais baixo.
        //
        // Derivando de "altura atual + offset", o repouso e sempre o mesmo,
        // qualquer que seja a ordem em que as pecas acordem.
        RefreshRestY();
        if (startHidden)
        {
            HideNow();
        }
    }

    /// <summary>
    /// Recalcula a altura de repouso a partir de onde o botao esta agora.
    /// Chame depois de mover o botao manualmente, para o proximo
    /// <see cref="HideNow"/> usar a altura certa.
    /// </summary>
    public void RefreshRestY()
    {
        restY = transform.localPosition.y + (hidden ? offsetY : 0f);
        hidden = false;
    }

    /// <summary>True enquanto o botao estiver no estado escondido.</summary>
    public bool IsHidden()
    {
        return hidden;
    }

    /// <summary>
    /// Some com o botao na hora, sem animar. E o que <see cref="startHidden"/>
    /// chama no Start.
    ///
    /// Mexe so no Y, entao o botao desce para baixo de onde esta, mantendo o
    /// lugar dele na fila.
    /// </summary>
    public void HideNow()
    {
        bool wasRunning = running;
        animationVersion++;
        running = false;
        // Deriva o repouso de onde o botao esta agora, em vez de confiar no
        // restY guardado. Se o botao ja estava escondido, a altura atual ja e
        // o repouso; se estava visivel, o repouso e a propria altura atual.
        // Nos dois casos o resultado e o mesmo e nao depende de ordem de Start.
        if (!wasRunning) RefreshRestY();
        transform.localPosition = new UnityEngine.Vector3(
            transform.localPosition.x, restY - offsetY, transform.localPosition.z);
        SetAlpha(0f);
        hidden = true;
    }

    /// <summary>
    /// Aparece subindo. E o que o dono da mesa dispara quando a vez chega, e
    /// tambem o que traz de volta o botao marcado como
    /// <see cref="startHidden"/>.
    /// </summary>
    public void FadeInAndRise()
    {
        int version = ++animationVersion;
        running = true;
        Scheduler.Run(() => RunIn(version));
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
        int version = ++animationVersion;
        running = true;
        SetInputEnabled(false);
        Scheduler.Run(() => RunOut(version));
    }

    private async System.Threading.Tasks.Task RunIn(int version)
    {
        // X e Z ficam como estao: a animacao desloca so a altura, para o botao
        // subir no lugar, sem andar de lado nem para frente.
        float from = transform.localPosition.y;
        float alpha = CurrentAlpha();
        await Drive(from, restY, alpha, 1f, version);
        if (version != animationVersion) return;

        transform.localPosition = new UnityEngine.Vector3(transform.localPosition.x, restY, transform.localPosition.z);
        SetAlpha(1f);
        hidden = false;
        running = false;
    }

    private async System.Threading.Tasks.Task RunOut(int version)
    {
        await Drive(transform.localPosition.y, restY - offsetY, CurrentAlpha(), 0f, version);
        if (version != animationVersion) return;

        transform.localPosition = new UnityEngine.Vector3(transform.localPosition.x, restY - offsetY, transform.localPosition.z);
        SetAlpha(0f);
        hidden = true;
        running = false;
    }

    private float CurrentAlpha()
    {
        if (canvasGroups != null && canvasGroups.Length > 0 && canvasGroups[0] != null)
            return canvasGroups[0].alpha;
        return hidden ? 0f : 1f;
    }

    /// <summary>
    /// Leva a altura de <paramref name="fromY"/> ate <paramref name="toY"/>
    /// enquanto <paramref name="alphaFrom"/> vira <paramref name="alphaTo"/>, no
    /// mesmo relogio e na mesma curva. Compartilhado pelos dois sentidos, para o
    /// fade in e o fade out sao sempre simetricos.
    /// </summary>
    private async System.Threading.Tasks.Task Drive(float fromY, float toY, float alphaFrom, float alphaTo, int version)
    {
        float elapsed = 0f;
        while (elapsed < duration && version == animationVersion)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
            float e = curve.Evaluate(t);
            if (Mathf.Abs(smoothness - 1f) > 0.001f) e = Mathf.Pow(e, smoothness);
            transform.localPosition = new UnityEngine.Vector3(
                transform.localPosition.x,
                Mathf.LerpUnclamped(fromY, toY, e),
                transform.localPosition.z);
            SetAlpha(Mathf.LerpUnclamped(alphaFrom, alphaTo, e));
            await Scheduler.NextFrame();
        }
    }

    private void SetAlpha(float value)
    {
        if (canvasGroups == null) return;
        for (int i = 0; i < canvasGroups.Length; i++)
            if (canvasGroups[i] != null)
            {
                canvasGroups[i].alpha = value;
                canvasGroups[i].interactable = value >= 0.999f;
                canvasGroups[i].blocksRaycasts = value >= 0.999f;
            }
    }

    private void SetInputEnabled(bool enabled)
    {
        if (canvasGroups == null) return;
        for (int i = 0; i < canvasGroups.Length; i++)
            if (canvasGroups[i] != null)
            {
                canvasGroups[i].interactable = enabled;
                canvasGroups[i].blocksRaycasts = enabled;
            }
    }
}
