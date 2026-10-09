# Integração do menu na VRCDefaultWorldScene

Fluxo: IntroSystem -> BasementSpawn dentro do Cube -> TwentyOne/Play1 (host) e Play2 (guest).

WorldMatchRuntime usa WorldMatchSession (UdonSharp, sincronização manual). Reservas de host e guest são validadas pelo owner via eventos NetworkCallable. Apenas o host inicia; é necessário haver dois jogadores distintos. O dealer e Slot 0 pertencem ao host; Slot 1 ao guest. CardDealer.preserveLobbySlots evita substituir essas reservas por uma ordenação geral dos jogadores da instância.

O TwentyOne fica oculto até iniciar a partida; Create apenas reserva a mesa. O teleporte de cada participante é local, com fade-out, tela preta e fade-in. Espectadores permanecem no Cube. A saída retorna ao basement e mantém o mapa visível até concluir o fade. Respawn de participante durante partida retorna à sua posição na mesa.

Menu em inglês, preto e branco, sem cards ou personalização. MatchLobby verifica o estado a cada 100 ms em Update; isso elimina reentrada do Scheduler causada pela atualização assíncrona do lobby. A interface libera Start somente quando ambos estão presentes.

Cube: colisão côncava por MeshCollider no mesh do ProBuilder; spawn acima do piso; vigas, estrutura metálica, prateleiras, caixas, juntas e luminárias feitos com ProBuilder. Materiais em Assets/_Project/Gameplay/WorldSession/Materials. Pipe usa aço escuro.

## Validação no ClientSim

- Intro Play chegou a (-8.65, 0.20, 12.70); TwentyOne oculto.
- Teste histórico: Create reservou host 1. Na revisão de 07/10, Create conserva TwentyOne desativado; Start o ativa.
- Start bloqueado antes de haver guest; liberado com segunda reserva.
- Host chegou ao Play1 (-0.03, 0.00, 22.98), orientado para a mesa; fade concluído.
- Dealer iniciou matchStarted=true, matchOver=false, logCount=6.
- Hit pela UI do jogador 1 aumentou logCount para 7 e mudou currentPlayer para 1.
- Guest local simulado chegou ao Play2 (-0.03, 0.00, 21.14), com fade concluído.
- Guest não conseguiu iniciar a mesa.
- Participante removido voltou ao basement; não participante permaneceu no lobby.
- Leave concluiu em BasementSpawn, transitionState=0, fade desabilitado e mapa desativado. Durante saída, mapa permaneceu ativo para evitar desaparecimento antes do fade.
- Sem erros C#/Udon originados pelo fluxo testado após a correção do lobby.

Limite: ClientSim oferece um jogador local; a reserva do guest e os estados de guest/espectador foram injetados somente em Play Mode, com ghosts, e descartados ao sair. Isso valida lógica local, ownership de slots, dealer e UI; não substitui teste de transporte/sincronização dos eventos com dois clientes VRChat reais. Respawn e desconexão real não foram testados em rede.

Ferramentas de montagem: Tools/Twenty One/Configure World Session e Improve Basement. Elas reposicionam objetos e reconstroem WarehouseDetails; não executar sobre o layout atual sem revisão. Backups anteriores às alterações estão na pasta work/integration-before do chat.

## Otimização dos ambientes (revisão 07/10/2026)

TwentyOne é ativado ao iniciar a partida, antes do teleporte sob tela preta; criar a mesa sozinho não ativa o mapa. Os quatro roots do basement/menu são ocultados localmente após chegar ao TwentyOne. Na volta, o basement é ativado antes do teleporte e TwentyOne é ocultado após concluir a saída. Espectadores conservam seu lobby ativo.

IntroSafetyFloor, TEMP_WarehouseTest e BlackLobbyRoom são ocultados após o teleporte do intro. O controlador e o fade continuam ativos para concluir a transição e tratar respawn. Nenhum mapa é destruído; SetActive reduz a atualização/renderização dos objetos inativos.

## Resultado da rodada (07/10/2026)

CardDealer mantém a mesa visível por resultDisplaySeconds, revela as cartas numéricas ocultas e bloqueia Hit/Stay/trumps durante roundResolving. MatchHUD apresenta vida, aposta, rodada, vez e resultado. A próxima rodada ou matchOver só ocorre após essa pausa; WorldMatchSession então trata o retorno por fade ao terminar a partida. Timeout resolve a rodada, sem aplicar gancho.

As coordenadas acima são registros históricos do teste, não instruções para reposicionar Play1/Play2 nem BasementSpawn. Os Transforms atuais da cena são a fonte dos destinos.
## Ativação do basement no intro

lobbyRoots inclui explicitamente Environment da raiz da cena (fora do TwentyOne), seus elementos do basement e MenuSystem. O destino fica ativo no estado 3 do intro, ainda sob tela preta e antes do teleporte, e permanece ativo durante o fade-in. Gameplay/TwentyOne fica inativo até Start. Testes isolados devem restaurar WorldMatchRuntime ao terminar; não salvar estado temporário de QA.

O intro usa Update para animação/transição, com estado público de instância no MenSharp. Seleção/prévia não podem desativar o controlador quando uma referência opcional estiver vazia. Validação e áudio: validacao-07-10.md.
