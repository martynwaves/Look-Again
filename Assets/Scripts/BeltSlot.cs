// A slot on the belt: of the free slots an item is near, only the nearest previews it and takes it, gliding it gently into place by its middle, facing out.
// The preview shows the item still, as it will sit once stowed, leaving out text and any part that's hidden or has no mesh yet.
// In a headset a stowed item is handed out by the belt when a grip is squeezed: to a laser pointed at it, or to a hand right at it, whichever neighbour is nearest; stowed, it follows the belt exactly.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

// Lets an item show the belt how it looks once stowed, when that differs from how it looks now
public interface IStowedShape
{
    // The item's middle once stowed, in its own space; a slot holds it there
    Vector3 StowedMiddle { get; }

    // How the item is turned in a slot, relative to the slot, which faces in toward the body
    Quaternion StowedTurn { get; }

    // Whether a part shows once stowed, whether or not it shows now
    bool ShowsStowed(Transform part);

    // A part's pose once stowed, in the item's own space
    Matrix4x4 StowedPose(Transform part);

    // A part's mesh once stowed, which may be shaped differently from now
    Mesh StowedMesh(MeshFilter part);
}

public class BeltSlot : XRSocketInteractor
{
    static readonly List<BeltSlot> slots = new();
    static readonly List<MeshFilter> parts = new();
    static readonly ReachOnly reachOnly = new();
    static readonly Dictionary<XRGrabInteractable, (bool position, bool rotation)> smoothing = new();
    static readonly Dictionary<GameObject, int> layers = new();

    // In a headset the toolkit's hands never take a stowed item by themselves, as their own choice would fight the belt's; the belt hands it out. On a monitor the mouse's laser is the only hand there is
    class ReachOnly : IXRHoverFilter, IXRSelectFilter
    {
        public bool canProcess => true;
        public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable) => Allows(interactor);
        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable) => Allows(interactor);

        static bool Allows(IXRInteractor interactor) => interactor is XRSocketInteractor || !UnityEngine.XR.XRSettings.isDeviceActive || TakingOut;
    }

    // Set while the belt itself hands a stowed item to a hand
    public static bool TakingOut { get; set; }

    // The layer stowed items' colliders are on, which the hands' lasers pass through in a headset, so a vase on the floor beyond the belt is still reached
    public const string StowedLayer = "Stowed";

    // How near a laser must pass a stowed item to take it without hitting it, as the sine of the angle it misses by; how near the hand must be to its surface to take it
    // wherever the laser points; and how near the hand must be to anything loose for that to come first instead
    const float Aimed = 0.06f, Touch = 0.05f, LooseTouch = 0.1f;
    static readonly List<IXRInteractable> interactables = new();

    // The stowed item a hand should take when its grip is squeezed, or null, and the spot on it its laser leads to
    public static XRGrabInteractable ChooseFor(NearFarInteractor hand, out Vector3 spot)
    {
        var stowed = new List<Transform>();
        foreach (BeltSlot slot in slots)
            if (slot.HoldsByMiddle && slot.hasSelection && slot.firstInteractableSelected is XRGrabInteractable item) stowed.Add(item.transform);
        // Loose: anything else the hand could take, such as a vase, a door or what the other hand holds
        var loose = new List<Transform>();
        hand.interactionManager.GetRegisteredInteractables(interactables);
        foreach (IXRInteractable interactable in interactables)
            if (interactable is XRBaseInteractable other && other.isActiveAndEnabled && (other.interactionLayers & hand.interactionLayers) != 0 && other.GetComponentInParent<XRGrabInteractable>() is not { firstInteractorSelecting: XRSocketInteractor })
                loose.Add(other.transform);
        Transform from = hand.TryGetComponent(out CurveInteractionCaster caster) && caster.castOrigin != null ? caster.castOrigin : hand.transform;
        Transform chosen = ChooseStowed(hand.transform.position, new Ray(from.position, from.forward), stowed, loose, out spot);
        return chosen != null ? chosen.GetComponent<XRGrabInteractable>() : null;
    }

    // The same choice from plain positions, so it can be checked without a headset. The laser takes what it runs into; just passing a stowed item,
    // it takes it unless it passes something loose more closely; and a hand right at a stowed item takes that wherever its laser points
    public static Transform ChooseStowed(Vector3 hand, Ray laser, IReadOnlyList<Transform> stowed, IReadOnlyList<Transform> loose, out Vector3 spot)
    {
        spot = default;
        Transform aimed = null, touched = null;
        Vector3 aimedSpot = default, touchedSpot = default;
        float bestAim = Aimed, bestCentred = float.MaxValue, bestTouch = Touch;
        foreach (Transform item in stowed)
        {
            float off = float.MaxValue, away = float.MaxValue;
            Vector3 itemSpot = default, byHand = default;
            foreach (Collider part in item.GetComponentsInChildren<Collider>())
            {
                if (!part.enabled || part.isTrigger) continue;
                Vector3 closest = Closest(part, hand);
                float partAway = Vector3.Distance(closest, hand);
                if (partAway < away)
                {
                    away = partAway;
                    byHand = closest;
                }
                float partOff = Miss(part, laser, 3f, out Vector3 partSpot);
                if (partOff < off)
                {
                    off = partOff;
                    itemSpot = partSpot;
                }
            }
            if (off == float.MaxValue) itemSpot = byHand;
            if (away < bestTouch)
            {
                bestTouch = away;
                touched = item;
                touchedSpot = itemSpot;
            }
            // Between two the laser runs right through, the one it runs nearer the middle of
            Vector3 toMiddle = item.TransformPoint(LocalMiddle(item)) - laser.origin;
            float along = Vector3.Dot(toMiddle, laser.direction);
            float centred = along > 0f ? (toMiddle - laser.direction * along).magnitude / toMiddle.magnitude : 1f;
            if (off < bestAim || off == 0f && bestAim == 0f && centred < bestCentred)
            {
                bestAim = off;
                bestCentred = centred;
                aimed = item;
                aimedSpot = itemSpot;
            }
        }
        if (aimed == null && touched == null) return null;

        float looseAway = float.MaxValue, looseOff = float.MaxValue;
        foreach (Transform item in loose)
            foreach (Collider part in item.GetComponentsInChildren<Collider>())
            {
                if (!part.enabled || part.isTrigger) continue;
                looseAway = Mathf.Min(looseAway, Vector3.Distance(Closest(part, hand), hand));
                float off = Miss(part, laser, 10f, out Vector3 near);
                if (off < looseOff && InSight(laser.origin, near, part)) looseOff = off;
            }
        if (looseAway <= LooseTouch && (touched == null || looseAway < bestTouch)) return null;

        if (aimed != null && (bestAim == 0f || bestAim < looseOff))
        {
            spot = aimedSpot;
            return aimed;
        }
        spot = touchedSpot;
        return touched;
    }

    // Whether no wall or piece of furniture stands between a laser's start and a spot on something
    static bool InSight(Vector3 from, Vector3 spot, Collider part) =>
        !Physics.Linecast(from, Vector3.MoveTowards(spot, from, 0.02f), out RaycastHit block, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || block.collider == part || block.collider.attachedRigidbody != null;

    // The nearest point of a collider; a hollow mesh can't give its own, so its box does
    static Vector3 Closest(Collider part, Vector3 point) => part is MeshCollider { convex: false } ? part.bounds.ClosestPoint(point) : part.ClosestPoint(point);

    // How far a laser misses a collider, as the sine of the angle from its start, zero when it hits; and the spot on it the laser passes nearest
    static float Miss(Collider part, Ray laser, float reach, out Vector3 spot)
    {
        if (part.Raycast(laser, out RaycastHit hit, reach))
        {
            spot = hit.point;
            return 0f;
        }
        Vector3 from = laser.origin, along = laser.direction;
        Vector3 onLaser = from + along * Mathf.Clamp(Vector3.Dot(part.bounds.center - from, along), 0f, reach);
        for (int i = 0; i < 3; i++) onLaser = from + along * Mathf.Clamp(Vector3.Dot(Closest(part, onLaser) - from, along), 0f, reach);
        spot = Closest(part, onLaser);
        if (Vector3.Dot(spot - from, along) <= 0.02f) return float.MaxValue;
        return (spot - onLaser).magnitude / (spot - from).magnitude;
    }

    [Tooltip("Seconds a taken item takes to glide from where it was into place.")]
    [SerializeField] float glideTime = 0.45f;

    // Whether items sit by their middle rather than their pivot. Off for the slot that holds the notebook up to read, which shows it by its spine
    public bool HoldsByMiddle { get; set; } = true;

    Pose glideFrom, rest;
    float glided = 1f;

    // Where the attach point rests for an item: so that the item's middle sits on the slot, turned as the item asks
    Pose Rest(IXRInteractable interactable)
    {
        if (!HoldsByMiddle) return Pose.identity;
        Transform item = interactable.transform;
        Quaternion turn = item.TryGetComponent(out IStowedShape shape) ? shape.StowedTurn : Quaternion.identity;
        return new Pose(-(turn * Vector3.Scale(LocalMiddle(item), item.lossyScale)), turn);
    }

    static Vector3 Middle(IXRInteractable interactable) => interactable.transform.TransformPoint(LocalMiddle(interactable.transform));

    // An item's middle in its own space: where it says it sits once stowed, else the middle of its mesh or collider
    public static Vector3 LocalMiddle(Transform item)
    {
        if (item.TryGetComponent(out IStowedShape shape)) return shape.StowedMiddle;
        if (item.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null) return filter.sharedMesh.bounds.center;
        if (item.TryGetComponent(out Collider part)) return item.InverseTransformPoint(part.bounds.center);
        return Vector3.zero;
    }

    // The item follows the attach point, which starts where the item is and glides to rest
    protected override void OnSelectEntering(SelectEnterEventArgs args)
    {
        Transform itemAttach = args.interactableObject.GetAttachTransform(this);
        attachTransform.SetPositionAndRotation(itemAttach.position, itemAttach.rotation);
        glideFrom = new Pose(attachTransform.localPosition, attachTransform.localRotation);
        rest = Rest(args.interactableObject);
        glided = 0f;
        base.OnSelectEntering(args);
        // Smoothing that softens an item in the hand would leave it trailing behind the belt as the player walks
        if (args.interactableObject is XRGrabInteractable grabbed && !smoothing.ContainsKey(grabbed))
        {
            smoothing[grabbed] = (grabbed.smoothPosition, grabbed.smoothRotation);
            grabbed.smoothPosition = grabbed.smoothRotation = false;
        }
        if (HoldsByMiddle && args.interactableObject is XRBaseInteractable stowed)
        {
            stowed.hoverFilters.Add(reachOnly);
            stowed.selectFilters.Add(reachOnly);
            foreach (Collider part in stowed.GetComponentsInChildren<Collider>(true))
            {
                if (part.isTrigger || !layers.TryAdd(part.gameObject, part.gameObject.layer)) continue;
                part.gameObject.layer = LayerMask.NameToLayer(StowedLayer);
            }
        }
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);
        if (args.interactableObject is XRGrabInteractable released && smoothing.Remove(released, out var was))
        {
            released.smoothPosition = was.position;
            released.smoothRotation = was.rotation;
        }
        if (args.interactableObject is XRBaseInteractable taken)
        {
            taken.hoverFilters.Remove(reachOnly);
            taken.selectFilters.Remove(reachOnly);
            foreach (Collider part in taken.GetComponentsInChildren<Collider>(true))
                if (layers.Remove(part.gameObject, out int layer)) part.gameObject.layer = layer;
        }
        glided = 1f;
        attachTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
    }

    // The glide places stowed items; the toolkit's snapping would tug a held item onto the slot, then let it jump back to the hand
    protected override bool StartSocketSnapping(XRGrabInteractable grabInteractable) => false;

    public override void ProcessInteractor(XRInteractionUpdateOrder.UpdatePhase updatePhase)
    {
        base.ProcessInteractor(updatePhase);
        if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic || glided >= 1f) return;
        glided = Mathf.MoveTowards(glided, 1f, Time.deltaTime / Mathf.Max(glideTime, 0.001f));
        // Eases in and out with no sudden start or stop
        float t = glided * glided * glided * (glided * (glided * 6f - 15f) + 10f);
        attachTransform.SetLocalPositionAndRotation(Vector3.Lerp(glideFrom.position, rest.position, t), Quaternion.Slerp(glideFrom.rotation, rest.rotation, t));
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        slots.Add(this);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        slots.Remove(this);
    }

    // Only things that can be carried go in a slot, not parts such as the lantern's crank
    public override bool CanHover(IXRHoverInteractable interactable) => interactable is XRGrabInteractable && base.CanHover(interactable);

    // Only the nearest slot takes an item let go among several; one put here by a script is taken regardless
    public override bool CanSelect(IXRSelectInteractable interactable)
    {
        return interactable is XRGrabInteractable && base.CanSelect(interactable) &&
            (IsSelecting(interactable) || ReferenceEquals(interactable, placing) || interactable is not IXRHoverInteractable hoverable || !IsHovering(hoverable) || Nearest(hoverable));
    }

    IXRSelectInteractable placing;

    // Puts an item in a slot from a script: into that slot, even when another one is nearer it
    public static void Place(XRSocketInteractor slot, IXRSelectInteractable item)
    {
        if (slot == null || item == null) return;
        if (slot is not BeltSlot belt)
        {
            slot.interactionManager.SelectEnter((IXRSelectInteractor)slot, item);
            return;
        }
        belt.placing = item;
        belt.interactionManager.SelectEnter((IXRSelectInteractor)belt, item);
        belt.placing = null;
    }

    // Whether this is the nearest of the free slots offering to take the item
    public bool Nearest(IXRHoverInteractable interactable)
    {
        float mine = Distance(this, interactable);
        foreach (BeltSlot other in slots)
        {
            if (other == this || other.hasSelection || !other.IsHovering(interactable)) continue;
            float theirs = Distance(other, interactable);
            if (theirs < mine || theirs == mine && slots.IndexOf(other) < slots.IndexOf(this)) return false;
        }
        return true;
    }

    static float Distance(BeltSlot slot, IXRInteractable interactable) => (slot.transform.position - Middle(interactable)).sqrMagnitude;

    protected override void DrawHoveredInteractables()
    {
        Material look = interactableHoverMeshMaterial;
        if (!showInteractableHoverMeshes || hasSelection || look == null) return;

        foreach (IXRHoverInteractable interactable in interactablesHovered)
        {
            // Nothing to preview for an item another slot already has, such as one gliding past on its way in
            if (interactable == null || !Nearest(interactable)) continue;
            if (interactable is IXRSelectInteractable { isSelected: true } held && held.firstInteractorSelecting is XRSocketInteractor) continue;
            Transform item = interactable.transform;
            item.TryGetComponent(out IStowedShape shape);

            // Where the slot will put the item: its attach point at rest on the slot, keeping its size
            Transform itemAttach = interactable.GetAttachTransform(this);
            Pose atRest = Rest(interactable);
            Matrix4x4 attachToItem = Matrix4x4.TRS(itemAttach.position, itemAttach.rotation, Vector3.one).inverse * Matrix4x4.TRS(item.position, item.rotation, Vector3.one);
            Matrix4x4 placed = transform.localToWorldMatrix * Matrix4x4.TRS(atRest.position, atRest.rotation, Vector3.one) * attachToItem * Matrix4x4.Scale(item.lossyScale);

            item.GetComponentsInChildren(parts);
            foreach (MeshFilter part in parts)
            {
                Mesh mesh = shape != null ? shape.StowedMesh(part) : part.sharedMesh;
                if (mesh == null || !part.gameObject.activeInHierarchy || part.TryGetComponent(out TMP_Text _)) continue;
                if (!part.TryGetComponent(out Renderer partLook) || !(shape != null ? shape.ShowsStowed(part.transform) : partLook.enabled)) continue;
                Matrix4x4 pose = placed * (shape != null ? shape.StowedPose(part.transform) : item.worldToLocalMatrix * part.transform.localToWorldMatrix);
                // A see-through preview casts and takes no shadow
                for (int i = 0; i < mesh.subMeshCount; i++) Graphics.DrawMesh(mesh, pose, look, gameObject.layer, null, i, null, false, false);
            }
        }
    }
}
