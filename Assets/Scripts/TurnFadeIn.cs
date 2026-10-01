using MenSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// Reage a virada de vez do <see cref="CardDealer"/> tocando o
/// <see cref="FadeRise"/> dos botoes.
///
/// Este script nao observa nada: quem avisa e o dono do baralho, pelo
/// <see cref="CardDealer.NotifyTurnChanged"/>, chamado em todo ponto onde a vez
/// pode mudar. Antes ele conferia o turno a cada quadro, o que e desperdicio —
/// e pior, repartia a animacao entre "chegou a vez" e "o turno ainda era o de
/// outro" numa rajada de quadros, com o botao piscando. Com o evento, a
/// animacao toca uma vez, no instante certo.
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
    [Tooltip("O dono do baralho, que dispara OnTurnChanged.")]
    public CardDealer dealer;

    [Tooltip("De qual jogador e este menu. 0 = primeiro a entrar, 1 = segundo.")]
    public int playerIndex = 0;

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
    /// Chamado pelo <see cref="CardDealer.NotifyTurnChanged"/>. E o unico ponto
    /// de entrada: nada aqui roda por quadro.
    /// </summary>
    /// <param name="actionable">False quando nao ha jogada para fazer — partida
    /// nao comecada, abertura ainda nao saiu, cartas voando ou partida acabada.
    /// O menu fica escondido.</param>
    /// <param name="turnPlayer">De quem e a vez, ou -1 quando
    /// <paramref name="actionable"/> e false.</param>
    public void OnTurnChanged(bool actionable, int turnPlayer)
    {
        // A mesa so avisa quando o estado muda, mas por seguranca: uma animacao
        // repetida no mesmo estado mostraria o botao no meio da partida sem a
        // vez ter virado.
        bool mine = actionable && turnPlayer == playerIndex;
        if (mine == lastWasActionable)
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