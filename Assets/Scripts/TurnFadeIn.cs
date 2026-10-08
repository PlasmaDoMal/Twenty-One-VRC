using MenSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// Reage a virada de vez do <see cref="CardDealer"/>, tocando o
/// <see cref="FadeRise"/> dos botoes.
///
/// Este script nao observa nada: quem avisa e o dono do baralho. O aviso chega
/// por <see cref="ApplyTurn"/>, que tem de ser sem parametros — ver o metodo.
///
/// Quem decide continua sendo a mesa: <c>turnIndex</c> e sincronizado e so o
/// dono do baralho escreve nele, e a jogada so chega pelo
/// <see cref="PlayerSlot"/> do jogador, entao nao ha como furar a vez.
///
/// O alpha do <see cref="FadeRise"/> e do CanvasGroup do proprio botao, e o do
/// <see cref="OwnerOnlyUIVisibility"/> e do menu inteiro. Sao grupos diferentes e
/// um multiplica o outro, entao quem decide "e a vez deste jogador" continua
/// sendo o dono da mesa, e nao a animacao.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TurnFadeIn : MenSharpBehaviour
{
    [Header("Turn source")]
    [Tooltip("O dono do baralho, que chama ApplyTurn.")]
    public CardDealer dealer;

    [Tooltip("De qual jogador e este menu. 0 = primeiro a entrar, 1 = segundo.")]
    public int playerIndex = 0;

    [Header("Ultimo estado recebido do dono da mesa")]
    [Tooltip("Preenchido pelo CardDealer antes de chamar ApplyTurn.")]
    [HideInInspector] public bool incomingActionable;
    [HideInInspector] public int incomingTurnPlayer;
    [HideInInspector] public bool incomingSolo;

    [Header("Targets")]
    [Tooltip("Botoes que tocam a animacao quando a vez chega.")]
    public FadeRise[] fades;

    [Tooltip("Desses, os que comecam escondidos e so aparecem na vez (ex.: Stay, que nao vale antes da primeira compra).")]
    public FadeRise[] hiddenUntilTurn;

    [Tooltip("Botoes de escolha deste jogador. Recebem ResetBusy quando a vez chega, para nao ficarem travados depois do primeiro clique.")]
    public ChoiceButton[] choices;

    [Header("Debug")]
    [Tooltip("Loga cada vez que a animacao toca.")]
    public bool logTriggers = false;

    // Publicos de proposito: no MenSharp um campo privado vira estatico, e um
    // estatico seria compartilhado pelos dois TurnFadeIn da mesa.
    [HideInInspector] public bool lastWasActionable;
    public int lastTurnPlayer = -1;
    [HideInInspector] public bool turnPending;

    public void Start()
    {
        Scheduler.Run(() => HideUntilTurn());
    }

    /// <summary>
    /// Esconde os botoes de <see cref="hiddenUntilTurn"/> depois de um quadro.
    ///
    /// O atraso nao e enfeite: o <see cref="FadeRise"/> guarda a posicao de
    /// repouso no Start dele, e o Unity nao garante qual Start roda primeiro. Se
    /// este escondesse antes, o FadeRise leria a posicao ja deslocada e a
    /// animacao de entrada subiria de baixo de mais baixo.
    /// </summary>
    private async System.Threading.Tasks.Task HideUntilTurn()
    {
        await Scheduler.DelayFrames(1);
        // O aviso pode chegar antes deste primeiro frame. Nao esconda uma vez
        // que ja ficou jogavel; o fade em andamento pertence ao turno atual.
        if (lastWasActionable || (dealer != null && dealer.TurnIsActionable()
            && dealer.turnIndex == playerIndex))
        {
            return;
        }
        if (hiddenUntilTurn == null)
        {
            return;
        }
        for (int i = 0; i < hiddenUntilTurn.Length; i++)
        {
            if (hiddenUntilTurn[i] != null)
            {
                hiddenUntilTurn[i].HideNow();
            }
        }
    }

    /// <summary>
    /// Aplica o estado de turno mais recente. E o unico evento que o
    /// <see cref="CardDealer"/> consegue chamar.
    ///
    /// Tem de ser sem parametros: o Udon so expoe como evento os metodos sem
    /// argumentos, e o <c>SendCustomEvent</c> leva so o nome. Com uma assinatura
    /// <c>OnTurnChanged(bool, int)</c> o metodo nem era exportado e a chamada
    /// do CardDealer nao existia no programa — o log mostrava a mesa avisando a
    /// vez e nenhum menu recebia. Por isso o estado vem em dois campos
    /// (<see cref="incomingActionable"/> e <see cref="incomingTurnPlayer"/>) e
    /// este metodo so aplica.
    /// </summary>
    public void ApplyTurn()
    {
        // Deal completion can notify us inside the shared MenSharp scheduler.
        // Start animations on Update so their scheduler does not re-enter it.
        turnPending = true;
    }

    public void Update()
    {
        if (!turnPending) return;
        turnPending = false;
        ApplyPendingTurn();
    }

    private void ApplyPendingTurn()
    {
        // Consume the notification without calling back into the dealer's scheduler.
        bool solo = incomingSolo;
        bool mine = incomingActionable && (solo ? playerIndex == 0 : incomingTurnPlayer == playerIndex);
        bool turnChanged = lastTurnPlayer != incomingTurnPlayer;
        lastTurnPlayer = incomingTurnPlayer;
        if (mine == lastWasActionable && !(solo && mine && turnChanged))
        {
            return;
        }
        lastWasActionable = mine;
        if (!mine)
        {
            HideAll();
            return;
        }
        PlayFade();
    }

    /// <summary>Some com todos os botoes deste menu, sem animar.</summary>
    public void HideAll()
    {
        if (fades == null)
        {
            return;
        }
        for (int i = 0; i < fades.Length; i++)
        {
            if (fades[i] != null)
            {
                fades[i].HideNow();
            }
        }
        ResetChoices();
    }

    /// <summary>Toque a animacao agora, sem esperar a proxima virada de vez.</summary>
    public void PlayFade()
    {
        // A vez chegou: os botoes destravam. Sem isso o ChoiceButton ficaria
        // com busy travado depois do primeiro clique da rodada.
        ResetChoices();
        if (fades != null)
        {
            for (int i = 0; i < fades.Length; i++)
            {
                if (fades[i] != null)
                {
                    fades[i].FadeInAndRise();
                }
            }
        }
        if (logTriggers)
        {
            Debug.Log("TurnFadeIn: vez do jogador " + playerIndex + ", animacao tocada.");
        }
    }

    private void ResetChoices()
    {
        if (choices == null)
        {
            return;
        }
        for (int i = 0; i < choices.Length; i++)
        {
            if (choices[i] != null)
            {
                choices[i].ResetBusy();
            }
        }
    }
}
