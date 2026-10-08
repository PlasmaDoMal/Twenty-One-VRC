using System.Collections.Generic;
using MenSharp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.Udon;

/// <summary>
/// Monta o menu do Twenty One na cena aberta.
///
/// O menu e so tipografia sobre preto. Nao ha painel preenchido, nem botao com
/// fundo, nem cor decorativa: cada opcao e um numero apagado mais um texto, com
/// um fio branco de 2 pixels embaixo que cresce quando o ponteiro entra. O que
/// da a sensacao de "puxar" o item e o texto inteiro clarear junto com o fio,
/// entao nada precisa estar desenhado atras dele.
///
/// Nenhum asset: a fonte e a <c>LegacyRuntime.ttf</c> que vem dentro do
/// proprio Unity, e o fundo e uma <c>Image</c> preta com o material padrao da
/// UI. Isso mantem o menu inteiro em tres scripts de comportamento e um canvas.
///
/// A area de clique e o proprio texto do botao, com a altura da linha toda: a UI
/// da VRChat so acerta o que tem <c>raycastTarget</c>, entao e o <c>Text</c>
/// que precisa estar ligado, e nenhum retangulo invisivel e necessario.
///
/// Como chega na cena:
/// <list type="number">
/// <item>a hierarquia nasce com <see cref="RectTransform"/> explicito, porque
/// <c>new GameObject(name, typeof(RectTransform))</c> e o unico jeito de um
/// objeto de UI nascer com retangulo em vez de cubo;</item>
/// <item>os comportamentos MenSharp ganham o <c>UdonBehaviour</c> que os executa
/// por <c>SyncPairs</c> — antes disso nao ha programa paired, e e para o
/// UdonBehaviour que os <c>UnityEvent</c> apontam;</item>
/// <item>os botoes sao ligados por <c>SendCustomEvent</c>, e nao por interface:
/// a UI procura um componente que implemente <c>IPointerClickHandler</c>, e no
/// runtime o componente e o UdonBehaviour, que so conhece metodos por nome.</item>
/// </list>
/// </summary>
public static class MenuVRBuilder
{
    /// <summary>Margem esquerda da coluna de texto, em unidades do canvas.</summary>
    private const float Margin = 200f;
    /// <summary>Largura da coluna de texto.</summary>
    private const float Column = 2000f;

    // Paleta: branco sobre preto, e nada mais. Os tres tons de cinza sao a
    // hierarquia inteira do menu — rotulo, estado de repouso, estado apagado.
    private static readonly Color Paper = new Color(1f, 1f, 1f, 1f);
    private static readonly Color Rest = new Color(0.84f, 0.84f, 0.84f, 1f);
    private static readonly Color Dim = new Color(0.55f, 0.55f, 0.55f, 1f);
    private static readonly Color Faint = new Color(0.42f, 0.42f, 0.42f, 1f);
    private static readonly Color Blocked = new Color(0.38f, 0.38f, 0.38f, 1f);

    private static MenuRouter _router;

    [MenuItem("Tools/Menu VR/Build Menu (GameMenu2)")]
    public static void BuildMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("MenuVRBuilder: nao ha cena valida aberta.");
            return;
        }

        _router = null;
        GameObject existing = GameObject.Find("MenuSystem");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }
        // Restos de uma montagem anterior ficariam na raiz da cena, cobrindo tudo.
        foreach (string orphanName in new[] { "Backdrop", "Panel", "Content" })
        {
            GameObject orphan = GameObject.Find(orphanName);
            if (orphan != null && orphan.transform.parent == null)
            {
                Undo.DestroyObjectImmediate(orphan);
            }
        }

        Font font = BuiltinFont();
        if (font == null)
        {
            Debug.LogError("MenuVRBuilder: a fonte embutida do Unity nao foi encontrada.");
            return;
        }

        Canvas canvas = BuildCanvas(new GameObject("MenuSystem"));
        RectTransform backdrop = BuildBackdrop(canvas.transform);
        RectTransform content = NewUI("Content", canvas.transform).GetComponent<RectTransform>();
        Stretch(content);

        var router = content.gameObject.AddComponent<MenuRouter>();
        router.rootGroup = content.gameObject.AddComponent<CanvasGroup>();
        router.panel = content;

        var lobby = content.gameObject.AddComponent<MatchLobby>();
        lobby.router = router;
        router.lobby = lobby;
        _router = router;

        // A linha de cabecalho e o alvo da varredura da revelacao: e o unico
        // elemento do menu com tempo proprio.
        router.accentLine = BuildHeader(content, font);
        RectTransform screens = NewUI("Screens", content).GetComponent<RectTransform>();
        Stretch(screens);

        ScreenBuild main = BuildMainScreen(screens, font);
        ScreenBuild create = BuildCreateScreen(screens, font);
        ScreenBuild join = BuildJoinScreen(screens, font);
        router.screens = new MenuScreen[] { main.screen, create.screen, join.screen };
        router.createButton = main.create;
        router.joinButton = main.join;
        router.settingsButton = main.settings;
        router.startButton = create.start;
        router.leaveButton = null;
        router.backButton = main.back;

        // GameMenu2 e um menu isolado, sem CardDealer: as linhas existem e mudam
        // de valor, mas nao tem mesa para onde aplicar. Arraste o baralho para
        // MenuRouter.dealer quando a cena do jogo for montada. Ver o README.
        router.dealer = null;
        router.tableSettings = new MenuSettingRow[0];
        router.applyToTable = false;
        router.idleAmplitude = 0f;
        router.introDuration = 0.18f;
        router.introStagger = 0.025f;

        router.statusText = BuildLine(content, "StatusText", -470f, font, 32, Rest, TextAnchor.MiddleLeft);
        router.hintText = BuildLine(content, "HintText", -555f, font, 28, Dim, TextAnchor.MiddleLeft);
        BuildLine(content, "Footer", -655f, font, 21, Faint, TextAnchor.MiddleLeft).text = "TWENTY ONE                                                VRCHAT / MULTIPLAYER";

        EditorUtility.SetDirty(router);
        EditorUtility.SetDirty(lobby);
        EditorUtility.SetDirty(backdrop.gameObject);
        foreach (MenuScreen screen in router.screens)
        {
            if (screen != null)
            {
                EditorUtility.SetDirty(screen);
            }
        }

        // O UdonBehaviour e o alvo dos UnityEvent, entao tem de existir antes de
        // qualquer ligacao.
        PairAll(content.gameObject);

        Wire(main);
        Wire(create);
        Wire(join);
        MenSharpProxy.SyncThenTransfer(new List<GameObject>(ButtonTargets(content.gameObject)) { content.gameObject }, false);

        AimCameraAtMenu(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("MenuVRBuilder: menu montado em \"" + scene.name + "\" — 3 telas, "
            + (main.buttons.Count + create.buttons.Count + join.buttons.Count)
            + " alvos ligados. O Main Camera foi apontado para o canvas.");
    }

    /// <summary>
    /// Aponta a camera da cena para o canvas, na altura dos olhos e recuada o
    /// suficiente para o menu inteiro caber.
    ///
    /// Nao e大步: sem isso a cena fica com a camera na pose padrao, o canvas entra
    /// pela borda do visor e o menu parece quebrado quando se abre a cena. So a
    /// pose e mudada; nada e criado, para nao sequestrar uma camera que ja estivesse
    /// posicionada de proposito.
    /// </summary>
    private static void AimCameraAtMenu(Scene scene)
    {
        Camera camera = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            camera = root.GetComponentInChildren<Camera>();
            if (camera != null)
            {
                break;
            }
        }
        if (camera == null)
        {
            Debug.LogWarning("MenuVRBuilder: a cena nao tem Camera; o menu foi montado, mas nao ha o que olhar.");
            return;
        }

        Transform camTransform = camera.transform;
        camTransform.position = new Vector3(0f, 1.5f, -2.4f);
        camTransform.rotation = Quaternion.identity;
        camera.fieldOfView = 36f;
        if (scene.name == "GameMenu2")
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var descriptor = root.GetComponent<VRCSceneDescriptor>();
                if (descriptor == null) continue;
                descriptor.ReferenceCamera = camera.gameObject;
                foreach (Transform spawn in descriptor.spawns)
                {
                    if (spawn == null) continue;
                    spawn.SetPositionAndRotation(new Vector3(0f, 0f, -2.4f), Quaternion.identity);
                }
                EditorUtility.SetDirty(descriptor);
            }
        }

        // A cena e so o menu: nada ha para ver atras dele, e o fundo preto e a
        // propria paleta do design em vez do ceu do editor atrapalhando a leitura.
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
    }

    /// <summary>
    /// A fonte que ja vem dentro do Unity. Nada e importado de Assets: e a
    /// <c>LegacyRuntime.ttf</c>, que e o que a UI legada usa por padrao, e por
    /// isso o pacote de TMP nao precisa estar envolvido.
    /// </summary>
    private static Font BuiltinFont()
    {
        // 2022.2+ renomeou Arial.ttf para LegacyRuntime.ttf. O nome antigo ainda
        // resolve em algumas versoes, entao os dois sao tentados.
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        return font;
    }

    // ------------------------------------------------------------------ canvas

    private static Canvas BuildCanvas(GameObject system)
    {
        RectTransform rect = system.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(2400f, 1500f);
        rect.localScale = Vector3.one * 0.001f;
        // A altura e em METROS do mundo, nao em unidades de canvas: 2400 x 1500
        // vezes 0.001 da 2,4 x 1,5 m, entao o centro fica na altura dos olhos.
        // Usar 1550 aqui — o numero do canvas antes da escala — punha o menu
        // 1,5 km acima da camera, invisivel.
        rect.localPosition = new Vector3(0f, 1.5f, 0f);

        Canvas canvas = system.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = null;
        canvas.sortingOrder = 20;
        system.AddComponent<GraphicRaycaster>();
        system.AddComponent<VRCUiShape>();
        return canvas;
    }

    /// <summary>
    /// O preto do fundo. E um retangulo do tamanho do canvas com a UI padrao:
    /// nao ha material, nem textura, nem shader.
    ///
    /// <c>raycastTarget</c> fica ligado de proposito. O fundo e opaco e cobre o
    /// canvas inteiro, entao sem isso um clique que caia entre dois textos
    /// atravessaria para o objeto que estivesse atras do menu.
    /// </summary>
    private static RectTransform BuildBackdrop(Transform parent)
    {
        GameObject go = NewUI("Backdrop", parent);
        RectTransform rect = go.GetComponent<RectTransform>();
        Stretch(rect);
        Image image = go.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;
        go.AddComponent<VRCUiShape>();
        return rect;
    }

    /// <summary>
    /// Cabecalho: o titulo e o fio que o separa do resto. Devolve o fio, que e o
    /// alvo da varredura da revelacao.
    ///
    /// Nao ha olho acima do titulo. Um rotulo pequeno com letras espacadas acima
    /// de um cabecalho e a assinatura de um tema, nao informacao: o titulo ja
    /// diz o que o menu e. E o titulo fica em caixa de sentenca — versalete em
    /// 96 unidades seria um outdoor, e o menu nao e uma placa.
    /// </summary>
    private static RectTransform BuildHeader(RectTransform content, Font font)
    {
        BuildLine(content, "Eyebrow", 580f, font, 24, Dim, TextAnchor.MiddleLeft).text = "ONE VERSUS ONE";
        BuildLine(content, "Title", 430f, font, 136, Paper, TextAnchor.MiddleLeft).text = "TWENTY ONE";
        BuildLine(content, "Subtitle", 295f, font, 30, Dim, TextAnchor.MiddleLeft).text = "Every card changes the game.";
        var watermark = BuildLine(content, "Watermark", -40f, font, 620, new Color(1f, 1f, 1f, 0.055f), TextAnchor.MiddleRight);
        watermark.text = "21";
        watermark.transform.SetAsFirstSibling();

        // Um fio, nao uma caixa. E o unico elemento que separa o cabecalho do
        // conteudo: sem ele os textos flutuam juntos e a coluna perde o topo.
        //
        // A largura aqui e a largura de REPOUSO, e e a que o MenuRouter lê no
        // Start para saber ate onde varrer. Deixar zero aqui faria a varredura
        // ir de zero a zero e o fio nunca apareceria.
        GameObject rule = NewUI("TitleRule", content);
        RectTransform ruleRect = rule.GetComponent<RectTransform>();
        ruleRect.anchorMin = new Vector2(0f, 0.5f);
        ruleRect.anchorMax = new Vector2(0f, 0.5f);
        ruleRect.pivot = new Vector2(0f, 0.5f);
        ruleRect.sizeDelta = new Vector2(Column, 2f);
        ruleRect.anchoredPosition = new Vector2(Margin, 205f);
        Image ruleImage = rule.AddComponent<Image>();
        ruleImage.color = new Color(1f, 1f, 1f, 0.16f);
        ruleImage.raycastTarget = false;
        rule.AddComponent<VRCUiShape>();
        return ruleRect;
    }

    // ------------------------------------------------------------------ texto

    /// <summary>
    /// Uma linha de texto ancorada na margem esquerda da coluna. Todo texto do
    /// menu passa por aqui, para o alinhamento ser o mesmo em todos e a coluna
    /// nao encostar nas bordas por causa de um alinhamento esquecido.
    /// </summary>
    private static Text BuildLine(
        Transform parent, string name, float y, Font font, int size, Color color, TextAnchor alignment)
    {
        GameObject go = NewUI(name, parent);
        RectTransform rect = go.GetComponent<RectTransform>();
        Place(rect, Margin, y, Column, size * 1.9f);

        Text text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        go.AddComponent<VRCUiShape>();
        return text;
    }

    // ------------------------------------------------------------------ telas

    private sealed class ScreenBuild
    {
        public MenuScreen screen;
        /// <summary>Botoes da cascata de revelacao, na ordem em que aparecem.</summary>
        public List<GameObject> buttons = new List<GameObject>();
        /// <summary>Passos de configuracao. Fora da cascata: cada um tem seu tempo.</summary>
        public List<GameObject> steppers = new List<GameObject>();
        public MenuButton create, join, settings, start, leave, back;

    }

    private static ScreenBuild NewScreen(RectTransform screens, string id, int order)
    {
        var build = new ScreenBuild();
        RectTransform rect = NewUI("Screen_" + id, screens).GetComponent<RectTransform>();
        Stretch(rect);
        build.screen = rect.gameObject.AddComponent<MenuScreen>();
        build.screen.id = id;
        build.screen.order = order;
        build.screen.duration = 0.14f;
        build.screen.stagger = 0.02f;
        build.screen.slideDistance = 18f;
        build.screen.childSlide = 10f;
        build.screen.body = rect;
        build.screen.group = rect.gameObject.AddComponent<CanvasGroup>();
        build.screen.group.alpha = id == "main" ? 1f : 0f;
        build.screen.group.blocksRaycasts = id == "main";
        build.screen.group.interactable = id == "main";
        return build;
    }

    private static ScreenBuild BuildMainScreen(RectTransform screens, Font font)
    {
        ScreenBuild b = NewScreen(screens, "main", 0);
        b.create = AddButton(b, "BtnCreate", "Create match  →", 40f, "Host", font, false);
        b.join = AddButton(b, "BtnJoin", "Join match  →", -135f, "JoinNow", font, false);
        Finish(b);
        return b;
    }

    private static ScreenBuild BuildCreateScreen(RectTransform screens, Font font)
    {
        ScreenBuild b = NewScreen(screens, "create", 1);
        BuildLine(b.screen.body, "LobbyTitle", 65f, font, 58, Paper, TextAnchor.MiddleLeft).text = "Your table is ready.";
        BuildLine(b.screen.body, "LobbyInfo", -35f, font, 29, Dim, TextAnchor.MiddleLeft).text = "Waiting for a second player.";
        b.start = AddButton(b, "BtnStart", "Start match", -180f, "Start", font, false);
        b.start.incomingBlocked = true;
        b.start.blocked = true;
        b.start.label.color = Blocked;
        b.back = AddButton(b, "BtnCreateBack", "←  Leave table", -340f, "Leave", font, true);
        Finish(b);
        return b;
    }

    private static ScreenBuild BuildJoinScreen(RectTransform screens, Font font)
    {
        ScreenBuild b = NewScreen(screens, "join", 2);
        BuildLine(b.screen.body, "LobbyTitle", 65f, font, 58, Paper, TextAnchor.MiddleLeft).text = "You are at the table.";
        BuildLine(b.screen.body, "LobbyInfo", -35f, font, 29, Dim, TextAnchor.MiddleLeft).text = "Waiting for the host to start.";
        b.back = AddButton(b, "BtnJoinBack", "←  Leave table", -230f, "Leave", font, true);
        Finish(b);
        return b;
    }

    private static void Finish(ScreenBuild b)
    {
        var children = new MenuButton[b.buttons.Count];
        for (int i = 0; i < b.buttons.Count; i++)
        {
            children[i] = b.buttons[i].GetComponent<MenuButton>();
        }
        b.screen.children = children;
    }

    // ------------------------------------------------------------------ botoes

    /// <summary>
    /// Uma opcao do menu: um texto e um fio de acento embaixo.
    ///
    /// Nao ha numero de ordem e nao ha <c>Image</c> em lugar nenhum aqui. As
    /// opcoes do menu nao sao uma sequencia — sao destinos — e numera-las faria o
    /// menu parecer um formulario preenchivel. O que da ordem a leitura e o
    /// proprio peso do texto: a opcao principal e maior que as secundarias.
    ///
    /// O alvo de clique e o proprio texto, com a altura da linha inteira, entao o
    /// botao e clicavel do tamanho do texto sem nada desenhado atras.
    /// </summary>
    private static MenuButton AddButton(
        ScreenBuild b, string name, string label, float y,
        string action, Font font, bool secondary)
    {
        GameObject go = NewUI(name, b.screen.body);
        RectTransform rect = go.GetComponent<RectTransform>();
        AnchorLeft(rect, y, secondary ? 92f : 118f);

        Text text = BuildLine(rect, "Label", 0f, font, secondary ? 52 : 72,
            secondary ? Dim : Rest, TextAnchor.MiddleLeft);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = new Vector2(0f, 0.5f);
        textRect.anchorMax = new Vector2(0f, 0.5f);
        textRect.pivot = new Vector2(0f, 0.5f);
        textRect.sizeDelta = new Vector2(1000f, secondary ? 92f : 118f);
        textRect.anchoredPosition = Vector2.zero;
        // O texto e o alvo de toque. Sem isto o botao nao receberia nada.
        text.raycastTarget = true;
        text.text = label;

        // O fio nasce embaixo do texto e cresce para a direita quando o ponteiro
        // entra. A linha comeca na margem, alinhada com o texto, e nao recuada
        // para dentro dele: o alinhamento e o que segura a coluna.
        GameObject line = NewUI("Accent", rect);
        RectTransform lineRect = line.GetComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0f, 0f);
        lineRect.anchorMax = new Vector2(0f, 0f);
        lineRect.pivot = new Vector2(0f, 0.5f);
        lineRect.sizeDelta = new Vector2(secondary ? 16f : 22f, 2f);
        lineRect.anchoredPosition = new Vector2(0f, 3f);
        Image lineImage = line.AddComponent<Image>();
        lineImage.color = secondary ? new Color(1f, 1f, 1f, 0.45f) : new Color(1f, 1f, 1f, 0.7f);
        lineImage.raycastTarget = false;
        line.AddComponent<VRCUiShape>();

        Button button = go.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = text;

        MenuButton menuButton = go.AddComponent<MenuButton>();
        menuButton.router = _router;
        menuButton.action = action;
        menuButton.body = rect;
        menuButton.accent = lineRect;
        menuButton.group = go.AddComponent<CanvasGroup>();
        menuButton.label = text;
        menuButton.button = button;
        menuButton.startHidden = true;
        // Sem preenchimento, o movimento tem de ser discreto: um texto grande que
        // sobe 10 unidades parece sair do lugar, nao reagir.
        menuButton.lift = secondary ? 0f : 6f;
        menuButton.hoverDuration = 0.075f;
        menuButton.pressDuration = 0.04f;
        menuButton.hoverScale = 1.008f;
        menuButton.pressScale = 0.996f;
        menuButton.accentRest = secondary ? 16f : 22f;
        menuButton.accentFull = secondary ? 240f : 430f;
        menuButton.restColor = secondary ? Dim : Rest;
        menuButton.hoverColor = Paper;
        menuButton.blockedColor = Blocked;

        b.buttons.Add(go);
        return menuButton;
    }

    // ------------------------------------------------------------------ eventos

    private static void PairAll(GameObject root)
    {
        var targets = new List<GameObject> { root };
        targets.AddRange(ButtonTargets(root));
        foreach (GameObject target in targets)
        {
            if (target.GetComponents<MenSharpBehaviour>().Length == 0)
            {
                continue;
            }
            MenSharpProxy.SyncPairs(target, quiet: true, undoable: false);
        }
    }

    private static List<GameObject> ButtonTargets(GameObject root)
    {
        var targets = new List<GameObject>();
        foreach (MenuButton button in root.GetComponentsInChildren<MenuButton>(true))
        {
            targets.Add(button.gameObject);
        }
        foreach (MenuScreen screen in root.GetComponentsInChildren<MenuScreen>(true))
        {
            targets.Add(screen.gameObject);
        }
        foreach (MenuSettingRow row in root.GetComponentsInChildren<MenuSettingRow>(true))
        {
            targets.Add(row.gameObject);
        }
        return targets;
    }

    private static void Wire(ScreenBuild b)
    {
        var all = new List<GameObject>(b.buttons);
        all.AddRange(b.steppers);
        foreach (GameObject go in all)
        {
            // O UdonBehaviour paired e o alvo: e ele que executa o programa em
            // runtime, porque o componente de autoria e removido antes do build.
            UdonBehaviour udon = UdonFor(go);
            if (udon == null)
            {
                Debug.LogWarning("MenuVRBuilder: sem UdonBehaviour em " + go.name + "; clique nao ligado.");
                continue;
            }

            Button button = go.GetComponent<Button>();
            AddSend(new SerializedObject(button), "m_OnClick", udon, "Activate");

            // O EventTrigger cobre o ponteiro e o botao do controle de VR. Select e
            // Deselect sao os ids 9 e 10 do EventTriggerType, e nao 12 e 13 como
            // parece pela ordem dos eventos do ponteiro — errar aqui deixaria o
            // hover sem resposta nenhuma no headset.
            EventTrigger trigger = go.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = go.AddComponent<EventTrigger>();
            }
            var so = new SerializedObject(trigger);
            SetTrigger(so, udon, 0, "HoverEnter");
            SetTrigger(so, udon, 1, "HoverExit");
            SetTrigger(so, udon, 2, "PressDown");
            SetTrigger(so, udon, 3, "PressUp");
            SetTrigger(so, udon, 9, "HoverEnter");
            SetTrigger(so, udon, 10, "HoverExit");
            SetTrigger(so, udon, 15, "Activate");
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static UdonBehaviour UdonFor(GameObject go)
    {
        foreach (MenSharpBehaviour proxy in go.GetComponents<MenSharpBehaviour>())
        {
            UdonBehaviour paired = MenSharpProxy.FindPaired(proxy);
            if (paired != null)
            {
                return paired;
            }
        }
        return null;
    }

    private static void SetTrigger(SerializedObject so, UdonBehaviour udon, int eventId, string method)
    {
        SerializedProperty entries = so.FindProperty("m_Delegates");
        SerializedProperty entry = null;
        for (int i = 0; i < entries.arraySize; i++)
        {
            if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("eventID").intValue == eventId)
            {
                entry = entries.GetArrayElementAtIndex(i);
                break;
            }
        }
        if (entry == null)
        {
            entries.arraySize = entries.arraySize + 1;
            entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            entry.FindPropertyRelative("eventID").intValue = eventId;
        }
        WriteCall(entry.FindPropertyRelative("callback"), udon, method);
    }

    /// <summary>
    /// Escreve um <c>SendCustomEvent("Nome")</c> no UnityEvent serializado.
    ///
    /// Vai pelo <see cref="SerializedObject"/> e nao pelo
    /// <c>UnityEventTools.AddPersistentListener</c> porque aquele usa o objeto
    /// ativo do editor como alvo, e aqui o alvo e um UdonBehaviour especifico de
    /// cada botao. O indice 5 do modo e <c>String</c>, o modo que leva o nome do
    /// evento no <c>m_StringArgument</c>.
    /// </summary>
    private static void AddSend(SerializedObject so, string property, UdonBehaviour udon, string method)
    {
        // so.FindProperty("m_OnClick") e o UnityEvent. E WriteCall que desce ate
        // a lista de chamadas: passar direto o elemento da lista aqui faria a
        // funcao procurar "m_PersistentCalls" dentro de uma chamada, e nao ha
        // nada com esse nome embaixo dela.
        WriteCall(so.FindProperty(property), udon, method);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WriteCall(SerializedProperty unityEvent, UdonBehaviour udon, string method)
    {
        // No Unity 2022 a lista de chamadas fica dois niveis abaixo do evento:
        // m_OnClick -> m_PersistentCalls -> m_Calls. Pegar so o
        // m_PersistentCalls devolve um Generic, e setar arraySize nele falha com
        // "Invalid property to resize array". A descida e feita por busca em
        // vez de por nome fixo, porque o EventTrigger embrulha a mesma estrutura
        // em um TriggerEvent.
        SerializedProperty calls = unityEvent.FindPropertyRelative("m_PersistentCalls");
        if (calls != null && !calls.isArray && calls.FindPropertyRelative("m_Calls") != null)
        {
            calls = calls.FindPropertyRelative("m_Calls");
        }
        if (calls == null || !calls.isArray)
        {
            Debug.LogWarning("MenuVRBuilder: nenhum evento serializado em " + unityEvent.propertyPath + "; clique nao ligado.");
            return;
        }
        calls.arraySize = 0;
        calls.arraySize = 1;
        SerializedProperty call = calls.GetArrayElementAtIndex(0);
        call.FindPropertyRelative("m_Target").objectReferenceValue = udon;
        call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = "VRC.Udon.UdonBehaviour, VRC.Udon";
        call.FindPropertyRelative("m_MethodName").stringValue = "SendCustomEvent";
        call.FindPropertyRelative("m_Mode").enumValueIndex = 5;
        SerializedProperty args = call.FindPropertyRelative("m_Arguments");
        args.FindPropertyRelative("m_ObjectArgument").objectReferenceValue = null;
        args.FindPropertyRelative("m_IntArgument").intValue = 0;
        args.FindPropertyRelative("m_FloatArgument").floatValue = 0f;
        args.FindPropertyRelative("m_StringArgument").stringValue = method;
        args.FindPropertyRelative("m_BoolArgument").boolValue = false;
        call.FindPropertyRelative("m_CallState").enumValueIndex = 2;
    }

    // ------------------------------------------------------------------ util

    private static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");
        return go;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
    }

    /// <summary>Ancora a esquerda com a margem da coluna, que e a coluna do menu.</summary>
    private static void AnchorLeft(RectTransform rect, float y, float height)
    {
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(Column, height);
        rect.anchoredPosition = new Vector2(Margin, y);
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, y);
    }
}


