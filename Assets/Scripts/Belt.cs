// A belt at the player's waist whose slots hold items: let go of an item by a slot to stow it there, grab it to take it back out.
// In a headset, anything that's been on the belt goes back to it when let go near the body; only held out at arm's length does it drop.
// It stays under the head and turns after it slowly, so the slots are where the hands expect them.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class Belt : MonoBehaviour
{
    static Belt instance;

    [Tooltip("The player's head, which the belt hangs under.")]
    [SerializeField] Transform head;

    [Tooltip("How far below the eyes the belt sits, in meters.")]
    [SerializeField] float belowEyes = 0.56f;

    [Tooltip("How far behind the eyes the body's middle is, in meters.")]
    [SerializeField] float behindEyes = 0f;

    [Tooltip("The same without a headset, where the hip slots would sit behind the view; negative hangs the belt ahead of the eyes.")]
    [SerializeField] float behindEyesOnMonitor = -0.12f;

    [Tooltip("How quickly the belt turns after the head. Lower trails further behind.")]
    [SerializeField] float follow = 3f;

    [Header("Catching")]
    [Tooltip("How far out from the belt, across, a carried item let go of still goes back to a slot, in meters. Further out it drops.")]
    [SerializeField] float catchReach = 0.5f;

    [Tooltip("How far below and above the belt that reaches, in meters.")]
    [SerializeField] Vector2 catchHeight = new(0.5f, 0.65f);


    XRSocketInteractor[] slots;
    NearFarInteractor[] hands;
    BeltPointer[] pointers;
    readonly HashSet<XRGrabInteractable> carried = new();
    readonly Dictionary<XRGrabInteractable, float> letGoAt = new();

    // Whether the player has yet put something on the belt by letting go of it there
    public static bool HandStowed { get; private set; }

    // Whether an item belongs on the belt, having been on it
    public static bool IsCarried(XRGrabInteractable item) => instance != null && item != null && instance.carried.Contains(item);
    bool placed;

    void Awake()
    {
        instance = this;
        HandStowed = false;
        slots = GetComponentsInChildren<XRSocketInteractor>();
        hands = transform.root.GetComponentsInChildren<NearFarInteractor>(true);
        pointers = transform.root.GetComponentsInChildren<BeltPointer>(true);
    }

    void OnEnable()
    {
        foreach (XRSocketInteractor slot in slots)
        {
            slot.selectEntered.AddListener(Carry);
            if (slot.startingSelectedInteractable is XRGrabInteractable item) Remember(item);
        }
    }

    void OnDisable()
    {
        foreach (XRSocketInteractor slot in slots) slot.selectEntered.RemoveListener(Carry);
        foreach (XRGrabInteractable item in carried)
            if (item != null) item.selectExited.RemoveListener(LetGo);
        carried.Clear();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // An empty slot, or null when every slot is taken
    public static XRSocketInteractor FreeSlot()
    {
        if (instance == null) return null;
        foreach (XRSocketInteractor slot in instance.slots)
            if (IsFree(slot)) return slot;
        return null;
    }

    // The slot an item starts the game in, which it goes back to by default
    public static XRSocketInteractor HomeOf(Object item)
    {
        if (instance == null || item == null) return null;
        foreach (XRSocketInteractor slot in instance.slots)
            if (slot.startingSelectedInteractable == item) return slot;
        return null;
    }

    public static bool IsFree(XRSocketInteractor slot) => slot != null && slot.isActiveAndEnabled && !slot.hasSelection;

    void Carry(SelectEnterEventArgs args)
    {
        if (args.interactableObject is not XRGrabInteractable item) return;
        if (letGoAt.TryGetValue(item, out float when) && Time.time - when < 1f) HandStowed = true;
        Remember(item);
    }

    void Remember(XRGrabInteractable item)
    {
        if (carried.Add(item)) item.selectExited.AddListener(LetGo);
    }

    void LetGo(SelectExitEventArgs args)
    {
        if (args.interactorObject is not XRSocketInteractor && args.interactableObject is XRGrabInteractable released) letGoAt[released] = Time.time;
        // On a monitor the mouse sets things down at a distance, and the keyboard puts tools back on the belt
        if (args.isCanceled || args.interactorObject is XRSocketInteractor || !isActiveAndEnabled || !UnityEngine.XR.XRSettings.isDeviceActive) return;
        if (args.interactableObject is XRGrabInteractable item) StartCoroutine(Catch(item));
    }

    // A frame later, so a slot it was let go over, a puzzle spot or the other hand takes it first
    IEnumerator Catch(XRGrabInteractable item)
    {
        yield return null;
        if (item == null || item.isSelected || !NearBody(item)) yield break;
        XRSocketInteractor home = HomeOf(item);
        XRSocketInteractor to = IsFree(home) ? home : NearestFree(item.transform.position);
        BeltSlot.Place(to, item);
    }

    bool NearBody(XRGrabInteractable item)
    {
        Vector3 middle = item.transform.TransformPoint(BeltSlot.LocalMiddle(item.transform));
        Vector3 off = middle - transform.position;
        return new Vector2(off.x, off.z).magnitude <= catchReach && off.y >= -catchHeight.x && off.y <= catchHeight.y;
    }

    XRSocketInteractor NearestFree(Vector3 point)
    {
        XRSocketInteractor nearest = null;
        foreach (XRSocketInteractor slot in slots)
            if (IsFree(slot) && (nearest == null || (slot.transform.position - point).sqrMagnitude < (nearest.transform.position - point).sqrMagnitude)) nearest = slot;
        return nearest;
    }

    // A carried item that's fallen out of the world comes back to the belt rather than being lost
    void Update()
    {
        foreach (XRGrabInteractable item in carried)
        {
            if (item == null || item.isSelected || item.transform.position.y > transform.position.y - 20f) continue;
            XRSocketInteractor home = HomeOf(item);
            XRSocketInteractor to = IsFree(home) ? home : NearestFree(transform.position);
            BeltSlot.Place(to, item);
        }
    }

    void LateUpdate()
    {
        if (head == null) return;
        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        Quaternion facing = forward.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(forward) : transform.rotation;
        Quaternion turned = placed ? Quaternion.Slerp(transform.rotation, facing, 1f - Mathf.Exp(-follow * Time.deltaTime)) : facing;
        placed = true;
        float behind = UnityEngine.XR.XRSettings.isDeviceActive ? behindEyes : behindEyesOnMonitor;
        transform.SetPositionAndRotation(head.position + Vector3.down * belowEyes - turned * Vector3.forward * behind, turned);
        HelpGrab();
    }

    // In a headset, a grip squeezed with an empty hand takes the stowed item the belt chooses for it, which its laser shows: pointed at, or right by the hand
    void HelpGrab()
    {
        foreach (BeltPointer pointer in pointers)
            if (pointer != null) pointer.Refresh();
        if (!UnityEngine.XR.XRSettings.isDeviceActive) return;
        foreach (NearFarInteractor hand in hands)
        {
            if (hand == null || !hand.isActiveAndEnabled || hand.hasSelection || !hand.logicalSelectState.wasPerformedThisFrame) continue;
            XRGrabInteractable nearest = hand.TryGetComponent(out BeltPointer pointer) ? pointer.Chosen : BeltSlot.ChooseFor(hand, out _);
            if (nearest == null) continue;
            nearest.interactionManager.SelectExit(nearest.firstInteractorSelecting, (IXRSelectInteractable)nearest);
            BeltSlot.TakingOut = true;
            if (hand.TryGetComponent(out FarHold hold)) hold.Take(nearest);
            else nearest.interactionManager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)nearest);
            BeltSlot.TakingOut = false;
        }
    }
}
