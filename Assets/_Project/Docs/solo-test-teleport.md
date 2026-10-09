# Solo Test: teleporte e respawn

Correção em 09/10/2026. Basement/Environment permanece desativado localmente durante o jogo para performance; WorldMatchSession mantém a política original de lobbyRoots e gameRoot.

Play1 e Play2 estavam em Y=-0.23, abaixo do piso Gameplay/TwentyOne/Environment/Map/Plane (Y≈0.0146). Destinos corrigidos na cena para piso + 0.05, Y≈0.0646. Referências ausentes em lobbyRoots e introVisualRoots foram removidas sem mudar os objetos válidos.

IntroMenuController.matchSession referencia WorldMatchRuntime. Ao respawn, o IntroSystem verifica launching e hostId/guestId; se o jogador participa da partida, WorldMatchSession cuida do retorno à mesa. A mesma verificação ocorre em RedirectRespawn para evitar redirecionamentos pendentes para BasementSpawn.

Validação ClientSim pelo fluxo Play → _RequestCreate → _RequestSoloStart: partida iniciada, basementActive=false, gameActive=true, teleported=true e jogador em Play1 apoiado no piso. Respawn real via LocalPlayer.Respawn retornou a Play1; IntroSystem não deixou respawnPending. Console sem erros.
