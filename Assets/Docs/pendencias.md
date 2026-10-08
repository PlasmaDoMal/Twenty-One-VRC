# Estado do Twenty One

Cena integrada: `Assets/Scenes/VRCDefaultWorldScene.unity`.
Revisão: 07/10/2026. Esta lista substitui as pendências da cena Game de setembro.

## Implementado

- Intro -> basement -> TwentyOne, com fade e destinos Play1/Play2.
- Reservas de host/guest; Start requer dois jogadores distintos e somente o host pode iniciar.
- TwentyOne ativa antes do teleporte. Basement e visuais do intro são ocultados localmente quando deixam de ser necessários e reativados antes do retorno.
- Menu em inglês, preto e branco, sem personalização; Hit/Stay ligados ao dealer.
- Baralho único 1..11, abertura de seis cartas. Cada jogador vê a face da própria carta especial; os demais veem o verso até o resultado. Materiais são aplicados localmente conforme o dono do slot.
- Vida, aposta crescente, alvo variável, empate e os 25 efeitos de trump.
- Dois tarots por jogador; máximo oito; chance configurável após Hit; descarte por rodada.
- Tarots com pickup, descrição interpolada, retorno suave, spawn fora da mesa e consumo somente na própria vez. Histórico dos usados separado dos efeitos contínuos.
- As 25 texturas em `Docs/trump-cards` estão associadas aos materiais Tarot_01..25 pelo ID. UV preservado. O símbolo provisório é ocultado quando há textura.
- Timer padrão 60 segundos e TVs CRT com contagem/alerta. Zero desliga o timeout.
- Timeout resolve a rodada imediatamente a favor do adversário. Remove/Exchange não revertem uma rodada resolvida. Bless continua seguindo a regra de sobrevivência ao dano; não muda o vencedor.
- Fase de resultado: cartas numéricas ocultas são reveladas para todos, ações são bloqueadas e o resultado fica visível por `resultDisplaySeconds` (padrão 3 s), antes da próxima rodada ou do fim da partida.
- MatchHUD apresenta vida dos dois jogadores, rodada, aposta, vez/tempo e resultado em inglês.

## Validação

Compilação, cena/prefab e resultados dos testes desta revisão são registrados em `validacao-07-10.md`. Não confundir testes locais do ClientSim com transporte de rede real.

## Validação local ainda necessária

- Os 25 tipos passaram em cenários controlados de pickup/drop e efeito. Ainda cobrir limites de capacidade, falta de alvo e descarte em casos adversos de rede.
- Revelação conferida nos Renderers: seis faces corretas no resultado. Timeout, perda de vida, HUD, bloqueio, Bless e passagem para a rodada seguinte passaram no ClientSim.
- Reinício com a mesa ociosa passou durante a bateria. Ainda conferir reinício/saída enquanto houver animações em andamento.
- O erro interno de PlayerLoop ocorreu em uma sessão anterior com capturas MCP; a bateria final terminou sem erros no console. Evitar confundir essa ocorrência com um erro Udon confirmado.

## Ainda depende de teste com dois clientes reais

- Mouse/laser VR dos menus e pickup/drop dos dois jogadores.
- Guest enviando Hit/Stay/trump ao owner; nenhuma ação fora da vez.
- Entrada atrasada: cartas, materiais, mão de tarot, efeitos ativos e histórico usado.
- Saída do host/guest, transferência de ownership, respawn e nova partida.
- Fade e estado local dos mapas para participante e espectador.
- Performance no dispositivo alvo e build do SDK.

## Decisões atuais

Vida inicial 3; aposta inicial 1 e crescimento 1; duas trumps; oito na mão; bônus 20%; descarte por rodada. Valores editáveis no Inspector.

O gancho deixa de ser aplicado pelo timeout: não é necessário um modelo de gancho para essa regra. hookMask permanece no código das trumps por compatibilidade, sem ser gerado pelo cronômetro.

## Cuidados

Não executar `Configure World Session` ou `Improve Basement` sem revisar o código: essas ferramentas reposicionam objetos/reconstroem geometria e podem sobrescrever o layout editado pelo usuário.

Há uma correção local de SyncMetadataTable no SDK ignorada pelo Git. Uma atualização do pacote pode substituí-la.
## Revisão 08/10/2026
Alvo definido: PC. Proteções de pedidos e cancelamento implementadas; validação final interrompida por limite de uso. Consultar validacao-08-10.md antes de considerar essas proteções aprovadas.
