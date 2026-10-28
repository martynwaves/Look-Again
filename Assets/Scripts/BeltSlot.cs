// A slot on the belt: of the free slots an item is near, only the nearest previews it and takes it, gliding it gently into place by its middle, facing out.
// The preview shows the item still, as it will sit once stowed, leaving out text and any part that's hidden or has no mesh yet.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

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
}

public class BeltSlot : XRSocketInteractor
{
    static readonly List<BeltSlot> slots = new();
    static readonly List<MeshFilter> parts = new();

    [Tooltip("Seconds a taken item takes to glide from where it was into place.")]
    [SerializeField] float glideTime = 0.45f;

    // Whether items sit by their middle rather than their pivot. Off for the slot that holds the notebook up to read, which shows it by its spine
    public bool HoldsByMiddle { get; set; } = true;

    Pose glideFrom, rest;
    float glided = 1f;

    // Where the attach point rests for an item: so that the item's middle sits on the slot, turned as the item asks
    Pose Rest(IXRInteractable interactable)
    {
        if (!HoldsByMiddle || !interactable.transform.TryGetComponent(out IStowedShape shape)) return Pose.identity;
        Quaternion turn = shape.StowedTurn;
        return new Pose(-(turn * Vector3.Scale(shape.StowedMiddle, interactable.transform.lossyScale)), turn);
    }

    static Vector3 Middle(IXRInteractable interactable)
    {
        Transform item = interactable.transform;
        return item.TryGetComponent(out IStowedShape shape) ? item.TransformPoint(shape.StowedMiddle) : item.position;
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
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);
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

    // Only the nearest slot takes an item let go among several; one put here by a script is taken regardless
    public override bool CanSelect(IXRSelectInteractable interactable)
    {
        return base.CanSelect(interactable) &&
            (IsSelecting(interactable) || interactable is not IXRHoverInteractable hoverable || !IsHovering(hoverable) || Nearest(hoverable));
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
                Mesh mesh = part.sharedMesh;
                if (mesh == null || !part.gameObject.activeInHierarchy || part.TryGetComponent(out TMP_Text _)) continue;
                if (!part.TryGetComponent(out Renderer partLook) || !(shape != null ? shape.ShowsStowed(part.transform) : partLook.enabled)) continue;
                Matrix4x4 pose = placed * (shape != null ? shape.StowedPose(part.transform) : item.worldToLocalMatrix * part.transform.localToWorldMatrix);
                for (int i = 0; i < mesh.subMeshCount; i++) Graphics.DrawMesh(mesh, pose, look, gameObject.layer, null, i);
            }
        }
    }
}
