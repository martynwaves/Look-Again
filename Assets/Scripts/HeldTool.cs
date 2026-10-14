// Shared by the notebook and the spectral camera: a button raises the tool in front of the eyes,
// and its corner icon shows while every tool is lowered. In a headset the tool floats in the world and turns after the head.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public abstract class HeldTool : MonoBehaviour
{
    static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    static readonly List<HeldTool> enabledTools = new();

    [Header("Buttons")]
    [SerializeField] InputAction headsetButton;
    [Tooltip("The headset button's name, as the hint shows it.")]
    [SerializeField] string headsetButtonName;
    [SerializeField] InputAction keyboardKey;
    [Tooltip("On a keyboard, a tap toggles the tool instead of the key being held.")]
    [SerializeField] bool keyboardTaps = true;

    [Header("Raising")]
    [SerializeField] protected float distance = 0.5f;
    [SerializeField, FormerlySerializedAs("openTime")] float raiseTime = 0.18f;
    [SerializeField, FormerlySerializedAs("closeTime")] float lowerTime = 0.12f;

    [Header("Icon")]
    [SerializeField, FormerlySerializedAs("icon1"), FormerlySerializedAs("icon2")] MeshRenderer icon;
    [SerializeField, FormerlySerializedAs("iconHint1"), FormerlySerializedAs("hint2")] TMP_Text hint;
    [SerializeField] Vector2 iconAngles = new(32f, 27f);
    [SerializeField] Vector2 iconOnScreen = new(0.86f, 0.72f);
    [SerializeField] float iconSize = 4.5f;
    [SerializeField, Range(0f, 1f)] float iconAlpha = 0.6f;
    [Tooltip("How far away the icon floats, in meters. It keeps its size in view; far enough away, it doesn't double in a headset while the eyes look past it.")]
    [SerializeField] float iconDistance = 1.5f;

    [Header("Headset")]
    [Tooltip("How quickly the tool turns after the head while lowered. Lower trails further behind.")]
    [SerializeField] float loweredFollow = 5f;
    [Tooltip("How quickly the tool turns after the head while raised.")]
    [SerializeField] float raisedFollow = 14f;

    // How far the tool is raised, eased, from 0 (lowered) to 1 (raised)
    public float Raised { get; private set; }

    protected bool Headset => headset;
    protected Camera View => view;
    protected TMP_Text Hint => hint;

    // Whether the corner icon is wanted at all
    protected virtual bool ShowsIcon => true;

    Camera view;
    MaterialPropertyBlock iconBlock;
    float raised;
    bool headset, tappedOpen;
    bool? hintForHeadset;

    protected HeldTool(string name, string headsetBinding, string headsetButtonName, string keyboardBinding)
    {
        headsetButton = new($"{name} (headset)", InputActionType.Button, headsetBinding);
        this.headsetButtonName = headsetButtonName;
        keyboardKey = new($"{name} (keyboard)", InputActionType.Button, keyboardBinding);
    }

    protected virtual void Awake()
    {
        view = GetComponentInParent<Camera>();
        iconBlock = new MaterialPropertyBlock();
    }

    protected virtual void OnEnable()
    {
        headsetButton.Enable();
        keyboardKey.Enable();
        enabledTools.Add(this);
    }

    protected virtual void OnDisable()
    {
        headsetButton.Disable();
        keyboardKey.Disable();
        enabledTools.Remove(this);
        Raised = 0f;
    }

    void Update()
    {
        headset = UnityEngine.XR.XRSettings.isDeviceActive;
        bool open = WantsRaised();
        float step = Time.deltaTime / Mathf.Max(open ? raiseTime : lowerTime, 0.001f);
        raised = Mathf.MoveTowards(raised, open ? 1f : 0f, step);
        Raised = 1f - (1f - raised) * (1f - raised) * (1f - raised);

        LayOut(Raised);
        LayOutIcon();
    }

    // Whether the player is asking for the tool this frame: the headset button, or the keyboard key held or tapped
    protected virtual bool WantsRaised()
    {
        if (headset) return headsetButton.IsPressed();
        if (!keyboardTaps) return keyboardKey.IsPressed();
        if (keyboardKey.WasPressedThisFrame()) tappedOpen = !tappedOpen;
        return tappedOpen;
    }

    // Head-locked things feel stuck to the face in a headset and judder under reprojection, so there the tool eases after the head
    void LateUpdate()
    {
        if (view == null) return;
        Transform head = view.transform;
        Transform parent = headset ? head.parent : head;
        if (transform.parent != parent)
        {
            transform.SetParent(parent, false);
            transform.SetPositionAndRotation(head.position, headset ? Facing(head) : head.rotation);
        }
        if (!headset) return;

        float follow = Mathf.Lerp(loweredFollow, raisedFollow, Raised);
        Quaternion turned = Quaternion.Slerp(transform.rotation, Facing(head), 1f - Mathf.Exp(-follow * Time.deltaTime));
        transform.SetPositionAndRotation(head.position, turned);
    }

    // Which way the tool faces in a headset: where the head looks, kept level
    protected virtual Quaternion Facing(Transform head) => Upright(head);

    // The head's facing without its tilt
    static Quaternion Upright(Transform head)
    {
        return Vector3.Cross(head.forward, Vector3.up).sqrMagnitude > 1e-4f ? Quaternion.LookRotation(head.forward, Vector3.up) : head.rotation;
    }

    protected abstract void LayOut(float eased);

    // Tangents of a direction from the eye: fixed angles in a headset, a fraction of the screen on a monitor
    protected Vector2 Direction(Vector2 angles, Vector2 onScreen)
    {
        if (headset || view == null) return new(Mathf.Tan(angles.x * Mathf.Deg2Rad), Mathf.Tan(angles.y * Mathf.Deg2Rad));
        float up = Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
        return Vector2.Scale(onScreen, new Vector2(up * view.aspect, up));
    }

    void LayOutIcon()
    {
        if (icon == null) return;

        float alpha = ShowsIcon ? iconAlpha : 0f;
        foreach (HeldTool tool in enabledTools) alpha *= 1f - tool.Raised;

        bool shown = alpha > 0f;
        if (icon.gameObject.activeSelf != shown) icon.gameObject.SetActive(shown);
        if (!shown) return;

        Vector2 at = Direction(iconAngles, iconOnScreen);
        icon.transform.localPosition = new Vector3(iconDistance * at.x, -iconDistance * at.y, iconDistance);
        icon.transform.localScale = Vector3.one * (2f * iconDistance * Mathf.Tan(iconSize * 0.5f * Mathf.Deg2Rad));

        icon.GetPropertyBlock(iconBlock);
        iconBlock.SetFloat(AlphaId, alpha);
        icon.SetPropertyBlock(iconBlock);

        if (hint == null) return;
        if (hint.alpha != alpha) hint.alpha = alpha;
        if (hintForHeadset != headset)
        {
            // The headset buttons are always held, while a keyboard tap may toggle
            hint.text = headset ? $"Hold {headsetButtonName}" : $"{(keyboardTaps ? "Press" : "Hold")} {keyboardKey.GetBindingDisplayString()}";
            hintForHeadset = headset;
        }
    }
}
