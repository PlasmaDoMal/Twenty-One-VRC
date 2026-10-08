using MenSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

/// <summary>
/// Um botao do menu, com o proprio movimento.
///
/// Sobe e acende quando o ponteiro entra, afunda quando e apertado, e volta
/// sozinho quando o ponteiro sai. Quem manda nos eventos e o
/// <see cref="Button"/> e o <c>EventTrigger</c> do mesmo objeto, por
/// <c>SendCustomEvent</c> — um comportamento MenSharp nao pode implementar
/// <c>IPointerEnterHandler</c> e companhia, porque o que a UI procura no objeto
/// e um componente, e no runtime o componente e o UdonBehaviour, que so conhece
/// metodos por nome.
///
/// O tracinho de acento cresce pela largura da imagem, nao por propriedade de
/// material: assim o brilho continua andando na velocidade certa enquanto a
/// barra cresce, e nenhum script precisa tocar em <c>Material</c>.
///
/// Campos de instancia sao publicos de proposito: no MenSharp um campo privado
/// vira estatico, e um estatico seria compartilhado por todos os botoes da mesa.
/// Ha um MenuButton por botao, entao o estado de animacao precisa ser um de cada.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MenuButton : MenSharpBehaviour
{
    [Header("Roteamento")]
    [Tooltip("Acao passada ao MenuRouter quando o botao e ativado. Vazio = o botao so anima e nao navega.")]
    public string action = "";
    [Tooltip("Linha de configuracao que este botao mexe. Preenchido, o botao muda a linha em vez de navegar.")]
    public MenuSettingRow stepRow;
    [Tooltip("Quanto o botao muda a linha. -1 tira, +1 poe, 0 e nenhum passo.")]
    public int stepBy = 0;

    [Header("Alvos")]
    [Tooltip("O roteador que recebe a acao. Vazio = procura em quem e pai.")]
    public MenuRouter router;
    [Tooltip("O retangulo que sobe e muda de escala. O proprio objeto serve, se ficar vazio.")]
    public RectTransform body;
    [Tooltip("A barra de acento que cresce na base. A ancora esquerda e o pivot ficam a esquerda, para crescer para a direita.")]
    public RectTransform accent;
    [Tooltip("Recebe alpha e o bloqueio de clique. Precisa estar no objeto do botao ou acima dele.")]
    public CanvasGroup group;
    [Tooltip("A cor do botao e animada no hover. Vazio = usa o label.")]
    public Image surface;
    [Tooltip("Texto do botao. E o alvo de clique quando nao ha superficie desenhada.")]
    public Text label;
    [Tooltip("Componente Button do mesmo objeto. So e desliga o botao quando ele e bloqueado.")]
    public Button button;

    [Header("Movimento")]
    public float hoverDuration = 0.16f;
    public float pressDuration = 0.08f;
    [Tooltip("Quanto o botao sobe no hover, em unidades do RectTransform.")]
    public float lift = 9f;
    public float hoverScale = 1.03f;
    public float pressScale = 0.975f;
    [Tooltip("Largura do tracinho de acento parado, em unidades do RectTransform.")]
    public float accentRest = 30f;
    [Tooltip("Largura do tracinho de acento no hover.")]
    public float accentFull = 330f;
    public Color restColor = new Color(0.72f, 0.78f, 0.84f, 1f);
    public Color hoverColor = new Color(1f, 1f, 1f, 1f);
    public Color blockedColor = new Color(0.42f, 0.46f, 0.52f, 0.6f);
    public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Arranque")]
    [Tooltip("Comeca escondido, para a entrada em cascata do MenuRouter poder revelar um a um.")]
    public bool startHidden = false;

    // Estado da animacao, um conjunto por botao. Publicos porque campo privado
    // vira estatico no MenSharp.
    [HideInInspector] public int animationVersion;
    [HideInInspector] public bool hovered;
    [HideInInspector] public bool pressed;
    [HideInInspector] public bool blocked;
    [HideInInspector] public bool revealInput = true;
    [HideInInspector] public bool hidden = true;
    [HideInInspector] public float restX;
    [HideInInspector] public float restY;
    [HideInInspector] public float liftNow;
    [HideInInspector] public float slideNow;
    [HideInInspector] public float scaleNow = 1f;
    [HideInInspector] public float accentNow;
    [HideInInspector] public float alphaNow = 1f;
    [HideInInspector] public float incomingAlpha;
    [HideInInspector] public float incomingSlide;
    [HideInInspector] public bool incomingBlocked;
    [HideInInspector] public bool restCached;

    private void CacheRest()
    {
        if (restCached) return;
        if (body == null) body = transform as RectTransform;
        restX = body.localPosition.x;
        restY = body.localPosition.y;
        restCached = true;
    }

    public void ApplyReveal()
    {
        CacheRest();
        SetRevealAlpha(incomingAlpha);
        SetRevealSlide(incomingSlide);
    }

    public void ApplyBlocked() { SetBlocked(incomingBlocked); }

    public void Start()
    {
        // A posicao de repouso e derivada de onde o botao esta agora, e nao
        // lida de um campo: o Start pode rodar em qualquer ordem em relacao ao
        // roteador, e um repouso guardado sairia errado. Ver o mesmo cuidado em
        // FadeRise.RefreshRestY.
        if (body == null)
        {
            body = transform as RectTransform;
        }
        CacheRest();

        if (accent != null)
        {
            accentNow = accentRest;
        }

        if (startHidden)
        {
            // some sem animar, e devolve para o estado de repouso
            hidden = true;
            hovered = false;
            pressed = false;
            liftNow = -lift;
            scaleNow = 1f;
            accentNow = accentRest;
            alphaNow = 0f;
            slideNow = 0f;
            revealInput = false;
            Apply();
            SetInteractive(false);
        }
        else
        {
            hidden = false;
            liftNow = 0f;
            alphaNow = 1f;
            revealInput = true;
            Apply();
            SetInteractive(true);
        }
    }

    /// <summary>O ponteiro entrou, ou o controle de VR selecionou o botao.</summary>
    public void HoverEnter()
    {
        if (blocked || hidden || !revealInput)
        {
            return;
        }
        hovered = true;
        Restart(hoverDuration);
    }

    /// <summary>O ponteiro saiu, ou o controle de VR tirou a selecao.</summary>
    public void HoverExit()
    {
        if (blocked)
        {
            return;
        }
        hovered = false;
        pressed = false;
        Restart(hoverDuration);
    }

    /// <summary>O botao foi apertado.</summary>
    public void PressDown()
    {
        if (blocked || hidden || !revealInput)
        {
            return;
        }
        pressed = true;
        Restart(pressDuration);
    }

    /// <summary>O botao foi solto.</summary>
    public void PressUp()
    {
        if (!pressed)
        {
            return;
        }
        pressed = false;
        Restart(pressDuration);
    }

    /// <summary>
    /// Torna este botao um passo de uma linha de configuracao, em vez de um
    /// botao de navegacao. <paramref name="by"/> negativo tira, positivo poe.
    ///
    /// E um metodo e nao so dois campos porque os dois precisam ser ajustados
    /// juntos: um <c>stepRow</c> com <c>stepBy == 0</c> seria um botao que parece
    /// funcionar e nao muda nada.
    /// </summary>
    public void SetStepTarget(MenuSettingRow row, int by)
    {
        stepRow = row;
        stepBy = by;
        action = "";
    }

    /// <summary>
    /// O botao foi ativado. Um clique e um <c>OnSubmit</c> chegam aqui, entao
    /// o teclado e o controle de VR chegam pelo mesmo caminho do ponteiro.
    /// </summary>
    public void Activate()
    {
        if (blocked || hidden || !revealInput)
        {
            return;
        }
        if (stepRow != null && stepBy != 0)
        {
            // Passo de configuracao: mexe na linha e fica na tela. O pulso do
            // proprio botao ainda roda, porque sem ele o clique some num clique.
            int stepVersion = ++animationVersion;
            Scheduler.Run(() => Drive(stepVersion, 0f, pressScale * 0.99f, accentNow, hoverColor, pressDuration));
            if (stepBy > 0)
            {
                stepRow.Next();
            }
            else
            {
                stepRow.Previous();
            }
            return;
        }
        // Um pulso curto antes de navegar: sem isso o botao some no mesmo quadro
        // do clique e o aperto nunca chega a ser visto.
        MenuRouter target = router;
        if (target != null)
        {
            target.incomingAction = action;
            target.ApplyAction();
        }
    }

    /// <summary>
    /// Liga ou desliga o botao. Desligado ele nao reage ao ponteiro e some um
    /// pouco, o que avisa que a opcao nao esta disponivel agora.
    /// </summary>
    public void SetBlocked(bool value)
    {
        if (blocked == value)
        {
            return;
        }
        blocked = value;
        if (value)
        {
            hovered = false;
            pressed = false;
        }
        SetInteractive(!value);
        Restart(hoverDuration);
    }

    private async System.Threading.Tasks.Task PulseThenGo()
    {
        int version = ++animationVersion;
        await Drive(version, -lift * 0.35f, pressScale, accentFull, hoverColor, pressDuration);
        if (version != animationVersion || action == "")
        {
            return;
        }
        // Devolve para o estado de repouso e so depois navega, para a volta ter
        // tempo de ser vista.
        await Drive(version, 0f, scaleNow, accentNow, hoverColor, pressDuration);
        if (version != animationVersion)
        {
            return;
        }
        MenuRouter target = router != null ? router : GetComponentInParent<MenuRouter>();
        if (target != null)
        {
            target.incomingAction = action;
            target.ApplyAction();
        }
    }

    /// <summary>Cancela a animacao corrente e comeca outra para o estado atual.</summary>
    private void Restart(float duration)
    {
        int version = ++animationVersion;
        Scheduler.Run(() => Settle(version, duration));
    }

    private async System.Threading.Tasks.Task Settle(int version, float duration)
    {
        float targetLift = blocked ? -lift * 0.5f : (pressed ? -lift * 0.35f : (hovered ? lift : 0f));
        float targetScale = blocked ? 1f : (pressed ? pressScale : (hovered ? hoverScale : 1f));
        float targetAccent = blocked ? accentRest : (hovered || pressed ? accentFull : accentRest);
        Color targetColor = blocked ? blockedColor : (hovered || pressed ? hoverColor : restColor);

        // A duracao vem de quem chamou, nao deste metodo: o aperto tem de ser
        // mais rapido que o hover, porque e a resposta ao toque e resposta
        // tardia parece travado.
        await Drive(version, targetLift, targetScale, targetAccent, targetColor, duration);
    }

    /// <summary>
    /// O elemento que recebe a cor do hover: a superficie quando o botao tem
    /// uma, e o proprio texto quando ele e so tipografia — que e o caso de um
    /// menu sem fundo, onde nao ha nada desenhado para acender.
    /// </summary>
    private Graphic TintTarget()
    {
        if (surface != null)
        {
            return surface;
        }
        return label;
    }

    /// <summary>
    /// Leva todos os canais de uma vez para o alvo, no mesmo relogio e na mesma
    /// curva. Um canal por vez deixaria a barra crescendo enquanto o botao sobe,
    /// e o conjunto ficaria visivelmente desmontado.
    /// </summary>
    private async System.Threading.Tasks.Task Drive(int version, float targetLift, float targetScale, float targetAccent, Color targetColor, float duration)
    {
        Graphic tint = TintTarget();
        float fromLift = liftNow;
        float fromScale = scaleNow;
        float fromAccent = accentNow;
        Color fromColor = tint != null ? tint.color : Color.white;
        float fromAlpha = alphaNow;

        float elapsed = 0f;
        float step = Mathf.Max(0.0001f, duration);
        while (elapsed < step)
        {
            elapsed += Time.deltaTime;
            float t = curve.Evaluate(Mathf.Clamp01(elapsed / step));
            liftNow = Mathf.LerpUnclamped(fromLift, targetLift, t);
            scaleNow = Mathf.LerpUnclamped(fromScale, targetScale, t);
            accentNow = Mathf.LerpUnclamped(fromAccent, targetAccent, t);
            if (tint != null)
            {
                tint.color = Color.LerpUnclamped(fromColor, targetColor, t);
            }
            Apply();
            await Scheduler.NextFrame();
            if (version != animationVersion)
            {
                return;
            }
        }

        // Assenta no alvo exato. Sem isto o ultimo quadro fica um epsilon
        // antes do destino, e um botao que nunca chega em 1.0 de escala acumula
        // erro a cada hover.
        liftNow = targetLift;
        scaleNow = targetScale;
        accentNow = targetAccent;
        if (tint != null)
        {
            tint.color = targetColor;
        }
        if (hidden && alphaNow > 0f)
        {
            alphaNow = 1f;
        }
        Apply();
    }

    /// <summary>
    /// Alpha de entrada, controlado pelo <see cref="MenuScreen"/> durante a
    /// revelacao em cascata. Separado do hover de proposito: o
    /// <see cref="Drive"/> nunca escreve o alpha, entao os dois se misturam sem
    /// um apagar o outro.
    /// </summary>
    public void SetRevealAlpha(float value)
    {
        alphaNow = value;
        hidden = value <= 0.001f;
        if (group != null)
        {
            group.alpha = value;
        }
    }

    /// <summary>
    /// Deslize de entrada, em unidades do RectTransform. Vai somado ao repouso
    /// e independente do <see cref="liftNow"/>, porque um e deslocamento
    /// horizontal de chegada e o outro e altura do hover.
    /// </summary>
    public void SetRevealSlide(float value)
    {
        slideNow = value;
        Apply();
    }

    private void Apply()
    {
        if (body != null)
        {
            body.localPosition = new Vector3(restX + slideNow, restY + liftNow, body.localPosition.z);
            body.localScale = new Vector3(scaleNow, scaleNow, 1f);
        }
        if (accent != null)
        {
            accent.sizeDelta = new Vector2(accentNow, accent.sizeDelta.y);
        }
        if (group != null)
        {
            group.alpha = alphaNow;
        }
    }

    private void SetInteractive(bool value)
    {
        bool live = value && revealInput;
        if (group != null)
        {
            group.interactable = live;
            group.blocksRaycasts = live;
        }
        if (button != null)
        {
            button.interactable = live;
        }
    }

    /// <summary>
    /// Libera o ponteiro para o botao depois que a revelacao terminou. Antes
    /// disso ele ja esta desenhado mas nao aceita clique, para o botao nao ser
    /// ativado por um toque que caiu na regiao dele enquanto ainda chegava.
    /// </summary>
    public void EnableRevealInput()
    {
        if (revealInput)
        {
            return;
        }
        revealInput = true;
        SetInteractive(!blocked);
    }

    /// <summary>Retira o botao do alcance do ponteiro, mantendo o desenho.</summary>
    public void DisableRevealInput()
    {
        if (!revealInput)
        {
            return;
        }
        revealInput = false;
        hovered = false;
        pressed = false;
        SetInteractive(false);
    }
}
