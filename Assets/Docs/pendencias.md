# Estado do protótipo Twenty One

Conferido com `Assets/Docs/twenty-one-trump-cards.md` e com a cena
`Assets/Scenes/Game.unity` em 30/09/2026. `mensharp.md` descreve o
compilador usado pelos comportamentos de Udon.

## Implementado e verificado no ClientSim

- A partida espera dois jogadores e um pedido de início do dono do Slot 0.
  `PlayerSlot` autentica as ações, e somente o dono do baralho altera o estado.
- Cada rodada começa com seis cartas numéricas: uma primeira carta de face
  oculta e duas cartas abertas por jogador. Os placeholders visuais estão
  desativados na cena.
- O baralho tem uma única carta de cada valor de 1 a 11. A semente e o histórico
  das compras são sincronizados para reconstruir as mãos nos clientes.
- Os ScoreCubes mostram a soma da mão (`total/alvo`). O texto do adversário é
  escondido localmente por `OwnerOnlyVisibility`.
- A altura das cartas é controlada por `cardWorldY` (padrão 1,0825 m) e
  `layerThickness` (padrão 0), sem consultar o Card da cena em runtime.
- Hit passa o turno e zera a sequência de passadas. Duas passadas seguidas
  comparam as somas; quem estoura perde, e se os dois estourarem perde quem
  tiver a soma maior. Empate não causa dano.
- A vida começa em `startingLife`. A aposta base começa em `roundDamage`, sobe
  por `roundDamageGrowth` e pode ser alterada pelas trumps da mesa. Bless pode
  salvar o perdedor e reduzir a aposta base seguinte. A partida termina por
  vida zerada ou por `maxRounds`.
- `turnTimeoutSeconds` cria um gancho lógico, que prevalece sobre a soma e pode
  ser removido por Remove ou Exchange. Fica em zero na cena até existir o visual.
  Quando habilitado, a contagem começa após a distribuição animada.
- Limpeza de rodada interrompe animações antigas. Deserialização reconstrói
  a mesa se o histórico for alterado, zerado ou a semente mudar.
- As 25 trumps descritas em `twenty-one-trump-cards.md` têm IDs, mão própria,
  compra, uso e efeitos em `CardDealer.cs`. Há duas por jogador no início da
  rodada. A chance de ganhar outra após Hit é configurável; Disservice não a
  concede. Trumps colocadas na mesa ficam sincronizadas até serem destruídas
  ou até a rodada acabar. Testes no ClientSim cobriram distribuição, Go For,
  Destroy, Remove, Hush, Exchange, Refresh e uma carta numérica específica.

## Ainda necessário para jogar sem chamadas de teste

- Controles para iniciar, comprar e passar, indicador de turno, vida, rodada e
  vencedor. O menu está fora do escopo atual, mas a interface durante a
  partida ainda precisa ser ligada aos métodos `Request*`.
- **Visual das tarot cards:** criar um modelo/prefab único, com uma textura ou
  material para cada um dos 25 tipos, como foi feito para as cartas numéricas.
  Posicionar as cartas da mão e as trumps contínuas na mesa, e ligar esses
  objetos aos arrays sincronizados `trumpType`, `trumpOwner`, `tableTrumpType`
  e `tableTrumpOwner`. O código de regras não instancia trumps visuais ainda.
- Controles da mão de trumps: selecionar uma carta e chamar
  `RequestUseTrump(indiceNaMinhaMao)`. `TrumpCountInHand`, `TrumpAt` e
  `TrumpName` fornecem os dados sem depender do visual.
### Timer de turno: 1 minuto como padrão, configurável

O `CardDealer.cs` já tem o gancho lógico do timeout, mas ele está desligado
(`turnTimeoutSeconds = 0` na cena) e ainda não há nada que o jogador veja
contando. Falta fechar isto em três partes.

**1. Timer no `CardDealer`, com valor padrão de 1 minuto**

- Mudar o padrão do campo `turnTimeoutSeconds` de `0f` para `60f`
  (`Assets/Scripts/CardDealer.cs:161`). Continua sendo variável pública e
  editável no inspetor, então dá para testar com 5 ou 10 segundos sem
  compilar.
- `0f` continua significando "sem timeout". Manter esse contrato para não
  quebrar o modo de teste.
- A contagem já respeita o momento certo: só começa depois da distribuição
  animada (`openingDealt && !dealing && pendingFly.Count == 0`,
  `Assets/Scripts/CardDealer.cs:1740`) e é reiniciada por
  `ResetTurnDeadline()` a cada virada de vez
  (`Assets/Scripts/CardDealer.cs:1493`).
- **Mudança de regra pedida:** hoje o estouro do tempo dá um *gancho*
  (`hookMask |= 1 << turnIndex`, linha 1753), que faz o jogador perder a
  rodada mas ainda depende da comparação final de somas. O pedido atual é
  **perder a rodada automaticamente**, sem passar pelo gancho. Escolher uma
  das duas saídas:
  - dar o gancho e chamar a mesma rotina de fim de rodada que já existe
    (`hookMask == 3` já encerra a partida com `-1`, linha 1755), ou
  - manter o gancho como mecânica separada e adicionar um caminho de timeout
    que chame direto a resolução da rodada.
  Enquanto isso não for decidido, o timeout segue sendo só um gancho.
- A derrota por tempo precisa de um sinal para a interface: um evento/flag
  sincronizado para o HUD e para as TVs anunciarem *quem* perdeu por tempo.
- `Remove` e `Exchange` ainda limpam `hookMask` (linhas 1461-1468). Definir se
  esse cancelamento continua valendo quando a derrota for automática por
  tempo. Uma trump não deve conseguir salvar quem já perdeu por tempo.
- Registrar no ClientSim: timeout com os dois jogadores parados, timeout
  interrompido no meio da distribuição, e o caso de os dois estourarem o
  tempo.

**2. Câmera de contagem**

Criar uma câmera dedicada que renderiza só o cronômetro, isolada da vista do
jogador, para servir de fonte de textura.

- GameObject novo dentro de `TwentyOne`, ao lado de `Post Processing Volume`.
  Sugestão de nome: `TimerCamera`.
- Câmera ortográfica, `clearFlags = SolidColor`, `cullingMask` de uma layer
  dedicada (ex.: `TimerOnly`) para que nada além do texto entre no frame.
- Distante de `Main Camera`, sem `AudioListener` (só a câmera principal pode
  ter um). Não entra no `VRCSceneDescriptor` nem em nenhum array de câmera.
- `targetTexture` apontando para uma `RenderTexture` (ex.: 512x256), com
  `antiAliasing` e `depth` ajustados para o custo no Quest.
- Um Canvas em Screen Space - Camera apontando para ela, ou um TextMeshPro
  3D posicionado no frustum, com o texto grande e centralizado. O TMP é
  preferível: os outros textos do projeto já usam TMP.
- O texto é escrito por um UdonBehaviour local (nada de rede): lê o tempo
  restante do `CardDealer` e formata `M:SS`. Estejam todos na mesma cena,
  então os dois lados vendo a mesma câmera veem o mesmo número.

**3. Shader que leva a saída da câmera para as TVs**

Todas as TVs do mundo apontam para a mesma `RenderTexture`, então basta um
material com um shader que amostra essa textura.

- Shader novo, por exemplo em `Assets/Shaders/TimerBroadcast.shader`,
  seguindo o estilo dos shaders próprios do projeto em
  `Assets/RoomAtmosphere/Materials/` (ver `CRTReference.shader` para o
  padrão de CRT que as TVs já usam).
- O shader amostra a `RenderTexture` do timer e combina por cima do que a TV
  já exibia. Precisa ser um blend, não uma troca: se a câmera falhar, a TV não
  pode ficar preta.
- Propriedades expostas no material: `_TimerTex`, corte/zoom do texto dentro
  da textura, intensidade do overlay, e um `_TimerStrength` para subir a
  opacidade conforme o tempo acaba (fica vermelho e pisca nos últimos
  segundos, por exemplo).
- **Restrição do VRChat:** o material precisa ser compatível com a
  whitelist de shaders. Um shader customizado com Properties expostas passa
  pela whitelist, mas só se estiver entre os shaders permitidos da build ou
  usando um dos shaders standard. Verificar
  `ProjectSettings/GraphicsSettings.asset` → `m_AlwaysIncludedShaders` e
  confirmar com o SDK antes de Investir no shader. Fallback se não passar:
  fazer as TVs apontarem direto para a `RenderTexture` e sobrepor o TMP como
  objeto 3D na frente do painel, sem shader.
- Como não há TVs na cena ainda (`Assets/Scenes/Game.unity` não tem nenhum
  objeto com nome de TV, Screen ou Monitor), essa parte depende de as TVs
  existirem. Definir quantas são e onde ficam antes de montar o material.
- Uma `RenderTexture` compartilhada por várias TVs é o caminho certo: uma
  câmera, uma textura, N materiais apontando para ela. Não criar uma câmera
  por TV.
- Custo: essa câmera roda todo quadro. Verificar no profiler se o custo é
  aceitável e, se não for, considerar renderizar a 30fps em vez de todo
  quadro, já que o cronômetro só muda uma vez por segundo.
- A revelação da carta oculta ao fim da rodada ainda não foi definida. A
  primeira carta de cada jogador permanece visualmente oculta até a limpeza.
- Entrada ou saída de jogador durante a partida ainda não faz nova atribuição
  dos Slots. A partida deve ser reiniciada com dois jogadores presentes.
- Validação em duas instâncias reais do VRChat, incluindo um cliente que não é
  dono do baralho e a reconstrução da mesa após entrar atrasado.

## Decisões de adaptação pendentes

- Vida e aposta iniciais: a cena usa 3 de vida, aposta 1 e crescimento 1.
- Valores atuais configuráveis: trumps não usadas são descartadas a cada
  rodada (`clearTrumpsEachRound`), máximo de 8 na mão e 20% de chance de
  ganhar uma após Hit. Confirmar se esses padrões devem mudar.
- Quando e para quem revelar a carta numérica oculta.
- Como mostrar a derrota por tempo e sua interação futura com `Remove` e
  `Exchange`. Ver a seção "Timer de turno" acima: falta decidir se a derrota
  por tempo é automática ou passa pelo gancho.
- Se o valor de 1 minuto é o padrão final do timeout, e se ele muda entre
  rodadas ou por trump.
- Quantas TVs o mundo tem e onde elas ficam, para o shader do cronômetro.

## Limitação do editor

O ClientSim exibiu uma `NullReferenceException` repetida no inspetor de
variáveis Udon (`Packages/com.vrchat.worlds/.../UdonProgramAsset.cs`). Foi
aplicada uma guarda de `SyncMetadataTable` nessa cópia local do pacote. O
arquivo está ignorado pelo Git e uma atualização do SDK pode substituí-lo.
Não foi observado erro equivalente no fluxo Hit/Stay do `CardDealer` após a
correção local.
