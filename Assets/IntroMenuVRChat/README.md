# Menu inicial VRChat — Twenty One

Cena `Assets/Scenes/GameMap.unity`, Unity 2022.3.22f1, VRCSDK Worlds e MenSharp.

## Migração para MenSharp

`IntroMenuController` e `LogoIntroAnimator` herdam de `MenSharpBehaviour`.
A pasta `Scripts` está marcada com `.mensharp`; os programas ficam em
`Scripts/Programs`. Os programas UdonSharp antigos foram removidos.
O botão e os eventos de hover da cena e do prefab apontam para o programa
MenSharp do controlador. Os UdonBehaviours gerados continuam necessários
para executar MenSharp no VRChat e ficam ocultos pelo editor MenSharp.

A atualização por quadro e os adiamentos usam `Scheduler`. O acompanhamento
do fade usa `PostLateUpdate`. Para forçar a atualização do programa serializado
após uma migração, use `MenSharp > Rebuild All Programs`.
As ferramentas de validação leem os UdonBehaviours em execução porque os
componentes de autoria MenSharp são removidos antes do ClientSim.

Os relatos abaixo documentam a montagem original. Os arquivos de `Backups`
são cópias históricas anteriores à migração.

## Objetos criados

IntroSystem
- IntroSpawn: spawn inicial em (0, 0.2, -30).
- WarehouseSpawnPlaceholder: destino em (0, 0.15, 12).
- IntroMenuRoot / MenuCanvas: Canvas World Space, GraphicRaycaster, VRCUiShape e CanvasGroup.
  - Logo: título temporário TWENTY ONE.
  - PlayButton / PlayText: único botão JOGAR.
  - PlayButton / SelectionIndicator: ponto branco suave para hover/seleção.
- BlackBackdrop: blackout local de tela inteira.
- FadeSystem / FadeVisual: fade local independente.
- IntroSafetyFloor: collider de apoio invisível para a área inicial.
- IntroMenuController: comportamento MenSharp e três AudioSources filhos:
  PlayHover, PlayClick, TransitionWhoosh, todos sem clipes e sem playOnAwake.
- TEMP_WarehouseTest: TestFloor, TestWall e TestLight. Pode ser removido após preparar o destino real.

Os cinco objetos originais da cena foram preservados. A referência de spawn do VRCWorld foi alterada para IntroSpawn, necessária para esta entrada. Nenhuma alteração de iluminação global ou configuração de projeto.
Backup anterior: Assets/IntroMenuVRChat/Backups/Menu_BeforeIntro.unity.
O blackout e o fade estão ocultos apenas na Scene View pelo recurso de visibilidade do Editor, para permitir editar o mapa; continuam ativos no jogo.

## Arquivos

Todos os assets ficam em Assets/IntroMenuVRChat/:
- Scripts/IntroMenuController.cs: lógica local, estados, imobilização, áudio opcional, teleporte e respawn.
- Scripts/Programs/IntroMenuController.asset e LogoIntroAnimator.asset: programas MenSharp.
- Editor/IntroMenuSetup.cs: montagem pelo menu Tools > Intro Menu VRChat > Create System; recusa duplicar IntroSystem.
- Editor/IntroMenuValidation.cs: verificação automatizada somente no Editor.
- Materials/LocalBlackout.shader e MenuTypography.shader, materiais do blackout, fade, tipografia e área temporária.
- UI/FullViewportQuad.asset e SelectionDot.png.
- Prefabs/IntroSystem.prefab.

O título usa texto editável; não é um logo desenhado. A fonte é Lato Bold, já fornecida pelo SDK, comprimida horizontalmente para a aparência condensada. Não havia vídeo ou logo anexado, apenas o documento de requisitos; a composição seguiu suas medidas, adaptadas ao Canvas de 2,5 m a 2 m do jogador.

## Inspector

Selecione IntroSystem/IntroMenuController:
- introSpawn: IntroSpawn.
- warehouseSpawn: WarehouseSpawnPlaceholder.
- introMenuRoot: IntroMenuRoot.
- blackoutRoot: BlackBackdrop.
- fadeRenderer: FadeVisual.
- playButton, menuGroup e selectionIndicator: referências de UI preenchidas.
- hoverAudio, clickAudio e transitionAudio: AudioSources preenchidos; arraste os clipes nos respectivos campos AudioClip dos AudioSources.
- immobilizePlayer: true.
- fadeToBlackDuration: 0.8 s.
- blackHoldDuration: 0.2 s, mínimo efetivo de 0.15 s.
- postTeleportDelay: 0.1 s, mínimo efetivo de 0.1 s.
- fadeFromBlackDuration: 1.0 s.
- menuRevealDuration: 0.4 s.

introCompleted e state são locais, não sincronizados e ocultos no Inspector.
O comportamento usa BehaviourSyncMode.None, não envia eventos de rede nem usa VRC Object Sync.
O botão chama SendCustomEvent("Play") no UdonBehaviour local.

## Fluxo

Entrada com blackout ativo desde a cena -> menu aparece -> jogador local imobilizado.
Clique -> bloqueia novas interações -> SmoothStep até alpha 1 -> espera em preto -> teleporte local em evento adiado -> oculta menu e blackout inicial -> espera 0.1 s -> fade abre -> libera movimento.
Respawn antes da conclusão permanece na introdução; depois do clique, a conclusão é retida na sessão e o destino é usado sem reabrir o menu.
Uma nova entrada na instância reinicia o estado.

O blackout usa um quad em clip space com macros de estéreo, ZTest Always, Cull Off e limites amplos para evitar recorte por frustum. Não move a câmera.
Ordem de renderização explícita: blackout 32765, menu 32766, fade 32767; filas 4997, 4998 e 5000.
Não reduzir essas ordens sem rever a composição: o primeiro teste identificou texto acima do fade e isso foi corrigido e retestado.

## Validação executada

Compilação C# e UdonSharp sem erros; Console final sem erros ou avisos.
Houve inicialmente cache incompleto de referências netstandard no compilador UdonSharp; a reconstrução do cache em memória resolveu, sem modificar o SDK. A compilação foi repetida após recarregar scripts e continuou sem erros.

18 verificações automatizadas aprovadas no ClientSim:
1. Estado inicial aguarda JOGAR.
2. Menu e blackout inicialmente ativos.
3. Jogador local imobilizado.
4. Sincronização Udon desativada.
5. Botão bloqueado imediatamente.
6. Evento do botão inicia fade.
7. Segundo clique não reinicia nem pula etapas.
8. Captura renderizada em preto total: todos os pixels RGB zero.
9. Teleporte observado somente depois de preto total, com alpha exatamente 1.
10. Menu desativado depois do teleporte.
11. Imobilização mantida durante a transição.
12. Teleporte ocorreu.
13. Conclusão retida localmente.
14. Fade termina transparente e renderer desativa.
15. Movimento liberado após o fade.
16. Chegada ao destino referenciado.
17. Respawn usa destino movido sem alterar código.
18. Respawn não reabre menu.

Tempos observados:
- 0.800 s: preto total.
- 1.001 s: teleporte pendente.
- 1.010 s: teleporte concluído e espera para abrir.
- 2.111 s: fim do fade e movimento liberado.

O destino movido durante o teste foi apenas uma alteração de Play Mode e não foi salvo.
A captura menu-final.png é uma prévia renderizada no Editor; fully-black.png é a captura do teste.
validation.txt contém o relatório bruto.

## Como testar

1. Abra Assets/Scenes/GameMap.unity e entre em Play Mode com ClientSim habilitado.
2. Aceite ou feche a janela própria do ClientSim. Essa interface pertence ao simulador e não ao menu criado.
3. Confira preto atrás de TWENTY ONE e JOGAR; tente caminhar e olhar ao redor.
4. Clique JOGAR repetidamente: deve haver uma única transição e movimento somente depois da abertura.
5. Use Respawn: deve voltar ao destino sem mostrar o menu.
6. Pare Play Mode antes de editar posições ou clipes.
7. No VRChat SDK, faça Build & Test para validar no cliente real.
8. Em headset, confira ambos os olhos, bordas, olhar para trás/cima/baixo, room scale e ponteiro de cada mão.
9. Em uma instância com dois clientes, mantenha B no menu enquanto A joga; teste também o respawn de A.

Limites da validação: não foi executado um headset VR nem uma instância real com dois clientes. Suporte de shader a estéreo e ausência de sincronização foram implementados, mas não substituem esses testes. O teste automático aciona o UnityEvent do botão; não valida por si só o raycast de um controle VR. Interfaces nativas do VRChat/ClientSim não são controladas pelo sistema de mundo. Novos shaders/UI com prioridade de renderização superior precisam ser testados junto ao blackout.

## Quando o galpão estiver pronto

Mova WarehouseSpawnPlaceholder para um ponto seguro sobre o chão do galpão e ajuste sua rotação, ou atribua outro Transform ao campo warehouseSpawn.
Remova TEMP_WarehouseTest quando houver chão/collider real no destino.
Não é necessário alterar código.
Troque o texto Logo ou substitua seu visual no Canvas mantendo o material/ordem compatíveis com a transição.
