# Estado do Twenty One

Cena integrada: `Assets/_Project/Scenes/VRCDefaultWorldScene.unity`.
Revisão: 09/10/2026. Esta lista substitui as pendências da cena Game de setembro.

## Implementado

- Intro -> basement -> TwentyOne, com fade e destinos Play1/Play2.
- Reservas de host/guest; Start normal requer dois jogadores distintos e somente o host pode iniciar. Solo test permite ao host controlar os dois slots usando o menu de Play1.
- TwentyOne ativa antes do teleporte. Basement e visuais do intro são ocultados localmente quando deixam de ser necessários e reativados antes do retorno.
- Menu e descrições das 25 trumps em inglês; UI preta e branca, sem personalização; Hit/Stay ligados ao dealer.
- Baralho único 1..11, abertura de quatro cartas (duas por jogador, primeira oculta). Cada jogador vê a face da própria carta especial; os demais veem o verso até o resultado. Materiais são aplicados localmente conforme o dono do slot.
- Vida 20, aposta inicial 1 e +1 por rodada, alvo variável, empate e os 25 efeitos de trump. Rodada termina por duas passadas seguidas sem trump ou timeout. Estouro bloqueia Hit; cartas ocultas são reveladas e vencedor comparado apenas na resolução.
- Dois tarots por jogador; máximo oito; chance configurável após Hit; descarte por rodada.
- Tarots com pickup, descrição interpolada, retorno suave, spawn fora da mesa e consumo somente na própria vez. Histórico dos usados separado dos efeitos contínuos.
- As 25 texturas em `Docs/trump-cards` estão associadas aos materiais Tarot_01..25 pelo ID. UV preservado. O símbolo provisório é ocultado quando há textura.
- Timer padrão 60 segundos e TVs CRT com contagem/alerta. Zero desliga o timeout. Intervalo padrão de 2 s entre turnos, sincronizado pelo relógio do servidor, sem consumir o tempo da próxima vez.
- Timeout encerra a rodada com derrota de quem deixou o tempo esgotar. Remove/Exchange não revertem uma rodada resolvida. Bless continua seguindo a regra de sobrevivência ao dano; não muda o vencedor.
- Fase de resultado: cartas numéricas ocultas são reveladas para todos, ações são bloqueadas e o resultado fica visível por `resultDisplaySeconds` (padrão 5 s), antes da próxima rodada ou do fim da partida.
- MatchHUD apresenta vida dos dois jogadores, rodada, aposta, vez/tempo e resultado em inglês.

## Validação

Compilação, cena/prefab e resultados dos testes desta revisão são registrados em `validacao-08-10.md`; `validacao-07-10.md` é histórico. Não confundir testes locais do ClientSim com transporte de rede real.

## Validação confirmada pelo usuário (09/10/2026)

- Jogo com dois clientes reais: funcionando, conforme relato do usuário.
- Interação em VR: funcionando, conforme relato do usuário.
- Performance no PC: aprovada pelo usuário; sem medição numérica de FPS registrada.
- Bake ainda não realizado. Preparação documentada em bake-preparacao-PC.md e lightvolumes-menu-08-10.md.
- Testes locais do ClientSim e correções: validacao-08-10.md. Registros anteriores são históricos.

## Pendências específicas

- Realizar e conferir bake de lightmaps, probes e Light Volumes quando solicitado.
- Casos extremos sem validação específica registrada: reinício/saída durante animações, limite de trumps e efeito sem alvo. Funcionamento geral em rede/VR já confirmado.
- Garantir reprodução da correção local de SyncMetadataTable em outras máquinas e após atualização do SDK.
- MenSharp CompileAll corrigido com fallback por entry point para a versão 0.1.3, reaplicado automaticamente pelo patch em Assets/Editor. Compilação completa validada; detalhes em validacao-08-10.md.

## Decisões atuais

Vida inicial 20; aposta inicial 1 e crescimento 1; duas trumps; oito na mão; bônus 20%; descarte por rodada. Valores editáveis no Inspector.

O gancho deixa de ser aplicado pelo timeout: não é necessário um modelo de gancho para essa regra. hookMask permanece no código das trumps por compatibilidade, sem ser gerado pelo cronômetro.

## Cuidados

Não executar `Configure World Session` ou `Improve Basement` sem revisar o código: essas ferramentas reposicionam objetos/reconstroem geometria e podem sobrescrever o layout editado pelo usuário.

Há uma correção local de SyncMetadataTable no SDK ignorada pelo Git. Uma atualização do pacote pode substituí-la.
