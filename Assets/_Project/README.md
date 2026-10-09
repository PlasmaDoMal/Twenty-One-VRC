# Assets do Twenty-One

Os assets próprios do mundo ficam nesta pasta:

| Pasta | Conteúdo |
| --- | --- |
| Art | Models e Materials |
| Audio/SFX | Efeitos sonoros |
| Docs | Documentação do jogo e registros de validação |
| Editor | Correções persistentes do compilador MenSharp |
| Environment | Lobby, Warehouse, TableRoom, iluminação e atmosferas |
| Gameplay | Scripts do jogo e WorldSession |
| Prefabs | Prefabs do projeto |
| Scenes | Cenas; a cena integrada é VRCDefaultWorldScene.unity |
| UI | MenuVR, IntroMenuVRChat e Fonts |

Os assets foram movidos junto com seus arquivos .meta, preservando os GUIDs e referências. As pastas de pacotes e ferramentas de terceiros, como Poiyomi, Bakery, TextMesh Pro, UdonSharp e VRWorldToolkit, continuam nos caminhos originais. Assets/MenSharp e Assets/SerializedUdonPrograms contêm arquivos gerados pelos compiladores.

Foram removidos os scripts de geração e alteração automática de cena que ofereciam Configure World Session, Improve Basement, Galpao VRChat, Intro Menu VRChat e Menu VR no menu Tools. Os componentes usados durante o jogo e os prefabs existentes continuam nas pastas acima.
