// The ghost orb, an evil spirit's source that only the camera shows: it bobs and drifts in the dark.
// Holding a lit lantern out close by for a few seconds dispels it for good; meanwhile the camera comes up to the eye by itself, so the player sees it go.

using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class GhostOrb : MonoBehaviour
{
    [Tooltip("The part that hovers: the orb itself, its halo and the motes it sheds.")]
    [SerializeField] Transform body;

    [Tooltip("How far the body bobs up and down, and drifts sideways, in meters.")]
    [SerializeField] float bob = 0.06f, drift = 0.03f;

    [Tooltip("Bobs per second.")]
    [SerializeField] float speed = 0.12f;

    [Header("Dispelling")]
    [Tooltip("How near the lantern must be to dispel the orb, in meters.")]
    [SerializeField] float dispelRange = 1.5f;

    [Tooltip("Seconds of shining the lantern on it to dispel it.")]
    [SerializeField] float dispelTime = 3f;

    [Tooltip("Seconds a partly dispelled orb takes to recover once the light stops.")]
    [SerializeField] float recoverTime = 2f;

    [Tooltip("Seconds it takes to shrink away once dispelled.")]
    [SerializeField] float fadeTime = 1.2f;

    [Tooltip("Motes thrown off in the burst as it's dispelled.")]
    [SerializeField] int burst = 60;

    [Tooltip("How much larger the hint is than the other hints.")]
    [SerializeField] float hintSize = 1.8f;

    [Tooltip("Called once the orb is dispelled, for whatever it was holding back.")]
    [SerializeField] UnityEvent dispelled;

    [Header("Sound")]
    [Tooltip("A looping hum, rising as the orb is dispelled and dying away with it. Keep its source off the orb, which switches itself off once gone.")]
    [SerializeField] AudioSource hum;

    [Tooltip("Played as the orb bursts. Keep its source off the orb too.")]
    [SerializeField] AudioSource dispelSound;

    Lantern lantern;
    Transform[] grounded;
    Vector3[] groundedScale;
    ParticleSystem motes;
    TMP_Text tip;
    Vector3 rest, restScale;
    float progress, gone;
    bool done;

    // Whether it's been dispelled for good
    public bool Dispelled => done;

    // Where the orb hovers
    public Vector3 Middle => body.position;

    // Loses any progress made dispelling it, as when the spirit catches the player mid-way
    public void Undo()
    {
        if (!done) progress = 0f;
    }

    void Awake()
    {
        rest = body.localPosition;
        restScale = body.localScale;
        motes = GetComponentInChildren<ParticleSystem>();
        // The parts that stay on the ground, such as the light it casts on the floor, shrink away with it
        var ground = new System.Collections.Generic.List<Transform>();
        foreach (Renderer part in GetComponentsInChildren<Renderer>())
            if (!part.transform.IsChildOf(body)) ground.Add(part.transform);
        grounded = ground.ToArray();
        groundedScale = System.Array.ConvertAll(grounded, g => g.localScale);
    }

    void Start()
    {
        lantern = FindAnyObjectByType<Lantern>();
        tip = PlayerHints.Tip("");
        if (tip != null) tip.fontSize *= hintSize;
    }

    void OnDisable() => PlayerHints.Show(tip, false, default);

    void Update()
    {
        float dt = Time.deltaTime;
        float near = lantern != null ? Vector3.Distance(lantern.LightPosition, body.position) : float.MaxValue;
        bool inRange = !done && near <= dispelRange;
        bool shining = inRange && lantern.Shining;
        if (!done)
        {
            progress = Mathf.MoveTowards(progress, shining ? 1f : 0f, dt / Mathf.Max(shining ? dispelTime : recoverTime, 0.001f));
            if (progress >= 1f) Dispel();
        }
        else gone = Mathf.MoveTowards(gone, 1f, dt / Mathf.Max(fadeTime, 0.001f));
        Sfx.Loop(hum, 1f - gone, 1f + progress * 0.4f, 3f);

        // The camera comes up while it's being dispelled, and stays until the last motes have settled
        bool finished = done && gone >= 1f && (motes == null || !motes.IsAlive(true));
        if (shining || done && !finished) SpectralCamera.RequestView();

        // It shudders harder and shrinks as it's dispelled, then collapses away
        float t = Time.time * speed * 2f * Mathf.PI;
        Vector3 sway = new(Mathf.Sin(t * 0.63f) * drift, Mathf.Sin(t) * bob, Mathf.Cos(t * 0.41f) * drift);
        body.localPosition = rest + sway + Random.insideUnitSphere * (progress * progress * 0.025f * (1f - gone));
        float size = Mathf.Lerp(1f, 0.55f, progress) * (1f - gone * gone);
        body.localScale = restScale * Mathf.Max(size, 0.001f);
        for (int i = 0; i < grounded.Length; i++) grounded[i].localScale = groundedScale[i] * Mathf.Max(1f - gone, 0.001f);

        if (finished)
        {
            if (motes != null) Destroy(motes.gameObject);
            Sfx.Stop(hum);
            gameObject.SetActive(false);
        }
        // Shown through the camera, or whenever the lantern is held out close by
        else PlayerHints.Show(tip, inRange && (SpectralCamera.Reveal > 0.05f || lantern.Ready) && SetHint(shining), body.position + Vector3.up * 0.4f);
    }

    // What the player should do next, close by
    bool SetHint(bool shining)
    {
        if (tip == null || lantern == null) return false;
        bool headset = UnityEngine.XR.XRSettings.isDeviceActive;
        tip.text = !lantern.Lit ? "Wind the lantern to light it,\nthen hold it out here to dispel the spirit"
            : shining ? "Dispelling...\nKeep the lantern held out"
            : headset ? "Hold the lit lantern out\nto dispel the spirit" : "Hold the lantern out (R)\nto dispel the spirit";
        return true;
    }

    // Bursts and lets its motes fly free of the shrinking body, so they play out rather than vanish with it
    void Dispel()
    {
        done = true;
        progress = 1f;
        Sfx.Play(dispelSound, 1f, 0f);
        if (motes != null)
        {
            motes.transform.SetParent(transform, true);
            motes.Emit(burst);
            motes.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        PlayerHints.Show(tip, false, default);
        dispelled.Invoke();
    }
}
