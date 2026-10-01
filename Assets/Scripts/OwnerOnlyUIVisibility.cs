using System.Threading.Tasks;
using MenSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// Versao de <see cref="OwnerOnlyVisibility"/> para menus em Canvas.
///
/// O <see cref="OwnerOnlyVisibility"/> funciona desligando <c>Renderer.enabled</c>,
/// e texto de UI nao tem Renderer: TextMeshProUGUI usa <c>CanvasRenderer</c>. Por
/// isso aqui aVisibility e feita ligando/desligando o GameObject inteiro (ou
/// mandando o CanvasGroup para alpha 0), que e o equivalente na UI.
///
/// Mesma regra de vaga: 0 e o primeiro jogador a entrar no world, 1 o segundo.
/// Alguem que ainda nao entrou em nenhuma vaga fica escondido, para nao vazar
/// a escolha do rival por um frame. Em editor / single player nao ha ninguem,
/// e fica visivel (<see cref="showWhenOffline"/>), senao nao daria para montar
/// a cena.
/// </summary>
public class OwnerOnlyUIVisibility : MenSharpBehaviour
{
    [Tooltip("Objetos que so o dono enxerga (o CanvasGroup do menu, ou o proprio GameObject).")]
    public GameObject[] targets;

    [Tooltip("Se marcado, usa CanvasGroup.alpha em vez de ligar/desligar o GameObject.")]
    public bool useCanvasGroup = true;

    [Tooltip("De quem e o menu. 0 = primeiro jogador a entrar, 1 = segundo.")]
    public int ownerPlayerIndex = 0;

    [Tooltip("Com ninguem no world (editor, single player), deixa visivel para dar para montar a cena.")]
    public bool showWhenOffline = true;

    [Tooltip("Marca o estado ja aplicado, para nao mexer nos alvos todo frame.")]
    public bool applied;

    [Tooltip("Ultimo valor aplicado.")]
    public bool appliedValue;

    // quem entrou em qual vaga. O playerId muda a cada sessao, entao a vaga
    // e reconstruida por ordem de playerId, e nao guardada em campo sync.
    public int[] slots = new int[2];

    public bool slotsDirty = true;

    public void Start()
    {
        ApplyVisibility();
        Scheduler.Run(() => WatchVisibility());
    }

    private async Task WatchVisibility()
    {
        while (true)
        {
            ApplyVisibility();
            await Scheduler.NextFrame();
        }
    }

    /// <summary>True quando o menu esta visivel para quem esta olhando.</summary>
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

    public void ApplyVisibility()
    {
        bool show = ShouldShow();
        if (applied && show == appliedValue)
        {
            return;
        }
        appliedValue = show;
        applied = true;
        if (targets == null)
        {
            return;
        }
        for (int i = 0; i < targets.Length; i++)
        {
            GameObject target = targets[i];
            if (target == null)
            {
                continue;
            }
            CanvasGroup group = target.GetComponent<CanvasGroup>();
            if (useCanvasGroup && group != null)
            {
                group.alpha = show ? 1f : 0f;
                group.interactable = show;
                group.blocksRaycasts = show;
            }
            else
            {
                target.SetActive(show);
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