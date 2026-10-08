using MenSharp;
using TMPro;
using UnityEngine;
using VRC.SDK3.Components;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TarotPickup : MenSharpBehaviour
{
    public GameObject cardObject;
    public Transform cardTransform;
    public TarotVisuals visuals;
    public int ownerPlayer, handIndex, tarotType;
    public bool onTable, ownedLocally, held, pendingUse, moving, awaitingUse;
    public Vector3 homePosition;
    public Quaternion homeRotation;
    public TextMeshPro descriptionText, logoText;
    public MeshRenderer cardRenderer;
    public Rigidbody body;
    public BoxCollider cardCollider;
    public VRCPickup pickup;
    public Material face;
    public string symbol, description;
    public Vector3 incomingDestination;
    public Quaternion incomingRotation;
    public bool incomingConsumed;
    public Vector3 moveStart, moveDestination;
    public Quaternion moveStartRotation, moveDestinationRotation;
    public float moveElapsed, fadeElapsed, fadeStart, fadeTarget;
    public bool fading;
    public float moveDuration = 0.4f, descriptionFadeDuration = 0.25f;

    // Cross-behaviour calls use fields and parameterless events in MenSharp.
    public void Configure()
    {
        cardObject = gameObject;
        cardTransform = transform;
        body = GetComponent<Rigidbody>();
        cardCollider = GetComponent<BoxCollider>();
        pickup = GetComponent<VRCPickup>();
        cardRenderer = GetComponent<MeshRenderer>();
        Transform text = transform.Find("Description");
        descriptionText = text != null ? text.GetComponent<TextMeshPro>() : null;
        Transform logo = transform.Find("Logo");
        logoText = logo != null ? logo.GetComponent<TextMeshPro>() : null;
        if (cardRenderer != null && face != null)
        {
            Material[] materials = cardRenderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = face;
            cardRenderer.sharedMaterials = materials;
        }
        if (logoText != null)
        {
            logoText.text = symbol;
            logoText.gameObject.SetActive(face == null || face.mainTexture == null);
        }
        if (descriptionText != null)
        {
            descriptionText.gameObject.SetActive(true);
            descriptionText.enabled = true;
            descriptionText.text = description;
            SetDescriptionAlpha(0f);
        }
        transform.SetPositionAndRotation(homePosition, homeRotation);
        if (pickup != null) pickup.pickupable = ownedLocally;
        if (body != null) { body.useGravity = false; body.isKinematic = true; }
    }
    public void OnPickup()
    {
        if (!ownedLocally || pendingUse || awaitingUse) return;
        held = true;
        moving = false;
        BeginFade(1f);
    }
    public void OnDrop()
    {
        held = false;
        BeginFade(0f);
        if (onTable || visuals == null) { ReturnHome(); return; }
        visuals.incomingDroppedCard = this;
        visuals.CardDropped();
    }
    public void ReturnHome()
    {
        if (pendingUse) return;
        incomingDestination = homePosition;
        incomingRotation = homeRotation;
        incomingConsumed = false;
        ApplyMove();
    }
    public void ApplyMove()
    {
        moveStart = transform.position;
        moveStartRotation = transform.rotation;
        moveDestination = incomingDestination;
        moveDestinationRotation = incomingRotation;
        pendingUse = incomingConsumed;
        moveElapsed = 0f;
        moving = true;
        if (!incomingConsumed) { homePosition = moveDestination; homeRotation = moveDestinationRotation; }
        if (pickup != null) pickup.pickupable = false;
        if (body != null) body.isKinematic = true;
        if (cardCollider != null) cardCollider.enabled = false;
    }
    public void Update()
    {
        if (fading)
        {
            fadeElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(fadeElapsed / Mathf.Max(0.01f, descriptionFadeDuration));
            SetDescriptionAlpha(Mathf.Lerp(fadeStart, fadeTarget, t));
            if (t >= 1f) fading = false;
        }
        if (held || awaitingUse) return;
        if (!moving)
        {
            // Pickups can receive a final physics/simulator pose after release.
            // Keep an idle card at its assigned spawn or used-table position.
            if (cardTransform != null)
            {
                cardTransform.position = homePosition;
                cardTransform.rotation = homeRotation;
            }
            return;
        }
        moveElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(moveElapsed / Mathf.Max(0.01f, moveDuration));
        float eased = progress * progress * (3f - 2f * progress);
        transform.SetPositionAndRotation(Vector3.Lerp(moveStart, moveDestination, eased),
            Quaternion.Slerp(moveStartRotation, moveDestinationRotation, eased));
        if (progress < 1f) return;
        moving = false;
        if (pendingUse)
        {
            pendingUse = false;
            onTable = true;
            homePosition = moveDestination;
            homeRotation = moveDestinationRotation;
        }
        if (cardCollider != null) cardCollider.enabled = true;
        if (pickup != null) pickup.pickupable = ownedLocally;
        if (body != null) { body.isKinematic = true; body.useGravity = false; }
    }
    private void BeginFade(float target)
    {
        if (descriptionText == null) return;
        fadeStart = descriptionText.color.a;
        fadeTarget = target;
        fadeElapsed = 0f;
        fading = true;
    }
    private void SetDescriptionAlpha(float alpha)
    {
        if (descriptionText == null) return;
        Color color = descriptionText.color;
        color.a = alpha;
        descriptionText.color = color;
    }
}
