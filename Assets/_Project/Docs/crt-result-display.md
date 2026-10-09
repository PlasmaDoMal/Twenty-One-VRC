# Resultado nas TVs

CRTScreenTimer usa as mensagens já existentes em CRTReference: YouLost, YouWon, Draw, P1Won e P2Won. MatchHUD continua mostrando apenas os anúncios de rodada e turno.

O controlador reaplica _ShowText, _Seconds, _ColonBlink e _Message em cada atualização (0.1 s), para que materiais substituídos ou recriados recebam o resultado mesmo quando o estado não mudou. Antes, _Message era escrito apenas quando lastMessage mudava, permitindo que um material voltasse ao X padrão sem ser atualizado.

A mensagem anterior permanece durante o intervalo com timer zero entre a resolução e o próximo turno. A contagem positiva a substitui normalmente; sair da partida também encerra essa retenção.
