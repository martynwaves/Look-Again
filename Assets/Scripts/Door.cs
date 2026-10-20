// Swings a hinged door open or shut when the player grabs it, always away from them. A locked door only rattles.
// The object's pivot is the hinge, and the door turns about its up axis.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public class Door : MonoBehaviour
{
    [Tooltip("How far the door swings open, in degrees.")]
    [SerializeField] float openAngle = 100f;

    [Tooltip("Seconds the door takes to swing open or shut.")]
    [SerializeField] float swingTime = 1.2f;

    [Tooltip("A locked door won't open when grabbed, only when a script such as a hidden lever opens it, and then it stays open.")]
    [SerializeField] bool locked;

    [Tooltip("How far a locked door rattles when grabbed, in degrees, and for how many seconds.")]
    [SerializeField] float rattleAngle = 1.2f, rattleTime = 0.35f;

    public bool IsOpen { get; private set; }

    public bool Locked
    {
        get => locked;
        set => locked = value;
    }

    XRSimpleInteractable interactable;
    Quaternion closed;
    Vector3 middle;
    float angle, fromAngle, toAngle, swung = 1f, rattle;

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        closed = transform.localRotation;
        // The door's middle, in its own space, tells which way swings away from someone
        Renderer body = GetComponent<Renderer>();
        middle = body != null ? transform.InverseTransformPoint(body.bounds.center) : Vector3.left * 0.5f;
    }

    void OnEnable() => interactable.selectEntered.AddListener(OnGrabbed);
    void OnDisable() => interactable.selectEntered.RemoveListener(OnGrabbed);

    void OnGrabbed(SelectEnterEventArgs args)
    {
        if (!locked) Toggle(args.interactorObject.transform.position);
        else if (!IsOpen) rattle = rattleTime;
    }

    public void Toggle(Vector3 awayFrom)
    {
        if (IsOpen) Close();
        else Open(awayFrom);
    }

    public void Open(Vector3 awayFrom)
    {
        IsOpen = true;
        SwingTo(SwingsAway(openAngle, awayFrom) ? openAngle : -openAngle);
    }

    public void Close()
    {
        IsOpen = false;
        SwingTo(0f);
    }

    void SwingTo(float target)
    {
        fromAngle = angle;
        toAngle = target;
        swung = 0f;
    }

    // Whether turning by this angle takes the door's middle further from the point than the opposite turn does
    bool SwingsAway(float by, Vector3 point)
    {
        Transform parent = transform.parent;
        Vector3 hinge = transform.localPosition;
        Vector3 there = closed * Quaternion.Euler(0f, by, 0f) * middle;
        Vector3 back = closed * Quaternion.Euler(0f, -by, 0f) * middle;
        Vector3 local = parent != null ? parent.InverseTransformPoint(point) - hinge : point - hinge;
        return (there - local).sqrMagnitude >= (back - local).sqrMagnitude;
    }

    void Update()
    {
        if (swung >= 1f && rattle <= 0f) return;
        if (swung < 1f)
        {
            // A full swing takes swingTime, a reversed half-swing about half as long
            float span = Mathf.Abs(toAngle - fromAngle) / Mathf.Max(openAngle, 1f);
            swung = Mathf.MoveTowards(swung, 1f, Time.deltaTime / Mathf.Max(swingTime * span, 0.001f));
            angle = Mathf.Lerp(fromAngle, toAngle, Mathf.SmoothStep(0f, 1f, swung));
        }
        float shake = 0f;
        if (rattle > 0f)
        {
            rattle = Mathf.Max(rattle - Time.deltaTime, 0f);
            shake = Mathf.Sin(rattle * 70f) * rattleAngle * rattle / Mathf.Max(rattleTime, 0.001f);
        }
        transform.localRotation = closed * Quaternion.Euler(0f, angle + shake, 0f);
    }
}
