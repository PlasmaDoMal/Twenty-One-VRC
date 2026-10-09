# Bugs e armadilhas — menus Hit/Stay

Registro do que quebrou durante a montagem dos menus de escolha por jogador
(`Player1_Choices`, `Player2_Choices`) na cena `Assets/_Project/Scenes/GamePrototype.unity`.
Escrito em 01/10/2026.

Leia antes de mexer nesses scripts. Quase tudo aqui custou um ciclo de
Play Mode inteiro para achar, e vários não aparecem no console: só aparecem se
você olhar o arquivo compilado.

---

## 1. No MenSharp, campo privado vira estático

**Sintoma:** os quatro botões da mesa compartilhavam estado. O `busy` de um
`ChoiceButton` travava o botão do outro jogador no primeiro clique; o `hovered`
de um `HoverText` deixava o texto amarelo dos dois lados.

**Causa:** campo privado em `MenSharpBehaviour` é compilado como estático. Só
o `PlayerSlot` avisava isso, num comentário — e eu não li.

**Regra:** todo campo de instância é `public` e normalmente `[HideInInspector]`.

```csharp
// Errado: vira estatico, compartilhado pelos quatro botoes
private bool busy;

// Certo
[HideInInspector] public bool busy;
```

Atinge `FadeRise.running`, `HoverText.hovered`, `ChoiceButton.busy`,
`TurnFadeIn.lastWasActionable`, `CardDealer.turnEventActionable`.

---

## 2. Udon só exporta como evento método sem parâmetro

**Este foi o bug mais caro.** Levou várias rodadas porque **não aparece no
console**.

**Sintoma:** o `CardDealer` avisava a vez no log, os menus existiam, mas nenhum
botão reagia. Nenhum erro.

**Causa:** `TurnFadeIn.OnTurnChanged(bool actionable, int turnPlayer)` tem
parâmetros. O Udon só expõe como evento métodos **sem argumentos**, então o
método **não existia no programa compilado** e a chamada do `CardDealer` não
tinha para onde ir.

**Como achei:** contando ocorrências de string no artefato compilado.

```powershell
$f = "Assets\Scripts\Programs\CardDealer.asset"
$txt = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($f))
([regex]::Matches($txt, "OnTurnChanged")).Count   # 1 no asset, 0 no .uasm
```

**Regra:** comunicação entre behaviours passa por **campos + método sem
parâmetro**. Nunca por método com argumento.

```csharp
// Errado: nao e exportado, a chamada nao existe
listener.OnTurnChanged(actionable, player);

// Certo
listener.incomingActionable = actionable;
listener.incomingTurnPlayer = player;
listener.ApplyTurn();          // sem parametros
```

Vale para qualquer behaviour Menu <-> CardDealer.

---

## 3. `EventTrigger` criado por script não serializa

**Sintoma:** hover não funcionava. O `EventTrigger` estava no objeto com
**0 listeners**.

**Causa:** adicionei os listeners com `UnityEventTools.AddPersistentListener`
passando um lambda. O lambda não sobrevive à serialização da cena.

**Regra:** o listener precisa ser persistente e apontar para o
`UdonBehaviour.SendCustomEvent` com o nome do evento gravado como argumento.
Lambda e alvo C# do MenSharp deixam de funcionar no Play Mode.

---

## 4. Hover precisa chegar ao UdonBehaviour compilado

**Sintoma:** hover e clique não chegavam nos botões.

**Causa:** o `EventSystem` da cena usa `StandaloneInputModule` (input antigo,
mouse/teclado). No ClientSim quem aponta é o cursor dele; no VRChat é o laser do
controle. Nenhum dos dois passa pelo módulo antigo.

**Problema:** o `ClientSim` gerencia esse objeto em runtime. Ver item 10.

**Correção verificada no ClientSim:** em Play Mode o componente C# do
`HoverText` é substituído por `UdonBehaviour`; o listener de interface não
chegava ao programa Udon. Cada `HitArea` agora tem um `EventTrigger`
persistente: `PointerEnter` chama `SendCustomEvent("HoverOn")` e
`PointerExit` chama `SendCustomEvent("HoverExit")` no UdonBehaviour do próprio
`HoverText`. O mesmo princípio vale para `Button.onClick`: ele chama
`SendCustomEvent("ChooseHit")` ou `SendCustomEvent("ChooseStay")` no
UdonBehaviour do `ChoiceButton`. A entrega pelo laser em VR ainda requer teste
no cliente real.

---

## 5. HoverText precisa ficar no HitArea, não no botão pai

**Sintoma:** mesmo com as interfaces implementadas, o raycast não batia.

**Causa:** o `HoverText` estava no GameObject do botão, que é a raiz do Canvas
World Space. O raio atravessa o painel inteiro em vez de acertar a área
cliquável.

**Regra:** o componente de ponteiro fica no **`HitArea`** (o retângulo
invisível com `raycastTarget = true`), não no pai do canvas.

---

## 6. `hovered` preso em true deixava os dois botões amarelos

**Sintoma:** os dois textos abriam amarelos ao mesmo tempo, e nenhum hover
funcionava.

**Causa:** `hovered` é campo de instância que **vira estático** (item 1), e o
`Start()` não reiniciava o estado de repouso. Um texto marcado de uma sessão
anterior abria a partida no estado de hover.

**Regra:** o `Start()` sempre aplica o estado de repouso, nunca o de hover.

```csharp
public void Start()
{
    CacheRest();
    hovered = false;
    running = false;
    ApplyRest();
}
```

---

## 7. Animação deve mexer só no eixo Y, nunca na posição inteira

**Sintoma:** depois de animado, os dois botões voltavam para `X = 0` e ficavam
**sobrepostos** no mesmo ponto.

**Causa:** o `FadeRise` guardava `restPosition` (Vector3) e interpolava a
posição inteira. Se o botão fosse movido de lado no Inspector depois do `Start`,
a animação devolvia o X a zero.

**Regra:** guardar **só a altura** e deixar `X` e `Z` sempre virem do Transform.

```csharp
[HideInInspector] public float restY;

// Drive: interpola so o Y, copia X e Z do estado atual a cada quadro
transform.localPosition = new Vector3(
    transform.localPosition.x,
    Mathf.LerpUnclamped(fromY, toY, e),
    transform.localPosition.z);
```

---

## 8. Ordem dos `Start()` entre componentes é indeterminada

**Sintoma:** o `Stay` ficava escondido em `Y = -0.06` e o `Hit` em `Y = 0`, com
os dois em alpha 0. O `Stay` subia de baixo de mais baixo.

**Causa:** o `TurnFadeIn.HideUntilTurn()` espera 1 frame e chama `HideNow()`. O
`FadeRise.Start()` lê `restY` da altura atual. Se o `HideNow` rodasse antes do
`Start` daquele botão, o `Start` leria a altura já deslocada como se fosse a de
repouso. O `Hit` via `0`, o `Stay` via `-0.06` — a ordem muda conforme o
componente, e cada botão recebia uma resposta diferente.

**Regra:** não derive estado de reposição da posição atual em `Start()`. Derive
no momento da ação, com um método explícito.

```csharp
public void HideNow()
{
    running = false;
    RefreshRestY();          // calcula a partir de ONDE ESTA agora
    transform.localPosition = new Vector3(
        transform.localPosition.x, restY - offsetY, transform.localPosition.z);
    SetAlpha(0f);
    hidden = true;
}
```

Assim o resultado é o mesmo qualquer que seja a ordem em que as peças acordem.

---

## 9. `startHidden` fica preso na cena e sequestra o botão

**Sintoma:** o `Stay` **nunca** aparecia, nem quando a vez chegava. O `Hit`
aparecia normalmente.

**Causa:** `FadeRise.startHidden = true` tinha ficado gravado no asset de um
trabalho anterior (quando a ideia era esconder só o `Stay`). Com o campo ligado,
o `Start()` chamava `HideNow()` toda sessão, e nada no fluxo da vez o
desligava.

**Regra:** `startHidden` é para botão que **nunca** aparece automaticamente.
Quem esconde por condição de jogo é o `TurnFadeIn`, via `hiddenUntilTurn`.
Misturar os dois mecanismos sequestra o botão.

```csharp
// FadeRise: quem esconde por game state e o TurnFadeIn
fr.startHidden = false;
tf.hiddenUntilTurn = new FadeRise[] { hit, stay };  // os dois
```

---

## 10. Não mexer no `EventSystem` — o ClientSim gerencia o objeto

**Sintoma:** troquei `StandaloneInputModule` por `ClientSimInputModule` e ativei
o `EventSystem` para "consertar" o input. **Quebrei o Close Menu do VRChat.** O
ClientSim cria e gerencia esse objeto em runtime, e ao sair do Play Mode ele
reverte a cena para o estado anterior — o que desfaz a alteração na hora.

**Regra:** o `EventSystem` da cena fica como está. Para diagnosticar input,
trabalhar no `HoverText` (item 4), não no `EventSystem`.

---

## 11. `CanvasGroup` com `interactable`/`blocksRaycasts` falsos mata os cliques

**Sintoma:** o input inteiro do menu parou de funcionar depois de uma "correção"
que ajustou o `CanvasGroup` dos menus.

**Causa:** deixei `interactable = false` e `blocksRaycasts = false` no
`CanvasGroup` do menu inteiro, achando que era o botão invisível.

**Regra:** os dois menus ficam com `interactable = true` e
`blocksRaycasts = true`. Quem controla opacidade é o `OwnerOnlyUIVisibility`, e
só o `CanvasGroup` de cada **botão** é mexido pelo `FadeRise`.

```csharp
// Errado — derruba o input do menu inteiro
menuGroup.blocksRaycasts = false;

// Certo
menuGroup.interactable = true;
menuGroup.blocksRaycasts = true;
// opacidade só no grupo de cada botao, pelo FadeRise
```

---

## 12. Em Play Mode os scripts MenSharp somem do reflection

**Sintoma:** `FindObjectOfType<CardDealer>()` devolve `null` dentro do Play
Mode. Os componentes viram `VRC.Udon.UdonBehaviour` + `ClientSimUdonHelper`.

**Consequência:** em Play Mode **não dá para ler campos dos behaviours por
reflection**. Só dá para chamar `GetProgramVariable` no `UdonBehaviour` (e
`VRCPlayerApi` precisa de namespace explícito: `VRC.SDKBase.VRCPlayerApi`).

**Regra:** diagnóstico em Play Mode é pelo console. Para ler valores de campo,
sair do Play Mode.

---

## 13. `notify` tem que estar antes do early return de dedup

**Sintoma:** o log nunca aparecia, e eu não sabia se o método estava sendo
chamado.

**Causa:** o `Debug.Log` de diagnóstico estava **depois** do
`if (estado == anterior) return;`. O estado inicial já batia, então saía antes
de logar.

**Regra:** o log de portão vai **antes** do return. É o único registro de que o
método foi avaliado; depois do return, estado inalterado não deixa rastro, e não
dá para distinguir "avaliado e já estava certo" de "nunca chegou a avaliar".

---

## 14. `MatchCanStart` não logava em nenhum caminho de sucesso

**Sintoma:** partida não começava e o console não dizia nada.

**Causa:** todos os caminhos de falha logavam, mas o `return true` final era
mudo. Sem esse log, "recusado" e "nem tentou" eram indistinguíveis.

**Regra:** todo portão tem log no caminho de **sucesso** também.

---

## 15. `IsMatchStarter` tinha deadlock antes da primeira partida

**Sintoma:** a partida nunca começava, mesmo com o pedido indo pelo Slot certo.

**Causa:** `IsMatchStarter()` testava `slots[0].IsMine()`. Mas os Slots
pertencem ao master até `AssignSlots()` rodar — e `AssignSlots()` só roda dentro
de `StartMatch()`. Então: o Slot 0 é do master, o master não passa no teste
porque precisa ser dono de si mesmo no Slot, e a partida nunca começava.
Ninguém nunca seria.

**Regra:** antes da primeira jogada, quem pode iniciar é o **dono do baralho**,
não o dono do Slot 0. O dono do baralho é sempre alguém.

```csharp
if (!matchStarted)
{
    return IsDeckOwner();
}
return local.playerId == matchStarterPlayerId;
```

---

## 16. `StartRound()` precisa emitir a virada de vez

**Sintoma:** mesmo com o portão certo, os botões não apareciam no começo da
rodada.

**Causa:** `NotifyTurnChanged()` estava só no `DrainQueue` (fim do voo das
cartas) e no `AdvanceTurn`. Faltava `StartRound()`, que é onde a rodada abre e a
vez é definida.

**Regra:** a virada de vez em `NotifyTurnChanged()` é chamada em **todo** ponto
onde o estado pode mudar: `AdvanceTurn`, `DrainQueue` (no `finally`),
`OnDeserialization`, `EndMatch`, `StartRound` e `Start`.

---

## 17. ClientSim tem um só input real

**Sintoma:** clicar no botão do Player 2 age como o Player 1. A jogada vai
sempre para o Slot do jogador local.

**Causa:** não é bug. O "Remote Player" do ClientSim existe para você **ver** os
objetos dele, mas o input é seu. `ChoiceButton.ChooseHit()` chama
`dealer.RequestHit()`, que resolve `MySlotIndex()` — sempre o Slot do jogador
local.

**Regra:** com um input só, o fluxo de dois jogadores não é testável pelo
ClientSim para o segundo jogador. O que ele valida bem: o `OwnerOnlyUIVisibility`
esconde o menu do rival, o spread do estado pela rede e a reconstrução da mesa.

---

## 18. `GameObject.Find` não acha objeto desativado

**Sintoma:** `Find("EventSystem")` devolveu `null` e eu concluí que o objeto
tinha sido destruído. Ele estava lá, desativado.

**Regra:** para objeto desativado, usar
`Resources.FindObjectsOfTypeAll<T>()`. O `Find` só acha ativos.

---

## 19. `HideInInspector` não impede o dado de ser gravado

**Sintoma:** o `restY = 0` gravado na cena sugeria que o `FadeRise` estava com a
altura errada, quando o valor estava certo e o problema era outro.

**Causa:** campo `[HideInInspector]` continua serializado e é restaurado ao
recarregar a cena. Esconder no Inspector não impede o dado de existir no asset.

---

## 20. Estado escondido não deve ficar gravado na cena

**Sintoma:** `offsetY` dando `-0.12` em vez de `-0.06`, quando o valor
configurado era `0.06`.

**Causa:** eu tinha gravado `Y = -0.06` na cena como estado "escondido". O
`Start()` lia `-0.06` como se fosse a posição de repouso, e escondia mais
`-0.06` por cima.

**Regra:** a cena guarda o botão na **posição de repouso**, com alpha 1. Quem
aplica o estado escondido é o `HideNow()` em runtime.

---

## Checklist para mexer nesses scripts

- [ ] Campo de instância novo é `public` + `[HideInInspector]` (item 1)
- [ ] Nenhum método público novo **com parâmetros** que outro behaviour vá chamar (item 2)
- [x] `EventTrigger` e `Button.onClick` apontam para eventos persistentes do UdonBehaviour (itens 3 e 4)
- [ ] Componente de ponteiro no `HitArea`, não no pai do canvas (item 5)
- [ ] `Start()` aplica repouso, nunca hover (item 6)
- [ ] Animação mexe só em Y; X e Z vêm do Transform (item 7)
- [ ] Estado de repouso derivado no momento da ação, não no `Start()` (item 8)
- [ ] `startHidden` desligado quando quem esconde é o `TurnFadeIn` (item 9)
- [ ] `EventSystem` intocado (item 10)
- [ ] `CanvasGroup` do menu com `interactable` e `blocksRaycasts` verdadeiros (item 11)
- [ ] Todo portão tem log no caminho de sucesso (itens 13 e 14)
- [ ] Toda virada de estado emite o evento (item 16)
- [ ] Diagnóstico em Play Mode é pelo console, não por reflection (item 12)

## Ainda para checar

- [x] Hover testado no ClientSim pela invocação do callback persistente: o
      `hovered` do Udon mudou e o texto ficou amarelo; a saída restaurou branco.
      Falta verificar o apontamento físico do cursor e do laser.
- [x] Clique testado no Play Mode: `Button.onClick` manteve o alvo Udon válido
      e a chamada de `ChooseHit` alterou `ChoiceButton.busy` para `true`.
- [x] `ApplyTurn()` testado no programa Udon em Play Mode: os dois botões
      apareceram, ficaram interativos, e voltaram à mesma altura escondida.
- [ ] Testar os dois jogadores com duas instâncias reais do VRChat. O ClientSim
      não dá para o segundo jogador (item 17).
- [x] `FadeRise` testado em um ciclo de entrada/saída no Play Mode. O
      `CanvasGroup` do botão bloqueia raycasts somente quando está visível.
- [x] `HoverText` cancela a animação anterior quando o ponteiro muda de estado;
      uma entrada e saída rápida terminou em branco e no tamanho de repouso.
