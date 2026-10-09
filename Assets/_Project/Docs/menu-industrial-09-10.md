# Twenty One — terminal industrial

## 1. Conceito
Um terminal de equipamento industrial abandonado, instalado antes da mesa.
O título parece uma identificação antiga, impressa diretamente na chapa.
Marcas de vida e uma lâmina vermelha apresentam a aposta como ameaça física.
Ações são inscrições abertas, sem cards, caixas ou decoração de cassino.
O espaço vazio e a composição desigual deixam a outra cadeira parecer uma ausência.

Escopo aplicado: MenuSystem sob UI, com principal, lobby do host e lobby do convidado; cena VRCDefaultWorldScene e prefab MenuSystem. Inglês preservado. O desenho das demais telas abaixo orienta a continuidade; não substitui os tarots, materiais das cartas nem a lógica atual de gameplay.

## 2. Paleta, fontes e textura

| Uso | HEX |
|---|---|
| Chapa/fundo | #11100E |
| Texto principal | #E2DDD1 |
| Texto secundário | #9B9385 |
| Ação/perigo | #8E3730 |

Display: Special Elite; leitura: Oswald. As fontes estão em Assets/_Project/UI/MenuVR/Fonts com assets TextMeshPro. Special Elite e sua licença foram obtidas do repositório Google Fonts; Oswald já existia no projeto.

IndustrialMetal.png: textura procedural de 512×512, ruído determinístico de baixa amplitude, vinheta incorporada e riscos espaçados. Uma única imagem opaca compõe o fundo, evitando várias transparências sobrepostas. Linhas de desgaste, marcas de vida e lâmina usam Images simples compartilhando o material de UI. Não há pós-processamento nem ruído calculado por pixel a cada frame.

## 3. Hierarquia das telas

Coordenadas relativas ao centro do Canvas 2400×1500, World Space; escala atual ~0,001109, dimensão física ~2,66×1,66 m. Priorizar leitura a 1–2 m; títulos 170 px, ações 40–72 px, informações 30–38 px. Nenhuma transformação do mapa foi alterada.

- Principal implementado: título x=-645/y=290, largura 850; ações em x=510/y=230 e x=620/y=10, com larguras diferentes. Estados dos lugares no topo direito. Vida/aposta inicial na faixa inferior esquerda. Rodapé em y=-655.
- Host implementado: identificação do lugar em x=510/y=265; início em y=-15; solo em x=710/y=-200; sair em x=220/y=-320. As ações existentes continuam vinculadas ao WorldMatchSession. Lugar 01 corresponde ao host e 02 ao convidado; não foi criado um sistema novo de escolha ou de prontidão.
- Convidado implementado: identificação 02, espera pelo host e ação de sair. Estado de ocupação lido localmente do estado sincronizado da sessão.
- HUD proposto: vida em lados opostos, aposta central maior, alvo acima da mesa, soma visível junto à própria mão, turno/timer em faixa lateral. Nunca somar cartas ocultas do oponente. TwentyOneHUDView é um controlador de exemplo compilado; lê o snapshot do MatchHUD e não está aplicado como substituição do HUD existente.
- Mão de trumps: preservar os pickups e tooltips atuais. Distribuição linear irregular de até oito cartas, fora da mesa; usar uma única superfície para descrição, em vez de painel por carta.
- Trumps ativas: linha ordenada ao lado da mesa, diferenciando efeitos ativos do histórico de cartas usadas. A mais recente recebe um traço vermelho, sem alterar a regra de Destroy/Reincarnation.
- Resultado: pausa visual de 0,6 s, indicação de perdedor/dano e aposta seguinte. Vitória/derrota: uma inscrição grande, orientada ao jogador local, mantendo a carta oculta privada até a resolução. São orientações de integração futura, não alterações realizadas nessas telas neste pedido.

## 4. Movimento

| Elemento | Inicial | Final | Duração | Easing | Delay |
|---|---|---|---|---|---|
| Entrada do painel | alpha 0; scale 0,92; y -14 | alpha 1; scale 1; y 0 | 0,45 s | easeOutCubic | 0 |
| Identificação/título/termos | alpha 0; scale 0,92; y -14 | alpha 1; scale 1; repouso | 0,45 s | easeOutCubic | 0,08 s por elemento |
| Saída reutilizável | alpha atual; repouso | alpha 0; scale 0,92; y -14 | 0,45 s | easeInCubic | 0,08 s por elemento |
| Troca de tela | alpha 0; x -12 | alpha 1; x 0 | 0,22 s | easeOutCubic | 0,08 s por ação |
| Hover | cor de repouso; y 0 | branco; y +2; traço ampliado | 0,12 s | easeInOut | 0 |
| Título | alpha 1 | 0,78 e retorno | 0,06 s | corte discreto | intervalo aleatório 5–11 s |
| Aviso de aposta | alpha 0,84 | 1 e retorno | ~9 s | seno suave | 0 |
| Resultado do exemplo | alpha 0 | alpha 1 | 0,25 s | linear | pausa 0,6 s |
| Lâmina do exemplo | posição anterior | posição da aposta | imediato + tremor 0,18 s | seno de amplitude 2 px | ao mudar aposta |

Reduzir movimento é local: elimina deslocamento, escala, flicker, respiração e stagger novos; conserva fade curto de 0,1 s. O controle não altera regras nem sincronização. As transições legadas MenSharp continuam presentes; os scripts novos de apresentação usam Update e timers, sem async/coroutines/LINQ.

Cartas de gameplay mantêm suas animações atuais de distribuição e retorno. Flip/arco/descarte descritos no briefing não foram reimplementados neste pedido de MenuSystem.

## 5. Objetos

```text
UI
└─ MenuSystem [Canvas World Space, VRCUiShape]
   ├─ IndustrialMenuPresentation / UdonBehaviour
   ├─ TwentyOneUIAnimator / UdonBehaviour
   ├─ Backdrop [IndustrialMetal]
   └─ Content [MenuRouter, MatchLobby, CanvasGroup]
      ├─ Eyebrow / Title / Subtitle [Text fonte de dados + TMPVisual]
      ├─ SeatOneStatus / SeatTwoStatus
      ├─ WagerCaption / LifeReadout / TallyReadout
      ├─ LifeMark0..2 / TallyBlade / regras de composição
      ├─ BtnReducedMotion / MotionLabel
      ├─ Screens
      │  ├─ Screen_main / BtnCreate / BtnJoin
      │  ├─ Screen_create / BtnStart / BtnSoloTest / BtnCreateBack
      │  └─ Screen_join / BtnJoinBack
      └─ StatusText / HintText / Footer
```

Os Text legados ficam desativados visualmente, preservando as referências funcionais do roteador. A apresentação copia texto/cor para TMP a 10 Hz, somente quando necessário. Textos TMP não interceptam raycasts; as superfícies dos botões recebem o pointer. Scripts novos com sync None: preferências e animações permanecem locais.

## 6. Código funcional

Fontes completas: Assets/_Project/UI/MenuVR/UI/TwentyOneUIAnimator.cs, IndustrialMenuPresentation.cs e TwentyOneHUDView.cs. MatchHUD publica hudLifeOne/hudLifeTwo/hudBet/hudTarget/hudTurn/hudSeconds/hudWinner/hudResolving/hudMatchOver para o exemplo, usando os métodos públicos do dealer. A UI não decide vencedor nem dano e não escreve no dealer.

## 7. Assets

- SpecialElite-Regular.ttf e licença: display; atlas SDF de 1024 px, caracteres básicos pré-carregados.
- Oswald SDF: leitura, atlas de 1024 px, fonte já existente reutilizada.
- IndustrialMetal.png: chapa, grain/vinheta/riscos embutidos; gerado proceduralmente, 512 px comprimido.
- Images sem sprite: linhas retas, três marcas de vida e lâmina; sem novos materiais por elemento.
- Ícones de categoria propostos para as cartas: número = dois traços paralelos; regra = mira de quatro segmentos; aposta = lâmina; controle = cruz riscada; mão = três retângulos abertos. Construir em SVG monocromático e rasterizar num atlas único. Esses ícones não foram aplicados sobre as texturas de trump existentes neste pedido.

Capturas locais: Captures/menu-industrial-main.png, menu-industrial-create.png e menu-industrial-join.png. Não houve bake, publicação ou alteração de regras de partida.

Validação: compilações C#, UdonSharp e MenSharp concluídas. No ClientSim, callbacks reais de criar e sair mudaram o lobby para create/main e hostId para 1/-1. O controle de movimento mudou a preferência local, o animator e a legenda para MOTION / REDUCED. Ocupação atualizou para 01 / OCCUPIED. Console limpo antes desses cliques permaneceu sem erros. Na inicialização reapareceu o erro ownerPlayer de TarotVisuals já registrado nas pendências; ele não está declarado resolvido por esta alteração de menu. Pointer em headset e dois clientes reais ainda precisam de teste. Layout aplicado diretamente através do MCP; não existe builder novo para reconstruí-lo.

### TwentyOneUIAnimator.cs

```csharp
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

```

### TwentyOneHUDView.cs

```csharp
using UdonSharp;
using TMPro;
using UnityEngine;
using VRC.Udon;
using VRC.SDKBase;

// Read-only example view: bind snapshot to the existing MatchHUD program.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TwentyOneHUDView : UdonSharpBehaviour
{
    public UdonBehaviour snapshot;
    public TMP_Text lifeOne, lifeTwo, bet, target, turn, result;
    public RectTransform blade;
    public CanvasGroup resultGroup;
    public bool reduceMotion;
    private float nextPoll;
    private float revealAt;
    private bool wasResolving;
    private int lastBet = -1;
    private float shakeUntil;
    private Vector3 bladeRest;
    private void Start() { if (blade != null) bladeRest = blade.anchoredPosition3D; }
    private void Update()
    {
        if (snapshot == null) return;
        float now = Time.unscaledTime;
        if (now >= nextPoll)
        {
            nextPoll = now + 0.1f;
            int a = (int)snapshot.GetProgramVariable("hudLifeOne");
            int b = (int)snapshot.GetProgramVariable("hudLifeTwo");
            int stake = (int)snapshot.GetProgramVariable("hudBet");
            int goal = (int)snapshot.GetProgramVariable("hudTarget");
            int player = (int)snapshot.GetProgramVariable("hudTurn");
            int seconds = (int)snapshot.GetProgramVariable("hudSeconds");
            bool resolving = (bool)snapshot.GetProgramVariable("hudResolving");
            bool over = (bool)snapshot.GetProgramVariable("hudMatchOver");
            int winner = (int)snapshot.GetProgramVariable("hudWinner");
            if (lifeOne != null) lifeOne.text = "01 / LIFE " + a;
            if (lifeTwo != null) lifeTwo.text = "02 / LIFE " + b;
            if (bet != null) bet.text = "TALLY " + stake;
            if (target != null) target.text = "TARGET " + goal;
            if (turn != null) turn.text = "SEAT " + (player + 1) + " / " + seconds + "s";
            if (stake != lastBet) { if (lastBet >= 0) shakeUntil = now + 0.18f; lastBet = stake; }
            if ((resolving || over) && !wasResolving) revealAt = now + (reduceMotion ? 0f : 0.6f);
            wasResolving = resolving || over;
            if (result != null) result.text = winner < 0 ? "NO BLOOD. DRAW." : over ? "SEAT " + (winner + 1) + " SURVIVES." : "SEAT " + (2 - winner) + " / DAMAGE " + stake;
        }
        if (resultGroup != null) resultGroup.alpha = !wasResolving || now < revealAt ? 0f : Mathf.Clamp01((now - revealAt) / 0.25f);
        if (blade != null)
        {
            Vector3 p = bladeRest + new Vector3(Mathf.Clamp(lastBet, 0, 3) * 80f, 0f, 0f);
            if (!reduceMotion && now < shakeUntil) p.x += Mathf.Sin(now * 95f) * 2f;
            blade.anchoredPosition3D = p;
        }
    }
}

```

## Preferência visual posterior
MenuSystem convertido para preto, branco e cinza neutro. Removidos os acentos vermelhos e o tom quente do fundo. Todos os textos visuais usam Oswald; título e números não usam mais Special Elite. Textura procedural monocromática: IndustrialMetal-Monochrome.png. Cena e prefab atualizados via MCP; animações e callbacks preservados. Captura atual: Captures/menu-monochrome-main.png. Esta preferência substitui a paleta e a dupla tipográfica descritas acima.

