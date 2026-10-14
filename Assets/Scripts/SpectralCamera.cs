// The spectral camera: raising it opens a viewfinder that reveals spectral objects.
// In a headset it's held in the right hand and looked through by bringing it to the eye; on a monitor a key raises it.

using TMPro;
using UnityEngine;

public class SpectralCamera : HeldTool
{
    static readonly int OpenId = Shader.PropertyToID("_Open");
    static readonly int MarginId = Shader.PropertyToID("_Margin");
    static readonly int RadiusId = Shader.PropertyToID("_Radius");

    static SpectralCamera current;

    // How far the viewfinder is raised, read by the spectral render feature
    public static float Reveal => current != null ? current.Raised : 0f;

    [Header("Viewfinder")]
    [SerializeField] Transform viewfinder;
    [SerializeField] MeshRenderer mask;
    [SerializeField] MeshRenderer lens;
    [Tooltip("The window's size in a headset, in degrees. Narrower than the view, so both eyes see its edges.")]
    [SerializeField] Vector2 windowAngles = new(70f, 44f);
    [Tooltip("The window's size on a monitor, as a fraction of the screen.")]
    [SerializeField] Vector2 windowOnScreen = new(0.86f, 0.8f);
    [SerializeField] float raiseDrop = 0.35f;

    [Header("Held Camera")]
    [Tooltip("The camera in the right hand, facing the way it shoots. Left empty, the headset raises the viewfinder with a button instead.")]
    [SerializeField] Transform heldCamera;
    [Tooltip("Where the eye goes on the held camera, in its own space.")]
    [SerializeField] Vector3 eyepiece = new(0f, 0.01f, -0.03f);
    [Tooltip("How close the eyepiece must come to the eye to look through it, in meters.")]
    [SerializeField] float lookDistance = 0.2f;
    [Tooltip("How far the eyepiece must move from the eye to stop looking through it, in meters.")]
    [SerializeField] float stopDistance = 0.26f;
    [Tooltip("How closely the held camera must point where the head looks, from 0 (anywhere ahead) to 1 (exactly).")]
    [SerializeField, Range(0f, 1f)] float lookAlignment = 0.7f;

    MaterialPropertyBlock lensBlock, maskBlock;
    TMP_Text raiseTip;
    bool looking, lookedThrough;

    public SpectralCamera() : base("Spectral Camera", "<XRController>{RightHand}/secondaryButton", "B", "<Keyboard>/f") { }

    bool Held => Headset && heldCamera != null;

    // In a headset the camera in the hand shows where it is, so it needs no corner icon
    protected override bool ShowsIcon => !Held;

    protected override void Awake()
    {
        base.Awake();
        lensBlock = new MaterialPropertyBlock();
        maskBlock = new MaterialPropertyBlock();
        if (heldCamera != null) raiseTip = CreateTip(heldCamera);
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

    // In a headset, looking through means the eyepiece is at the eye and the camera points roughly where the head looks
    protected override bool WantsRaised()
    {
        if (!Held || View == null) return base.WantsRaised();
        Transform head = View.transform;
        float reach = Vector3.Distance(heldCamera.TransformPoint(eyepiece), head.position);
        bool aimed = Vector3.Dot(heldCamera.forward, head.forward) >= lookAlignment;
        looking = aimed && reach <= (looking ? stopDistance : lookDistance);
        if (looking) lookedThrough = true;
        return looking;
    }

    // While looking through, the viewfinder points where the held camera does
    protected override Quaternion Facing(Transform head) => Held ? heldCamera.rotation : base.Facing(head);

    protected override void LayOut(float eased)
    {
        bool shown = eased > 0f;
        if (heldCamera != null)
        {
            // Untracked hands sit at the eye, where the camera would block the view
            bool atEye = View != null && Vector3.Distance(heldCamera.position, View.transform.position) < 0.08f;
            bool modelShown = !shown && !atEye;
            if (heldCamera.gameObject.activeSelf != modelShown) heldCamera.gameObject.SetActive(modelShown);
        }
        LayOutTip();
        if (viewfinder.gameObject.activeSelf != shown) viewfinder.gameObject.SetActive(shown);
        if (!shown) return;

        Vector2 window = Direction(windowAngles * 0.5f, windowOnScreen);
        Material lensMaterial = lens.sharedMaterial;
        float margin = lensMaterial.GetFloat(MarginId);
        float width = 2f * distance * window.x, height = 2f * distance * window.y;
        Vector3 size = new(width + 2f * margin * height, height * (1f + 2f * margin), 1f);
        lens.transform.localScale = size;
        mask.transform.localScale = size;
        viewfinder.localPosition = new Vector3(0f, -(1f - eased) * raiseDrop * height, distance);

        lensBlock.SetFloat(OpenId, eased);
        lens.SetPropertyBlock(lensBlock);
        maskBlock.SetFloat(MarginId, margin);
        maskBlock.SetFloat(RadiusId, lensMaterial.GetFloat(RadiusId));
        mask.SetPropertyBlock(maskBlock);
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
