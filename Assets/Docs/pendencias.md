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
- A vida começa em `startingLife`. A aposta da rodada é `roundDamage +
  (roundNumber - 1) * roundDamageGrowth`, limitada a zero. A partida termina
  quando uma vida chega a zero ou quando `maxRounds` é alcançado.
- `turnTimeoutSeconds` permite derrota por tempo e fica em zero na cena até
  existir a apresentação da carta de gancho. Quando habilitado, a contagem
  começa após a distribuição animada e reinicia depois de uma jogada.
- Limpeza de rodada interrompe animações antigas. Deserialização reconstrói
  a mesa se o histórico for zerado ou a semente mudar.

## Ainda necessário para jogar sem chamadas de teste

- Controles para iniciar, comprar e passar, indicador de turno, vida, rodada e
  vencedor. O menu está fora do escopo atual, mas a interface durante a
  partida ainda precisa ser ligada aos métodos `Request*`.
- As 25 trumps descritas em `twenty-one-trump-cards.md` não têm mão própria,
  distribuição, efeitos, objetos na mesa nem interface. `RequestUseTrump` é
  ignorado deliberadamente e não consome a carta numérica oculta.
- A carta de gancho do timeout requer visual e uma regra de remoção por
  `Remove`/`Exchange`; por ora o timeout decide a rodada diretamente quando
  habilitado.
- A revelação da carta oculta ao fim da rodada ainda não foi definida. A
  primeira carta de cada jogador permanece visualmente oculta até a limpeza.
- Entrada ou saída de jogador durante a partida ainda não faz nova atribuição
  dos Slots. A partida deve ser reiniciada com dois jogadores presentes.
- Validação em duas instâncias reais do VRChat, incluindo um cliente que não é
  dono do baralho e a reconstrução da mesa após entrar atrasado.

## Decisões de adaptação pendentes

- Vida e aposta iniciais: a cena usa 3 de vida, aposta 1 e crescimento 1.
- Política de trumps não usadas, tamanho máximo da mão de trumps e chance de
  receber uma trump depois de Hit.
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
