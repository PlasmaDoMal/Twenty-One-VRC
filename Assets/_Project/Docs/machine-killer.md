# MachineKiller

A derrota de cada rodada aproxima a máquina usando a fração de vida perdida, limitada a 50% do percurso original. Mesmo com vida zero, o limite permanece até CardDealer.matchOver. Somente ao encerrar a partida com vencedor a máquina completa X/Z do perdedor; mantém o Y original.

WorldMatchSession referencia o Udon de MachineKillerMotion e espera finalArrivalComplete antes de solicitar o retorno ao lobby. Há um timeout de segurança de moveDuration + 1 segundo. Empate final não dispara avanço completo. moveDuration padrão: 1.2 segundos.

Teste isolado no editor: última rodada com vida zero permanece em 50%; matchOver inicia avanço completo; sinal de chegada continua ativo após novos polls; P1 e P2 conservam a altura; reset retorna à origem. Referências salvas na cena.
