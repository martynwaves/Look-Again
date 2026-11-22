// On a monitor, an item dragged with the mouse stays under the cursor, turned as it was taken, and the mouse wheel draws it nearer or pushes it further along the cursor's line.
// For items without their own held motion, which does the same for the tools. Runs before the wall stop, which can draw it back in.

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

public class MouseDrag : XRBaseGrabTransformer
{
    [Tooltip("How much one notch of the mouse wheel moves the item nearer or further, as a fraction of its distance.")]
    [SerializeField] float scrollStep = 0.1f;

    [Tooltip("How near and how far from the eyes the mouse wheel can move the item, in meters.")]
    [SerializeField] Vector2 scrollRange = new(0.3f, 3f);

    float distance;
    Vector3 offset;
    Quaternion turn;
    Ray pointedLast;
    bool dragging;
    int lastFrame = -1;

    public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase, ref Pose targetPose, ref Vector3 localScale)
    {
        if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic && updatePhase != XRInteractionUpdateOrder.UpdatePhase.OnBeforeRender) return;
        // Not held last frame, so this is a new drag
        if (Time.frameCount - lastFrame > 1) dragging = false;
        lastFrame = Time.frameCount;
        if (!HeldItemMotion.FollowsCursor(grabInteractable.firstInteractorSelecting, Camera.main, out Ray pointed))
        {
            dragging = false;
            return;
        }

        // Taken where it is, at that distance along the cursor's line
        if (!dragging)
        {
            distance = Vector3.Dot(transform.position - pointed.origin, pointed.direction);
            offset = transform.position - pointed.GetPoint(distance);
            turn = transform.rotation;
            dragging = true;
        }
        // The mouse wheel draws it nearer or pushes it further; right-click scrolling is the simulator's own
        if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic && !Mouse.current.rightButton.isPressed)
        {
            // One notch reads as 1, or as 120 on Windows when the input settings keep the platform's own range
            bool uniform = InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms;
            float notches = Mouse.current.scroll.ReadValue().y / (uniform ? 1f : 120f);
            if (notches != 0f) distance = Mathf.Clamp(distance * Mathf.Pow(1f + scrollStep, notches), scrollRange.x, scrollRange.y);
        }
        pointedLast = pointed;
        targetPose.position = pointed.GetPoint(distance) + offset;
        targetPose.rotation = turn;
    }

    // Brings its distance in by as much as something held it back, so it stays there
    public void DrawIn(Vector3 back)
    {
        if (dragging) distance = Mathf.Max(distance + Vector3.Dot(back, pointedLast.direction), scrollRange.x);
    }
}
