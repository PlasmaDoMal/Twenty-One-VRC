# MatchStatus

MatchHUD separa anúncio de rodada e anúncio de turno. ROUND N aparece uma vez na abertura de cada rodada. Depois entra YOUR TURN no display do assento local quando CardDealer.CanLocalPlayerAct libera a jogada. O outro display fica vazio. Cada turno é identificado por actionEpoch, impedindo repetição devido ao timer, polling, cartas ou término do fade.

Sem aposta, vidas, timer ou resultado. Fade in 0.32s, permanência visibleSeconds (2.5s), fade out 0.22s; deslocamento local 0.035. Após expirar, o mesmo turno permanece oculto. Se o aviso da rodada expirar durante a distribuição, YOUR TURN aguarda a liberação da jogada. Desativação limpa os anúncios para a próxima partida.

Validação ClientSim com mudanças controladas de turno: ROUND 1 nos dois displays; em seguida YOUR TURN apenas no P1; expiração deixou ambos vazios e polling não repetiu; próximo turno mostrou YOUR TURN apenas no P2; retorno ao P1 mostrou YOUR TURN, sem repetir ROUND 1. Console sem erros. Alterações temporárias de QA não foram salvas.
