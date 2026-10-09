using MenSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

/// <summary>
/// O menu. Escolhe a tela, cuida da entrada em cena e mantem os botoes
/// coerentes com o que a mesa permite.
///
/// Nao ha um tela por objeto: as quatro telas sao filhas do mesmo painel e o
/// roteador mostra uma de cada vez. Trocar de tela e um evento como outro
/// qualquer — um <c>SendCustomEvent("Show")</c> no UdonBehaviour da tela — o que
/// evita um programa por tela e deixa o roteador como o unico lugar que sabe
/// qual tela pode existir em cada estado.
///
/// A revelacao inicial e uma cascata de verdade: o painel entra primeiro, a
/// linha de acento varre, e so entao os botoes aparecem um a um. E o que faz o
/// menu parecer construido, e nao ligado.
///
/// Campos de instancia sao publicos de proposito: no MenSharp um campo privado
/// vira estatico, e um estatico seria compartilhado por todos os roteadores da
/// cena.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MenuRouter : MenSharpBehaviour
{
    [Header("Telas")]
    [Tooltip("Todas as telas do menu. A de id 'main' e a inicial.")]
    public MenuScreen[] screens;
    [Tooltip("Tela mostrada ao abrir o menu.")]
    public string initialScreen = "main";

    [Header("Alvos da revelacao")]
    public CanvasGroup rootGroup;
    public RectTransform panel;
    public RectTransform accentLine;
    public MatchLobby lobby;
    [Tooltip("O baralho, para aplicar as regras vindas das configuracoes.")]
    public CardDealer dealer;

    [Header("Configuracoes")]
    [Tooltip("Linhas que mudam regra de mesa, e nao preferencia local.")]
    public MenuSettingRow[] tableSettings;
    [Tooltip("Replica as linhas de regra no baralho. So o dono dele consegue, e por isso o aviso de que a regra e do dono.")]
    public bool applyToTable = true;

    [Header("Textos")]
    [Tooltip("Linha de status embaixo dos botoes. Vazio = sem linha.")]
    public Text statusText;
    public Text hintText;
    [Tooltip("Titulo que mostra o nome do jogador local, se houver texto de nome aqui.")]
    public Text playerNameText;

    [Header("Botoes do lobby")]
    [Tooltip("Botao que abre a tela de criar partida.")]
    public MenuButton createButton;
    public MenuButton joinButton;
    public MenuButton settingsButton;
    [Tooltip("Botao de iniciar partida, na tela do lobby.")]
    public MenuButton startButton;
    public MenuButton backButton;
    public MenuButton leaveButton;

    [Header("Entrada")]
    [Tooltip("Segundos que o painel demora a entrar.")]
    public float introDuration = 0.55f;
    [Tooltip("Intervalo entre um botao e o seguinte na revelacao inicial.")]
    public float introStagger = 0.06f;
    [Tooltip("Escala de onde o painel entra. 0.94 = um pouco menor, o que da a sensacao de aproximar.")]
    public float introScale = 0.94f;
    [Tooltip("Altura, em unidades do RectTransform, de onde o painel entra.")]
    public float introRise = 26f;
    public AnimationCurve introCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Respiracao")]
    [Tooltip("Amplitude da oscilacao de repouso do painel. 0 = painel parado.")]
    public float idleAmplitude = 1.6f;
    public float idleSpeed = 0.55f;

    // Estado de navegacao. Publicos porque campo privado vira estatico no MenSharp.
    [HideInInspector] public string currentScreen = "";
    [HideInInspector] public string statusMessage = "";
    [HideInInspector] public int introVersion;
    [HideInInspector] public bool ready;
    [HideInInspector] public float panelRestY;
    [HideInInspector] public float accentRestWidth;
    [HideInInspector] public float restScale = 1f;
    [HideInInspector] public string incomingAction = "";
    [HideInInspector] public string incomingStatus = "";
    [HideInInspector] public string incomingScreen = "main";
    public void ApplyNavigation() { Open(incomingScreen); }

    public void ApplyAction() { Perform(incomingAction); }
    public void ApplyStatus() { SetStatus(incomingStatus); }

    public void Start()
    {
        ready = false;
        if (rootGroup != null) rootGroup.alpha = 0f;
        Scheduler.Run(InitializeAfterFrame);
    }

    private async System.Threading.Tasks.Task InitializeAfterFrame()
    {
        await Scheduler.NextFrame();
        InitializeMenu();
    }

    private void InitializeMenu()
    {
        if (panel != null)
        {
            panelRestY = panel.localPosition.y;
        }
        if (accentLine != null)
        {
            accentRestWidth = accentLine.sizeDelta.x;
        }
        restScale = panel != null ? panel.localScale.x : 1f;

        PrepareScreens();

        // A tela inicial aparece com o container, mas com os botoes zerados: a
        // revelacao em cascata de PlayIntro e quem os traz. Nao da para usar
        // Open aqui, porque Open recusa enquanto o menu nao esta pronto — e ele
        // nao fica pronto ate o fim da revelacao.
        MenuScreen initial = FindScreen(initialScreen);
        if (initial != null)
        {
            initial.ShowContainerOnly();
            currentScreen = initialScreen;
        }

        Refresh();

        // A revelacao e o unico lugar que comeca antes do jogador chegar, entao
        // roda por Scheduler e nao direto: os Start das telas sao em ordem
        // indefinida e o roteador precisa que elas ja existam.
        Scheduler.Run(() => PlayIntro(++introVersion));
        Scheduler.Run(Breathe);
    }

    /// <summary>
    /// Deixa todas as telas em estado fechado e some com a interacao delas, para
    /// a revelacao inicial nao comecar com duas telas visiveis.
    /// </summary>
    private void PrepareScreens()
    {
        if (screens == null)
        {
            return;
        }
        for (int i = 0; i < screens.Length; i++)
        {
            if (screens[i] != null)
            {
                screens[i].HideInstant();
            }
        }
    }

    /// <summary>
    /// Cascata de entrada: fundo, painel, linha de acento, e so entao os botoes.
    ///
    /// Os botoes sao revelados com <see cref="MenuButton.SetRevealAlpha"/> um a
    /// um em vez de chamar <c>Show</c> nas telas: e o mesmo mecanismo que a
    /// troca de tela usa, entao a entrada e a navegacao nao podem divergir.
    /// </summary>
    private async System.Threading.Tasks.Task PlayIntro(int version)
    {
        float step = Mathf.Max(0.0001f, introDuration);
        float elapsed = 0f;

        // 1. o fundo e o painel sobem juntos, de um tamanho menor e de baixo.
        while (elapsed < step)
        {
            elapsed += Time.deltaTime;
            float t = introCurve.Evaluate(Mathf.Clamp01(elapsed / step));
            if (panel != null)
            {
                float s = Mathf.LerpUnclamped(introScale, restScale, t);
                panel.localScale = new Vector3(s, s, 1f);
                panel.localPosition = new Vector3(panel.localPosition.x, panelRestY - introRise * (1f - t), panel.localPosition.z);
            }
            if (rootGroup != null)
            {
                rootGroup.alpha = t;
            }
            await Scheduler.NextFrame();
            if (version != introVersion)
            {
                return;
            }
        }
        if (panel != null)
        {
            panel.localScale = new Vector3(restScale, restScale, 1f);
            panel.localPosition = new Vector3(panel.localPosition.x, panelRestY, panel.localPosition.z);
        }
        if (rootGroup != null)
        {
            rootGroup.alpha = 1f;
        }

        // 2. a linha de acento varre atravessando o painel. Ela e o unico
        // elemento com tempo proprio, para o olho ter um motivo para esperar.
        float sweepStep = 0.12f;
        elapsed = 0f;
        while (elapsed < sweepStep)
        {
            elapsed += Time.deltaTime;
            if (accentLine != null)
            {
                float t = introCurve.Evaluate(Mathf.Clamp01(elapsed / sweepStep));
                accentLine.sizeDelta = new Vector2(Mathf.LerpUnclamped(0f, accentRestWidth, t), accentLine.sizeDelta.y);
            }
            await Scheduler.NextFrame();
            if (version != introVersion)
            {
                return;
            }
        }
        if (accentLine != null)
        {
            accentLine.sizeDelta = new Vector2(accentRestWidth, accentLine.sizeDelta.y);
        }

        // 3. os botoes da tela inicial, um a um.
        ready = true;
        MenuScreen screen = FindScreen(initialScreen);
        if (screen != null)
        {
            await RevealChildren(screen, version);
        }
        ready = true;
    }

    private async System.Threading.Tasks.Task RevealChildren(MenuScreen screen, int version)
    {
        MenuButton[] children = screen.children;
        if (children == null || children.Length == 0)
        {
            return;
        }
        float step = Mathf.Max(0.0001f, 0.14f);
        float total = step + introStagger * (children.Length - 1);
        float elapsed = 0f;
        while (elapsed < total)
        {
            elapsed += Time.deltaTime;
            for (int i = 0; i < children.Length; i++)
            {
                MenuButton child = children[i];
                if (child == null)
                {
                    continue;
                }
                float t = introCurve.Evaluate(Mathf.Clamp01((elapsed - i * introStagger) / step));
                child.incomingAlpha = t;
                child.incomingSlide = Mathf.LerpUnclamped(22f, 0f, t);
                child.ApplyReveal();
                if (t >= 0.35f)
                {
                    child.EnableRevealInput();
                }
            }
            await Scheduler.NextFrame();
            if (version != introVersion)
            {
                return;
            }
        }
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null)
            {
                children[i].incomingAlpha = 1f;
                children[i].incomingSlide = 0f;
                children[i].ApplyReveal();
                children[i].EnableRevealInput();
            }
        }
    }

    /// <summary>
    /// A oscilacao de repouso do painel. Muito pequena e lenta de proposito: e o
    /// que impede a cena de parecer uma imagem parada, e nao um efeito.
    /// </summary>
    private async System.Threading.Tasks.Task Breathe()
    {
        while (true)
        {
            if (ready && panel != null && idleAmplitude > 0f)
            {
                float y = Mathf.Sin(Time.time * idleSpeed) * idleAmplitude;
                panel.localPosition = new Vector3(panel.localPosition.x, panelRestY + y, panel.localPosition.z);
            }
            await Scheduler.NextFrame();
        }
    }

    /// <summary>Abre uma tela pelo id, fechando as outras.</summary>
    public void Open(string id)
    {
        if (!ready)
        {
            // Durante a revelacao o roteador nao navega: os botoes ainda estao
            // chegando e uma troca de tela aqui trocaria a tela sob os pes do
            // jogador que ainda esta vendo a entrada.
            return;
        }
        if (currentScreen == id)
        {
            return;
        }
        MenuScreen target = FindScreen(id);
        if (target == null)
        {
            return;
        }
        // A navigation click cancels the initial cascade immediately.
        introVersion++;

        // Sai de todas antes de entrar em uma: duas telas animando ao mesmo tempo
        // se atropelam no meio do painel, e o resultado depende da ordem dos
        // Starts.
        if (screens != null)
        {
            for (int i = 0; i < screens.Length; i++)
            {
                if (screens[i] != null && screens[i].id != id)
                {
                    screens[i].Hide();
                }
            }
        }
        target.Show();
        currentScreen = id;
        Refresh();
    }

    /// <summary>Volta para a tela principal.</summary>
    public void GoMain()
    {
        Open("main");
    }

    /// <summary>Abre a tela de criar partida, se o lobby permitir.</summary>
    public void GoCreate()
    {
        if (lobby != null && lobby.MatchRunning())
        {
            return;
        }
        Open("create");
    }

    /// <summary>Abre a tela de entrar na partida, se o lobby permitir.</summary>
    public void GoJoin()
    {
        if (lobby != null && lobby.MatchRunning())
        {
            return;
        }
        Open("join");
    }

    /// <summary>Abre as configuracoes.</summary>
    public void GoSettings()
    {
        Open("settings");
    }

    /// <summary>
    /// O que o botao pediu. Um <c>switch</c> sobre string seria mais curto, mas
    /// o Udon resolve <c>switch</c> de string por comparacao e a cadeia de
    /// <c>if</c> sai mais barata — e este metodo roda em todo clique.
    /// </summary>
    public void Perform(string action)
    {
        if (action == "Create") { GoCreate(); return; }
        if (action == "Join") { GoJoin(); return; }
        if (action == "Settings") { GoSettings(); return; }
        if (action == "Back" || action == "Main") { GoMain(); return; }
        if (action == "Start") { if (lobby != null) lobby.RequestStart(); return; }
        if (action == "Host") { if (lobby != null) { lobby.RequestCreate(); if (lobby.previewOnly) Open("create"); } return; }
        if (action == "JoinNow") { if (lobby != null) { lobby.RequestJoin(); if (lobby.previewOnly) Open("join"); } return; }
        if (action == "Leave") { if (lobby != null) { lobby.RequestLeave(); if (lobby.previewOnly) GoMain(); } return; }
    }

    /// <summary>
    /// Reavalia o que cada botao pode fazer. Quem manda e o
    /// <see cref="MatchLobby"/>, nao este objeto: a UI so reflete a mesa.
    /// </summary>
    public void Refresh()
    {
        bool running = lobby != null && lobby.MatchRunning();
        bool host = lobby != null && lobby.IsHost();
        bool occupied = lobby != null && lobby.Occupied();
        bool canStart = lobby != null && lobby.CanStartMatch();

        if (createButton != null)
        {
            // Nao offering "criar" para quem nao tem o baralho: o CardDealer
            // recusaria, e um botao que promete e nao cumpre e pior que um botao
            // desligado.
            createButton.incomingBlocked = lobby == null || !lobby.CanCreate();
            createButton.ApplyBlocked();
        }
        if (joinButton != null)
        {
            joinButton.incomingBlocked = lobby == null || !lobby.CanJoin();
            joinButton.ApplyBlocked();
        }
        if (startButton != null)
        {
            startButton.incomingBlocked = !canStart;
            startButton.ApplyBlocked();
        }
        if (leaveButton != null)
        {
            leaveButton.incomingBlocked = !occupied;
            leaveButton.ApplyBlocked();
        }
        if (settingsButton != null)
        {
            // Configuracoes continuam valendo com a partida rodando; e o unico
            // lugar onde o jogador pode mexer em algo com a partida em curso.
            settingsButton.incomingBlocked = false;
            settingsButton.ApplyBlocked();
        }

        if (hintText != null)
        {
            hintText.text = HintFor(running, host, occupied);
        }
    }

    private string HintFor(bool running, bool host, bool occupied)
    {
        if (lobby != null && lobby.previewOnly)
        {
            return occupied ? "Waiting for players." : "Two players. One table. Get closer to 21.";
        }
        if (running)
        {
            return "Match in progress.";
        }
        if (host)
        {
            return "Waiting for " + Mathf.Max(0, 2 - PlayersPresent()) + " player(s).";
        }
        if (occupied)
        {
            return "Waiting for the host to start.";
        }
        return "Create a match or join a table.";
    }

    private int PlayersPresent()
    {
        return lobby != null ? lobby.PlayersPresent() : 0;
    }

    /// <summary>
    /// Uma linha de configuracao mudou. As de regra vao para o baralho; as de
    /// preferencia ficam so neste cliente.
    ///
    /// A regra so e replicada quando o jogador local tem o baralho, e nao por
    /// protecao contra o jogador: quem nao tem o baralho nao pode mudar a mesa,
    /// porque quem aplica a regra no proximo <c>StartMatch</c> e o dono, e a
    /// vida e a aposta vivem em campos locais de cada cliente. Escrever neles de
    /// um cliente que nao arbitra a partida nao teria efeito nenhum, alem de
    /// fazer o menu mentir.
    ///
    /// E o efeito vale a partir da proxima partida: os campos sao lidos em
    /// <c>StartMatch</c>, nao durante a rodada.
    /// </summary>
    public void OnSettingChanged(string id, int value)
    {
        if (!applyToTable || dealer == null || id == "")
        {
            return;
        }
        if (!dealer.LocalPlayerOwnsDeck())
        {
            return;
        }
        if (id == "life")
        {
            dealer.startingLife = 1 + value;
        }
        else if (id == "bet")
        {
            dealer.roundDamage = 1 + value;
        }
        else if (id == "growth")
        {
            dealer.roundDamageGrowth = value;
        }
        else if (id == "timeout")
        {
            dealer.turnTimeoutSeconds = value <= 0 ? 0f : 15f * value;
        }
        RefreshSettingsHint();
    }

    /// <summary>
    /// Escreve nas linhas o que o baralho esta usando agora, para o menu nunca
    /// discordar da mesa depois de um recarregamento ou de outro cliente mudar
    /// alguma coisa.
    /// </summary>
    public void RefreshSettingsHint()
    {
        if (dealer == null || tableSettings == null)
        {
            return;
        }
        for (int i = 0; i < tableSettings.Length; i++)
        {
            MenuSettingRow row = tableSettings[i];
            if (row == null || row.settingId == "")
            {
                continue;
            }
            if (row.settingId == "life")
            {
                row.SetValue(Mathf.Clamp(dealer.startingLife - 1, 0, row.OptionCount() - 1));
            }
            else if (row.settingId == "bet")
            {
                row.SetValue(Mathf.Clamp(dealer.roundDamage - 1, 0, row.OptionCount() - 1));
            }
            else if (row.settingId == "growth")
            {
                row.SetValue(Mathf.Clamp(dealer.roundDamageGrowth, 0, row.OptionCount() - 1));
            }
            else if (row.settingId == "timeout")
            {
                row.SetValue(dealer.turnTimeoutSeconds <= 0f ? 0 : Mathf.Clamp(Mathf.RoundToInt(dealer.turnTimeoutSeconds / 15f), 0, row.OptionCount() - 1));
            }
        }
    }

    /// <summary>Escreve a linha de status. Vazio limpa.</summary>
    public void SetStatus(string message)
    {
        statusMessage = message == null ? "" : message;
        if (statusText != null)
        {
            statusText.text = statusMessage;
        }
    }

    /// <summary>Atualiza o nome do jogador local, se houver TMP ligado.</summary>
    public void SetPlayerName(string name)
    {
        if (playerNameText != null)
        {
            playerNameText.text = name == null ? "" : name;
        }
    }

    public MenuScreen FindScreen(string id)
    {
        if (screens == null || id == "")
        {
            return null;
        }
        for (int i = 0; i < screens.Length; i++)
        {
            if (screens[i] != null && screens[i].id == id)
            {
                return screens[i];
            }
        }
        return null;
    }
}

