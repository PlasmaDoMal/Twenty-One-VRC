# Validação de 08/10/2026 — alvo PC

## Revisão de turnos e botões

Regra atual: dano somente na resolução da rodada. Timeout passa a vez, reinicia o cronômetro e zera a sequência de Stays, sem encerrar a rodada. Hit e o primeiro Stay não causam dano. Dois Stays consecutivos resolvem a rodada normalmente.

Os quatro callbacks Unity Button de Hit/Stay apontavam para componentes Udon sem programa. Referências corrigidas na cena e no prefab; esse defeito também afetava multiplayer. No solo, os dois turnos usam Player1_Choices, pois o jogador ocupa Play1 e controla os dois slots. Player2_Choices fica oculto. Notificações de turno iniciam animações no Update, evitando reentrada do scheduler durante a distribuição.

ClientSim: botão Solo iniciou seis cartas, vida [3,3]. Callback real Hit aumentou logCount para 7 e passou de turno 0 para 1; callback real Stay retornou para 0, sem alterar vida/rodada. Timeouts repetidos mantiveram rodada 1 e vida [3,3], com consecutiveStays=0. Dois Stays encerraram a rodada e aplicaram a aposta ao perdedor. MenSharp recompilou os 20 programas; novos campos conferidos no programa de TurnFadeIn.

Ainda foi observado erro de símbolo ownerPlayer em TarotVisuals durante a abertura. Não considerar toda a execução livre de erros: investigar esse caminho de criação/atualização dos tarots e repetir pickup/drop. Rede, entrada tardia e interação por laser VR continuam exigindo dois clientes reais.

Implementadas proteções de sequência, rodada, identidade do dono do slot e tipo esperado da carta; confirmação/rejeição de uso de tarot, retorno em expiração; cancelamento da distribuição ao abandonar a sessão e tratamento de transferência de ownership. Referências dos slots ao dealer vinculadas na cena e no prefab.

A compilação C#/MenSharp passou durante a revisão. O teste nativo de pickup/drop consumiu o tarot, mas revelou erro de referência `ownerPlayer` na apresentação da confirmação. A implementação final recria o visual usado a partir do histórico confirmado e anima desde a posição do descarte, evitando reaproveitar a referência problemática. Essa última alteração ainda exige regressão em Play Mode; não declarar o caso aprovado.

Interrupção solicitada pelo usuário por limite de uso. Ficaram sem conclusão: rejeição/expiração em runtime, capacidade máxima/ausência de alvos, reinício e saída durante distribuição, migração de dono com dois clientes, build SDK e medição de performance em PC. O painel SDK estava sem login. Nenhum mundo foi publicado. Testes ClientSim não comprovam transporte de rede real.

## Checklist para finalizar
- [ ] Regressão do pickup/drop após a última correção: descrição, confirmação, animação e retorno.
- [ ] Rejeição e expiração de pedidos sem consumo indevido; pedido atrasado de rodada anterior.
- [ ] Mão cheia, baralho vazio e efeitos sem alvo válido.
- [ ] Reiniciar ou sair durante a distribuição; reentrar e iniciar outra partida.
- [ ] Dois clientes VRChat: guest, entrada tardia, carta oculta, ownership e desconexão.
- [ ] Fluxo intro -> basement -> TwentyOne -> basement com fade e ativação local dos mapas.
- [ ] Build SDK para PC e performance no cliente real.
- [ ] Conferir a correção local de SyncMetadataTable do SDK em outra máquina: o pacote é ignorado pelo Git e a alteração não acompanha este commit.

## Correção do respawn após teste VRChat
O VRCSceneDescriptor estava com RespawnHeightY = 0, no nível do piso do basement. Alterado para -10 na cena VRCDefaultWorldScene, permitindo que o jogador assente no piso sem disparar o respawn por altura. Requer novo Build & Test no VRChat para confirmar o sintoma relatado.

## Iluminação e teste solo
Cena: duas luzes locais no basement e uma sobre a mesa, sem sombras adicionais. IntroMenuRoot ampliado para escala 2. SOLO TEST na tela Create inicia com o host nos dois slots; o mesmo jogador controla as duas mãos. A partida normal continua exigindo dois jogadores distintos. O modo solo é diagnóstico, não valida transporte de rede ou privacidade entre jogadores.

Validação solo no ClientSim: 1 jogador, clique no botão SOLO TEST, host=guest=1, launching=true, teleported=true, matchStarted=true, logCount=6, posição Play1=(-0.03,0.29,23.18). Compilação sem erros no console. Iluminação e tamanho final do menu ainda precisam de revisão visual no cliente VRChat.

## Pointer VR, menu privado e custo contínuo
Hit/Stay não tinham VRCUiShape e OwnerOnlyUIVisibility estava sem assignedSlot. Corrigidos no TwentyOne da cena e no prefab. Visibilidade tem sync None e usa ownership do slot local; CanvasGroup do pai controla alpha, interação e raycasts. Textos não interceptam o pointer; GraphicRaycaster não bloqueia a UI por colliders da mesa.
Reduzidos: ProcessSlots para 20 Hz, visibilidade e timer para 10 Hz, atualização de texto do HUD somente quando muda, correção de pose de tarot ocioso para 10 Hz e apenas se deslocado. Animações continuam por frame. TestLight com range 154 desativada; CRTs passam de ForcePixel para Auto, sombra secundária removida e sombra principal com resolução Low.
Essas alterações reduzem trabalho observado; FPS e pointer em headset exigem Build & Test. Não há medição de performance real disponível neste teste local.

Teste ClientSim de menus: com slot0 local, Player1_Choices alpha=1/raycasts=true e Player2_Choices alpha=0/raycasts=false. Invertendo ownership, os valores se inverteram. Hit mudou logCount de 6 para 7 e turnIndex de 0 para 1. Durante a bateria reapareceu erro ownerPlayer em TarotVisuals; chamadas IsLocalOwner foram separadas das atribuições de campos de pickups para evitar resolução incorreta do receiver pelo MenSharp.

Regressão final após correção de TarotVisuals: partida solo iniciou com 6 cartas e 4 tarots; quatro Canvas Hit/Stay com VRCUiShape. Pickup/drop de Perfect Draw (20) foi confirmado, usedTrumpCount=1, pendingCard=null, visual TarotTable_0_0_20 criado. Console zerado antes da bateria e permaneceu sem erros. Essa regressão cobre o erro recorrente encontrado nesta revisão; não comprova FPS no headset ou rede entre clientes reais.

## Correção dos lados e transição (08/10)
Play1 foi alinhado ao lado físico de Cards-Player1/Player1_Choices, olhando para a mesa; Play2 ao lado de Cards-Player2/Player2_Choices. Cena e TwentyOne.prefab salvos. A face numérica usa uma cópia CardFaceReadable do mesh com U invertido, corrigindo o espelhamento sem alterar texturas nem materiais locked. Card.prefab salvo. MatchStatus foi separado verticalmente do ScoreText.
WorldMatchSession limpa a mesa no início da transição e só solicita StartMatch depois de transitionState == 0, teleporte concluído e startAt atingido. A abertura de seis cartas documentada é mantida.
ClientSim: durante fade prolongado de 7 segundos, transitionState=3 e teleported=true, matchStarted=false e logCount=0. Após concluir: round=1, life=[3,3], abertura=6. Clique real HitArea do Player1: cartas=7, turno=1, round=1, life=[3,3]. Screenshot Captures/solo-seat-corrected.png verifica controles no lado local, direção horizontal das faces corrigida e HUD separado do placar. Teste encerrado em Edit Mode; interação e rede entre clientes VRChat continuam pendentes.


Espaçamento ajustado: cardGap=1.55 (antes 1.0), conferido no Udon nativo da cena e do TwentyOne.prefab. As duas instâncias Card-Placeholder em Cards-Player1/2 foram desativadas e os overrides salvos no prefab; as âncoras Card-PosPlaceholder continuam ativas. O Card.prefab usado pelo dealer permanece ativo para que as cartas distribuídas sejam visíveis.

