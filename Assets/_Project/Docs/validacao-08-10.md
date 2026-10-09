## Estado atual — 09/10/2026
O usuário confirmou funcionamento com dois clientes reais, interação em VR e performance no PC. Esses são testes relatados pelo usuário, distintos das baterias ClientSim abaixo. Bake ainda não realizado. Vida 20, abertura 4, timeout perde a rodada, estouro bloqueia Hit sem revelar/encerrar, pausas 2 s/5 s. Descrições das 25 trumps traduzidas para inglês. Os registros abaixo preservam o histórico; valores e pendências de datas anteriores podem ter sido substituídos por esta revisão.

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


## Revisão de runtime (09/10/2026)
Identificados 64 componentes Udon duplicados, sem programSource mas com programa serializado, incluindo múltiplos CardDealer na mesma mesa. Sessão iniciava uma instância enquanto CRT/UI liam outra. Referências (175) religadas à instância atual, duplicatas removidas; scene e prefabs MenuSystem/TwentyOne/IntroSystem salvos. Após MenSharp CompileAll permaneceu um CardDealer, um CRTScreenTimer e um MatchHUD na mesa.
Play: rótulo explícito, visível no Create, sem ocultação por ausência de oponente. Callback Activate para MenuButton atual. ClientSim com host=1/guest=2: clique real Button.onClick mudou launching=false para true, botão alpha=1/interactable=true.
CRT: resultado tem prioridade sobre contagem. Shader suporta DRAW, P1 WON, P2 WON, além de YOU WON/LOST para dois jogadores. FinishRound ignora resolução duplicada, zera deadline e impõe mínimo de 3 s. Observador ClientSim capturou _Message=4 (P1 WON) e 2.893 s restantes ao primeiro frame em que a mensagem apareceu. Primeira Stay não causou dano; segunda resolveu e a rodada seguinte abriu normalmente. Timeout segue apenas passando a vez; timer de 60 s mantido.
Seats: além de OnPlayerLeft/OnOwnershipTransferred, proprietário verifica IDs órfãos a cada 0.5 s. Saída real do remoto no ClientSim liberou guestId de 2 para -1, manteve hostId=1 e launching=false.
Tarots (correção posterior): o SDK inicializava referências Udon serializadas vazias como o próprio TarotVisuals; pendingCard apontava para o gerenciador antes do Start e ResolvePendingUse tentava ler ownerPlayer nele. Start agora limpa explicitamente pendingCard, incomingDroppedCard e lastDroppedCard; Update ignora arrays vazios antes da inicialização. Removido também um Udon duplicado do Tarot.prefab. MenSharp CompileAll concluído. ClientSim: quatro tarots criados, pendingCard=null; evento _onPickup mostrou descrição com alpha=1; _onDrop iniciou interpolação e retornou à posição salva, descrição alpha=0. Uso de Perfect Draw (20) pela carta foi aceito: usedTrumpCount=1, pendingCard=null e TarotTable_0_0_20 criado. Console limpo antes da bateria permaneceu sem erros. Teste encerrado em Edit Mode. Não realizado bake nem publicação. Rede entre clientes reais continua pendente.


## Intervalo entre turnos e derrota por timeout (09/10/2026)
A pedido, AdvanceTurn aplica intervalo padrão de 1 s via turnReadyAt sincronizado pelo relógio do servidor. Ações ficam bloqueadas durante a pausa; o deadline da próxima vez só começa depois dela e da animação das cartas. NotifyTurnChanged também é avaliado no polling para reabrir controles em todos os clientes ao terminar a pausa. Timeout agora encerra a rodada com vitória do adversário e dano normal da aposta, seguindo FinishRound e a fase de resultado de no mínimo 3 s; substitui a regra anterior de apenas passar a vez.
ClientSim: Stay do P1 iniciou pausa de 1 s, ações bloqueadas e timer=0; controles voltaram após a pausa. Timeout reduzido a 2 s apenas no teste: 2.019 s após liberar controles, P2 perdeu, vencedor=0, vida=[3,2], resultado com 3 s restantes. Console sem erros. Teste encerrado; configuração persistida mantém timeout de 60 s. MenSharp CompileAll concluído; rede entre dois clientes reais ainda pendente.

Ajuste de ritmo solicitado depois: intervalo entre turnos aumentado para 2 s e exibição do resultado entre rodadas para 5 s. Valores gravados no CardDealer da cena e no TwentyOne.prefab; timer de jogada permanece 60 s.

## Spawn das trump cards (09/10/2026)
Reproduzido: quatro cartas existiam, mas os spawns estavam invertidos (slot0 ligado ao marcador Player2) e HandPosition usava BoxCollider.bounds desativado. Esses bounds vazios colocavam as cartas no pivot y=0.36, dentro dos suportes. Referências corrigidas para Player1/Player2; posição agora calculada por center/size transformados, incluindo a extensão vertical e a espessura da carta, independentemente do collider estar habilitado.
MenSharp CompileAll concluído. ClientSim: quatro cartas ativas e pickupable; slot0 sobre suporte Player1 em y=0.80/z=21.13; slot1 sobre Player2 em y=0.80/z=23.33. Console sem erros; teste encerrado em Edit Mode.

## Configuração base e estouro sem resolução antecipada (09/10/2026)
Aplicada a configuração solicitada: vida 20, abertura de quatro cartas (duas por jogador, primeira especial/oculta), baralho único 1..11 refeito e embaralhado por rodada, aposta 1 com crescimento +1, duas trumps por jogador e bônus de 20% após Hit. Preservadas pausas de 2 s entre turnos e 5 s de resultado. Sem limite de rodadas. Validação da abertura alterada de mínimo três para duas cartas por jogador.
Correção posterior do pedido: estouro não termina a rodada, não causa dano e não revela cartas. AcceptHit e processamento autoritativo do Slot recusam compra quando HandTotal supera EffectiveTarget; a compra que estoura ainda passa a vez normalmente. Stay e trumps permanecem permitidos. Resolução ocorre apenas por duas passadas sem trump ou timeout, com comparação das mãos no fim e revelação via RevealRoundCards. Se uma trump reduzir a soma ou aumentar o alvo para uma situação válida, Hit volta a ser permitido.
MenSharp CompileAll concluído. ClientSim: abertura=4, vida=[20,20], duas entradas ocultas e quatro trumps. Cenário controlado de estouro usando alvo temporário 1: Hit recusado sem aumentar logCount, roundResolving=false, entradas ocultas preservadas. Duas Stay depois encerraram a rodada com vida=[19,20] e 5 s de resultado. Console sem erros; alvo 21 restaurado, teste encerrado e scene/prefab salvos. A estimativa de duração fornecida pelo usuário não foi validada em partidas com trumps ou rede real.

## Luzes CRT em Mixed (09/10/2026)
As sete luzes CRT_Bounce_South, CRT_Bounce_North (incluindo cópia) e CRT_Fill_South/CRT_Fill_North (incluindo cópias) foram vinculadas em CRTScreenTimer.screenLights e configuradas como Mixed. Cor inicial igual a normalTint; color temperature desligada para respeitar essa cor; intensidades e alcances preservados. Cena e TwentyOne.prefab salvos. O mesmo Apply que atualiza _Tint aplica Light.color quando muda o segundo ou a mensagem, localmente a partir do estado do dealer.
ClientSim: estados 60 -> 5 -> 60 segundos; sete de sete luzes acompanharam exatamente _Tint, normal (0.72,0.79,0.80) -> alerta (0.95,0.35,0.28) -> normal. Todas permaneceram Mixed; console sem erros na bateria final. Nenhum bake realizado.
CompileAll do MenSharp apresentou falha global (19 programas, sem diagnóstico específico), deixando o programa antigo. Compilação individual de CRTScreenTimer via compiler.rsp com --emit-udon CRTScreenTimer funcionou; importado com MenSharpImporter.CreateOrUpdate no asset existente, GUID preservado, referências transferidas e comportamento validado. A falha do modo CompileAll permanece para investigação separada.

## CompileAll corrigido (09/10/2026)
A emissão --emit-udon-all do binário MenSharp 0.1.3 recusava 19 programas sem diagnóstico específico; emissão por entry point funcionava. Adicionado fallback em RunCompiler para compilar as classes MenSharp de cada MonoScript com --emit-udon, preservando a emissão de estáticos e restaurando o campo source no metadata individual para manter a pasta de cada programa e seus GUIDs. O fluxo original de importação/rebuild permanece em Compile.
Correção persistente em Assets/_Project/Editor/MenSharpCompileAllPatch.cs e .json: aplicada ao pacote VPM 0.1.3 no carregamento ou pelo menu Tools/TwentyOne/Repair MenSharp Compile All; não sobrescreve outras versões ou fontes inesperadas. Pacotes VPM continuam ignorados pelo Git, mas o instalador do patch é versionável.
CompileAll validado: 20 programas (19 scripts + MenSharp.Statics); nova execução 20 unchanged, zero erros. UdonSharp CompileAllCsPrograms também concluído sem erros. Assets de programa nas pastas originais; cena e prefabs reimportados, zero Udon nativo sem programSource. Nenhum bake realizado.


## Revisão CRT para Bakery (09/10/2026)
A configuração Mixed acima foi substituída a pedido do usuário: sete luzes CRT em Realtime, sem BakeryPointLight ou outros componentes Bakery Light. A cor continua controlada por CRTScreenTimer.screenLights. Cena e TwentyOne.prefab atualizados; nenhum bake executado.
