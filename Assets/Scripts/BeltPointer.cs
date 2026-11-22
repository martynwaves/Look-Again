// Bends an empty hand's laser onto the stowed item its grip would take from the belt, drawn and buzzed as if hovering it; the toolkit then leaves everything else alone.
// Otherwise the laser shows what the hand itself points at, reaching out to the floor or wall when that's nothing; the line visual reads its curve from here.

using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Feedback;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

[RequireComponent(typeof(NearFarInteractor))]
public class BeltPointer : MonoBehaviour, ICurveInteractionDataProvider, IXRHoverFilter, IXRSelectFilter
{
    static readonly RaycastHit[] hits = new RaycastHit[16];
    static readonly System.Collections.Generic.List<BeltPointer> pointers = new();

    // Whether any hand's grip would take this item from the belt now
    public static bool Chooses(XRGrabInteractable item)
    {
        foreach (BeltPointer pointer in pointers)
            if (pointer.Chosen == item) return true;
        return false;
    }

    NearFarInteractor hand;
    XRInteractionManager manager;
    CurveInteractionCaster laser;
    SphereInteractionCaster reach;
    ICurveInteractionDataProvider own;
    SimpleHapticFeedback feedback;
    HapticImpulsePlayer haptics;
    Vector3 localSpot;

    // The stowed item the grip would take now, or null
    public XRGrabInteractable Chosen { get; private set; }

    void Awake()
    {
        hand = GetComponent<NearFarInteractor>();
        own = hand;
        laser = GetComponent<CurveInteractionCaster>();
        reach = GetComponent<SphereInteractionCaster>();
        feedback = GetComponent<SimpleHapticFeedback>();
        haptics = feedback != null && feedback.hapticImpulsePlayer != null ? feedback.hapticImpulsePlayer : GetComponentInParent<HapticImpulsePlayer>();
    }

    void OnEnable() => pointers.Add(this);

    void Start()
    {
        manager = hand.interactionManager;
        if (manager == null) return;
        manager.hoverFilters.Add(this);
        manager.selectFilters.Add(this);
    }

    void OnDestroy()
    {
        if (manager == null) return;
        manager.hoverFilters.Remove(this);
        manager.selectFilters.Remove(this);
    }

    // While the belt has something for it, the hand takes nothing else
    public bool canProcess => isActiveAndEnabled;
    public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable) => Allows(interactor);
    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable) => Allows(interactor);
    bool Allows(IXRInteractor interactor) => !ReferenceEquals(interactor, hand) || !Showing || BeltSlot.TakingOut;

    void OnDisable()
    {
        pointers.Remove(this);
        Chosen = null;
    }

    // Chooses again, once a frame, after the belt has moved
    public void Refresh()
    {
        XRGrabInteractable was = Chosen;
        Chosen = null;
        bool headset = UnityEngine.XR.XRSettings.isDeviceActive;
        SeeStowed(!headset);
        if (isActiveAndEnabled && headset && !hand.hasSelection)
        {
            Chosen = BeltSlot.ChooseFor(hand, out Vector3 spot);
            if (Chosen != null) localSpot = Chosen.transform.InverseTransformPoint(spot);
        }
        if (Chosen != null && Chosen != was && haptics != null && feedback != null && feedback.playHoverEntered && feedback.hoverEnteredData != null)
            haptics.SendHapticImpulse(feedback.hoverEnteredData.amplitude, feedback.hoverEnteredData.duration, feedback.hoverEnteredData.frequency);
    }

    // Whether the toolkit's own casts meet stowed items, as the mouse's must on a monitor
    void SeeStowed(bool see)
    {
        int stowed = LayerMask.GetMask(BeltSlot.StowedLayer);
        if (laser != null) laser.raycastMask = see ? laser.raycastMask | stowed : laser.raycastMask & ~stowed;
        if (reach != null) reach.physicsLayerMask = see ? reach.physicsLayerMask | stowed : reach.physicsLayerMask & ~stowed;
    }

    // Where a laser on nothing it can take meets a wall, the floor or furniture, or its far end
    Vector3 Surface()
    {
        Vector3 from = curveOrigin.position, along = curveOrigin.forward;
        float nearest = laser.castDistance;
        int count = Physics.RaycastNonAlloc(from, along, hits, nearest, laser.raycastMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (!hits[i].collider.transform.IsChildOf(transform.root)) nearest = Mathf.Min(nearest, hits[i].distance);
        return from + along * nearest;
    }

    bool Showing => Chosen != null && !hand.hasSelection;

    public bool isActive => Showing || own.isActive;
    public bool hasValidSelect => own.hasValidSelect;
    public Transform curveOrigin => own.curveOrigin;
    public NativeArray<Vector3> samplePoints => own.samplePoints;
    public Vector3 lastSamplePoint => own.lastSamplePoint;

    public EndPointType TryGetCurveEndPoint(out Vector3 endPoint, bool snapToSelectedAttachIfAvailable = false, bool snapToSnapVolumeIfAvailable = false)
    {
        if (!Showing)
        {
            EndPointType type = own.TryGetCurveEndPoint(out endPoint, snapToSelectedAttachIfAvailable, snapToSnapVolumeIfAvailable);
            if (type != EndPointType.None && type != EndPointType.EmptyCastHit || hand.hasSelection || laser == null) return type;
            endPoint = Surface();
            return EndPointType.EmptyCastHit;
        }
        endPoint = Chosen.transform.TransformPoint(localSpot);
        return EndPointType.AttachPoint;
    }

    public EndPointType TryGetCurveEndNormal(out Vector3 endNormal, bool snapToSelectedAttachIfAvailable = false)
    {
        if (!Showing) return own.TryGetCurveEndNormal(out endNormal, snapToSelectedAttachIfAvailable);
        endNormal = (curveOrigin.position - Chosen.transform.TransformPoint(localSpot)).normalized;
        return EndPointType.AttachPoint;
    }
}
