using MenSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

/// <summary>
/// A via de entrada de um jogador na mesa. Existe um Slot por posicao de jogo.
///
/// Nao tem nada a ver com cadeira: e so o objeto que carrega a intencao do
/// jogador ("quero comprar", "quero passar", "quero usar esta carta de tarot").
/// O menu de criar e entrar em salas vem depois e decide quem ocupa cada Slot;
/// enquanto isso o dono do baralho distribui os jogadores pelos Slots na ordem
/// em que eles entraram.
///
/// O porque deste objeto existir e o problema que ele resolve. A VRChat nao
/// informa quem enviou um <c>SendCustomNetworkEvent</c>: o dono do baralho recebe
/// a jogada e nao tem como saber se veio do jogador 0 ou do 1. Se o pedido fosse
/// direto para o <see cref="CardDealer"/>, qualquer um poderia comprar para a
/// mao do outro, ou jogar na vez do outro.
///
/// Aqui nao tem esse furo, porque a posse de rede de cada Slot fica com o jogador
/// que o ocupa, e so o dono de um objeto consegue escrever nos campos
/// sincronizados daquele objeto. Ou seja: um jogador nao consegue mexer no Slot
/// do outro — o maximo que ele faz e escrever no proprio, e o dono do baralho
/// le e valida.
///
/// Por isso o estado do jogo inteiro fica no <see cref="CardDealer"/>, que o
/// master controla. O Slot so carrega a intencao, com um contador que so anda
/// para frente: o dono do baralho guarda o ultimo numero que processou e so age
/// quando o numero avanca, entao repetir a mesma intencao nao compra duas vezes.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PlayerSlot : MenSharpBehaviour
{
    /// <summary>Intencao: nada novo.</summary>
    public const int ActionNone = 0;

    /// <summary>Intencao: comprar uma carta do baralho (hit).</summary>
    public const int ActionHit = 1;

    /// <summary>Intencao: ficar com o que tem (stay).</summary>
    public const int ActionStay = 2;

    /// <summary>Intencao: usar uma carta de tarot.</summary>
    public const int ActionTrump = 3;

    /// <summary>Intencao de iniciar ou reiniciar a partida.</summary>
    public const int ActionStartMatch = 4;

    [Tooltip("Qual posicao da mesa este Slot controla. O dono do baralho preenche em runtime, pela ordem de entrada dos jogadores.")]
    public int playerIndex = 0;

    [Tooltip("Nome do jogador que ocupa este Slot. So para ajudar a depurar; o dono do baralho preenche em runtime.")]
    public string playerName = "";

    [Tooltip("Loga no console cada intencao enviada e cada intencao recusada.")]
    public bool logRequests = true;

    // Contador que so anda para frente. Cada jogada soma um; o dono do baralho
    // processa cada numero uma unica vez.
    //
    // Publicos de proposito: no MenSharp um campo privado vira estatico, e um
    // estatico seria compartilhado pelos dois Slots da mesa. Se actionSeq fosse
    // o mesmo nos dois, os dois jogadores pareceriam ter jogado juntos e a
    // validao de vez deixaria de funcionar.
    [UdonSynced] public int actionSeq = 0;
    [UdonSynced] public int actionType = 0;
    [UdonSynced] public int actionArg = 0;

    /// <summary>Numero da ultima jogada enviada por este Slot.</summary>
    public int ActionSeq
    {
        get { return actionSeq; }
    }

    /// <summary>Tipo da ultima jogada enviada.</summary>
    public int ActionType
    {
        get { return actionType; }
    }

    /// <summary>Argumento da ultima jogada enviada (indice da carta, no caso da tarot).</summary>
    public int ActionArg
    {
        get { return actionArg; }
    }

    /// <summary>
    /// Este Slot e do jogador local? E o que separa o botao do jogador 0 do
    /// botao do jogador 1, sem confiar em nada que o cliente diga.
    /// </summary>
    public bool IsMine()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null)
        {
            // sem rede (editor, single player) quem chama e o dono
            return true;
        }
        return Networking.IsOwner(local, gameObject);
    }

    /// <summary>
    /// Este Slot ja tem alguem na mesa? Antes do primeiro jogador entrar a posse
    /// e do master, e um Slot sem jogador nao pode ser usado.
    /// </summary>
    public bool HasPlayer()
    {
        return Networking.GetOwner(gameObject) != null;
    }

    /// <summary>Jogador dono deste Slot, ou null se ainda nao tem ninguem.</summary>
    public VRCPlayerApi OwnerPlayer()
    {
        return Networking.GetOwner(gameObject);
    }

    /// <summary>Este Slot e do jogador <paramref name="player"/>?</summary>
    public bool IsOwnedBy(VRCPlayerApi player)
    {
        return player != null && Networking.IsOwner(player, gameObject);
    }

    /// <summary>
    /// Passa a posse deste Slot para <paramref name="player"/>. So o dono do
    /// baralho chama, e e o que amarra a posicao 0 da mesa a um jogador
    /// especifico — sem isso o primeiro botao apertado seria o dono.
    /// </summary>
    public void GiveOwnershipTo(VRCPlayerApi player)
    {
        if (player == null || IsOwnedBy(player))
        {
            return;
        }
        Networking.SetOwner(player, gameObject);
    }

    /// <summary>
    /// Envia a intencao de comprar. So funciona se este Slot for do jogador
    /// local: e o proprio dono do objeto escrevendo, entao nao ha como falsificar.
    /// </summary>
    public void RequestHit()
    {
        Send(ActionHit, 0);
    }

    /// <summary>Envia a intencao de passar (stay).</summary>
    public void RequestStay()
    {
        Send(ActionStay, 0);
    }

    public void RequestStartMatch()
    {
        Send(ActionStartMatch, 0);
    }

    /// <summary>
    /// Envia a intencao de usar a carta de tarot
    /// <paramref name="cardIndex"/>. A mao de trumps ainda nao foi
    /// implementada; este pedido e ignorado pelo CardDealer.
    /// </summary>
    public void RequestUseTrump(int cardIndex)
    {
        Send(ActionTrump, cardIndex);
    }

    private void Send(int type, int arg)
    {
        if (!IsMine() || !HasPlayer())
        {
            if (logRequests)
            {
                Debug.Log("PlayerSlot: intencao ignorada, este Slot nao e do jogador local.");
            }
            return;
        }
        actionType = type;
        actionArg = arg;
        actionSeq++;
        RequestSerialization();
        if (logRequests)
        {
            Debug.Log("PlayerSlot: jogador " + playerIndex + " pediu acao " + type
                + " (jogada " + actionSeq + ", arg " + arg + ").");
        }
    }

    /// <summary>
    /// O dono do baralho chama isto quando ja processou a jogada, so para o log
    /// ficar limpo. Nao e preciso para a corretude: quem barra a repeticao e o
    /// <c>actionSeq</c> guardado no <see cref="CardDealer"/>.
    /// </summary>
    public void Acknowledge()
    {
        if (!IsMine())
        {
            return;
        }
        actionType = ActionNone;
        actionArg = 0;
        RequestSerialization();
    }

    /// <summary>
    /// Define a posicao da mesa e o nome de quem ocupa este Slot. Quem chama e
    /// sempre o dono do baralho.
    /// </summary>
    public void Assign(int slotPlayerIndex, string slotPlayerName)
    {
        playerIndex = slotPlayerIndex;
        playerName = slotPlayerName == null ? "" : slotPlayerName;
    }
}
