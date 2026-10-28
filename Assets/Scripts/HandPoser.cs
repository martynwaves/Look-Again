// Poses a hand model on a controller: the grip curls the lower fingers, the trigger curls the index, and the hand closes around whatever it holds.
// Works on a mirrored copy of the same hand too. Without a headset the hand tilts up to reach forward, as simulated controllers point dead ahead.

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

    void OnEnable() => hands.Add(this);
    void OnDisable() => hands.Remove(this);

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

    void Update()
    {
        // Checked each frame, as a headset can take a few seconds to start
        bool keyboard = !UnityEngine.XR.XRSettings.isDeviceActive;
        if (keyboard != tilted)
        {
            tilted = keyboard;
            Quaternion tilt = keyboard ? Quaternion.Euler(-keyboardTilt, 0f, 0f) : Quaternion.identity;
            transform.SetLocalPositionAndRotation(tilt * placed.position, tilt * placed.rotation);
        }
        if (fingers[0] == null) return;
        float pressGrip = Read(grip), pressTrigger = Read(trigger);
        float hold = interactor != null && interactor.hasSelection ? holdCurl : 0f;
        float blend = 1f - Mathf.Exp(-follow * Time.deltaTime);

        for (int f = 0; f < 5; f++)
        {
            float target = f switch
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
}
