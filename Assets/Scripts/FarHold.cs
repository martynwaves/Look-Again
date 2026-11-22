// Keeps a hand's hold on an item out along its laser: a wall can draw it in but never past the hand, which would turn its zoom around.
// An item in the hand, pulled all the way in or taken from the belt, can be pushed out with the thumbstick.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(NearFarInteractor), typeof(InteractionAttachController))]
public class FarHold : MonoBehaviour
{
    [Tooltip("How near the hand a wall can draw an item, and where pushing it back out starts, in meters.")]
    [SerializeField] float shortest = 0.1f;

    [Tooltip("Whether the thumbstick pushes an item out of the hand. Off for a hand whose thumbstick walks while an item is in it.")]
    [SerializeField] bool pushesOut = true;

    [Tooltip("How far the thumbstick must be pushed to start that.")]
    [SerializeField] float pushThreshold = 0.3f;

    NearFarInteractor hand;
    InteractionAttachController attach;

    void Awake()
    {
        hand = GetComponent<NearFarInteractor>();
        attach = GetComponent<InteractionAttachController>();
    }

    void Update()
    {
        // Only a carried item, not a door, and not one held up to the eye
        if (!pushesOut || !hand.hasSelection || attach.hasOffset) return;
        Transform held = hand.firstInteractableSelected.transform;
        if (!held.TryGetComponent(out WallStop _) || held.TryGetComponent(out HeldItemMotion motion) && motion.EyePose.HasValue) return;
        if (!attach.manipulationInput.TryReadValue(out Vector2 stick) || stick.y < pushThreshold || stick.y < Mathf.Abs(stick.x)) return;
        Transform origin = Point.parent;
        Attach.MoveTo(origin.position + origin.forward * shortest);
    }

    IInteractionAttachController Attach => attach;
    Transform Point => Attach.GetOrCreateAnchorTransform();

    // Takes an item. One the laser is on that isn't held its own way in the hand stays where it is, to be zoomed from there
    public void Take(XRGrabInteractable item)
    {
        Vector3 middle = item.transform.TransformPoint(BeltSlot.LocalMiddle(item.transform)), toHand = Point.position - middle;
        bool ahead = Vector3.Dot(toHand, Point.parent.forward) < -0.9f * toHand.magnitude;
        bool stays = pushesOut && ahead && toHand.magnitude > shortest && !item.TryGetComponent(out HeldItemMotion _);
        // Gripped by its middle, then the hold moved out to where the item was
        if (stays) item.transform.position += toHand;
        hand.interactionManager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)item);
        if (!stays) return;
        item.transform.position -= toHand;
        if (hand.IsSelecting(item)) Attach.MoveTo(middle);
    }

    // Draws the hold in along its own line by as much as something held the item back
    public void DrawIn(Vector3 back)
    {
        if (!attach.hasOffset) return;
        Vector3 origin = Point.parent.position, offset = Point.position - origin;
        float length = offset.magnitude;
        if (length < 1e-4f) return;
        Vector3 line = offset / length;
        float drawn = Mathf.Max(length + Vector3.Dot(back, line), Mathf.Min(shortest, length));
        if (length - drawn > 1e-4f) Attach.MoveTo(origin + line * drawn);
    }
}
