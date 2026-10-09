# Twenty One (Roblox) — regras e Trump Cards

Twenty One é um Blackjack 1v1 em que cada jogador usa 25 tipos de trump cards para mudar a regra, a aposta e as mãos.

## Regra atual da adaptação VRC (09/10/2026)

Vida inicial 20; duas cartas por jogador, primeira oculta; aposta inicial 1 e +1 por rodada. O timeout encerra a rodada com vitória do adversário e dano da aposta. Estourar bloqueia apenas Hit; a rodada continua até duas passadas seguidas sem trump ou timeout. Cartas ocultas são reveladas e as mãos comparadas somente na resolução. Intervalo entre turnos 2 s e resultado visível por 5 s. A descrição do gancho abaixo documenta o jogo original, não a regra atual desta adaptação.

## Como o jogo funciona

Ganha a rodada quem chega mais perto de 21 sem passar. O perdedor da rodada sofre a aposta (tally) e, quando o contador dele acaba, perde o jogo.

- **Baralho:** cartas numéricas de 1 a 11, apenas uma de cada. Uma carta que um jogador pegou não pode ser pega pelo outro.
- **Turno:** cada jogador compra uma carta do baralho ou passa (stay). Duas passadas seguidas, sem nenhuma trump no meio, encerram a rodada.
- **Estouro:** quem passa de 21 perde. Se os dois passam de 21, perde quem tiver o número maior.
- **Trumps:** a cada rodada cada jogador recebe 2 trump cards, com chance de receber outra ao comprar uma carta do baralho.
- **Aposta e vida:** no original, um jogador é amarrado a uma serra e o outro a uma cadeira elétrica. O perdedor da rodada avança o valor da aposta (distância da serra, ou intensidade da cadeira). A aposta sobe a cada rodada. Quando ela alcança o contador restante do perdedor, ele morre e o outro vence.
- **Timeout:** se o jogador não comprar nem passar a tempo, recebe uma carta em forma de gancho. Ela o faz perder a rodada independentemente da soma. Só Remove e Exchange a tiram (o Exchange a remove, não a troca).

A versão adaptada pode trocar serra e cadeira por pontos de vida. A mecânica é a mesma: uma aposta crescente contra um contador.

## Como as trump cards funcionam

Trumps são cartas de habilidade que ajudam quem as usa ou atrapalham o oponente, e cada uma é usada uma vez.

- **Como se obtêm:** 2 no início de cada rodada e, às vezes, ao comprar uma carta do baralho.
- **Cartas colocadas na mesa:** as de efeito contínuo (Go For, One-Up, Two-Up, Shield, Shield+, Bless, Bloodshed) ficam na mesa enquanto durarem. Podem ser destruídas por Destroy ou Reincarnation.
- **Cartas instantâneas:** as demais agem na hora (compram, removem ou trocam cartas) e não ficam na mesa.
- **Uso:** a qualquer momento do turno do jogador, com a tecla espaço no original. Usar uma trump não conta como comprar nem passar.

Categorias abaixo: número, regra, aposta e vida, controle de trumps e mão. Ao todo são 25 cartas.

## Cartas de número

| Carta | Efeito |
| --- | --- |
| 2-Card | Você pega a carta de número 2 do baralho. |
| 3-Card | Você pega a carta 3. |
| 4-Card | Você pega a carta 4. |
| 5-Card | Você pega a carta 5. |
| 6-Card | Você pega a carta 6. |
| 7-Card | Você pega a carta 7. |

Se o número pedido já saiu do baralho, a trump é gasta e você não recebe nada.

## Cartas de regra (Go For)

| Carta | Efeito |
| --- | --- |
| Go For 17 | A rodada passa a ser decidida por quem chega mais perto de 17. |
| Go For 24 | O alvo passa a ser 24. |
| Go For 27 | O alvo passa a ser 27. |

- Se já há uma Go For na mesa, colocar outra remove a anterior.
- Destroy ou Reincarnation removem a Go For e o alvo volta a 21.
- Com Go For na mesa, o estouro passa a valer para o novo alvo, e a Perfect Draw também mira o novo alvo.

## Cartas de aposta e vida

| Carta | Efeito | Detalhes |
| --- | --- | --- |
| One-Up | Aumenta a aposta em +1 enquanto estiver na mesa. | Empilha com outras One-Up e Two-Up. |
| Two-Up | Aumenta a aposta em +2 enquanto estiver na mesa. | Empilha com One-Up e Two-Up. |
| Shield | Reduz a aposta em 1 enquanto estiver na mesa. | Empilha com as outras cartas de aposta. |
| Shield+ | Reduz a aposta em 2 enquanto estiver na mesa. | Empilha com as outras cartas de aposta. |
| Bless | Se você perderia o jogo nesta rodada, você não morre. | Só age quando você iria morrer. O oponente também não morre se você usar. |
| Bloodshed | Você compra 1 trump e aumenta a aposta em +1 enquanto estiver na mesa. | Fica na mesa e empilha com outras Bloodshed. |

- Todas as cartas de aposta valem para os dois jogadores e podem ser destruídas por Destroy ou Reincarnation.
- Depois da rodada, se havia cartas de aposta na mesa, a aposta é resetada.
- **Bless:** se salvar uma vida, a aposta da rodada seguinte diminui em 1 em vez de subir. Se o Bless foi usado mas não salvou ninguém, a aposta segue normalmente.
- **Uso estratégico:** pôr um One-Up ou Two-Up na mesa protege seu Bless ou Go For de um Destroy, porque o Destroy só atinge a carta mais recente. Outra tática é subir a aposta até igualar seu contador e usar Bless no fim para anular o avanço.
- **Bloodshed:** é a saída mais segura para conseguir uma trump quando você está mal, pois a compra é garantida.

## Cartas de controle de trumps

| Carta | Efeito | Detalhes |
| --- | --- | --- |
| Destroy | Destrói a trump mais recente colocada pelo oponente. | Sem efeito se não houver trump do oponente na mesa. Serve para desfazer Go For e apostas altas. |
| Reincarnation | Destrói a trump mais recente do oponente e você compra 1 trump. | Sem efeito se o oponente não tiver nenhuma trump na mesa, e nesse caso você não compra trump. |
| Friendship | Você e o oponente compram 2 trumps cada. | Saída de emergência, mas dá o mesmo benefício ao oponente. |

- Destroy e Reincarnation atingem só a trump mais recente do oponente, nunca a sua.
- Contra One-Up e Two-Up, o Destroy anula a aposta que estava na mesa.
- Uma combinação forte é Destroy ou Reincarnation para voltar o alvo a 21 e depois Refresh.

## Cartas de mão

| Carta | Efeito | Detalhes |
| --- | --- | --- |
| Hush | Você compra uma carta virada para baixo, oculta para o oponente. | Serve para blefar sobre sua soma real. |
| Perfect Draw | Você recebe a carta de número que faz sua soma chegar a 21. | Não dá carta oculta e nunca dá uma carta que estoura. Com Go For na mesa, mira 17, 24 ou 27. Revela indiretamente sua carta oculta ao oponente. |
| Remove | Remove a última carta que o oponente comprou. | Pode atingir uma carta oculta vinda de Hush, mas não a última carta oculta do oponente. |
| Return | Remove a última carta que você comprou. | Mesmas restrições do Remove: não retira sua última carta oculta. |
| Exchange | Troca a sua última carta comprada pela última carta do oponente. | Não troca a última carta oculta. A carta de gancho do timeout não é trocada, é removida. |
| Disservice | Força o oponente a comprar uma carta. | O oponente não recebe trump por essa compra forçada. |
| Refresh | Remove todas as suas cartas de número (inclusive a oculta) e dá 2 novas, sendo 1 oculta. | Bom para resetar uma mão ruim, ou para vencer depois de voltar o alvo a 21. |

- **Return e Remove:** também podem remover a primeira carta que você recebeu, se ela for a última, sem contar a oculta.
- **Hush + Perfect Draw:** a combinação compensa em parte a desvantagem de o oponente deduzir sua carta oculta.

## Regras especiais e pontos em aberto

Regras confirmadas no FAQ do wiki:

- Duas passadas seguidas, sem trump no meio, encerram a rodada.
- Return, Remove e Exchange não podem pegar a última carta oculta.
- Perfect Draw não dá carta oculta e nunca faz você estourar.
- Trumps não vêm de um Disservice.
- Bless não mata o oponente também, e mover a aposta ainda acontece se o Bless não salvou ninguém.

Pontos que o wiki não fecha e que você precisa decidir na adaptação:

- Valor inicial do contador de cada jogador e da aposta na primeira rodada, e quanto a aposta sobe por rodada.
- Qual das duas cartas iniciais é a oculta e quando ela é revelada no fim da rodada.
- Tamanho máximo da mão de trumps e o que acontece com trumps não usadas ao fim da rodada.
- Com Go For na mesa, se o estouro passa a valer para o novo alvo. O wiki sugere que sim, sem dizer explicitamente.
- Chance exata de comprar uma trump junto com uma carta do baralho.

## Fontes

A página Trumps do Fandom retornou erro 402 ao ser aberta, então usei o resumo das trumps da NamuWiki e trechos das páginas do Fandom vistos na busca.

- [Twenty One (Roblox), NamuWiki](https://en.namu.wiki/w/Twenty%20One(Roblox)): aberta por inteiro, base das regras e da lista das 25 trumps.
- [FAQ and more Rules, Twenty One Wiki](https://twenty-one.fandom.com/wiki/FAQ_and_more_Rules): trechos vistos na busca, regras especiais.
- [Bloodshed](https://twenty-one.fandom.com/wiki/Bloodshed-Trump), [Reincarnation](https://twenty-one.fandom.com/wiki/Reincarnation), [Destroy](https://twenty-one.fandom.com/wiki/Destroy), [Bless](https://twenty-one.fandom.com/wiki/Bless), [Friendship](https://twenty-one.fandom.com/wiki/Friendship), [Hush](https://twenty-one.fandom.com/wiki/Hush), [Refresh](https://twenty-one.fandom.com/wiki/Refresh), [One-Up](https://twenty-one.fandom.com/wiki/One-Up), [Two-Up](https://twenty-one.fandom.com/wiki/Two-Up): trechos vistos na busca.
- [Twenty One, Videogaming Wiki](https://videogaming.fandom.com/wiki/Twenty_One): visão geral e lista de cartas.
