> Histórico de 07/10. Em 09/10 o usuário confirmou funcionamento de rede com dois clientes reais, VR e performance no PC. Bake ainda não realizado. Estado atual: pendencias.md e validacao-08-10.md.

# Validação — 07/10/2026

## Materiais

25/25 texturas de `Assets/Docs/trump-cards` associadas ao MainTex dos materiais Tarot_01..25, seguindo IDs de tarot-cards.md. Cores de tint em branco; UV e shader existentes preservados. Logo provisório ocultado quando há textura.

## Intro e ambientes

Teste real dos callbacks compilados no ClientSim:

- Antes de Play: initialized=true, readyForPlay=true, IntroMenuRoot ativo, Environment externo inativo, TwentyOne inativo.
- Após Play e fade: Environment externo ativo, TwentyOne inativo; jogador em BasementSpawn, posição física (-0.10, 0.00, 2.03).
- IntroMenuController continuou ativo no estado 5; não foi desativado ao clicar.
- Corrigidos campos de estado MenSharp para públicos, polling em Update e referências GameObject opcionais. No Udon, uma referência GameObject vazia pode resolver para o próprio objeto; a seleção não pode desativar o controlador e a prévia não deve comandar a ativação do intro.
- lobbyRoots inclui Environment externo da raiz, e não Gameplay/TwentyOne/Environment. Basement é ativado no estado 3, sob tela preta e antes do teleporte.
- Botão Play vinculado ao UdonBehaviour compilado atual.
- Cinco sons curtos de interface gerados localmente e atribuídos: LogoEntry, LogoRelocation, PlayHover, PlayClick, TransitionWhoosh. As fontes estão ativas, com clips válidos, volume 0.5 e áudio 2D.

## Regras e HUD

Timeout agora resolve imediatamente a rodada a favor do adversário. roundResolving bloqueia ações durante a apresentação. Cartas ocultas são reveladas para todos durante três segundos antes da próxima rodada/fim da partida. MatchHUD mostra rodada, aposta efetiva da rodada resolvida, vida, vez e resultado em inglês.

A bateria inicial dos 25 tipos foi interrompida. A retomada abaixo registra os testes curtos efetivamente concluídos; não equivale à aprovação dos 25 efeitos.

## Limites

ClientSim oferece um jogador local. Jogadores remotos e ownership usados na validação são simulações. Transporte de rede com dois clientes VRChat reais, Quest/performance e apontamento físico do laser continuam pendentes.
## Interrupção da validação dos tarots

A sequência automatizada foi interrompida por perda da conexão MCP e falta de resposta do editor Unity. Último teste completo registrado: tipo 1 com pickup/descrição, consumo e colocação na mesa. A bateria dos 25 tipos e o teste final de timeout não foram concluídos; não há validação suficiente para marcá-los como aprovados. As alterações de regras/HUD compilaram, mas timeout, revelação e transição entre rodadas ainda exigem teste funcional.

O teste suspendia o WorldMatchRuntime somente em Play Mode e alterava ownership/estado do dealer localmente. Esses estados não foram salvos na cena. Parar/reiniciar o Play Mode descarta a simulação e restaura o fluxo de intro/lobby salvo.


## Retomada após reiniciar o Unity

Testes curtos no ClientSim, sem reconstruir o histórico do owner durante animações:

- Intro pronto e Play: estado 5, Environment externo ativo antes de iniciar a mesa.
- Host local + guest simulado: StartTable ativou o jogo e teleportou o host para Play1 (-0.03, 0.29, 23.18). O guest foi reservado pelo MCP; sua interação remota e teleporte não foram comprovados.
- Timeout de teste 5 s: roundResolving=true, lastTimeoutPlayer=0, lastRoundWinner=1, vida passou de 3/3 para 2/3. Ações bloqueadas e HUD dos dois lados mostrou PLAYER 2 WINS / TIME OUT. A fase seguinte chegou à rodada 2 e reabriu ações após distribuição.
- Tarot tipo 7 da rodada 2: callback _onPickup elevou a descrição a alpha=1 (texto preenchido); drop fora da mesa voltou ao spawn com distância zero e alpha=0.
- Drop na mesa: usedTrumpCount=1, onTable=true, animação encerrada no marcador (0.13, 1.11, 22.54). Novo pickup/drop da carta usada voltou ao mesmo homePosition (distância zero); usedTrumpCount permaneceu 1.
- Sons também atribuídos ao prefab IntroSystem; 25/25 materiais ainda têm textura válida.
- Play Mode encerrado. Cena salva com timeout 60 s, resultado 3 s, Runtime ativo, launching=false, host/guest=-1. Ajustes de teste descartados.

Não validado nesta retomada: todos os 25 efeitos, inspeção visual da revelação das cartas numéricas, reinício durante animação e transporte com dois clientes VRChat reais.

O console apresentou erros internos de PlayerLoop recursivo durante esta sessão com capturas MCP. Não houve erro Udon reportado na consulta anterior às capturas; não há evidência suficiente para atribuir a causa. O editor permaneceu acessível e saiu do Play Mode normalmente.


## Bateria concluída e auditoria MenSharp/rede

Os resultados anteriores acima são históricos. Esta seção é o estado mais recente; evidências antes/depois em `qa-07-10.json`.

25/25 tipos passaram em cenários controlados no ClientSim pelo callback nativo de pickup/drop, consumo autenticado via PlayerSlot e efeito no dealer. Todos também passaram em descrição alpha=1, material por ID e interpolação até o marcador. Tipos 1..6 foram testados com o número solicitado disponível no baralho. Go For substituiu outro alvo ativo; os valores calculados de alvo e aposta foram conferidos diretamente no Udon. Destroy/Reincarnation foram testados com duas cartas contínuas adversárias; Friendship, Hush, Perfect Draw, Remove, Return, Exchange, Disservice e Refresh alteraram as mãos/históricos conforme esperado.

A falha aparente do Reincarnation era do teste: ele criava vetores de mesa com 16 entradas, embora a cena configure 32. EnsureTrumpBuffers corrigia a capacidade ao comprar a trump e zerava o estado inválido. Corrigido o cenário, a regra original passou: uma destruição e uma compra extra. Não foi necessário mudar essa regra. A consulta de Friendship também foi ajustada para distinguir a carta usada de uma nova carta na mão com o mesmo tipo.

Correção real: TarotPickup mantém a posição/rotação de repouso mediante setters em cardTransform; isso impede deslocamento após a liberação no simulador. A verificação final dos marcadores teve distância zero.

### Carta especial local

CardDealer aplica o material de face para o dono do slot, e card-hidden para os demais enquanto a carta estiver marcada como oculta e a rodada não estiver no resultado. A aplicação inicial, OnDeserialization e atualização local a cada 0,2 s usam essa mesma regra, inclusive quando o ownership dos slots muda. O material não é sincronizado: cada cliente monta sua apresentação. Os efeitos que produzem cartas ocultas, como Hush, mantêm sua marca original.

Teste com ownership simulado alternado:

- Slot 0 local: carta 1 visível para ele; primeira carta do slot 1 (2) com verso.
- Slot 1 local: carta 2 visível para ele; primeira carta do slot 0 (1) com verso.
- Espectador: dois versos; demais quatro cartas com faces corretas.
- Resultado por timeout: zero versos, seis faces corretas e ações bloqueadas.
- Bless em cenário de dano fatal: vida do perdedor ficou 1 e aposta base caiu de 3 para 2.

Isso comprova ocultação visual local. Os valores numéricos ainda fazem parte do estado Udon sincronizado necessário à reconstrução; não se trata de sigilo dos dados contra um cliente modificado.

### Compilação e wiring

Lido `MenSharp.md`, inclusive o fluxo salvar -> compilar UdonAssembly -> callbacks, ownership, RequestSerialization e OnDeserialization. Executado Rebuild All Programs; nenhuma mensagem de erro de compilação. Os 36 componentes MenSharp da cena têm programa compilado e backing UdonBehaviour corretos. WorldMatchSession referencia os programas reais de IntroMenuController e CardDealer. A última execução em Play Mode terminou sem erros no console.

Metadata do programa carregado, não somente atributos no C#:

| Programa | Modo | Campos sincronizados |
| --- | --- | ---: |
| CardDealer | Manual | 32 |
| PlayerSlot (cada um) | Manual | 3 |
| WorldMatchSession | Manual | 5 |
| Intro, tarot visual/pickup, menus, HUD e fades | None / apresentação local | 0 |

Dealer sincroniza seed/log numérico, dono/estado das cartas, mãos e históricos de tarot, vez, vida, aposta, timeout e resultado. Slots sincronizam sequência/tipo/argumento da intenção; somente o owner do dealer processa essas intenções. Sessão sincroniza host, guest, início e epoch; os eventos Create/Join/Start/Leave do UdonSharp possuem NetworkCallable e validam o caller no owner. Interfaces usam o estado recebido e se atualizam localmente.

### Limites atuais

Não há dois clientes VRChat reais disponíveis nesta sessão. A auditoria e os testes ClientSim não confirmam transporte real, atraso/rejeição de intenções, entrada tardia, troca de ownership, reconexão, saída do host/guest, Quest ou build/upload. Também não cobrem todos os casos sem alvo ou limites de capacidade de cada efeito. Portanto não é correto afirmar que todos os scripts funcionarão em toda situação de rede somente porque compilaram.

Play Mode encerrado; ajustes de teste não salvos. Cena mantém timeout 60 s, resultado 3 s e host/guest não reservados.
