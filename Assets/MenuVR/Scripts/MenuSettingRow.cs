using MenSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

/// <summary>
/// Uma linha de configuracao: um rotulo, um valor e dois botoes que mudam o
/// valor de um em um.
///
/// Nao ha campo de texto de proposito. A VRChat nao da para escrever texto por
/// VR, entao um <c>TMP_InputField</c> seria um controle que ninguem consegue
/// usar de headset. Todas as opcoes do Twenty One sao poucas e discretas — vida,
/// aposta, tempo de vez — e um par de +/- e mais rapido e mais preciso que
/// digitar um numero.
///
/// O valor vive aqui e e lido pelo <see cref="MenuRouter"/>, que decide o que
/// fazer com ele. A linha nao escreve no <see cref="CardDealer"/> sozinha: quem
/// aplica a mudanca e o roteador, porque a regra e dele e nao dela.
///
/// Campos de instancia sao publicos de proposito: no MenSharp um campo privado
/// vira estatico, e um estatico seria compartilhado por todas as linhas.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MenuSettingRow : MenSharpBehaviour
{
    [Header("Identidade")]
    [Tooltip("Nome que o roteador usa para saber qual valor e este.")]
    public string settingId = "";
    [Tooltip("Rotulo da linha. Sobrescreve o texto do TMP quando esta vazio.")]
    public string label = "";

    [Header("Valor")]
    [Tooltip("Opcoes em texto, na ordem. Uma opcao so = interruptor.")]
    public string[] options = { "Desligado", "Ligado" };
    [HideInInspector] public int value = 0;

    [Header("Alvos")]
    public Text labelText;
    public Text valueText;
    [Tooltip("Quem recebe o aviso de que o valor mudou.")]
    public MenuRouter router;

    [Header("Movimento")]
    [Tooltip("Pulso do valor ao trocar, para o olho perceber mesmo sem ler.")]
    public float pulseDuration = 0.26f;
    public float pulseScale = 1.18f;
    public RectTransform valueBody;

    [HideInInspector] public int animationVersion;
    [HideInInspector] public float valueRestX;
    [HideInInspector] public float valueRestY;

    public void Start()
    {
        if (valueBody == null)
        {
            valueBody = transform as RectTransform;
        }
        valueRestX = valueBody.localPosition.x;
        valueRestY = valueBody.localPosition.y;
        Normalize();
        WriteText();
    }

    /// <summary>Quantas opcoes a linha tem.</summary>
    public int OptionCount()
    {
        return options != null ? options.Length : 0;
    }

    /// <summary>Avanca um. Chamado pelo botao "+".</summary>
    public void Next()
    {
        Step(1);
    }

    /// <summary>Volta um. Chamado pelo botao "-".</summary>
    public void Previous()
    {
        Step(-1);
    }

    /// <summary>
    /// Troca o valor e avisa. Dar a volta no fim e o que torna a linha um
    /// interruptor quando so tem duas opcoes, e evita que o jogador fique
    /// apertando "-" numa linha que ja esta no primeiro valor.
    /// </summary>
    private void Step(int by)
    {
        int count = OptionCount();
        if (count <= 1)
        {
            return;
        }
        value = ((value + by) % count + count) % count;
        WriteText();
        Scheduler.Run(() => Pulse(++animationVersion));
        if (router != null)
        {
            router.OnSettingChanged(settingId, value);
        }
    }

    /// <summary>
    /// Escreve um valor vindo de fora — o roteador, quando uma mudanca de regra
    /// e aceita por outro caminho. Nao dispara o pulso: a linha nao foi mexida
    /// pela mao do jogador.
    /// </summary>
    public void SetValue(int newValue)
    {
        value = newValue;
        Normalize();
        WriteText();
    }

    /// <summary>Texto da opcao atual. O roteador usa para montar o resumo.</summary>
    public string ValueText()
    {
        int count = OptionCount();
        if (count <= 0)
        {
            return "";
        }
        if (value < 0 || value >= count)
        {
            return "";
        }
        return options[value];
    }

    private void Normalize()
    {
        int count = OptionCount();
        if (count <= 0)
        {
            return;
        }
        if (value < 0)
        {
            value = 0;
        }
        if (value >= count)
        {
            value = count - 1;
        }
    }

    private void WriteText()
    {
        if (labelText != null && label != "")
        {
            labelText.text = label;
        }
        if (valueText != null)
        {
            valueText.text = ValueText();
        }
    }

    /// <summary>
    /// O valor da um pulo e volta. Curto de proposito: se durasse mais que o
    /// intervalo entre dois cliques, os pulsos se empilhariam e o numero
    /// pareceria tremido em vez de destacado.
    /// </summary>
    private async System.Threading.Tasks.Task Pulse(int version)
    {
        float elapsed = 0f;
        float step = Mathf.Max(0.0001f, pulseDuration);
        while (elapsed < step)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / step);
            // Sobe rapido e desce devagar: e um aceno, nao uma ida e volta.
            float k = t < 0.35f ? t / 0.35f : 1f - (t - 0.35f) / 0.65f;
            float s = Mathf.LerpUnclamped(1f, pulseScale, k);
            if (valueBody != null)
            {
                valueBody.localScale = new Vector3(s, s, 1f);
            }
            await Scheduler.NextFrame();
            if (version != animationVersion)
            {
                return;
            }
        }
        if (valueBody != null)
        {
            valueBody.localScale = new Vector3(1f, 1f, 1f);
        }
    }
}
