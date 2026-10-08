# Preparação de bake para PC — 08/10/2026

Cena: Assets/Scenes/VRCDefaultWorldScene.unity. Nenhum bake foi iniciado.

- Auto Generate desligado (On Demand). Configuração salva em Assets/Lighting/PC_BakeSettings.lighting.
- Progressive CPU; Baked GI ligado, Realtime GI desligado; 20 texels/unidade; atlas 2048; padding 4; amostras direct/indirect/environment 64/256/128; três bounces; AO indireto com distância 0,35 m.
- Luzes fixas do basement e TwentyOne em Baked. TestLight permanece desligada.
- Geometria fixa contribui para GI e recebe Lightmaps. Cartas, tarots, rigidbodies, Canvas, textos de gameplay e glows dinâmicos ficam fora da contribuição. Nenhum objeto foi reposicionado.
- Generate Lightmap UVs habilitado nos três FBX de cenário/mesa; três malhas locais receberam cópias com UV2 em Assets/Lighting/BakeMeshes. Materiais e UVs de textura das cartas não foram modificados.
- Light Probes separados por ambiente: Basement_LightProbes sob o Environment externo, redistribuído pelo warehouse; TwentyOne_LightProbes sob TwentyOne, redistribuído pela sala e junto aos jogadores. Removidas posições detectadas dentro de colliders sólidos (margem 0,12 m), mantidas várias alturas. Grupo da sala também atualizado no prefab TwentyOne. As duas distribuições não se sobrepõem espacialmente.
- Dois Reflection Probes Baked, resolução 128, com box projection. Nenhum RenderProbe foi executado.
- Basement e TwentyOne estão ativos no Editor para participar do bake. Os scripts continuam controlando a ativação local durante o jogo.

Quando decidir executar: fora de Play Mode, manter os dois ambientes ativos e abrir Window > Rendering > Lighting; clicar Generate Lighting. Depois conferir os probes, possíveis avisos de UV/texel e o resultado nas áreas de spawn e na mesa. A avaliação de sombras, vazamento, posição fina dos probes e performance final depende desse bake e do teste no VRChat. Não executar apenas um ambiente ativo, pois o outro pode ficar sem iluminação calculada.

WornFelt é um Surface Shader Standard: suporte de lightmap é gerado pelo compilador de Surface Shaders. Textos TMP foram excluídos da contribuição de GI.
