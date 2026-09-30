using MenSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// Esconde um conjunto de renderers para todo mundo menos o dono.
///
/// No jogo cada jogador tem o seu proprio placar: o texto do Player 1 nao pode
/// aparecer para o Player 2, senao ele le a pontuacao alheia espiando de
/// lado. Aqui a coisa e ligada por <see cref="ownerPlayerIndex"/>: 0 e o
/// primeiro jogador a entrar no world, 1 o segundo.
///
/// Como funciona: quem mostra o texto e so o dono. Nao ha "esconder para
/// quem estiver perto" — o placar e privado. Ainda assim a checagem passa por
/// <see cref="Networking.LocalPlayer"/>, porque e ele que diz de quem e a vez,
/// e nao o dono do objeto.
///
/// Em editor ou em single player nao ha ninguem no world, e o texto fica
/// visivel (<see cref="showWhenOffline"/>), senao nao daria para montar a cena.
/// </summary>
public class OwnerOnlyVisibility : MenSharpBehaviour
{
    [Tooltip("Renderers que so o dono enxerga. Guarde aqui o MeshRenderer do texto, nao o do cubo.")]
    public Renderer[] hideFromOthers;

    [Tooltip("De quem e o placar. 0 = primeiro jogador a entrar, 1 = segundo.")]
    public int ownerPlayerIndex = 0;

    [Tooltip("Com ninguem no world (editor, single player), deixa o texto visivel para dar para montar a cena.")]
    public bool showWhenOffline = true;

    [Tooltip("Marca o estado ja aplicado, para nao mexer no renderer todo frame.")]
    private bool applied;

    [Tooltip("Ultimo valor aplicado em hideFromOthers.")]
    private bool appliedValue;

    // quem entrou em qual vaga. O playerId muda a cada sessao, entao a vaga
    // e reconstruida por ordem de playerId, e nao guardada em campo sync.
    private int[] slots = new int[2];

    private bool slotsDirty = true;

    /// <summary>True quando o texto esta visivel para quem esta olhando.</summary>
    public bool IsVisibleToViewer
    {
        get { return appliedValue; }
    }

    /// <summary>
    /// Recalcula quem e a vaga 0 e quem e a vaga 1. Roda quando alguem entra
    /// ou sai, e nao todo frame.
    /// </summary>
    private void RebuildSlots()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = 0;
        }
        int count = VRCPlayerApi.GetPlayerCount();
        if (count <= 0)
        {
            slotsDirty = false;
            return;
        }

        VRCPlayerApi[] players = new VRCPlayerApi[count];
        VRCPlayerApi.GetPlayers(players);

        // ordena por playerId para a ordem ser a mesma em todas as maquinas
        for (int i = 1; i < players.Length; i++)
        {
            VRCPlayerApi key = players[i];
            int j = i - 1;
            while (j >= 0 && players[j].playerId > key.playerId)
            {
                players[j + 1] = players[j];
                j--;
            }
            players[j + 1] = key;
        }
        for (int i = 0; i < players.Length && i < slots.Length; i++)
        {
            slots[i] = players[i].playerId;
        }
        slotsDirty = false;
    }

    public void OnPlayerJoined(VRCPlayerApi player)
    {
        slotsDirty = true;
    }

    public void OnPlayerLeft(VRCPlayerApi player)
    {
        slotsDirty = true;
    }

    public void OnPreLateUpdate()
    {
        bool show = ShouldShow();
        if (applied && show == appliedValue)
        {
            return;
        }
        appliedValue = show;
        applied = true;
        for (int i = 0; i < hideFromOthers.Length; i++)
        {
            Renderer renderer = hideFromOthers[i];
            if (renderer != null)
            {
                renderer.enabled = show;
            }
        }
    }

    private bool ShouldShow()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null)
        {
            // editor / single player: nao ha rival para esconder
            return showWhenOffline;
        }
        if (slotsDirty)
        {
            RebuildSlots();
        }
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == local.playerId)
            {
                return i == ownerPlayerIndex;
            }
        }
        // ainda nao entrou em nenhuma vaga: esconde, para nao vazar por um frame
        return false;
    }
}