# Light Volumes e menu — 08/10/2026

Pacote instalado pelo usuário: VRC Light Volumes 2.1.3, em Packages/red.sim.lightvolumes.

Menu Create: Start match em Y=-180 (altura 118), Solo test em Y=-350 (altura 96, fonte 56), Leave table em Y=-510 (altura 92). Todos no plano Z=0 e alinhados em X=200. Captura de verificação: Captures/menu-solo-aligned.png. Evento SOLO TEST preservado.

Setup global em Runtime/Light Volume Manager. Volumes com nomes únicos e vinculados a seus mapas para acompanhar a ativação local:
- LV_Basement_Warehouse: 28 x 9 x 33 voxels, 2 voxels/m.
- LV_TwentyOne_GameRoom: 18 x 11 x 22 voxels, 3 voxels/m; prioridade 10.

Bounds baseados na geometria estática com margem de 0,3 m por face. Unity Progressive, Bake=true, Dynamic=false, Auto Update Volumes=false, blending 0,25 m, fallback para probes Unity, denoise/dilation habilitados. Manager e instâncias vinculados a Udon nativo, sync None. Materiais Poiyomi auditados já tinham Light Volumes habilitado; shaders comuns continuam usando probes/lightmaps Unity.

Nenhum bake foi iniciado. Texturas 3D e atlas ainda não existem: são gerados pelo bake futuro. Manter os dois mapas e volumes ativos ao Generate Lighting. Não criar luzes Point Light Volume adicionais para duplicar as luzes existentes.

Referências:
https://github.com/REDSIM/VRCLightVolumes/blob/main/Documentation/HowToUse.md
https://github.com/REDSIM/VRCLightVolumes/blob/main/Documentation/BestPractices.md

Para restaurar o pacote VPM em outra máquina, adicionar ao VCC a listagem https://redsim.github.io/vpmlisting/ e restaurar red.sim.lightvolumes 2.1.3, conforme Packages/vpm-manifest.json. O conteúdo dos pacotes VPM continua ignorado pelo Git.
