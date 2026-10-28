// A belt at the player's waist whose slots hold items: let go of an item by a slot to stow it there, grab it to take it back out.
// It stays under the head and turns after it slowly, so the slots are where the hands expect them.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class Belt : MonoBehaviour
{
    static Belt instance;

    [Tooltip("The player's head, which the belt hangs under.")]
    [SerializeField] Transform head;

    [Tooltip("How far below the eyes the belt sits, in meters.")]
    [SerializeField] float belowEyes = 0.65f;

    [Tooltip("How far behind the eyes the body's middle is, in meters.")]
    [SerializeField] float behindEyes = 0.08f;

    [Tooltip("The same without a headset, where the hip slots would sit behind the view; negative hangs the belt ahead of the eyes.")]
    [SerializeField] float behindEyesOnMonitor = -0.12f;

    [Tooltip("How quickly the belt turns after the head. Lower trails further behind.")]
    [SerializeField] float follow = 3f;

    XRSocketInteractor[] slots;
    bool placed;

    void Awake()
    {
        instance = this;
        slots = GetComponentsInChildren<XRSocketInteractor>();
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

    void LateUpdate()
    {
        if (head == null) return;
        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        Quaternion facing = forward.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(forward) : transform.rotation;
        Quaternion turned = placed ? Quaternion.Slerp(transform.rotation, facing, 1f - Mathf.Exp(-follow * Time.deltaTime)) : facing;
        placed = true;
        float behind = UnityEngine.XR.XRSettings.isDeviceActive ? behindEyes : behindEyesOnMonitor;
        transform.SetPositionAndRotation(head.position + Vector3.down * belowEyes - turned * Vector3.forward * behind, turned);
    }
}
