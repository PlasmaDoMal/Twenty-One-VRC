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
- Visual da carta de gancho para o timeout.
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
  `Exchange`.

## Limitação do editor

O ClientSim exibiu uma `NullReferenceException` repetida no inspetor de
variáveis Udon (`Packages/com.vrchat.worlds/.../UdonProgramAsset.cs`). Foi
aplicada uma guarda de `SyncMetadataTable` nessa cópia local do pacote. O
arquivo está ignorado pelo Git e uma atualização do SDK pode substituí-lo.
Não foi observado erro equivalente no fluxo Hit/Stay do `CardDealer` após a
correção local.
