using MenSharp;
using UnityEngine;

/// <summary>
/// Escreve a contagem do turno do <see cref="CardDealer"/> nas TVs CRT.
///
/// O material <c>CRTScreen</c> ja sabe desenhar MM:SS: o shader monta os digitos
/// a partir de <c>_Seconds</c>. Aqui esse valor vem de
/// <see cref="CardDealer.turnSecondsLeft"/>, que o dono do baralho sincroniza,
/// entao todos os clientes mostram o mesmo numero. A cada segundo o mostrador da
/// um pulso de brilho e, nos ultimos segundos, a cor vira para alerta.
///
/// Sem contagem valendo (fim de rodada ou partida encerrada), a TV passa a
/// mostrar <c>_Message</c>: "YouWon" para quem ganhou a ultima rodada e "YouLost"
/// para o outro, usando <see cref="CardDealer.lastRoundWinner"/> e o assento
/// local. Sem nada disso, o shader volta ao X padrao.
///
/// Nada neste script e sincronizado: cada cliente anima o proprio material a
/// partir dos valores compartilhados. E puramente visual.
///
/// Publicos de proposito: no MenSharp um campo privado vira estatico, e um
/// estatico seria compartilhado pelas TVs da mesa.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class CRTScreenTimer : MenSharpBehaviour
{
    [Header("Source")]
    [Tooltip("O dono do baralho, de onde sai o tempo restante. Vazio = procura no mesmo objeto.")]
    public CardDealer dealer;

    [Header("Screens")]
    [Tooltip("Renderers das TVs que usam o material CRTScreen (os CRT_ScreenGlow).")]
    public Renderer[] screens;

    [Tooltip("CRT lights that follow the screen tint locally.")]
    public Light[] screenLights;

    [Header("Cores")]
    public Color normalTint = new Color(0.72f, 0.79f, 0.80f, 1f);
    public Color normalText = new Color(0.85f, 0.95f, 0.90f, 1f);

    [Tooltip("A partir de quantos segundos restantes a tela entra em alerta.")]
    public int alertSeconds = 10;
    public Color alertTint = new Color(0.95f, 0.35f, 0.28f, 1f);
    public Color alertText = new Color(1f, 0.30f, 0.24f, 1f);

    [Header("Pulso por segundo")]
    [Tooltip("Brilho extra somado no tique de cada segundo.")]
    public float pulseStrength = 0.6f;
    [Tooltip("Quanto tempo o pulso leva para sumir, em segundos.")]
    public float pulseDuration = 0.18f;
    [Tooltip("Intensidade de repouso do material (a mesma que veio do CRTScreen).")]
    public float baseIntensity = 1.5f;

    [Header("Debug")]
    [Tooltip("Loga cada segundo que a TV desenha.")]
    public bool logTicks = false;

    // Publicos de proposito: no MenSharp um campo privado vira estatico.
    [HideInInspector] public int lastSeconds = -1;
    [HideInInspector] public int lastMessage = -1;
    [HideInInspector] public float pulse = 0f;

    // Valores do enum _Message do shader (None, YouLost, YouWon).
    private const int MessageNone = 0;
    private const int MessageLost = 1;
    private const int MessageWon = 2;

    public void Start()
    {
        if (dealer == null)
        {
            dealer = GetComponent<CardDealer>();
        }

    }

    public float nextTimerPoll;
    public float lastTimerPoll;
    public void Update()
    {
        if (Time.time < nextTimerPoll) return;
        float delta = Time.time - lastTimerPoll;
        lastTimerPoll = Time.time;
        nextTimerPoll = Time.time + 0.1f;
        Apply(delta);
    }
    /// <summary>
    /// Le o estado do dealer e acerta as TVs. A contagem manda enquanto ha
    /// tempo; fora dela, mostra YouWon/YouLost para o jogador local conforme o
    /// vencedor da ultima rodada; sem nada disso, cai no X padrao do shader. O
    /// pulso e disparado na mudanca do segundo, e nao no relogio local, entao o
    /// flash acompanha o numero que acabou de chegar, em qualquer cliente.
    /// </summary>
    private void Apply(float deltaTime)
    {
        if (dealer == null || screens == null)
        {
            return;
        }

        bool result = dealer.roundResolving || dealer.matchOver;
        int seconds = dealer.matchStarted && !result ? dealer.turnSecondsLeft : 0;
        bool ticked = seconds != lastSeconds;
        if (ticked)
        {
            lastSeconds = seconds;
            pulse = 1f;
            if (logTicks)
            {
                Debug.Log("CRTScreenTimer: " + seconds + "s restantes.");
            }
        }
        else if (pulse > 0f)
        {
            pulse = Mathf.Max(0f, pulse - deltaTime / Mathf.Max(0.001f, pulseDuration));
        }

        // Sem contagem valendo, a TV passa a mostrar o resultado da ultima
        // rodada: quem venceu le "YouWon", o outro le "YouLost". Espectador
        // (nem um nem outro) fica so com o X do shader.
        bool counting = seconds > 0;
        int message = MessageNone;
        if (result)
        {
            int winner = dealer.lastRoundWinner;
            if (winner < 0) message = 3; // DRAW
            else if (winner <= 1)
            {
                bool first = dealer.IsLocalPlayer(0);
                bool second = dealer.IsLocalPlayer(1);
                if ((first && second) || (!first && !second)) message = 4 + winner;
                else if (dealer.IsLocalPlayer(winner))
                {
                    message = MessageWon;
                }
                else if (dealer.IsLocalPlayer(1 - winner))
                {
                    message = MessageLost;
                }
            }
        }

        bool messageChanged = message != lastMessage;
        if (messageChanged)
        {
            lastMessage = message;
            if (logTicks)
            {
                Debug.Log("CRTScreenTimer: mensagem " + message + ".");
            }
        }

        bool alert = counting && seconds <= alertSeconds;
        Color tint = alert ? alertTint : normalTint;
        Color text = alert || message == MessageLost ? alertText : normalText;

        if ((ticked || messageChanged) && screenLights != null)
        {
            for (int i = 0; i < screenLights.Length; i++)
                if (screenLights[i] != null) screenLights[i].color = tint;
        }

        for (int i = 0; i < screens.Length; i++)
        {
            Renderer screen = screens[i];
            if (screen == null)
            {
                continue;
            }
            Material material = screen.material;
            if (material == null)
            {
                continue;
            }
            if (ticked)
            {
                material.SetFloat("_Seconds", (float)seconds);
                material.SetFloat("_ColonBlink", 1f);
            }
            if (messageChanged)
            {
                material.SetFloat("_Message", (float)message);
            }
            if (ticked || messageChanged)
            {
                material.SetColor("_Tint", tint);
                material.SetColor("_TextColor", text);
            }
            material.SetFloat("_Intensity", baseIntensity + pulse * pulseStrength);
        }
    }
}
