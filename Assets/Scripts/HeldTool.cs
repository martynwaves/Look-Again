// Shared by the notebook, the spectral camera and the lantern, each a real item the player carries in either hand or on their belt.
// On a keyboard a key takes the item up in front of the eyes and back, and its corner icon shows while the player has it.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public abstract class HeldTool : MonoBehaviour
{
    static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    static readonly List<HeldTool> enabledTools = new();

    [Header("Item")]
    [Tooltip("The tool itself, which the player picks up, puts down, passes between hands and carries on their belt.")]
    [SerializeField] protected XRGrabInteractable item;

    [Header("Keyboard")]
    [SerializeField] InputAction keyboardKey;
    [Tooltip("A tap toggles the tool instead of the key being held.")]
    [SerializeField] bool keyboardTaps = true;

    [Header("Raising")]
    [SerializeField] protected float distance = 0.5f;
    [SerializeField] float raiseTime = 0.18f;
    [SerializeField] float lowerTime = 0.12f;

    [Header("Icon")]
    [Tooltip("The corner icon, placed in the scene; this only fades it in and out.")]
    [SerializeField] MeshRenderer icon;
    [Tooltip("The key hint under the icon, written in the scene; this only fades it with the icon.")]
    [SerializeField] TMP_Text hint;
    [SerializeField, Range(0f, 1f)] float iconAlpha = 0.6f;

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

    // Whether the player has the item with them: in a hand, on the belt, or taken up from the keyboard
    protected bool InReach => item != null && item.isSelected && OnPlayer(item.firstInteractorSelecting);

    // Whether one of the player's hands holds the item
    protected bool InHand => item != null && item.isSelected && item.firstInteractorSelecting is not XRSocketInteractor && OnPlayer(item.firstInteractorSelecting);

    // Where the item sits while taken up from the keyboard, and how it's turned there
    protected abstract Transform RaisedAnchor { get; }
    protected virtual Quaternion RaisedRotation => Quaternion.identity;

    // Whether the corner icon is wanted at all
    protected virtual bool ShowsIcon => !headset && InReach;

    Camera view;
    Transform rig;
    XRSocketInteractor raisedSlot, lastSlot;
    MaterialPropertyBlock iconBlock;
    float raised;
    bool headset, tappedOpen, tookOut;

    // Whether the item is up in front of the eyes, taken there from the keyboard
    protected bool TakenOut => raisedSlot != null && raisedSlot.IsSelecting(item);

    protected HeldTool(string name, string keyboardBinding)
    {
        keyboardKey = new($"{name} (keyboard)", InputActionType.Button, keyboardBinding);
    }

    protected virtual void Awake()
    {
        view = GetComponentInParent<Camera>();
        rig = view != null ? view.transform.root : null;
        iconBlock = new MaterialPropertyBlock();
        if (item == null) return;

        // The player walks through what they carry rather than being stopped by it
        CharacterController body = rig != null ? rig.GetComponentInChildren<CharacterController>() : null;
        if (body != null)
            foreach (Collider part in item.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(body, part);

        // Taken up from the keyboard, a trigger-less slot in front of the eyes holds the item, so it glides there and a hand can still take it
        if (RaisedAnchor == null) return;
        GameObject slot = new($"{name} (raised slot)");
        slot.transform.SetParent(RaisedAnchor, false);
        slot.transform.localRotation = RaisedRotation;
        BeltSlot raising = slot.AddComponent<BeltSlot>();
        raising.HoldsByMiddle = false;
        raising.showInteractableHoverMeshes = false;
        raisedSlot = raising;
    }

    protected virtual void OnEnable()
    {
        keyboardKey.Enable();
        enabledTools.Add(this);
        if (item != null) item.selectEntered.AddListener(RememberSlot);
    }

    protected virtual void OnDisable()
    {
        keyboardKey.Disable();
        enabledTools.Remove(this);
        if (item != null) item.selectEntered.RemoveListener(RememberSlot);
        Raised = 0f;
    }

    // The belt slot the item last sat in, which the keyboard puts it back into
    void RememberSlot(SelectEnterEventArgs args)
    {
        if (args.interactorObject is BeltSlot slot && slot != raisedSlot) lastSlot = slot;
    }

    bool OnPlayer(IXRSelectInteractor interactor) => rig != null && interactor.transform.IsChildOf(rig);

    void Update()
    {
        headset = UnityEngine.XR.XRSettings.isDeviceActive;
        // A hand took the item from in front of the eyes, so it's no longer the keyboard's to put back
        if (tookOut && !TakenOut)
        {
            tookOut = false;
            tappedOpen = false;
        }

        // The item glides up as soon as it's asked for and back as soon as it isn't, rather than first dropping out of view
        bool open = WantsRaised();
        if (open && !tookOut && !headset) TakeOut();
        else if (!open && tookOut) PutBack();
        float step = Time.deltaTime / Mathf.Max(open ? raiseTime : lowerTime, 0.001f);
        raised = Mathf.MoveTowards(raised, open ? 1f : 0f, step);
        Raised = 1f - (1f - raised) * (1f - raised) * (1f - raised);

        LayOut(Raised);
        LayOutIcon();
    }

    // Whether the player is asking for the tool this frame: on a keyboard, its key held or tapped while they have the item
    protected virtual bool WantsRaised()
    {
        if (headset || !InReach) return tappedOpen = false;
        if (!keyboardTaps) return keyboardKey.IsPressed();
        if (keyboardKey.WasPressedThisFrame()) tappedOpen = !tappedOpen;
        return tappedOpen;
    }

    // Takes the item from the belt or a hand up to the eyes
    void TakeOut()
    {
        if (item == null || raisedSlot == null || raisedSlot.hasSelection) return;
        if (item.isSelected) item.interactionManager.SelectExit(item.firstInteractorSelecting, item);
        item.interactionManager.SelectEnter((IXRSelectInteractor)raisedSlot, (IXRSelectInteractable)item);
        tookOut = TakenOut;
    }

    // Puts the item on the belt (the slot it last sat in, else its home slot, else any free one, else it drops); never into a hand, which would leave it hanging at the eyes
    void PutBack()
    {
        tookOut = false;
        XRSocketInteractor home = Belt.HomeOf(item);
        IXRSelectInteractor to = Belt.IsFree(lastSlot) ? lastSlot : Belt.IsFree(home) ? home : Belt.FreeSlot();
        item.interactionManager.SelectExit((IXRSelectInteractor)raisedSlot, (IXRSelectInteractable)item);
        if (to != null) item.interactionManager.SelectEnter(to, item);
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

    // Fades the icon and its hint out while any tool is raised, leaving them where the scene puts them
    void LayOutIcon()
    {
        if (icon == null) return;

        float alpha = ShowsIcon ? iconAlpha : 0f;
        foreach (HeldTool tool in enabledTools) alpha *= 1f - tool.Raised;

        bool shown = alpha > 0f;
        if (icon.gameObject.activeSelf != shown) icon.gameObject.SetActive(shown);
        if (!shown) return;

        icon.GetPropertyBlock(iconBlock);
        iconBlock.SetFloat(AlphaId, alpha);
        icon.SetPropertyBlock(iconBlock);

        if (hint != null && hint.alpha != alpha) hint.alpha = alpha;
    }
}
