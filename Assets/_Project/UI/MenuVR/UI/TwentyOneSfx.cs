using UdonSharp;
using UnityEngine;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TwentyOneSfx : UdonSharpBehaviour
{
    public UdonBehaviour dealer;
    public AudioSource choiceClick;
    public AudioSource cardHandling;
    public AudioSource crtStartup;
    public AudioSource tvSwitchOn;
    public AudioSource ambientLoop;
    public AudioSource randomAmbient;
    public AudioClip pickupCard;
    public AudioClip slideCard;
    [Range(0.1f, 3f)] public float choicePitch = 1f;
    [Range(0.1f, 3f)] public float cardPitchMin = 0.9f;
    [Range(0.1f, 3f)] public float cardPitchMax = 1.1f;
    [Range(0.1f, 3f)] public float startupPitch = 1f;
    [Range(0f, 1f)] public float ambientChance = 0.05f;
    [Min(1f)] public float ambientIntervalMin = 90f;
    [Min(1f)] public float ambientIntervalMax = 180f;
    private bool initialized;
    private bool wasMatchStarted;
    private int seenHit;
    private int lastCardClip = -1;
    private float nextPoll;
    private float nextAmbient;

    private void OnDisable()
    {
        initialized = false;
        wasMatchStarted = false;
        nextAmbient = 0f;
        if (ambientLoop != null) ambientLoop.Stop();
        if (randomAmbient != null) randomAmbient.Stop();
    }
    public void PlayChoice()
    {
        if (choiceClick == null || choiceClick.clip == null) return;
        choiceClick.pitch = choicePitch;
        choiceClick.Play();
    }
    private void Update()
    {
        if (dealer == null || Time.time < nextPoll) return;
        nextPoll = Time.time + 0.05f;
        int hit = (int)dealer.GetProgramVariable("hitSoundSequence");
        bool started = (bool)dealer.GetProgramVariable("matchStarted");
        bool active = started && !(bool)dealer.GetProgramVariable("matchOver");
        if (!initialized) { initialized = true; seenHit = hit; }
        if (active && !wasMatchStarted)
        {
            if (crtStartup != null && crtStartup.clip != null) { crtStartup.pitch = startupPitch; crtStartup.Play(); }
            if (tvSwitchOn != null && tvSwitchOn.clip != null) { tvSwitchOn.pitch = startupPitch; tvSwitchOn.Play(); }
        }
        wasMatchStarted = started;
        if (ambientLoop != null && ambientLoop.clip != null)
        {
            if (active && !ambientLoop.isPlaying) ambientLoop.Play();
            else if (!active && ambientLoop.isPlaying) ambientLoop.Stop();
        }
        if (!active)
        {
            nextAmbient = 0f;
            if (randomAmbient != null && randomAmbient.isPlaying) randomAmbient.Stop();
        }
        else if (nextAmbient <= 0f || Time.time >= nextAmbient)
        {
            if (nextAmbient > 0f && randomAmbient != null && randomAmbient.clip != null
                && !randomAmbient.isPlaying && Random.value < ambientChance) randomAmbient.Play();
            float low = Mathf.Max(1f, Mathf.Min(ambientIntervalMin, ambientIntervalMax));
            float high = Mathf.Max(low, Mathf.Max(ambientIntervalMin, ambientIntervalMax));
            nextAmbient = Time.time + Random.Range(low, high);
        }
        if (hit == seenHit) return;
        seenHit = hit;
        if (!started || cardHandling == null) return;
        // Alternate the two variants after a random first choice, avoiding repeats.
        int choice = lastCardClip < 0 ? Random.Range(0, 2) : 1 - lastCardClip;
        lastCardClip = choice;
        AudioClip clip = choice == 0 ? pickupCard : slideCard;
        if (clip == null) clip = choice == 0 ? slideCard : pickupCard;
        if (clip == null) return;
        cardHandling.clip = clip;
        cardHandling.pitch = Random.Range(Mathf.Min(cardPitchMin, cardPitchMax), Mathf.Max(cardPitchMin, cardPitchMax));
        cardHandling.Play();
    }
}
