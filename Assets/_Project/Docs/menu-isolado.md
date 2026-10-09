# Menu isolado — GameMenu2

Atualizado em 02/10/2026.

- UI em inglês, preto e branco, sem cards nem customização.
- Create match / Join match abrem os respectivos lobbies locais; Leave table volta ao início.
- Clique despacha a ação imediatamente. Hover: 75 ms; transição: 140 ms; cascata: 20 ms.
- MenSharp: comunicação entre componentes usa campos públicos e eventos sem parâmetros.
- Posições dos botões são capturadas antes da primeira transformação, independente da ordem de Start.
- Chamadas UnityEvent são persistentes e habilitadas em runtime.
- Apenas a tela visível recebe raycasts; uma troca cancela a cascata inicial.
- MatchLobby.previewOnly permanece true; dealer não está ligado. Start match permanece desabilitado.
- Integrar depois na VRCDEFAULTWORLDSCENE. O estado de lobby aqui é local, sem reservar vagas de rede.

Validação: eventos compilados no ClientSim para criar, sair, entrar e voltar; ausência de erros no fluxo. Laser e multiplayer reais ainda exigem validação após a integração.
