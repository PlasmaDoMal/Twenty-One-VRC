# Solo Test: teleporte e respawn

Correção em 09/10/2026. Basement/Environment permanece desativado localmente durante o jogo para performance; WorldMatchSession mantém a política original de lobbyRoots e gameRoot.

Play1 e Play2 estavam em Y=-0.23, abaixo do piso Gameplay/TwentyOne/Environment/Map/Plane (Y≈0.0146). Destinos corrigidos na cena para piso + 0.05, Y≈0.0646. Referências ausentes em lobbyRoots e introVisualRoots foram removidas sem mudar os objetos válidos.

IntroMenuController.matchSession referencia WorldMatchRuntime. Ao respawn, o IntroSystem verifica launching e hostId/guestId; se o jogador participa da partida, WorldMatchSession cuida do encerramento e do retorno ao lobby. A mesma verificação ocorre em RedirectRespawn para evitar redirecionamentos pendentes concorrentes.

Validação anterior ClientSim pelo fluxo Play → _RequestCreate → _RequestSoloStart: partida iniciada, basementActive=false, gameActive=true, teleported=true e jogador em Play1 apoiado no piso.

Regra atual: Respawn de qualquer participante durante launching pede EndTableOnRespawn ao dono da sessão. O dono valida o remetente, libera ambos os assentos, sincroniza launching=false e encerra a partida pelo fluxo existente de AbortMatch/retorno ao lobby. Respawn de espectadores apenas encerra sua visita local, sem cancelar a partida dos jogadores. O evento antigo _RespawnAtTable também encerra a partida, caso ainda esteja enfileirado.

Validação da regra atual no ClientSim Solo Test: matchStarted=true antes do Respawn real. Depois: launching=false, hostId=guestId=-1, matchStarted=false, logCount=0, teleported=false, transitionState=0. Console sem erros. A condição de retorno foi corrigida também para cancelamentos recebidos durante o teleporte, sem depender de wasParticipant permanecer verdadeiro.
