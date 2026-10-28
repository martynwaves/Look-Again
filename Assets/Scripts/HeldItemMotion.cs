// Shapes how an item moves while a hand or ray holds it: newly taken, it eases over slowly (slower still out of a belt slot), it can be kept turned to face the eyes,
// and held up to the eye it settles into place there. Dragged with the mouse on a monitor, it stays under the cursor. Slots place the item themselves.

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

public class HeldItemMotion : XRBaseGrabTransformer
{
    [Tooltip("Seconds the item takes to ease over to the hand or ray when taken out of a belt slot.")]
    [SerializeField] float takeOutTime = 0.8f;

    [Tooltip("Seconds it takes when picked up from anywhere else.")]
    [SerializeField] float pickUpTime = 0.35f;

    [Tooltip("Keep the item upright with its front (its local forward) turned toward the eyes while held.")]
    [SerializeField] bool faceEyes;

    [Tooltip("Seconds the item takes to settle at the eye when held up to it, and to leave it again.")]
    [SerializeField] float eyeSnapTime = 0.15f;

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
                easeTime = holder is XRSocketInteractor ? takeOutTime : pickUpTime;
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
        if (cursor) targetPose.position = pointed.GetPoint(cursorDistance) + cursorOffset;

        if (faceEyes && view != null)
        {
            Vector3 toEyes = Vector3.ProjectOnPlane(view.transform.position - targetPose.position, Vector3.up);
            if (toEyes.sqrMagnitude > 1e-4f) targetPose.rotation = Quaternion.LookRotation(toEyes, Vector3.up);
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
