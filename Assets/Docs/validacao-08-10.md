# Validação de 08/10/2026 — alvo PC

Implementadas proteções de sequência, rodada, identidade do dono do slot e tipo esperado da carta; confirmação/rejeição de uso de tarot, retorno em expiração; cancelamento da distribuição ao abandonar a sessão e tratamento de transferência de ownership. Referências dos slots ao dealer vinculadas na cena e no prefab.

A compilação C#/MenSharp passou durante a revisão. O teste nativo de pickup/drop consumiu o tarot, mas revelou erro de referência `ownerPlayer` na apresentação da confirmação. A implementação final recria o visual usado a partir do histórico confirmado e anima desde a posição do descarte, evitando reaproveitar a referência problemática. Essa última alteração ainda exige regressão em Play Mode; não declarar o caso aprovado.

Interrupção solicitada pelo usuário por limite de uso. Ficaram sem conclusão: rejeição/expiração em runtime, capacidade máxima/ausência de alvos, reinício e saída durante distribuição, migração de dono com dois clientes, build SDK e medição de performance em PC. O painel SDK estava sem login. Nenhum mundo foi publicado. Testes ClientSim não comprovam transporte de rede real.

## Checklist para finalizar
- [ ] Regressão do pickup/drop após a última correção: descrição, confirmação, animação e retorno.
- [ ] Rejeição e expiração de pedidos sem consumo indevido; pedido atrasado de rodada anterior.
- [ ] Mão cheia, baralho vazio e efeitos sem alvo válido.
- [ ] Reiniciar ou sair durante a distribuição; reentrar e iniciar outra partida.
- [ ] Dois clientes VRChat: guest, entrada tardia, carta oculta, ownership e desconexão.
- [ ] Fluxo intro -> basement -> TwentyOne -> basement com fade e ativação local dos mapas.
- [ ] Build SDK para PC e performance no cliente real.
- [ ] Conferir a correção local de SyncMetadataTable do SDK em outra máquina: o pacote é ignorado pelo Git e a alteração não acompanha este commit.
