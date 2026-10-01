using System.Threading.Tasks;
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
    public int ownerPlayer;
    public int handIndex;
    public int tarotType;
    public bool onTable;
    public bool ownedLocally;
    public bool held;
    public bool pendingUse;
    public bool moving;
    public Vector3 homePosition;
    public Quaternion homeRotation;
    public TextMeshPro descriptionText;
    public TextMeshPro logoText;
    public MeshRenderer cardRenderer;
    public Rigidbody body;
    public BoxCollider cardCollider;
    public VRCPickup pickup;
    public int textFadeVersion;
    public int moveVersion;

    public void Configure(TarotVisuals source, int owner, int index, int type, bool placed,
        Material face, Vector3 home, Quaternion rotation, string symbol, string description,
        bool localOwner)
    {
        cardObject = gameObject;
        cardTransform = transform;
        visuals = source;
        ownerPlayer = owner;
        handIndex = index;
        tarotType = type;
        onTable = placed;
        ownedLocally = localOwner;
        homePosition = home;
        homeRotation = rotation;
        body = GetComponent<Rigidbody>();
        cardCollider = GetComponent<BoxCollider>();
        pickup = GetComponent<VRCPickup>();
        cardRenderer = GetComponent<MeshRenderer>();
        descriptionText = transform.Find("Description").GetComponent<TextMeshPro>();
        Transform logo = transform.Find("Logo");
        logoText = logo != null ? logo.GetComponent<TextMeshPro>() : null;
        if (cardRenderer != null && face != null)
        {
            Material[] materials = cardRenderer.sharedMaterials;
            if (materials != null && materials.Length > 0)
            {
                for (int i = 0; i < materials.Length; i++) materials[i] = face;
                cardRenderer.sharedMaterials = materials;
            }
        }
        if (logoText != null) logoText.text = symbol;
        if (descriptionText != null)
        {
            descriptionText.text = description;
            SetDescriptionAlpha(0f);
        }
        transform.position = home;
        transform.rotation = rotation;
        bool canPickUp = !placed && localOwner;
        if (pickup != null) pickup.pickupable = canPickUp;
        if (body != null)
        {

            body.useGravity = false;
            body.isKinematic = true;
        }
    }

    public void OnPickup()
    {
        if (onTable || visuals == null || !ownedLocally) return;
        held = true;
        moving = false;
        moveVersion++;
        textFadeVersion++;
        Scheduler.Run(() => FadeDescription(textFadeVersion, 1f));
    }

    public void OnDrop()
    {
        held = false;
        textFadeVersion++;
        Scheduler.Run(() => FadeDescription(textFadeVersion, 0f));
        if (visuals != null) visuals.CardDropped(this);
    }

    public void ReturnHome()
    {
        if (pendingUse) return;
        MoveTo(homePosition, homeRotation, false);
    }

    public void MoveTo(Vector3 destination, Quaternion rotation, bool consumed)
    {
        moving = true;
        pendingUse = consumed;
        moveVersion++;
        if (pickup != null) pickup.pickupable = false;
        if (body != null)
        {

            body.isKinematic = true;
        }
        if (cardCollider != null) cardCollider.enabled = false;
        Scheduler.Run(() => AnimateMove(moveVersion, destination, rotation, consumed));
    }

    private async Task AnimateMove(int version, Vector3 destination, Quaternion rotation, bool consumed)
    {
        Vector3 start = transform.position;
        Quaternion startRotation = transform.rotation;
        float elapsed = 0f;
        const float duration = 0.4f;
        while (elapsed < duration && version == moveVersion)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            transform.position = Vector3.Lerp(start, destination, t);
            transform.rotation = Quaternion.Slerp(startRotation, rotation, t);
            await Scheduler.NextFrame();
        }
        if (version != moveVersion) return;
        transform.position = destination;
        transform.rotation = rotation;
        moving = false;
        if (consumed)
        {
            Destroy(gameObject);
            return;
        }
        if (cardCollider != null) cardCollider.enabled = true;
        if (pickup != null) pickup.pickupable = !onTable && ownedLocally;
        if (body != null)
        {
            body.isKinematic = true;
            body.useGravity = false;
        }
    }

    private async Task FadeDescription(int version, float target)
    {
        if (descriptionText == null) return;
        float initial = descriptionText.color.a;
        float elapsed = 0f;
        const float duration = 0.25f;
        while (elapsed < duration && version == textFadeVersion)
        {
            elapsed += Time.deltaTime;
            SetDescriptionAlpha(Mathf.Lerp(initial, target, Mathf.Clamp01(elapsed / duration)));
            await Scheduler.NextFrame();
        }
        if (version == textFadeVersion) SetDescriptionAlpha(target);
    }

    private void SetDescriptionAlpha(float alpha)
    {
        if (descriptionText == null) return;
        Color color = descriptionText.color;
        color.a = alpha;
        descriptionText.color = color;
    }
}