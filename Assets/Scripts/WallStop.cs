// Keeps a held item on the player's side of walls and furniture, judged from the eyes, so it can't be carried through one and dropped out of reach.
// Runs after the item's other grab transformers; stowed items are left to their slot. Held back, the hold itself is drawn in to match, so the item stays there once the way is clear.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

public class WallStop : XRBaseGrabTransformer
{
    static readonly RaycastHit[] hits = new RaycastHit[16];

    [Tooltip("How close to a wall the item's middle may come, in meters.")]
    [SerializeField] float clearance = 0.05f;

    Vector3 middle;

    void Awake()
    {
        // The item's middle in its own space, from its colliders, so a pivot at its base doesn't let half of it through
        Bounds? around = null;
        foreach (Collider part in GetComponentsInChildren<Collider>())
        {
            if (part.isTrigger) continue;
            if (around is Bounds b) { b.Encapsulate(part.bounds); around = b; }
            else around = part.bounds;
        }
        middle = around is Bounds all ? transform.InverseTransformPoint(all.center) : Vector3.zero;
    }

    public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase, ref Pose targetPose, ref Vector3 localScale)
    {
        if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic && updatePhase != XRInteractionUpdateOrder.UpdatePhase.OnBeforeRender) return;
        if (grabInteractable.firstInteractorSelecting is XRSocketInteractor) return;
        Camera view = Camera.main;
        if (view == null) return;

        Vector3 from = view.transform.position;
        Vector3 to = targetPose.position + targetPose.rotation * Vector3.Scale(middle, transform.lossyScale) - from;
        float reach = to.magnitude;
        if (reach < 0.001f) return;
        Vector3 along = to / reach;

        // The nearest wall or piece of furniture between the eyes and the item; loose items, the player and the item itself don't count
        Transform rig = view.transform.root;
        float nearest = reach;
        int count = Physics.SphereCastNonAlloc(from, clearance, along, hits, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hit = hits[i].collider;
            if (hits[i].distance <= 0f || hit.attachedRigidbody != null || hit.transform.IsChildOf(rig) || hit.transform.IsChildOf(transform)) continue;
            nearest = Mathf.Min(nearest, hits[i].distance);
        }
        if (nearest >= reach) return;
        Vector3 back = along * (nearest - reach);
        targetPose.position += back;
        if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic && back.sqrMagnitude > 1e-4f) DrawIn(grabInteractable, back);
    }

    // Brings the laser's hold, or the mouse's, in by as much as the item was held back, so it doesn't remember the far side of the wall
    void DrawIn(XRGrabInteractable grabInteractable, Vector3 back)
    {
        if (grabInteractable.firstInteractorSelecting is NearFarInteractor hand && hand.TryGetComponent(out FarHold hold)) hold.DrawIn(back);
        if (TryGetComponent(out HeldItemMotion motion)) motion.DrawIn(back);
        if (TryGetComponent(out MouseDrag drag)) drag.DrawIn(back);
    }
}
