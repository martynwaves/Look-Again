// The spectral camera: on a monitor a key raises a viewfinder that reveals spectral objects inside its window.
// In a headset, bringing it to the eye turns the whole view spectral, framed in black; taking it away shrinks the frame onto the camera in the hand and fades it there.

using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

public class SpectralCamera : HeldTool
{
    static readonly int OpenId = Shader.PropertyToID("_Open");
    static readonly int MarginId = Shader.PropertyToID("_Margin");
    static readonly int RadiusId = Shader.PropertyToID("_Radius");
    static readonly int FrameId = Shader.PropertyToID("_Frame");
    static readonly int SurroundId = Shader.PropertyToID("_Surround");

    static SpectralCamera current;

    // How strongly spectral objects show, read by the spectral render feature: as far as the viewfinder is raised, fading with the frame as it lands on the camera
    public static float Reveal => current != null ? current.Raised * current.revealLeft : 0f;

    // Whether spectral objects show across the whole view rather than only in the window, which is while looking through in a headset
    public static bool FullView => current != null && current.Headset && current.looking;

    // Asks for the camera at the eye this frame, whether or not the player holds it there, such as while dispelling a spirit
    public static void RequestView() => requestedFrame = Time.frameCount;

    // Whether a point is in sight through the camera, walls or not: inside the frame within some distance, from the first moment it's raised to the last of lowering it
    public static bool Sees(Vector3 point, float maxDistance)
    {
        if (current == null || current.View == null || Reveal <= 0f) return false;
        Camera view = current.View;
        Vector3 toPoint = point - view.transform.position;
        if (toPoint.magnitude > maxDistance) return false;
        if (current.Headset)
        {
            Vector3 local = view.transform.InverseTransformDirection(toPoint);
            if (local.z <= 0f) return false;
            float across = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, up = Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg;
            return Mathf.Abs(across) <= current.windowAngles.x * 0.5f && Mathf.Abs(up) <= current.windowAngles.y * 0.5f;
        }
        Vector3 onScreen = view.WorldToViewportPoint(point);
        return onScreen.z > 0f && Mathf.Abs(onScreen.x - 0.5f) <= current.windowOnScreen.x * 0.5f && Mathf.Abs(onScreen.y - 0.5f) <= current.windowOnScreen.y * 0.5f;
    }

    static int requestedFrame = -10;

    [Header("Viewfinder")]
    [SerializeField] Transform viewfinder;
    [SerializeField] MeshRenderer mask;
    [SerializeField] MeshRenderer lens;
    [Tooltip("The frame's size in a headset, in degrees. Just inside the view, so both eyes see all of it.")]
    [SerializeField] Vector2 windowAngles = new(84f, 66f);
    [Tooltip("The window's size on a monitor, as a fraction of the screen.")]
    [SerializeField] Vector2 windowOnScreen = new(0.86f, 0.8f);
    [SerializeField] float raiseDrop = 0.35f;
    [Tooltip("How far the lens reaches past the frame, in window heights, so the black past it fills the view.")]
    [SerializeField] float fullMargin = 4f;

    [Header("Held Camera")]
    [Tooltip("Where the eye goes on the camera item, in its own space. The item faces the way it shoots.")]
    [SerializeField] Vector3 eyepiece = new(0f, 0.01f, -0.03f);
    [Tooltip("How close the controller holding the camera must come to the eyes to look through it, in meters.")]
    [SerializeField] float lookDistance = 0.22f;
    [Tooltip("How far the controller must move from the eyes to stop looking through it, in meters.")]
    [SerializeField] float stopDistance = 0.32f;

    [Header("Leaving")]
    [Tooltip("As the camera leaves the eye in a headset, the framed window shrinks onto it in step with its travel back to the hand. Seconds it takes at most, should the camera not get there.")]
    [SerializeField] float collapseTime = 0.5f;

    MaterialPropertyBlock lensBlock, maskBlock;
    TMP_Text raiseTip;
    Renderer[] model;
    ParticleSystem[] effects;
    HeldItemMotion motion;
    HandPoser hiddenHand;
    GameObject hiddenLine;
    Transform hiddenController;
    readonly System.Collections.Generic.List<Renderer> hiddenParts = new();
    Vector3 modelMiddle;
    float modelWidth = 0.12f, collapse, collapseStart, startDistance, revealLeft = 1f;
    bool looking, collapsing, lookedThrough, autoRaised, modelShown = true;

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
        if (HeldCamera == null) return;
        model = HeldCamera.GetComponentsInChildren<Renderer>();
        effects = HeldCamera.GetComponentsInChildren<ParticleSystem>();
        motion = HeldCamera.GetComponent<HeldItemMotion>();
        raiseTip = CreateTip("Raise to your eye");
        MeasureModel();
    }

    // The camera body's middle in its own space, and how wide it is, which the frame shrinks onto
    void MeasureModel()
    {
        Bounds body = default;
        bool found = false;
        foreach (Renderer part in model)
        {
            if (!part.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null) continue;
            Bounds b = filter.sharedMesh.bounds;
            Matrix4x4 toItem = HeldCamera.worldToLocalMatrix * part.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = toItem.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                if (!found) body = new Bounds(corner, Vector3.zero);
                else body.Encapsulate(corner);
                found = true;
            }
        }
        if (!found) return;
        modelMiddle = body.center;
        modelWidth = body.size.x * HeldCamera.lossyScale.x;
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
        HideController(null);
    }

    // Leaving the eye in a headset keeps the viewfinder up while it shrinks into the camera
    protected override bool WantsRaised()
    {
        bool wasLooking = looking;
        bool raised = WantsLook();
        // Asked for and not already held to the eye, it's taken up there from the belt or hand, and put back after
        autoRaised = Time.frameCount - requestedFrame <= 1 && !looking;
        if (autoRaised)
        {
            raised = true;
            if (Headset) looking = true;
        }
        if (wasLooking && !looking && Headset)
        {
            collapsing = true;
            collapse = 0f;
            collapseStart = Time.time;
            startDistance = View != null ? Vector3.Distance(HeldCamera.TransformPoint(modelMiddle), View.transform.position) : 0f;
        }
        if (looking) collapsing = false;
        return raised || collapsing;
    }

    protected override bool TakesOut => !Headset || autoRaised;

    // In a headset, looking through means raising the controller that holds the camera up to the eyes, however it's turned; held out on a ray,
    // the camera flies in from wherever it is. The controller decides, as the camera itself settles on the eye
    bool WantsLook()
    {
        if (!Held || View == null)
        {
            looking = false;
            if (motion != null) motion.EyePose = null;
            return base.WantsRaised();
        }
        Transform head = View.transform;
        Vector3 eye = Vector3.Scale(eyepiece, HeldCamera.lossyScale);
        Vector3 toHand = item.firstInteractorSelecting.transform.position - head.position;
        // Beside or in front of the face, not behind the head
        bool upAtFace = Vector3.Dot(toHand.normalized, head.forward) > -0.3f;
        looking = upAtFace && toHand.magnitude <= (looking ? stopDistance : lookDistance);
        if (looking) lookedThrough = true;
        if (motion != null) motion.EyePose = looking ? new Pose(head.position - head.rotation * eye, head.rotation) : (Pose?)null;
        return looking;
    }

    // While looking through, the viewfinder is fixed dead ahead of the eyes from the first frame
    protected override bool LockedToEyes => looking;

    // Leaving the eye, it starts from there and eases after the head as it shrinks away
    protected override Quaternion Facing(Transform head) => collapsing ? head.rotation : base.Facing(head);

    protected override void LayOut(float eased)
    {
        bool shown = eased > 0f;
        if (model != null)
        {
            // At the eye (looked through, taken up from the keyboard, or held by an untracked hand) only its look is hidden, so it can still be grabbed.
            // In a headset it shows again as soon as it leaves the eye, so the frame can shrink onto it
            bool atEye = View != null && Vector3.Distance(HeldCamera.position, View.transform.position) < 0.08f;
            bool show = (Headset ? !looking : !shown) && !atEye;
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
        PlaceTip(raiseTip, Held && !lookedThrough, HeldCamera.position + Vector3.up * 0.08f);
        // Nothing on the holding controller, its laser, pointer or model, may show in front of the eyes while looking through
        HideController(looking && InHand ? item.firstInteractorSelecting.transform.parent : null);
        if (viewfinder.gameObject.activeSelf != shown) viewfinder.gameObject.SetActive(shown);
        if (collapsing)
        {
            collapse = Mathf.Max(collapse, Travelled(), Mathf.Clamp01((Time.time - collapseStart) / Mathf.Max(collapseTime, 0.001f)));
            if (collapse >= 1f) collapsing = false;
        }

        // How far the window has shrunk onto the camera, once it's left the eye in a headset
        float leaving = Headset && !looking ? (collapsing ? collapse : 1f) : 0f;
        revealLeft = 1f - Smooth(0.7f, 1f, leaving);
        if (!shown) return;

        Vector2 window = Direction(windowAngles * 0.5f, windowOnScreen);
        Material lensMaterial = lens.sharedMaterial;
        float margin = fullMargin;
        float width = 2f * distance * window.x, height = 2f * distance * window.y;
        Vector3 size = new(width + 2f * margin * height, height * (1f + 2f * margin), 1f);
        float shrunk = leaving;
        size *= Mathf.Lerp(1f, modelWidth / Mathf.Max(width, 0.001f), shrunk);
        lens.transform.localScale = size;
        mask.transform.localScale = size;
        viewfinder.SetLocalPositionAndRotation(new Vector3(0f, Headset ? 0f : -(1f - eased) * raiseDrop * height, distance), Quaternion.identity);
        if (shrunk > 0f && View != null && HeldCamera != null)
        {
            // From in front of the eyes onto the camera's body, facing the eyes all the way
            Vector3 to = HeldCamera.TransformPoint(modelMiddle);
            Quaternion facing = Quaternion.LookRotation(to - View.transform.position, Vector3.up);
            viewfinder.SetPositionAndRotation(Vector3.Lerp(viewfinder.position, to, shrunk), Quaternion.Slerp(viewfinder.rotation, facing, shrunk));
        }

        // Fades out over the camera as it finishes shrinking
        lensBlock.SetFloat(OpenId, eased * revealLeft);
        lensBlock.SetFloat(MarginId, margin);
        lensBlock.SetFloat(FrameId, 1f);
        // Black past the frame, clearing as the window shrinks into the camera in a headset so the room shows around it
        lensBlock.SetFloat(SurroundId, 1f - Smooth(0.15f, 0.6f, leaving));
        lens.SetPropertyBlock(lensBlock);
        maskBlock.SetFloat(MarginId, margin);
        maskBlock.SetFloat(RadiusId, lensMaterial.GetFloat(RadiusId));
        mask.SetPropertyBlock(maskBlock);
    }

    // How far the camera has come from the eye back toward where the hand holds it, from 0 to 1
    float Travelled()
    {
        if (motion == null || View == null || HeldCamera == null) return 0f;
        Vector3 eye = View.transform.position;
        Pose hand = motion.HandPose;
        float now = Vector3.Distance(HeldCamera.TransformPoint(modelMiddle), eye);
        // Back to the hand, or to the belt when it was taken up by itself
        Vector3 to = item.firstInteractorSelecting is UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor slot ? slot.transform.position
            : hand.position + hand.rotation * Vector3.Scale(modelMiddle, HeldCamera.lossyScale);
        float rest = Vector3.Distance(to, eye);
        return Mathf.Clamp01((now - startDistance) / Mathf.Max(rest - startDistance, 0.05f));
    }

    // Eases from 0 to 1 as x goes from a to b
    static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    // Hides what a controller draws, or shows it all again when given none; the camera itself is left alone
    void HideController(Transform controller)
    {
        if (controller == hiddenController) return;
        if (hiddenLine != null) hiddenLine.SetActive(true);
        foreach (Renderer part in hiddenParts)
            if (part != null) part.enabled = true;
        hiddenLine = null;
        hiddenParts.Clear();
        hiddenController = controller;
        if (controller == null) return;

        CurveVisualController laser = controller.GetComponentInChildren<CurveVisualController>(true);
        if (laser != null && laser.gameObject.activeSelf)
        {
            hiddenLine = laser.gameObject;
            hiddenLine.SetActive(false);
        }
        foreach (Renderer part in controller.GetComponentsInChildren<Renderer>())
        {
            if (!part.enabled || part.transform.IsChildOf(HeldCamera)) continue;
            part.enabled = false;
            hiddenParts.Add(part);
        }
    }
}
