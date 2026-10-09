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
- Na primeira rodada também: a distribuição ocorre após registrar as quatro
  cartas numéricas iniciais, no estado sincronizado `trumpType`/`trumpOwner`.
  A animação das cartas numéricas pode ainda estar em andamento. As cartas
  tarot aparecem nas áreas `Tarots_illspawnhere_Player1/2` assim que o estado
  sincronizado é atualizado. Com `logDeals` ativo, o Console mostra cada
  trump entregue e seu jogador.
- Máximo de oito na mão (`maxTrumpsPerPlayer`).
- Chance de 20% de receber uma trump após Hit (`bonusTrumpChancePercent`).
- Trumps não usadas são descartadas na rodada seguinte
  (`clearTrumpsEachRound`).
- Timeout padrão 60 s, zero desliga. Esgotar o tempo encerra a rodada com derrota do jogador da vez e dano normal da aposta.

## Integração visual

`Assets/Models/Tarot/Tarot.prefab` tem collider, Rigidbody, VRCPickup,
descrição e símbolo. `TarotVisuals` cria a mão nas duas áreas
`Tarots_illspawnhere_Player1/2` a partir de trumpType/trumpOwner.
Há 25 materiais Unity padrão em `Assets/Models/Tarot/Materials`, um por ID,
com as texturas de Assets/Docs/trump-cards associadas por ID. A descrição
começa invisível, aparece gradualmente ao pegar a carta e desaparece ao
soltar. A carta solta fora da mesa retorna à posição inicial. Ao soltar sobre
`TableTrigger` na própria vez, `TarotVisuals` chama
`RequestUseTrump(indice)` e anima a carta até a linha do jogador em
`Pos-Player1` ou `Pos-Player2`. O índice começa em zero na própria mão.

Os efeitos contínuos usam tableTrumpType/tableTrumpOwner. Os objetos usados são reconstruídos por usedTrumpType/usedTrumpOwner e ficam como histórico mesmo quando um efeito contínuo é destruído. A identidade e o
efeito da carta ficam no estado de rede; o prefab apresenta a carta localmente.

O timeout encerra a rodada com derrota do jogador da vez; não gera gancho. Dano só na resolução da rodada. Regra atualizada a pedido em 09/10/2026.
Funcionamento com dois clientes reais e interação VR confirmados pelo usuário em 09/10/2026.

As descrições apresentadas nas cartas estão em inglês. Documentação técnica abaixo permanece em português.

## Fluxo de pickup e uso (histórico de 07/10/2026)

VRCDefaultWorldScene agora tem TarotVisuals no objeto Tarot, com os dois Tarots_illspawnhere, um TableTrigger e Pos-Player1/Pos-Player2 na região ocupada pela prévia da mesa. Os spawns são associados à posição física dos slots: Play1 usa Tarots_illspawnhere_Player1 e Play2 usa Tarots_illspawnhere_Player2 (referências corrigidas em 09/10/2026).

Configure, ApplyMove e CardDropped usam campos públicos e eventos sem argumentos, conforme a regra do MenSharp. Opacidade da descrição e retorno de posição/rotação são interpolados em Update. Cartas disponíveis aparecem nos spawns; ao soltar fora da mesa ou fora da vez, retornam ao spawn. Sobre a mesa e na própria vez, o dealer valida a intenção e consome a carta. Cartas já usadas continuam com descrição e pickup, mas soltá-las só devolve à posição registrada.

usedTrumpType/usedTrumpOwner/usedTrumpCount no dealer sincronizam o histórico visual, inclusive efeitos instantâneos. Esse histórico é separado das trumps contínuas que ainda influenciam as regras e é limpo em cada rodada.

Validação no ClientSim (07/10/2026): eventos nativos `_onPickup` e `_onDrop`; descrição chegou a alpha 1; retorno fora da mesa e retorno de carta usada tiveram erro de posição 0; descarte válido registrou usedTrumpCount=1, reduziu a mão de 4 para 3 e terminou em Pos-Player1 (0.13, 1.11, 22.54). Console sem erros após os testes. A sessão foi suspensa apenas nessa execução de Play Mode para isolar a interação; não foi salvo esse estado temporário. Teste de rede com dois clientes reais ainda não executado.


Validação complementar: os 25 IDs passaram em cenários controlados de efeito e interação no ClientSim. Evidências em qa-07-10.json e validacao-07-10.md. TarotPickup conserva a posição local de repouso até pickup. A carta numérica especial mostra a face para o próprio dono e o verso para adversários/espectadores; o resultado revela todas as faces. Transporte com dois clientes VRChat reais continua sem validação.
