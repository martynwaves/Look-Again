// Shapes how an item moves while a hand or ray holds it: it always sits the same way in the hand, by its middle, and newly taken it eases over slowly. It can be kept
// upright or turned to face the eyes, and held up to the eye it settles into place there. Dragged with the mouse on a monitor, it stays under the cursor and the wheel moves it nearer or further.

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

public class HeldItemMotion : XRBaseGrabTransformer
{
    [Tooltip("Seconds the item takes to ease over to the ray when the mouse drags it out of a belt slot.")]
    [SerializeField] float takeOutTime = 0.8f;

    [Tooltip("Seconds it takes any other time.")]
    [SerializeField] float pickUpTime = 0.35f;

    [Tooltip("How the item is turned in the hand, relative to where the hand points, in degrees.")]
    [SerializeField] Vector3 heldTurn;

    [Tooltip("Keep the item standing upright while held, turning only as the hand turns about the vertical.")]
    [SerializeField] bool keepUpright;

    [Tooltip("Keep the item upright with its front (its local forward) turned toward the eyes while held.")]
    [SerializeField] bool faceEyes;

    [Tooltip("Seconds the item takes to settle at the eye when held up to it, and to leave it again.")]
    [SerializeField] float eyeSnapTime = 0.15f;

    [Tooltip("On a monitor, how much one notch of the mouse wheel moves a dragged item nearer or further, as a fraction of its distance.")]
    [SerializeField] float scrollStep = 0.1f;

    [Tooltip("How near and how far from the eyes the mouse wheel can move a dragged item, in meters.")]
    [SerializeField] Vector2 scrollRange = new(0.3f, 3f);

    // Where the item should sit while held up to the eye, or null; set by whatever looks through it
    public Pose? EyePose { get; set; }

    // Where the hand or ray holds the item, before any settling at the eye
    public Pose HandPose { get; private set; }

    IXRSelectInteractor holder;
    Pose from, eye;
    float easeTime, eased = 1f, atEye, cursorDistance;
    Vector3 cursorOffset;
    bool underCursor;
    int lastFrame = -1;

    // Eases in and out with no sudden start or stop
    static float Ease(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    // Whether the item is dragged by the mouse on a monitor: held at a distance by a ray the simulator aims at the mouse
    static bool FollowsCursor(IXRSelectInteractor interactor, Camera view, out Ray pointed)
    {
        pointed = default;
        if (UnityEngine.XR.XRSettings.isDeviceActive || view == null || Mouse.current == null) return false;
        if (XRInteractionSimulator.instance == null || !XRInteractionSimulator.instance.pointAndClickActive) return false;
        if (interactor is not NearFarInteractor ray || ray.selectionRegion.Value != NearFarInteractor.Region.Far) return false;
        pointed = view.ScreenPointToRay(Mouse.current.position.ReadValue());
        return true;
    }

    // How the item sits in the hand, however it was grabbed. The simulated controller spins as it aims down past the eyes, so a mouse drag stays level, facing the way the eyes do
    Quaternion HeldRotation(Transform hand, Camera view, bool cursor)
    {
        Quaternion turn = Quaternion.Euler(heldTurn);
        if (!keepUpright && !cursor) return hand.rotation * turn;
        Vector3 flat = Vector3.ProjectOnPlane(cursor ? view.transform.forward : hand.forward, Vector3.up);
        if (flat.sqrMagnitude < 0.01f) flat = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        return Quaternion.LookRotation(flat, Vector3.up) * turn;
    }

    public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase, ref Pose targetPose, ref Vector3 localScale)
    {
        if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic && updatePhase != XRInteractionUpdateOrder.UpdatePhase.OnBeforeRender) return;
        IXRSelectInteractor interactor = grabInteractable.firstInteractorSelecting;
        if (interactor == null) return;
        bool bySlot = interactor is XRSocketInteractor;
        bool dynamic = updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic;
        // Not held last frame, so whoever holds it now has just picked it up, even the same hand again
        if (Time.frameCount - lastFrame > 1) holder = null;
        lastFrame = Time.frameCount;

        Camera view = Camera.main;
        bool cursor = FollowsCursor(interactor, view, out Ray pointed);

        // A new hand or ray starts the item easing over from where it was
        if (!ReferenceEquals(interactor, holder))
        {
            if (!bySlot)
            {
                from = new Pose(transform.position, transform.rotation);
                easeTime = cursor && holder is XRSocketInteractor ? takeOutTime : pickUpTime;
                eased = 0f;
                atEye = 0f;
            }
            holder = interactor;
            underCursor = false;
        }
        if (bySlot) return;

        // The simulated controller aims from beside the eyes, so its ray lags the mouse; the item stays under the mouse at the distance it was taken
        if (cursor && !underCursor)
        {
            cursorDistance = Vector3.Dot(transform.position - pointed.origin, pointed.direction);
            cursorOffset = transform.position - pointed.GetPoint(cursorDistance);
        }
        underCursor = cursor;
        // The mouse wheel draws it nearer or pushes it further along the cursor's line; right-click scrolling is the simulator's own
        if (cursor && dynamic && !Mouse.current.rightButton.isPressed)
        {
            // One notch reads as 1, or as 120 on Windows when the input settings keep the platform's own range
            bool uniform = InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms;
            float notches = Mouse.current.scroll.ReadValue().y / (uniform ? 1f : 120f);
            if (notches != 0f) cursorDistance = Mathf.Clamp(cursorDistance * Mathf.Pow(1f + scrollStep, notches), scrollRange.x, scrollRange.y);
        }
        if (cursor) targetPose.position = pointed.GetPoint(cursorDistance) + cursorOffset;

        Transform hand = interactor.GetAttachTransform(grabInteractable);
        targetPose.rotation = HeldRotation(hand, view, cursor);
        if (faceEyes && view != null)
        {
            Vector3 toEyes = Vector3.ProjectOnPlane(view.transform.position - targetPose.position, Vector3.up);
            if (toEyes.sqrMagnitude > 1e-4f) targetPose.rotation = Quaternion.LookRotation(toEyes, Vector3.up);
        }
        if (!cursor)
        {
            Vector3 middle = TryGetComponent(out IStowedShape shape) ? shape.StowedMiddle : Vector3.zero;
            targetPose.position = hand.position - targetPose.rotation * Vector3.Scale(middle, transform.lossyScale);
        }

        if (eased < 1f)
        {
            if (dynamic) eased = Mathf.MoveTowards(eased, 1f, Time.deltaTime / Mathf.Max(easeTime, 0.001f));
            targetPose.position = Vector3.Lerp(from.position, targetPose.position, Ease(eased));
            targetPose.rotation = Quaternion.Slerp(from.rotation, targetPose.rotation, Ease(eased));
        }

        // Held up to the eye, it settles there, while the hand's own hold is kept for telling when it's taken away
        HandPose = targetPose;
        if (EyePose.HasValue) eye = EyePose.Value;
        if (dynamic) atEye = Mathf.MoveTowards(atEye, EyePose.HasValue ? 1f : 0f, Time.deltaTime / Mathf.Max(eyeSnapTime, 0.001f));
        if (atEye <= 0f) return;
        targetPose.position = Vector3.Lerp(targetPose.position, eye.position, Ease(atEye));
        targetPose.rotation = Quaternion.Slerp(targetPose.rotation, eye.rotation, Ease(atEye));
    }
}
