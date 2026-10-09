# SFX do Twenty One

Configuração em 09/10/2026. AudioSources nomeados em filhos SFX; podem ser reposicionados pelo Transform. Volume, distâncias e rolloff ficam nos AudioSources. Play On Awake e Loop ficam desligados, exceto Loop no ambiente industrial.

| Objeto | Evento | Arquivo | Espaço |
| --- | --- | --- | --- |
| IntroSystem/SFX/IntroPlay_2D | Play aceito na introdução | 613410 button-7 | 2D |
| IntroSystem/SFX/BasementArrival_3D | Teleporte inicial ao basement | 661726 light-switch-turn-on-flicker | 3D |
| MenuSystem/SFX/MenuClick_2D | Botões e toggle do menu | mixkit interface-device-click-2577 | 2D |
| TwentyOne/SFX/HitStayClick_2D | Hit/Stay válidos do usuário local | mixkit modern-technology-select-3124 | 2D |
| TwentyOne/SFX/CardHandling_3D | HIT aceito pelo dono do dealer | 817551 pickupcard05 / 817579 slidecard04 | 3D |
| TwentyOne/SFX/CRTStartup_3D | Partida começa | 415594 crt-computer-monitor-startup | 3D |
| TwentyOne/SFX/TVSwitchOn_3D | Junto ao startup do CRT | 869535 tv_switch_on | 3D |
| TwentyOne/SFX/IndustrialAmbient_2D | Enquanto a partida estiver ativa | 812417 industrial-ambient-loop | 2D, loop |
| TwentyOne/SFX/RandomEcho_3D | Chance local rara durante a partida | 187523 fx-ambient-echo-9 | 3D |

IntroMenuController: clickPitch=0.75 (abaixa o pitch do Play), basementArrivalPitch=1.
IndustrialMenuPresentation: menuPitchMin=0.9 e menuPitchMax=1.1.
TwentyOneSfx (no objeto TwentyOne/SFX): choicePitch=1, startupPitch=1, cardPitchMin=0.9 e cardPitchMax=1.1. Os dois sons de carta têm primeira variante aleatória e depois alternam para evitar repetição consecutiva. O AudioSource CardHandling recebe o clip em runtime; seus dois AudioClips ficam no controlador.
Eco: ambientChance=0.05 (5%), ambientIntervalMin=90 s, ambientIntervalMax=180 s. Não toca ao entrar no jogo; a primeira tentativa ocorre após o intervalo. Não sobrepõe outro eco. Cada cliente sorteia localmente; não altera a rede da partida. Loop e eco param ao terminar/sair da partida.
O hitSoundSequence sincronizado no CardDealer avança apenas depois de um HIT que distribuiu carta. TwentyOneSfx acompanha esse contador e toca o som localmente para cada cliente; late join não reproduz hits antigos. Compras da abertura e por trump não são tratadas como HIT.
Fontes 3D possuem VRCSpatialAudioSource, Doppler=0 e clips mono. Ambiente industrial é stereo com Streaming. Som de UI é local e não é retransmitido aos demais jogadores.

Validação ClientSim: Play reproduziu button-7 em pitch 0.75; teleporte reproduziu light-switch; vários cliques de menu registraram pitches de 0.911 a 1.066. Startup CRT e tv_switch_on registrados simultaneamente, junto ao loop industrial. Cliques reais em Hit dos dois lados reproduziram o select; os dois clips de carta foram registrados com pitches 0.941 e 1.092. Raridade final confirmada em runtime: chance=0.05, intervalo=90..180. Console sem erros após compilação. richText dos lugares foi movido para configuração do prefab porque a propriedade não é exposta ao Udon.
