using MenSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// Uma tela do menu: o conjunto de botoes que aparece junto.
///
/// some e aparece com um deslize curto e um fade, e os botoes dela entram em
/// cascata — um a um, com um intervalo curto entre eles. A cascata e o que faz
/// o menu parecer montado em vez de ligado.
///
/// O alpha dos botoes e escrito por <see cref="MenuButton.SetRevealAlpha"/>, que
/// nao mexe no hover: por isso os dois se misturam sem um depender do outro. O
/// <see cref="MenuButton"/> nunca escreve o alpha durante o hover, entao um
/// botao ainda nao revelado que receba o ponteiro nao salta de 0 para 1.
///
/// Campos de instancia sao publicos de proposito: no MenSharp um campo privado
/// vira estatico, e um estatico seria compartilhado por todas as telas.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MenuScreen : MenSharpBehaviour
{
    [Header("Identidade")]
    [Tooltip("Nome usado pelo MenuRouter para escolher esta tela.")]
    public string id = "main";
    [Tooltip("Posicao na cascata. A tela principal e 0 e entra primeiro.")]
    public int order = 0;

    [Header("Alvos")]
    public RectTransform body;
    public CanvasGroup group;
    [Tooltip("Botoes revelados em cascata, na ordem em que devem aparecer.")]
    public MenuButton[] children;

    [Header("Movimento")]
    [Tooltip("De quantos pixels a tela entra. 0 = so o fade.")]
    public float slideDistance = 48f;
    public float duration = 0.32f;
    [Tooltip("Intervalo entre um botao e o seguinte na cascata.")]
    public float stagger = 0.045f;
    public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("De quantos pixels cada botao entra deslocado, alem do alpha.")]
    public float childSlide = 26f;

    [Header("Arranque")]
    public bool startVisible = false;

    [HideInInspector] public bool visible;
    [HideInInspector] public int animationVersion;
    [HideInInspector] public float restX;

    public void Start()
    {
        if (body == null)
        {
            body = transform as RectTransform;
        }
        // Repouso derivado de onde a tela esta agora, pelo mesmo motivo do
        // MenuButton: a ordem dos Start nao e garantida.
        restX = body.localPosition.x;
        ApplyInstant(startVisible);
    }

    /// <summary>A tela esta visivel? O roteador usa para nao animar duas vezes.</summary>
    public bool IsVisible()
    {
        return visible;
    }

    /// <summary>Aparece a tela, com a cascata dos botoes.</summary>
    public void Show()
    {
        if (visible)
        {
            return;
        }
        visible = true;
        SetInteractive(true);
        Scheduler.Run(() => RunShow(++animationVersion));
    }

    /// <summary>Some com a tela, invertendo o mesmo caminho.</summary>
    public void Hide()
    {
        if (!visible)
        {
            return;
        }
        visible = false;
        SetInteractive(false);
        // Tira os botoes do alcance do ponteiro antes de comecar a sumir: durante
        // o deslize eles ainda estao desenhados, e um toque no meio do caminho
        // ativaria uma tela que ja saiu.
        SetChildInput(false);
        Scheduler.Run(() => RunHide(++animationVersion));
    }

    /// <summary>
    /// Aparece sem cascata e sem espera. E o que o roteador usa na revelacao
    /// inicial, em que a cascata de verdade acontece dentro do MenuButton.
    /// </summary>
    public void ShowInstant()
    {
        animationVersion++;
        visible = true;
        SetInteractive(true);
        ApplyInstant(true);
    }

    /// <summary>Some sem animar. Usado na revelacao inicial, para nao haver nada atras.</summary>
    public void HideInstant()
    {
        animationVersion++;
        visible = false;
        SetInteractive(false);
        ApplyInstant(false);
    }

    /// <summary>
    /// Mostra o container da tela e deixa os botoes prontos para a revelacao em
    /// cascata, sem animar nada. E o que o roteador usa na entrada: o painel
    /// aparece e os botoes chegam um a um depois.
    ///
    /// O container entra com <c>interactable</c> falso porque ele engole o clique
    /// dos botoes que ainda nao chegaram — sem isso o primeiro toque do jogador
    /// durante a revelacao cairia no container e nao faria nada.
    /// </summary>
    public void ShowContainerOnly()
    {
        animationVersion++;
        visible = true;
        SetInteractive(false);
        SetAlpha(1f);
        SetSlide(0f);
        SetChildInput(false);
        if (children != null)
        {
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null)
                {
                    children[i].incomingAlpha = 0f;
                    children[i].incomingSlide = childSlide;
                        children[i].ApplyReveal();
                }
            }
        }
    }

    private async System.Threading.Tasks.Task RunShow(int version)
    {
        // A espera total inclui o atraso do ultimo botao da cascata, senao o
        // ultimo ficaria com o alpha pela metade quando o laco terminasse.
        float span = Mathf.Max(0.0001f, duration + DelayForChild(Mathf.Max(0, children != null ? children.Length - 1 : 0)));
        float elapsed = 0f;
        while (elapsed < span)
        {
            elapsed += Time.deltaTime;
            float e = curve.Evaluate(Mathf.Clamp01(elapsed / duration));
            SetAlpha(e);
            SetSlide(Mathf.LerpUnclamped(-slideDistance, 0f, e));
            ApplyCascade(elapsed, true);
            await Scheduler.NextFrame();
            if (version != animationVersion)
            {
                return;
            }
        }
        SetAlpha(1f);
        SetSlide(0f);
        ApplyCascade(span, true);
    }

    private async System.Threading.Tasks.Task RunHide(int version)
    {
        float fromAlpha = CurrentAlpha();
        float fromSlide = body != null ? body.localPosition.x - restX : 0f;
        float step = Mathf.Max(0.0001f, duration * 0.8f);
        float elapsed = 0f;
        while (elapsed < step)
        {
            elapsed += Time.deltaTime;
            float t = curve.Evaluate(Mathf.Clamp01(elapsed / step));
            SetAlpha(Mathf.LerpUnclamped(fromAlpha, 0f, t));
            SetSlide(Mathf.LerpUnclamped(fromSlide, -slideDistance * 0.6f, t));
            // Na ida a cascata e invertida: o ultimo botao e o primeiro a sair,
            // o que le como desfazer o que a tela fez.
            ApplyCascade(step - elapsed, false);
            await Scheduler.NextFrame();
            if (version != animationVersion)
            {
                return;
            }
        }
        SetAlpha(0f);
        SetSlide(-slideDistance * 0.6f);
        ApplyCascade(0f, false);
    }

    /// <summary>
    /// Reparte o progresso da cascata entre os botoes: cada um comeca no seu
    /// intervalo e segue a mesma curva, entao todos chegam juntos ao fim.
    /// </summary>
    private void ApplyCascade(float elapsed, bool revealing)
    {
        if (children == null)
        {
            return;
        }
        for (int i = 0; i < children.Length; i++)
        {
            MenuButton child = children[i];
            if (child == null)
            {
                continue;
            }
            float t = curve.Evaluate(Mathf.Clamp01((elapsed - DelayForChild(i)) / Mathf.Max(0.0001f, duration)));
            child.incomingAlpha = t;
            child.incomingSlide = Mathf.LerpUnclamped(childSlide, 0f, t);
            child.ApplyReveal();
            if (revealing && t >= 0.35f)
            {
                // Espera o botao estar revelado antes de deixar o ponteiro
                // mexer nele, senao um clique na aria do botao o aciona ainda
                // invisivel.
                child.EnableRevealInput();
            }
        }
    }

    private void ApplyInstant(bool isVisible)
    {
        if (isVisible)
        {
            SetAlpha(1f);
            SetSlide(0f);
            SetChildInput(true);
            if (children != null)
            {
                for (int i = 0; i < children.Length; i++)
                {
                    if (children[i] != null)
                    {
                        children[i].incomingAlpha = 1f;
                        children[i].incomingSlide = 0f;
                        children[i].ApplyReveal();
                    }
                }
            }
        }
        else
        {
            SetAlpha(0f);
            SetSlide(-slideDistance * 0.6f);
            SetChildInput(false);
            if (children != null)
            {
                for (int i = 0; i < children.Length; i++)
                {
                    if (children[i] != null)
                    {
                        children[i].incomingAlpha = 0f;
                        children[i].incomingSlide = childSlide;
                        children[i].ApplyReveal();
                    }
                }
            }
        }
    }

    private void SetChildInput(bool value)
    {
        if (children == null)
        {
            return;
        }
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] == null)
            {
                continue;
            }
            if (value)
            {
                children[i].EnableRevealInput();
            }
            else
            {
                children[i].DisableRevealInput();
            }
        }
    }

    private float CurrentAlpha()
    {
        if (group != null)
        {
            return group.alpha;
        }
        return visible ? 1f : 0f;
    }

    private void SetAlpha(float value)
    {
        if (group != null)
        {
            group.alpha = value;
            group.interactable = visible && value >= 0.15f;
            group.blocksRaycasts = visible && value >= 0.15f;
        }
    }

    private void SetSlide(float offset)
    {
        if (body != null)
        {
            body.localPosition = new Vector3(restX + offset, body.localPosition.y, body.localPosition.z);
        }
    }

    private void SetInteractive(bool value)
    {
        if (group != null)
        {
            group.interactable = value;
            group.blocksRaycasts = value;
        }
    }

    /// <summary>Intervalo que a revelacao da cascata deve esperar antes deste botao.</summary>
    public float DelayForChild(int index)
    {
        return index * stagger;
    }
}

