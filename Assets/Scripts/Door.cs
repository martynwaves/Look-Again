// A hinged door the player swings by hand, either way: grabbed, it turns after the hand, and let go, it carries on and slows to a stop, clicking shut if it ends near closed.
// The object's pivot is the hinge, and the door turns about its up axis. A locked door only rattles as it's pulled, until a script such as a hidden lever opens it.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRSimpleInteractable))]
public class Door : MonoBehaviour
{
    [Tooltip("How far the door swings one way from shut, in degrees (zero or less). 0 keeps it from opening that way.")]
    [SerializeField] float minAngle = -100f;

    [Tooltip("How far the door swings the other way from shut, in degrees (zero or more). 0 keeps it from opening that way.")]
    [SerializeField] float maxAngle = 100f;

    [Tooltip("How quickly the door catches up with the hand. Lower feels heavier.")]
    [SerializeField] float follow = 14f;

    [Tooltip("How quickly a door let go mid-swing slows to a stop.")]
    [SerializeField] float friction = 3f;

    [Tooltip("Seconds a script such as a hidden lever takes to swing the door open.")]
    [SerializeField] float swingTime = 1.2f;

    [Tooltip("A locked door won't move when grabbed, only rattle, until a script such as a hidden lever opens it.")]
    [SerializeField] bool locked;

    [Tooltip("How far a locked door rattles when grabbed, in degrees, and for how many seconds.")]
    [SerializeField] float rattleAngle = 1.2f, rattleTime = 0.35f;

    [Tooltip("Degrees the hand must pull a held locked door around its hinge to rattle it again.")]
    [SerializeField] float rattlePull = 4f;

    [Tooltip("Let go within this many degrees of shut, swinging back or coming to rest, the door clicks shut.")]
    [SerializeField] float latchAngle = 10f;

    public bool Locked
    {
        get => locked;
        set => locked = value;
    }

    XRSimpleInteractable interactable;
    Quaternion closed;
    Vector3 middle;
    IXRSelectInteractor holder;
    Vector3 heldPoint;
    float angle, speed, grabAngle, handTurn, lastHand;
    float fromAngle, toAngle, swung = 1f, rattle;

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        closed = transform.localRotation;
        // The door's middle, in its own space, tells which way swings away from someone
        Renderer body = GetComponent<Renderer>();
        middle = body != null ? transform.InverseTransformPoint(body.bounds.center) : Vector3.left * 0.5f;
    }

    void OnEnable()
    {
        interactable.selectEntered.AddListener(OnGrabbed);
        interactable.selectExited.AddListener(OnReleased);
    }

    void OnDisable()
    {
        interactable.selectEntered.RemoveListener(OnGrabbed);
        interactable.selectExited.RemoveListener(OnReleased);
        holder = null;
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        // The grabbed point stays put relative to the controller, as if held on a stick, so a ray grab swings the door from where it hit
        holder = args.interactorObject;
        heldPoint = holder.transform.InverseTransformPoint(GrabPoint(holder));
        handTurn = 0f;
        HandAngle(holder.transform.TransformPoint(heldPoint), out lastHand);
        if (locked)
        {
            rattle = rattleTime;
            return;
        }
        swung = 1f;
        speed = 0f;
        grabAngle = angle;
    }

    void OnReleased(SelectExitEventArgs args)
    {
        if (args.interactorObject != holder) return;
        holder = null;
        speed = Mathf.Clamp(speed, -360f, 360f);
        Latch();
    }

    // Let go near shut, heading back or at rest, the door pulls the last bit closed
    void Latch()
    {
        if (holder != null || swung < 1f || angle == 0f || Mathf.Abs(angle) >= latchAngle) return;
        if (speed != 0f && Mathf.Sign(speed) == Mathf.Sign(angle)) return;
        fromAngle = angle;
        toAngle = 0f;
        swung = 0f;
        speed = 0f;
    }

    // Where the door was grabbed: the ray's hit for a grab from afar, the hand for one up close
    Vector3 GrabPoint(IXRSelectInteractor interactor)
    {
        bool far = interactor is NearFarInteractor nearFar ? nearFar.selectionRegion.Value == NearFarInteractor.Region.Far : interactor is IXRRayProvider;
        return far && interactor is IXRRayProvider ray ? ray.rayEndPoint : interactor.GetAttachTransform(interactable).position;
    }

    // Swings the door open away from a point, whichever way it can, and unlocks it
    public void Open(Vector3 awayFrom)
    {
        locked = false;
        holder = null;
        speed = 0f;
        bool maxIsAway = SwingsAway(maxAngle, minAngle, awayFrom);
        float away = maxIsAway ? maxAngle : minAngle, other = maxIsAway ? minAngle : maxAngle;
        fromAngle = angle;
        toAngle = Mathf.Approximately(away, 0f) ? other : away;
        swung = 0f;
    }

    // Whether turning by the first angle takes the door's middle further from the point than turning by the second does
    bool SwingsAway(float by, float instead, Vector3 point)
    {
        Vector3 local = Quaternion.Inverse(closed) * (ParentPoint(point) - transform.localPosition);
        Vector3 there = Quaternion.Euler(0f, by, 0f) * middle;
        Vector3 back = Quaternion.Euler(0f, instead, 0f) * middle;
        return (there - local).sqrMagnitude >= (back - local).sqrMagnitude;
    }

    // The hand's bearing around the hinge, in degrees, measured the same way as the door's angle; false too close to the hinge to tell
    bool HandAngle(Vector3 point, out float degrees)
    {
        Vector3 local = Quaternion.Inverse(closed) * (ParentPoint(point) - transform.localPosition);
        degrees = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        return local.x * local.x + local.z * local.z > 0.01f;
    }

    Vector3 ParentPoint(Vector3 point) => transform.parent != null ? transform.parent.InverseTransformPoint(point) : point;

    void Update()
    {
        if (holder == null && swung >= 1f && speed == 0f && rattle <= 0f) return;
        float dt = Time.deltaTime;

        if (holder != null && HandAngle(holder.transform.TransformPoint(heldPoint), out float hand))
        {
            handTurn += Mathf.DeltaAngle(lastHand, hand);
            lastHand = hand;
        }

        if (holder != null && locked)
        {
            // Pulling at a locked door keeps it rattling
            if (Mathf.Abs(handTurn) > rattlePull)
            {
                rattle = rattleTime;
                handTurn = 0f;
            }
        }
        else if (holder != null)
        {
            float target = Mathf.Clamp(grabAngle + handTurn, minAngle, maxAngle);
            float before = angle;
            angle = Mathf.Lerp(angle, target, 1f - Mathf.Exp(-follow * dt));
            speed = dt > 0f ? (angle - before) / dt : 0f;
        }
        else if (swung < 1f)
        {
            // A full scripted swing takes swingTime, a shorter one about as much less
            float span = Mathf.Abs(toAngle - fromAngle) / Mathf.Max(Mathf.Max(maxAngle, -minAngle), 1f);
            swung = Mathf.MoveTowards(swung, 1f, dt / Mathf.Max(swingTime * span, 0.001f));
            angle = Mathf.Lerp(fromAngle, toAngle, Mathf.SmoothStep(0f, 1f, swung));
        }
        else if (speed != 0f)
        {
            speed *= Mathf.Exp(-friction * dt);
            float before = angle;
            angle += speed * dt;
            // Swinging back through shut, it catches in the frame
            if (before != 0f && Mathf.Sign(angle) != Mathf.Sign(before)) angle = speed = 0f;
            else if (angle <= minAngle || angle >= maxAngle || Mathf.Abs(speed) < 2f)
            {
                angle = Mathf.Clamp(angle, minAngle, maxAngle);
                speed = 0f;
            }
        }

        Latch();

        float shake = 0f;
        if (rattle > 0f)
        {
            rattle = Mathf.Max(rattle - dt, 0f);
            // Timed by the clock rather than the countdown, so a rattle restarted mid-shake doesn't jump
            shake = Mathf.Sin(Time.time * 70f) * rattleAngle * rattle / Mathf.Max(rattleTime, 0.001f);
        }
        transform.localRotation = closed * Quaternion.Euler(0f, angle + shake, 0f);
    }
}
