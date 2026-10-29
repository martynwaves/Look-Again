// The crank lantern's light: it dims and flickers as its charge runs down, and winding the cap recharges it in a spray of sparks, flaring at each new level.
// In a headset a hand twists the cap; on a monitor the Lantern tool winds it from the keyboard.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class LanternLight : MonoBehaviour
{
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [Tooltip("The light the lantern casts.")]
    [SerializeField] Light lamp;

    [Tooltip("The lantern's body, whose second material is the glowing globe.")]
    [SerializeField] Renderer body;

    [Tooltip("The cap, which turns about its up axis as it's wound.")]
    [SerializeField] Transform cap;

    [Tooltip("Grabbing this and twisting the hand winds the lantern.")]
    [SerializeField] XRSimpleInteractable crank;

    [Header("Light")]
    [Tooltip("The light's intensity at full charge.")]
    [SerializeField] float intensity = 2.2f;

    [Tooltip("The globe's glow at full charge.")]
    [SerializeField, ColorUsage(false, true)] Color glow = new(0.3f, 0.52f, 0.58f);

    [Header("Charge")]
    [Tooltip("How charged the lantern is, from 0 (dark) to 1 (full).")]
    [SerializeField, Range(0f, 1f)] float charge;

    [Tooltip("Seconds a full charge lasts. The whole fade, from steady to sputtering out, plays out over this.")]
    [SerializeField] float lastsFor = 150f;

    [Tooltip("How much charge one full turn of the cap adds, either way.")]
    [SerializeField] float chargePerTurn = 0.2f;

    [Tooltip("Below this charge the light starts to falter, dipping now and then, more often and deeper the emptier it gets.")]
    [SerializeField, Range(0f, 1f)] float flickerBelow = 0.7f;

    [Header("Particles")]
    [Tooltip("Ghostly sparks thrown off where the cap meets the body, as winding's friction wakes the oil inside.")]
    [SerializeField] ParticleSystem sparks;

    [Tooltip("Sparks thrown off for each degree the cap turns.")]
    [SerializeField] float sparksPerDegree = 0.25f;

    [Tooltip("Sparks poured down inside the globe onto the flame as the cap turns, as if feeding it.")]
    [SerializeField] ParticleSystem fuel;

    [Tooltip("Sparks poured onto the flame for each degree the cap turns.")]
    [SerializeField] float fuelPerDegree = 0.6f;

    [Tooltip("Ghostly dust drifting about the lantern while it shines.")]
    [SerializeField] ParticleSystem dust;

    [Tooltip("Motes of dust a second at full brightness; fewer as it dims.")]
    [SerializeField] float dustRate = 14f;

    [Tooltip("The ghostly flame inside the globe and its hot core, as bright as the light.")]
    [SerializeField] ParticleSystemRenderer[] flames;

    [Tooltip("How much brighter the flame burns than its particles' own colour, at full charge.")]
    [SerializeField] float flameBrightness = 1.8f;

    [Tooltip("How small the flame gutters as the light dips and dies, as a fraction of its full size.")]
    [SerializeField, Range(0f, 1f)] float flameSmallest = 0.3f;

    [Header("Levels")]
    [Tooltip("How many levels a full charge is split into; winding past each one poofs the light.")]
    [SerializeField] int levels = 5;

    [Tooltip("The colour the light poofs as winding fills another level.")]
    [SerializeField] Color levelColor = new(0.5f, 0.62f, 1f);

    [Tooltip("Seconds the poof takes to fade back.")]
    [SerializeField] float levelFlashTime = 0.5f;

    [Tooltip("A ring of soft motes spreading out from the flame with each new level.")]
    [SerializeField] ParticleSystem levelPuff;

    [SerializeField] int levelPuffCount = 12;

    [Tooltip("How much bigger the flame flares for a moment at each new level, as a fraction of its size.")]
    [SerializeField] float levelFlameSurge = 0.6f;

    public float Charge => charge;
    public Transform Cap => cap;

    MaterialPropertyBlock glowBlock, flameBlock;
    IXRSelectInteractor winder;
    Quaternion lastTurn;
    Color lampColor;
    float noiseSeed, dipUntil, nextDip, dipDepth, sparksDue, fuelDue, flash, windingUntil;

    void Awake()
    {
        glowBlock = new MaterialPropertyBlock();
        flameBlock = new MaterialPropertyBlock();
        noiseSeed = Random.value * 100f;
        if (lamp != null) lampColor = lamp.color;
    }

    // How many full levels the charge holds
    int Level(float amount) => Mathf.FloorToInt(amount * Mathf.Max(levels, 1) + 1e-4f);

    void OnEnable()
    {
        if (crank == null) return;
        crank.selectEntered.AddListener(OnCrankGrabbed);
        crank.selectExited.AddListener(OnCrankReleased);
    }

    void OnDisable()
    {
        winder = null;
        if (crank == null) return;
        crank.selectEntered.RemoveListener(OnCrankGrabbed);
        crank.selectExited.RemoveListener(OnCrankReleased);
    }

    void OnCrankGrabbed(SelectEnterEventArgs args)
    {
        winder = args.interactorObject;
        lastTurn = HandTurn();
    }

    void OnCrankReleased(SelectExitEventArgs args)
    {
        if (ReferenceEquals(args.interactorObject, winder)) winder = null;
    }

    // The winding hand's rotation relative to the lantern, so carrying the lantern around doesn't wind it
    Quaternion HandTurn() => Quaternion.Inverse(transform.rotation) * winder.transform.rotation;

    // Turns the cap by some degrees, charging the lantern by how far it turned
    public void Wind(float degrees)
    {
        if (cap != null) cap.localRotation *= Quaternion.Euler(0f, degrees, 0f);
        // The charge holds while the cap turns, so a full turn always fills a full level; the grace spans a hand turning back between twists
        if (Mathf.Abs(degrees) > 0.01f) windingUntil = Time.time + 0.3f;
        int before = Level(charge);
        charge = Mathf.Min(1f, charge + Mathf.Abs(degrees) / 360f * chargePerTurn);
        if (Level(charge) > before)
        {
            flash = 1f;
            if (levelPuff != null) levelPuff.Emit(levelPuffCount);
        }
        Emit(sparks, ref sparksDue, Mathf.Abs(degrees) * sparksPerDegree);
        Emit(fuel, ref fuelDue, Mathf.Abs(degrees) * fuelPerDegree);
    }

    // Emits whole particles as they come due, carrying the fraction over to the next turn
    static void Emit(ParticleSystem system, ref float due, float more)
    {
        if (system == null) return;
        due += more;
        int count = (int)due;
        if (count <= 0) return;
        system.Emit(count);
        due -= count;
    }

    void Update()
    {
        if (winder != null)
        {
            // Only the hand's twist about the lantern's up axis turns the cap
            Quaternion now = HandTurn();
            Quaternion turn = now * Quaternion.Inverse(lastTurn);
            if (turn.w < 0f) turn = new Quaternion(-turn.x, -turn.y, -turn.z, -turn.w);
            Wind(2f * Mathf.Atan2(turn.y, turn.w) * Mathf.Rad2Deg);
            lastTurn = now;
        }

        if (Time.time >= windingUntil) charge = Mathf.Max(0f, charge - Time.deltaTime / Mathf.Max(lastsFor, 0.01f));
        // Dims steadily as it runs down, faster near the end
        float level = charge > 0f ? Mathf.Pow(charge, 0.6f) * Flicker() : 0f;
        // A new level flares brighter in a lighter royal blue, fading quickly back
        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime / Mathf.Max(levelFlashTime, 0.01f));
        float poof = flash * flash;
        if (lamp != null)
        {
            lamp.intensity = intensity * level * (1f + 1.5f * poof);
            lamp.color = Color.Lerp(lampColor, levelColor, poof);
            lamp.enabled = level > 0.001f;
        }
        // The frame inches from the light stays blown out until the charge is nearly gone, so the glass and flame dim the same way rather than in step
        float near = Mathf.Pow(Mathf.Max(level, 0f), 0.35f);
        if (body != null)
        {
            glowBlock.SetColor(EmissionId, Color.Lerp(glow, levelColor, poof) * Mathf.Max(near, poof));
            body.SetPropertyBlock(glowBlock, 1);
        }
        if (dust != null)
        {
            ParticleSystem.EmissionModule motes = dust.emission;
            motes.rateOverTimeMultiplier = dustRate * level;
        }
        Color burn = Color.Lerp(Color.white, levelColor, poof) * (Mathf.Max(near, poof) * flameBrightness);
        burn.a = 1f;
        flameBlock.SetColor(BaseColorId, burn);
        // The whole flame is scaled, wisps already alight included, so it shrinks toward the wick within a flicker and flares at a new level
        Vector3 size = Vector3.one * (Mathf.Lerp(flameSmallest, 1f, Mathf.Clamp01(level)) * (1f + levelFlameSurge * poof));
        foreach (ParticleSystemRenderer flame in flames)
        {
            if (flame == null) continue;
            flame.SetPropertyBlock(flameBlock);
            flame.transform.localScale = size;
        }
    }

    // A restless shimmer, with sudden dips once it falters that come closer together and go deeper as the charge runs out
    float Flicker()
    {
        float weak = Mathf.Clamp01(1f - charge / Mathf.Max(flickerBelow, 0.001f));
        float shimmer = 1f + (Mathf.PerlinNoise(Time.time * 9f, noiseSeed) - 0.5f) * (0.08f + 0.35f * weak);
        if (weak <= 0f) return shimmer;
        if (Time.time >= nextDip)
        {
            dipUntil = Time.time + Random.Range(0.04f, 0.1f + 0.2f * weak);
            dipDepth = Mathf.Min(Random.Range(0.25f, 0.45f) + 0.55f * weak, 0.97f);
            nextDip = dipUntil + Random.Range(0.4f, 1.3f) * Mathf.Lerp(2.5f, 0.12f, weak);
        }
        return Time.time < dipUntil ? shimmer * (1f - dipDepth) : shimmer;
    }
}
