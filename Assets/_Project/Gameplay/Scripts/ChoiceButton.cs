using MenSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// Liga o clique do botao a jogada de verdade e some com o botao.
///
/// A jogada vai pelo <see cref="CardDealer"/>, que por sua vez entrega ao
/// <see cref="PlayerSlot"/> do jogador local. Nao ha parametro de jogador de
/// proposito: quem chama ja e o proprio dono do Slot, e o dono do baralho
/// descobre de quem e a vez olhando o proprio estado. Passar o indice tornaria
/// a UI confiavel em quem a apertou.
///
/// O <see cref="FadeRise"/> roda depois, para o botao nao ficar parado em cima
/// da tela enquanto o dono do baralho processa a jogada e a vez vira. Como o
/// alpha do botao e um <c>CanvasGroup</c> separado do menu inteiro, e o
/// <see cref="OwnerOnlyUIVisibility"/> continua mandando em quem ve o menu.
///
/// Clicar durante a animacao e ignorado, para nao enfileirar duas jogadas.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ChoiceButton : MenSharpBehaviour
{
    [Header("Turn source")]
    public CardDealer dealer;
    public VRC.Udon.UdonBehaviour audioController;

    [Tooltip("O botao que some quando o jogador escolhe.")]
    public FadeRise fade;

    [Header("Debug")]
    public bool logClicks = false;

    // Publico de proposito: no MenSharp um campo privado vira estatico, e um
    // estatico seria compartilhado pelos quatro ChoiceButton da mesa — os dois
    // jogadores travariam o botao um do outro no primeiro clique.
    [HideInInspector] public bool busy;

    /// <summary>Compra uma carta.</summary>
public void ChooseHit()
    {
        if (busy || dealer == null || !dealer.CanLocalPlayerAct()) return;
        busy = true;
        if (audioController != null && audioController.gameObject != gameObject) audioController.SendCustomEvent("PlayChoice");
        dealer.RequestHit();
        Hide();
    }

    /// <summary>Fica com o que tem.</summary>
public void ChooseStay()
    {
        if (busy || dealer == null || !dealer.CanLocalPlayerAct()) return;
        busy = true;
        if (audioController != null && audioController.gameObject != gameObject) audioController.SendCustomEvent("PlayChoice");
        dealer.RequestStay();
        Hide();
    }

    /// <summary>
    /// Devolve o botao a poder de clique. O <see cref="TurnFadeIn"/> chama
    /// quando a vez chega de novo — sem isso o <c>busy</c> ficaria travado para
    /// sempre depois do primeiro clique.
    /// </summary>
    public void ResetBusy()
    {
        busy = false;
    }

    private void Hide()
    {
        if (logClicks)
        {
            Debug.Log("ChoiceButton: " + gameObject.name + " escolhido.");
        }
        if (fade != null)
        {
            fade.FadeOutAndDrop();
        }
    }
}