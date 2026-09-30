# Pendências do protótipo

Lista do que ainda não está pronto, decidido ou implementado no Twenty One. Serve
para não perder o que ficou pela metade e para deixar claro o que é bug e o que é
falta de implementação.

Status do código: `Assets/Scripts/CardDealer.cs`, `Assets/Scripts/PlayerSlot.cs` e
`Assets/Scripts/OwnerOnlyVisibility.cs`, mais a cena `Assets/Scenes/Game.unity`.

## Bugs corrigidos nesta rodada

- **Contador de rodada parado.** `StartRound()` não incrementava `roundNumber`, e
  `FinishRound()` chama `StartRound()` direto. Todo log dizia "rodada 1" e
  `maxRounds > 1` nunca encerrava a partida. Agora `StartMatch()` zera
  `roundNumber` e `StartRound()` incrementa, então a primeira rodada é a 1 e cada
  `FinishRound()` avança uma.
- **Só o dono do baralho conseguia iniciar.** Antes da primeira partida,
  `IsMatchStarter()` exigia `slots[0].IsMine()`, mas a posse dos Slots só é
  atribuída dentro de `StartMatch()`. Ou seja, antes de começar, todo Slot era do
  master e um cliente não-master era sempre recusado. Agora, enquanto
  `matchStarted` é falso, qualquer jogador pode pedir o início: quem executa
  continua sendo o dono do baralho, que revalida tudo em `MatchCanStart()`.

## Pendências

### UI de jogo (C, H)

Não existe um único botão nem um `Canvas` na cena. Hoje o protótipo só roda por
chamada direta no Inspector ou por script de teste.

Falta, no mínimo:

- Botão de **Iniciar partida**, ligado a `RequestStartMatch()`.
- Botão de **Comprar** e **Passar** por jogador, ligados a `RequestHit()` e
  `RequestStay()`. Como `RequestHit`/`RequestStay` já descobrem sozinhos o Slot do
  jogador local, um par de botões compartilhado serve para os dois lados.
- Destaque de de quem é a vez. `turnIndex` e `currentPlayer` já são sincronizados,
  então é só leitura.
- **Vida, número da rodada e vencedor.** `life`, `roundNumber` e `matchStarterPlayerId`
  já são sincronizados, mas `RefreshScores()` escreve só `total/targetScore`. Não há
  nada na tela mostrando quanta vida cada um tem, em que rodada está, nem quem
  venceu a rodada.
- Estado do botão: desabilitar Comprar/Passar quando não é a vez do jogador, e
  quando a partida não começou.

### Carta secreta nunca é revelada (E)

`ApplyCardMaterial()` troca o material da primeira carta de cada mão para
`hiddenMaterial` na compra, e nada reverte no fim da rodada. Hoje a carta fica
virada a rodada inteira. `twenty-one-trump-cards.md` deixa a pergunta aberta
("qual das duas cartas iniciais é a oculta e quando ela é revelada"), então é
decisão de design, não bug.

Decidir:

- Se a oculta é a primeira carta de cada mão (é o que o código faz hoje) ou a
  segunda.
- Se revela no fim da rodada, e se revela para os dois ou só para o dono.
- Se a carta revelada continua somando normalmente.

### "Carta especial" e "trump" são coisas diferentes (F, B)

Hoje o código usa um único conceito para os dois, e os nomes confundem:

- `markFirstCardSpecial` marca a **carta secreta** da abertura como "especial".
- `special` alimenta `IsSpecialCard()`, que `AcceptTrump()` consulta para decidir
  se o jogador pode "usar" a carta.
- Efeito disso: `RequestUseTrump(cardIndex)` na prática significa **queimar a sua
  própria carta secreta**, o que não é a regra do original. No original as trumps
  são 25 cartas de habilidade separadas, recebidas 2 por rodada.

Falta:

- Separar os dois conceitos no código e renomear (`IsSpecialCard` para algo como
  `IsHiddenCard`, e um registro separado de trumps na mão).
- Implementar `AcceptTrump()`: hoje ele só marca o uso em `trumpUsed` e tem o
  comentário "o efeito da carta entra aqui". Nenhum dos 25 efeitos está escrito.
- Distribuir as trumps: `trumpCardsPerRound` está declarado e nunca usado, e não
  existe nenhuma carta de tarot na cena.
- **Por isso a regra de "duas passadas" fica incompleta.** O original encerra a
  rodada com duas passadas seguidas *sem nenhuma trump no meio* (ver
  `twenty-one-trump-cards.md`). `AcceptTrump()` não zera `consecutiveStays`.
  Enquanto não existir trump nenhuma no jogo isso é inofensivo, porque a condição
  nunca é alcançada, mas precisa ser corrigido junto com a entrega das trumps.

### Limpeza de código (G)

Feito nesta rodada, sem mudança de comportamento:

- `requestedPlayer` removido. O campo apontava no tooltip para `Hit()` e `Stay()`,
  métodos que não existem mais desde a troca pelo `PlayerSlot`.
- Bloco de documentação órfão antes de `AcceptHit()`, que descrevia o antigo
  `Hit(int)` público removido.
- Tooltip de `currentPlayer` reescrito: ele é espelho de `turnIndex` para a UI, e
  não a fonte da verdade.

Ainda para verificar depois:

- `hideSpecialCards` tem tooltip dizendo "deixa desligado até o baralho de trumps
  entrar", mas a cena está com ele ligado, porque agora ele é o que esconde a
  carta secreta da abertura. As duas coisas se confundem no mesmo campo.
- O doc da classe ainda descreve a rodada em termos de `openingCards` e
  `RequestHit`/`RequestStay`; vale reler depois que a UI existir.
- `trumpCardsPerRound` e `twoStaysEndRound` são configuráveis, mas o primeiro
  nunca é lido e o segundo só funciona com duas mãos.

## Regras do original ainda não implementadas

Nada disto está no código; está em `twenty-one-trump-cards.md`.

- **A aposta sobe a cada rodada.** Hoje o dano é fixo (`roundDamage`, padrão 1).
  O original tem uma aposta crescente contra um contador, e é isso que dá a tensão
  do jogo.
- **Carta de gancho por timeout.** Se o jogador não comprar nem passar a tempo,
  recebe uma carta que faz perder a rodada na hora.
- **Refresh / Remove / Return / Exchange** e o resto das trumps de mão.
- **Cartas de regra (Go For 17 / 24 / 27)** mudam o alvo da rodada, e o estouro
  passa a valer para o novo alvo.
- **Cartas de aposta (One-Up, Two-Up, Shield, Bless, Bloodshed)** mexem na aposta
  e na vida.
- **Trucks de controle (Destroy, Reincarnation, Friendship)** agem sobre as trumps
  do oponente.
- **Hush e Perfect Draw** existem como mecânica de carta oculta; hoje a carta
  secreta é fixa na abertura, e não comprada por efeito.

## Decisões de adaptação ainda em aberto

As mesmas que `twenty-one-trump-cards.md` lista, mais as que apareceram agora:

- Valor inicial do contador e da aposta na primeira rodada, e quanto a aposta sobe.
- Qual das duas cartas iniciais é a oculta e quando ela é revelada.
- Tamanho máximo da mão de trumps e o que acontece com trumps não usadas no fim
  da rodada.
- Chance exata de comprar uma trump junto com uma carta do baralho.
- Se a revelação da carta oculta é só visual ou também muda a soma dos dois.

## Risco de rede ainda não verificado

O fluxo de dois jogadores foi testado no ClientSim com o dono do baralho e dois
Slots. Não foi verificado em instância real:

- Um cliente que não é o dono do baralho apertando Comprar/Passar.
- Um cliente que não é o dono tentando Iniciar (é o que o bug do `IsMatchStarter`
  corrigia; vale confirmar em rede real).
- Jogador entrando ou saindo no meio da partida. Não há tratamento nenhum: os
  Slots são atribuídos uma vez, em `AssignSlots()`, dentro de `StartMatch()`.
  Se alguém sai, o Slot dele continua com a posse de um `playerId` que não existe
  mais e o turno pode ficar preso esperando uma jogada que nunca vem.
- O Baralho do dono sendo perdido ou sendo destruído.
- Limite de `logCapacity` (32) numa rodada longa: o original tem 11 cartas no
  baralho, mas trumps de mão (Refresh, Friendship) compram cartas do baralho de
  novo e podem estourar o historico.
