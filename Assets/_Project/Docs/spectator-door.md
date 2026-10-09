# Porta de espectadores

DoorTP, em Environment/basementtwentyone/Props, usa SpectatorDoorTeleport e um BoxCollider para a interação VRChat "Assistir / Spectate".

O destino é TPHere, filho do DoorGame, já posicionado em frente à porta da sala. O teleporte usa sua posição com heightOffset=0.05 e apenas a rotação horizontal, evitando a inclinação herdada do modelo da porta. O marcador pode ser reposicionado no editor.

WorldMatchSession.spectating é um estado local, sem sincronização. Mantém gameRoot ativo e desativa os lobbyRoots locais durante a visita. Não ocupa um slot nem interfere no dealer. Os jogadores que já ocupam a partida não usam essa entrada. Respawn encerra a visita e restaura o lobby; uma transição normal de partida também encerra o estado de espectador. O ambiente industrial toca enquanto o espectador está na sala.

Validação ClientSim: evento de interação do Udon compilado levou o jogador a (2.116, 0.037, 24.094), a menos de 0.00001 m do destino configurado. spectating=true, gameRoot ativo, basement local inativo, hostId/guestId preservados em -1. Respawn encerrou spectating e reativou o basement. Console sem erros no teste.
