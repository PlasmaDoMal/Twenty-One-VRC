# Lobby sem decoração informativa falsa

Esta revisão substitui o layout descrito em menu-industrial-09-10.md. Cena e prefab MenuSystem editados diretamente pelo MCP.

Removidos: identificação burocrática, cabeçalho duplicado dos lugares, todas as divisórias/acentos, subtítulo de clima, INITIAL TERMS, números isolados e suas legendas, três segmentos/lâmina, aviso TALLY, rodapé, legendas dos botões e títulos duplicados do lobby. Textura monocromática e Oswald preservados.

Inventário: título do jogo; dois lugares com estado dentro dos respectivos botões; Life/Stake com valores reais lidos do dealer; Ready para o host quando existir oponente; Solo test e Leave table preservados como ações funcionais da tela de quem se sentou; um toggle Steady view para reduzir movimento localmente. Sem prontidão individual inventada: a sessão atual sincroniza ocupação e início, não um flag ready por jogador.

Canvas 2400×1500; âncora/pivô central (0,5;0,5). Somente três tamanhos de texto: 140, 64 e 42.

| Elemento | Centro x,y | Tamanho w,h | Fonte |
|---|---|---|---|
| Título | -650,350 | 840×430 | 140 |
| Life / Stake | -650,-60 | 840×230 | 42 |
| Lugar 1 | 500,330 | 1100×230 | 64 |
| Lugar 2 | 500,50 | 1100×230 | 64 |
| Ready (host com oponente) | 500,-210 | 1100×120 | 64 |
| Solo test (host) | 500,-375 | 1100×90 | 42 |
| Leave table (host) | 500,-500 | 1100×90 | 42 |
| Leave table (guest) | 500,-240 | 1100×90 | 42 |
| Toggle Steady view | -650,-590 | 840×80; caixa 40×40 | 42 |

Os lugares foram retirados da tela main e colocados em SeatOne/SeatTwo sob Content, para continuarem visíveis durante a espera. Main.children está vazio. Textos do roteador permanecem fontes de dados invisíveis apenas para controles que sobreviveram; referências de textos removidos foram limpas. Animação nova tem somente título, termos, os dois lugares e toggle como alvos: fade/scale de 0,45 s e stagger de 0,08 s; preferência reduzida usa fade de 0,1 s, sem escala/deslocamento/stagger.

IndustrialMenuPresentation lê hostId/guestId e atualiza o texto/cor de cada lugar; consulta startingLife/roundDamage/roundDamageGrowth para os termos. Não escreve regras no dealer. BtnStart fica oculto sem oponente. Toggle.onValueChanged chama SetReducedMotion; o script lê Toggle.isOn. A navegação consulta a sessão novamente quando o roteador está pronto, evitando perder uma reserva feita durante a entrada do menu.

Hierarquia relevante:
```text
MenuSystem
└─ Content
   ├─ Title / TMPVisual
   ├─ Terms
   ├─ SeatOne / BtnCreate / Label / TMPVisual
   ├─ SeatTwo / BtnJoin / Label / TMPVisual
   ├─ BtnReducedMotion [Toggle] / Checkbox / MotionLabel
   └─ Screens
      ├─ Screen_main
      ├─ Screen_create / BtnStart / BtnSoloTest / BtnCreateBack
      └─ Screen_join / BtnJoinBack
```

Validação ClientSim: callback real de Seat 1 reservou host=1; com oponente simulado válido guest=2, os dois botões mostraram Occupied uma vez cada e Ready ficou ativo, interativo e alpha=1. O checkbox alterou reduceMotion=true. Leave foi acionado pelo callback real. Compilação UdonSharp passou. O erro ownerPlayer preexistente de TarotVisuals reapareceu na inicialização e permanece fora desta correção de layout. Screenshot atual: Captures/menu-clean-main.png. Unity devolvida ao Edit Mode.

