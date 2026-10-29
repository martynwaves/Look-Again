// Poses a hand model on a controller: the grip curls the lower fingers, the trigger curls the index, and the hand closes around whatever it holds.
// Works on a mirrored copy of the same hand too. Without a headset it tilts up to reach forward, and scripts can steer it off the controller onto something.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class HandPoser : MonoBehaviour
{
    [Tooltip("The grip's press, from 0 to 1.")]
    [SerializeField] InputActionProperty grip;

    [Tooltip("The trigger's press, from 0 to 1.")]
    [SerializeField] InputActionProperty trigger;

    [Tooltip("This hand's interactor; while it holds something the fingers close around it.")]
    [SerializeField] XRBaseInputInteractor interactor;

    [Tooltip("The hand model, whose bones are named ThumbA, IndexB and so on.")]
    [SerializeField] Transform model;

    [Tooltip("How far the fingers close while holding something, from 0 (open) to 1 (a fist).")]
    [SerializeField, Range(0f, 1f)] float holdCurl = 0.6f;

    [Tooltip("How far the fingers stay curled at rest.")]
    [SerializeField, Range(0f, 1f)] float restCurl = 0.08f;

    [Tooltip("Degrees each finger joint turns in a fist, from the knuckle to the tip.")]
    [SerializeField] Vector3 fingerCurl = new(70f, 90f, 60f);

    [Tooltip("Degrees each thumb joint turns in a fist, from the base to the tip.")]
    [SerializeField] Vector3 thumbCurl = new(20f, 35f, 35f);

    [Tooltip("How quickly the fingers follow the buttons.")]
    [SerializeField] float follow = 14f;

    [Tooltip("Degrees the hand tilts up about the controller without a headset, so it reaches forward instead of hanging from the wrist.")]
    [SerializeField] float keyboardTilt = 55f;

    [Tooltip("Seconds the hand takes to move onto something steering it, and back to its controller.")]
    [SerializeField] float steerTime = 0.25f;

    static readonly string[] Names = { "Thumb", "Index", "Middle", "Ring", "Pinky" };
    static readonly List<HandPoser> hands = new();

    // The hand whose interactor this is, if any
    public static HandPoser Of(IXRSelectInteractor holder)
    {
        foreach (HandPoser hand in hands)
            if (ReferenceEquals(hand.interactor, holder)) return hand;
        return null;
    }

    // Whether the hand is drawn; hidden while it holds the camera to the eye, a few centimetres in front of it
    public bool Shown
    {
        set
        {
            foreach (Renderer part in looks) part.enabled = value;
        }
    }

    struct Joint
    {
        public Transform bone;
        public Quaternion rest;
        public Vector3 axis;
    }

    readonly Joint[][] fingers = new Joint[5][];
    readonly float[] curls = new float[5];
    Renderer[] looks;
    Pose placed;
    bool tilted;

    // The palm in this object's own space: its middle, the way it faces and the way the fingers point
    Vector3 palmMiddle, palmFacing, palmFingers;

    Transform steerSpace;
    Vector3 steerAt, steerFacing, steerFingers;
    float steerCurl, steered;
    int steeredFrame = -10;

    void OnEnable()
    {
        hands.Add(this);
        Application.onBeforeRender += Place;
    }

    void OnDisable()
    {
        hands.Remove(this);
        Application.onBeforeRender -= Place;
    }

    void Awake()
    {
        placed = new Pose(transform.localPosition, transform.localRotation);
        looks = model.GetComponentsInChildren<Renderer>();
        Transform wrist = Find("Wrist");
        Transform middle = Find("MiddleA"), middleTip = Find("MiddleC");
        if (wrist == null || middle == null) return;

        // The palm faces where the resting fingers bend, all in the model's own space
        Vector3 forward = Local(middle.position - wrist.position).normalized;
        Vector3 across = Local(Find("IndexA").position - Find("PinkyA").position).normalized;
        Vector3 palm = Vector3.Cross(forward, across).normalized;
        if (Vector3.Dot(palm, Local(middleTip.position - middle.position)) < 0f) palm = -palm;

        Matrix4x4 modelToHere = transform.worldToLocalMatrix * model.localToWorldMatrix;
        palmFingers = modelToHere.MultiplyVector(forward).normalized;
        palmFacing = modelToHere.MultiplyVector(palm).normalized;
        palmMiddle = transform.InverseTransformPoint(wrist.position) + palmFingers * 0.055f + palmFacing * 0.015f;

        for (int f = 0; f < 5; f++)
        {
            fingers[f] = new Joint[3];
            for (int j = 0; j < 3; j++)
            {
                Transform bone = Find(Names[f] + (char)('A' + j));
                Transform next = j < 2 ? Find(Names[f] + (char)('B' + j)) : bone.GetChild(0);
                Vector3 along = Local(next.position - bone.position).normalized;
                // Turning about this axis bends the joint toward the palm
                Vector3 axis = bone.worldToLocalMatrix.MultiplyVector(model.localToWorldMatrix.MultiplyVector(Vector3.Cross(along, palm))).normalized;
                fingers[f][j] = new Joint { bone = bone, rest = bone.localRotation, axis = axis };
            }
        }
    }

    Vector3 Local(Vector3 direction) => model.worldToLocalMatrix.MultiplyVector(direction);

    Transform Find(string prefix)
    {
        foreach (Transform bone in model.GetComponentsInChildren<Transform>())
            if (bone.name.StartsWith(prefix)) return bone;
        return null;
    }

    static float Read(InputActionProperty input) => input.action != null ? input.action.ReadValue<float>() : 0f;

    // Steers the hand for this frame: its palm's middle to a point, facing one way with the fingers pointing another, all in some object's space.
    // Once no longer steered it eases back onto its controller
    public void Steer(Transform space, Vector3 palmAt, Vector3 facing, Vector3 fingersPoint, float curl)
    {
        steerSpace = space;
        steerAt = palmAt;
        steerFacing = facing;
        steerFingers = fingersPoint;
        steerCurl = curl;
        steeredFrame = Time.frameCount;
    }

    bool Steering => Time.frameCount - steeredFrame <= 1 && steerSpace != null;

    void Update()
    {
        // Checked each frame, as a headset can take a few seconds to start
        tilted = !UnityEngine.XR.XRSettings.isDeviceActive;
        steered = Mathf.MoveTowards(steered, Steering ? 1f : 0f, Time.deltaTime / Mathf.Max(steerTime, 0.001f));
        if (fingers[0] == null) return;
        float pressGrip = Read(grip), pressTrigger = Read(trigger);
        float hold = interactor != null && interactor.hasSelection ? holdCurl : 0f;
        float blend = 1f - Mathf.Exp(-follow * Time.deltaTime);

        for (int f = 0; f < 5; f++)
        {
            float target = Steering ? steerCurl : f switch
            {
                0 => Mathf.Max(0.6f * pressGrip + 0.4f * pressTrigger, hold),
                1 => Mathf.Max(pressTrigger, hold),
                _ => Mathf.Max(pressGrip, hold),
            };
            curls[f] = Mathf.Lerp(curls[f], Mathf.Max(target, restCurl), blend);
            Vector3 full = f == 0 ? thumbCurl : fingerCurl;
            for (int j = 0; j < 3; j++)
            {
                Joint joint = fingers[f][j];
                joint.bone.localRotation = joint.rest * Quaternion.AngleAxis(full[j] * curls[f], joint.axis);
            }
        }
    }

    void LateUpdate() => Place();

    // On its controller, blended toward wherever it's steered; again just before rendering, after whatever it's steered onto has moved
    void Place()
    {
        Quaternion tilt = tilted ? Quaternion.Euler(-keyboardTilt, 0f, 0f) : Quaternion.identity;
        transform.SetLocalPositionAndRotation(tilt * placed.position, tilt * placed.rotation);
        if (steered <= 0f || steerSpace == null) return;

        // Matches the palm's facing and fingers, through this object's scale, which mirrors the right hand
        Vector3 scale = transform.localScale;
        Quaternion own = Quaternion.LookRotation(Vector3.Scale(palmFingers, scale), Vector3.Scale(palmFacing, scale));
        Quaternion wanted = Quaternion.LookRotation(steerSpace.TransformDirection(steerFingers), steerSpace.TransformDirection(steerFacing));
        Quaternion rotation = wanted * Quaternion.Inverse(own);
        Vector3 position = steerSpace.TransformPoint(steerAt) - rotation * Vector3.Scale(palmMiddle, scale);

        float t = steered * steered * (3f - 2f * steered);
        transform.SetPositionAndRotation(Vector3.Lerp(transform.position, position, t), Quaternion.Slerp(transform.rotation, rotation, t));
    }
}
