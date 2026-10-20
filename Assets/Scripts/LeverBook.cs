// A book that is really a hidden lever: grabbing it tips it out of the shelf until it catches partway, which opens the door for good.
// The object's pivot is the edge it tips about.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public class LeverBook : MonoBehaviour
{
    [Tooltip("The door this book opens.")]
    [SerializeField] Door door;

    [Tooltip("The axis the book tips about, in its own space. Positive turns tip the top toward the room.")]
    [SerializeField] Vector3 tiltAxis = Vector3.right;

    [Tooltip("How far the book tips before it catches, in degrees.")]
    [SerializeField] float tiltAngle = 22f;

    [Tooltip("The tip over time, in seconds, as a fraction of the tilt angle. Overshooting 1 and settling back makes it feel like it caught on something.")]
    [SerializeField] AnimationCurve tilt = new(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(0.4f, 1.12f, 0f, 0f), new Keyframe(0.55f, 1f, 0f, 0f));

    [Tooltip("Seconds between the book catching and the door opening.")]
    [SerializeField] float doorDelay = 0.5f;

    public bool Pulled { get; private set; }

    XRSimpleInteractable interactable;
    Quaternion rest;
    float pulledFor = -1f;
    bool doorOpened;

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        rest = transform.localRotation;
    }

    void OnEnable() => interactable.selectEntered.AddListener(OnGrabbed);
    void OnDisable() => interactable.selectEntered.RemoveListener(OnGrabbed);

    void OnGrabbed(SelectEnterEventArgs args) => Pull();

    public void Pull()
    {
        if (Pulled) return;
        Pulled = true;
        pulledFor = 0f;
    }

    void Update()
    {
        if (pulledFor < 0f) return;
        // Stuck for good, so there's nothing left to grab. Not turned off inside the grab event itself, which would cut it short
        if (interactable.enabled) interactable.enabled = false;
        pulledFor += Time.deltaTime;
        float end = tilt.length > 0 ? tilt.keys[tilt.length - 1].time : 0f;
        transform.localRotation = rest * Quaternion.AngleAxis(tilt.Evaluate(Mathf.Min(pulledFor, end)) * tiltAngle, tiltAxis);

        if (!doorOpened && pulledFor >= end + doorDelay)
        {
            doorOpened = true;
            if (door != null)
            {
                Camera view = Camera.main;
                door.Open(view != null ? view.transform.position : transform.position);
            }
        }
        if (doorOpened && pulledFor >= end) pulledFor = -1f;
    }
}
