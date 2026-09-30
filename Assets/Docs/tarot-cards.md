# Tarot cards: IDs e integração visual

As 25 trumps já têm lógica em `Assets/Scripts/CardDealer.cs`. Os IDs abaixo são
estáveis na mão sincronizada (`trumpType`) e nas cartas contínuas da mesa
(`tableTrumpType`). Um mesmo tipo pode aparecer mais de uma vez.

| ID | Carta | Efeito |
| ---: | --- | --- |
| 1 | 2-Card | Compra o número 2, se ainda estiver no baralho. |
| 2 | 3-Card | Compra o número 3, se disponível. |
| 3 | 4-Card | Compra o número 4, se disponível. |
| 4 | 5-Card | Compra o número 5, se disponível. |
| 5 | 6-Card | Compra o número 6, se disponível. |
| 6 | 7-Card | Compra o número 7, se disponível. |
| 7 | Go For 17 | Alvo da rodada passa a 17; substitui outra Go For. |
| 8 | Go For 24 | Alvo passa a 24; substitui outra Go For. |
| 9 | Go For 27 | Alvo passa a 27; substitui outra Go For. |
| 10 | One-Up | +1 na aposta enquanto estiver na mesa. |
| 11 | Two-Up | +2 na aposta enquanto estiver na mesa. |
| 12 | Shield | −1 na aposta enquanto estiver na mesa. |
| 13 | Shield+ | −2 na aposta enquanto estiver na mesa. |
| 14 | Bless | Evita a morte do perdedor nessa rodada; próxima aposta base cai em 1. |
| 15 | Bloodshed | +1 na aposta e compra uma trump. |
| 16 | Destroy | Destrói a trump contínua mais recente do adversário. |
| 17 | Reincarnation | Destrói a trump contínua mais recente do adversário e compra uma trump se conseguiu destruir. |
| 18 | Friendship | Cada jogador recebe duas trumps. |
| 19 | Hush | Compra uma carta numérica oculta. |
| 20 | Perfect Draw | Tenta comprar o número que completa o alvo ativo. |
| 21 | Remove | Remove a última carta do adversário, ou seu gancho. |
| 22 | Return | Remove sua última carta numérica. |
| 23 | Exchange | Troca as últimas cartas dos jogadores; se houver gancho, remove-o. |
| 24 | Disservice | Força o adversário a comprar uma carta numérica, sem bônus de trump. |
| 25 | Refresh | Descarta suas cartas numéricas e compra duas novas, a primeira oculta. |

A trump é consumida mesmo quando seu efeito não encontra um alvo válido. Remove,
Return e Exchange não retiram a última carta oculta de um jogador. Perfect Draw
não compra se a carta necessária não estiver disponível ou não for positiva.
Trumps contínuas são limpas ao fim da rodada.

## Configuração atual

- Duas trumps por jogador no início da rodada (`trumpCardsPerRound`).
- Na primeira rodada também: a distribuição ocorre após registrar as seis
  cartas numéricas iniciais, no estado sincronizado `trumpType`/`trumpOwner`.
  A animação das cartas numéricas pode ainda estar em andamento. Não há
  objetos de tarot na cena ainda. Com `logDeals` ativo, o Console mostra cada
  trump entregue e seu jogador.
- Máximo de oito na mão (`maxTrumpsPerPlayer`).
- Chance de 20% de receber uma trump após Hit (`bonusTrumpChancePercent`).
- Trumps não usadas são descartadas na rodada seguinte
  (`clearTrumpsEachRound`).
- O timeout fica desativado (`turnTimeoutSeconds = 0`) até existir o visual do
  gancho. Se ativado, o gancho permanece como estado até Remove/Exchange ou fim
  da rodada. Se ambos ficarem sem agir e receberem gancho, a partida termina
  empatada para evitar rodadas automáticas sem fim.

## Integração visual pendente

Criar um único modelo/prefab de tarot card e 25 texturas ou materiais, seguindo
a estratégia das cartas numéricas. Associar o material pelo ID acima. A mão de
cada jogador vem de `TrumpCountInHand(player)` e `TrumpAt(player, indice)`;
`TrumpName(id)` fornece o nome. Ao selecionar uma carta, chamar
`RequestUseTrump(indice)`, onde o índice começa em zero na **própria mão**.

As cartas contínuas usam `tableTrumpType[0..tableTrumpCount-1]` e
`tableTrumpOwner`. O visual deve ser reconstruído também quando esses campos
sincronizados mudarem. A identidade e o efeito da carta já estão no estado de
rede; o prefab será somente a apresentação.

A interface de seleção e o modelo/material do gancho ainda estão pendentes.
Testes finais em duas instâncias reais do VRChat permanecem necessários.
