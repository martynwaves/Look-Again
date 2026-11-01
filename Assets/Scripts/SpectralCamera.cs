// The spectral camera: on a monitor a key raises a viewfinder that reveals spectral objects inside its window.
// In a headset, bringing it to the eye turns the whole view spectral while its frame fades away, and taking it away shuts the view dark for a moment.

using TMPro;
using UnityEngine;

public class SpectralCamera : HeldTool
{
    static readonly int OpenId = Shader.PropertyToID("_Open");
    static readonly int MarginId = Shader.PropertyToID("_Margin");
    static readonly int RadiusId = Shader.PropertyToID("_Radius");
    static readonly int FrameId = Shader.PropertyToID("_Frame");
    static readonly int ShutId = Shader.PropertyToID("_Shut");

    static SpectralCamera current;

    // How far the viewfinder is raised, read by the spectral render feature
    public static float Reveal => current != null ? current.Raised : 0f;

    // Whether spectral objects show across the whole view rather than only in the window
    public static bool FullView => current != null && current.Headset;

    [Header("Viewfinder")]
    [SerializeField] Transform viewfinder;
    [SerializeField] MeshRenderer mask;
    [SerializeField] MeshRenderer lens;
    [Tooltip("The frame's size in a headset, in degrees. Just inside the view, so both eyes see it before it fades.")]
    [SerializeField] Vector2 windowAngles = new(84f, 66f);
    [Tooltip("The window's size on a monitor, as a fraction of the screen.")]
    [SerializeField] Vector2 windowOnScreen = new(0.86f, 0.8f);
    [SerializeField] float raiseDrop = 0.35f;
    [Tooltip("In a headset, how far the lens reaches past the frame, in window heights, so it fills the view.")]
    [SerializeField] float fullMargin = 4f;
    [Tooltip("Seconds the frame and its markings take to fade once looking through in a headset.")]
    [SerializeField] float frameFadeTime = 2f;

    [Header("Held Camera")]
    [Tooltip("Where the eye goes on the camera item, in its own space. The item faces the way it shoots.")]
    [SerializeField] Vector3 eyepiece = new(0f, 0.01f, -0.03f);
    [Tooltip("How close the eyepiece must come to the eye to look through it, in meters.")]
    [SerializeField] float lookDistance = 0.3f;
    [Tooltip("How far the eyepiece must move from the eye to stop looking through it, in meters.")]
    [SerializeField] float stopDistance = 0.4f;
    [Tooltip("How closely the held camera must point where the head looks, from 0 (anywhere ahead) to 1 (exactly).")]
    [SerializeField, Range(0f, 1f)] float lookAlignment = 0.4f;

    [Header("Leaving")]
    [Tooltip("Two dark halves that close across the view as the camera leaves the eye.")]
    [SerializeField] MeshRenderer shutter;
    [Tooltip("Seconds the halves take to close, how long the view stays dark, and how long they take to open.")]
    [SerializeField] float shutTime = 0.14f, darkTime = 0.12f, reopenTime = 0.3f;

    MaterialPropertyBlock lensBlock, maskBlock, shutterBlock;
    TMP_Text raiseTip;
    Renderer[] model;
    ParticleSystem[] effects;
    HeldItemMotion motion;
    HandPoser hiddenHand;
    float frame = 1f, shut, dark;
    bool looking, shutting, lookedThrough, modelShown = true;

    public SpectralCamera() : base("Spectral Camera", "<Keyboard>/f") { }

    Transform HeldCamera => item != null ? item.transform : null;

    bool Held => Headset && InHand;

    // Taken up from the keyboard, the camera goes to the eye, where it's hidden
    protected override Transform RaisedAnchor => transform;

    protected override void Awake()
    {
        base.Awake();
        lensBlock = new MaterialPropertyBlock();
        maskBlock = new MaterialPropertyBlock();
        shutterBlock = new MaterialPropertyBlock();
        if (HeldCamera == null) return;
        model = HeldCamera.GetComponentsInChildren<Renderer>();
        effects = HeldCamera.GetComponentsInChildren<ParticleSystem>();
        motion = HeldCamera.GetComponent<HeldItemMotion>();
        raiseTip = CreateTip(HeldCamera);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        current = this;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (current == this) current = null;
    }

    // Spectral vision stays on until the view has shut, so it never visibly drops out
    protected override bool WantsRaised()
    {
        bool wasLooking = looking;
        bool raised = WantsLook();
        shutting = !looking && (shutting || wasLooking);
        return raised || shutting;
    }

    // In a headset, looking through means the eyepiece near the eye, aimed roughly where the head looks; it's judged by the hand, as the camera settles on the eye
    bool WantsLook()
    {
        if (!Held || View == null)
        {
            looking = false;
            if (motion != null) motion.EyePose = null;
            return base.WantsRaised();
        }
        Transform head = View.transform;
        Pose hand = motion != null ? motion.HandPose : new Pose(HeldCamera.position, HeldCamera.rotation);
        Vector3 eye = Vector3.Scale(eyepiece, HeldCamera.lossyScale);
        float reach = Vector3.Distance(hand.position + hand.rotation * eye, head.position);
        bool aimed = Vector3.Dot(hand.rotation * Vector3.forward, head.forward) >= lookAlignment;
        looking = aimed && reach <= (looking ? stopDistance : lookDistance);
        if (looking) lookedThrough = true;
        if (motion != null) motion.EyePose = looking ? new Pose(head.position - head.rotation * eye, head.rotation) : (Pose?)null;
        return looking;
    }

    // While looking through, the viewfinder points where the held camera does
    protected override Quaternion Facing(Transform head) => Held ? HeldCamera.rotation : base.Facing(head);

    protected override void LayOut(float eased)
    {
        bool shown = eased > 0f;
        if (model != null)
        {
            // At the eye (looked through, taken up from the keyboard, or held by an untracked hand) only its look is hidden, so it can still be grabbed
            bool atEye = View != null && Vector3.Distance(HeldCamera.position, View.transform.position) < 0.08f;
            bool show = !shown && !atEye;
            if (modelShown != show)
            {
                modelShown = show;
                foreach (Renderer part in model) part.enabled = show;
                // Particles made while hidden at the eye would hang in front of it once shown again
                foreach (ParticleSystem effect in effects)
                {
                    if (show) effect.Play();
                    else effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
            // The hand holding it there would sit a few centimetres in front of the eyes, too close to focus on
            HandPoser holding = !show && InHand ? HandPoser.Of(item.firstInteractorSelecting) : null;
            if (holding != hiddenHand)
            {
                if (hiddenHand != null) hiddenHand.Shown = true;
                hiddenHand = holding;
                if (hiddenHand != null) hiddenHand.Shown = false;
            }
        }
        LayOutTip();
        LayOutShutter();
        if (viewfinder.gameObject.activeSelf != shown) viewfinder.gameObject.SetActive(shown);
        // The frame only marks the change of vision in a headset, where it would feel stuck to the eyes
        frame = shown && Headset ? Mathf.MoveTowards(frame, 0f, Time.deltaTime / Mathf.Max(frameFadeTime, 0.001f)) : 1f;
        if (!shown) return;

        Vector2 window = Direction(windowAngles * 0.5f, windowOnScreen);
        Material lensMaterial = lens.sharedMaterial;
        float margin = Headset ? fullMargin : lensMaterial.GetFloat(MarginId);
        float width = 2f * distance * window.x, height = 2f * distance * window.y;
        Vector3 size = new(width + 2f * margin * height, height * (1f + 2f * margin), 1f);
        lens.transform.localScale = size;
        mask.transform.localScale = size;
        viewfinder.localPosition = new Vector3(0f, Headset ? 0f : -(1f - eased) * raiseDrop * height, distance);

        lensBlock.SetFloat(OpenId, eased);
        lensBlock.SetFloat(MarginId, margin);
        lensBlock.SetFloat(FrameId, Mathf.SmoothStep(0f, 1f, frame));
        lens.SetPropertyBlock(lensBlock);
        maskBlock.SetFloat(MarginId, margin);
        maskBlock.SetFloat(RadiusId, lensMaterial.GetFloat(RadiusId));
        mask.SetPropertyBlock(maskBlock);
    }

    // Leaving the eye shuts the view like eyelids, holds it dark while the vision drops away, then lets it open again
    void LayOutShutter()
    {
        if (shutter == null)
        {
            shutting = false;
            return;
        }
        if (shutting)
        {
            shut = Mathf.MoveTowards(shut, 1f, Time.deltaTime / Mathf.Max(shutTime, 0.001f));
            if (shut >= 1f)
            {
                shutting = false;
                dark = darkTime;
            }
        }
        else if (dark > 0f) dark -= Time.deltaTime;
        else shut = Mathf.MoveTowards(shut, 0f, Time.deltaTime / Mathf.Max(reopenTime, 0.001f));

        bool shown = shut > 0f;
        if (shutter.enabled != shown) shutter.enabled = shown;
        if (!shown) return;
        shutterBlock.SetFloat(ShutId, Mathf.SmoothStep(0f, 1f, shut));
        shutter.SetPropertyBlock(shutterBlock);
    }

    // The tip shows by the camera in a headset until the player first looks through it, turned to face them
    void LayOutTip()
    {
        if (raiseTip == null) return;
        bool shown = Held && !lookedThrough;
        if (raiseTip.gameObject.activeSelf != shown) raiseTip.gameObject.SetActive(shown);
        if (shown && View != null) raiseTip.transform.rotation = Quaternion.LookRotation(raiseTip.transform.position - View.transform.position, Vector3.up);
    }

    // Small text above the camera, in the same font as the icon hints
    TMP_Text CreateTip(Transform parent)
    {
        TextMeshPro tip = new GameObject("Raise Tip", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
        tip.transform.SetParent(parent, false);
        tip.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        if (Hint != null)
        {
            tip.font = Hint.font;
            tip.fontSharedMaterial = Hint.fontSharedMaterial;
            tip.color = Hint.color;
        }
        tip.text = "Raise to your eye";
        tip.fontSize = 0.15f;
        tip.alignment = TextAlignmentOptions.Center;
        tip.textWrappingMode = TextWrappingModes.NoWrap;
        tip.rectTransform.sizeDelta = new Vector2(0.3f, 0.05f);
        tip.gameObject.SetActive(false);
        return tip;
    }
}
