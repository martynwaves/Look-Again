// Short lines that teach the player as they go, each shown until they've done the thing once: bringing a far-held item closer,
// stowing a carried item on the belt, and pulling a door open by its handle. Other scripts can make tips in the same style.

using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class PlayerHints : MonoBehaviour
{
    static PlayerHints instance;

    [Tooltip("Font of the lines.")]
    [SerializeField] TMP_FontAsset font;

    [Tooltip("An overlay material for that font, so the lines show over whatever's behind them.")]
    [SerializeField] Material material;

    [Tooltip("How far from the hand an item held on a laser must be for its line to show, in meters.")]
    [SerializeField] float farAway = 1.2f;

    [Tooltip("How large a line is from a meter away; it grows with distance so it reads the same anywhere.")]
    [SerializeField] float fontSize = 0.12f;

    [Tooltip("How near a door must be, in meters, and how squarely looked at, in degrees, for its line to show.")]
    [SerializeField] float doorDistance = 2.5f, doorAngle = 30f;

    NearFarInteractor[] hands;
    TMP_Text[] handTips;
    TMP_Text doorTip;
    bool[] wasFar;
    Door[] doors;
    Camera view;
    bool pulledIn;

    void Awake()
    {
        instance = this;
        hands = GetComponentsInChildren<NearFarInteractor>(true);
        view = GetComponentInChildren<Camera>();
        handTips = new TMP_Text[hands.Length];
        wasFar = new bool[hands.Length];
        for (int i = 0; i < hands.Length; i++) handTips[i] = CreateTip();
        doorTip = CreateTip();
        doorTip.text = "Grab the handle and pull";
        doorTip.alignment = TextAlignmentOptions.Center;
        doorTip.rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Start() => doors = FindObjectsByType<Door>();

    void LateUpdate()
    {
        if (view == null) return;
        bool headset = UnityEngine.XR.XRSettings.isDeviceActive;
        for (int i = 0; i < hands.Length; i++) LayOutHand(i, headset);
        LayOutDoor(headset);
    }

    // Beside what a hand holds: far out on a laser (or dragged far off by the mouse), how to pull it in; otherwise, if carried, how to put it on the belt
    void LayOutHand(int i, bool headset)
    {
        NearFarInteractor hand = hands[i];
        XRGrabInteractable held = hand != null && hand.hasSelection ? hand.firstInteractableSelected as XRGrabInteractable : null;
        bool onLaser = held != null && hand.selectionRegion.Value == NearFarInteractor.Region.Far;
        float reach = held != null ? Vector3.Distance(held.transform.position, hand.transform.position) : 0f;
        // Brought in from far off, back into the hand or well nearer, the player knows how
        if (held != null && wasFar[i] && (!onLaser || reach < farAway * 0.6f)) pulledIn = true;
        wasFar[i] = onLaser && reach > farAway;

        string line = null;
        if (held != null)
        {
            float fromEyes = Vector3.Distance(held.transform.position, view.transform.position);
            if (wasFar[i] && !pulledIn) line = headset ? "Tilt the thumbstick back\nto bring it closer" : "Scroll the mouse wheel\nto bring it closer";
            else if ((!headset || !onLaser) && !Belt.HandStowed && Belt.IsCarried(held) && fromEyes > 0.25f)
                line = headset ? "Let go near your waist\nto put it on your belt" : "Drop it on a belt slot\nto put it away";
        }
        TMP_Text tip = handTips[i];
        if (line == null)
        {
            if (tip.gameObject.activeSelf) tip.gameObject.SetActive(false);
            return;
        }
        tip.text = line;
        Bounds size = Size(held);
        Vector3 eyes = view.transform.position;
        float distance = Vector3.Distance(eyes, size.center);
        Vector3 right = Vector3.Cross(Vector3.up, size.center - eyes).normalized;
        Place(tip, size.center + right * (size.extents.magnitude + 0.04f * distance));
    }

    // By the handle of the nearest door looked at, until the player has grabbed one
    void LayOutDoor(bool headset)
    {
        Vector3 at = default;
        bool shown = false;
        if (!Door.EverGrabbed && doors != null)
        {
            Transform head = view.transform;
            float nearest = doorDistance;
            foreach (Door door in doors)
            {
                if (door == null || door.Locked) continue;
                Vector3 handle = Handle(door, head.position);
                float distance = Vector3.Distance(handle, head.position);
                if (distance > nearest || Vector3.Angle(head.forward, handle - head.position) > doorAngle) continue;
                nearest = distance;
                at = handle + Vector3.up * 0.12f;
                shown = true;
            }
        }
        if (!shown)
        {
            if (doorTip.gameObject.activeSelf) doorTip.gameObject.SetActive(false);
            return;
        }
        doorTip.text = headset ? "Grab the handle and pull" : "Click the handle and drag\nto open the door";
        Place(doorTip, at);
    }

    // The door's handle nearest the eyes, else its middle
    static Vector3 Handle(Door door, Vector3 eyes)
    {
        Vector3 best = door.transform.position;
        float bestDistance = float.MaxValue;
        foreach (Collider part in door.GetComponentsInChildren<Collider>())
        {
            if (!part.name.ToLowerInvariant().Contains("knob") && !part.name.ToLowerInvariant().Contains("handle")) continue;
            float distance = (part.bounds.center - eyes).sqrMagnitude;
            if (distance < bestDistance) { bestDistance = distance; best = part.bounds.center; }
        }
        if (bestDistance == float.MaxValue && door.TryGetComponent(out Collider body)) best = body.bounds.center;
        return best;
    }

    static Bounds Size(XRGrabInteractable item)
    {
        Bounds all = new(item.transform.position, Vector3.zero);
        foreach (Collider part in item.colliders)
            if (part != null) all.Encapsulate(part.bounds);
        return all;
    }

    // Shows a tip at a point, facing the eyes and sized for its distance
    static void Place(TMP_Text tip, Vector3 at)
    {
        Camera eyes = instance != null ? instance.view : Camera.main;
        if (eyes == null) return;
        if (!tip.gameObject.activeSelf) tip.gameObject.SetActive(true);
        Vector3 from = eyes.transform.position;
        tip.transform.SetPositionAndRotation(at, Quaternion.LookRotation(at - from, Vector3.up));
        tip.transform.localScale = Vector3.one * Vector3.Distance(from, at);
    }

    // Shows or hides another script's tip at a point, in the same way
    public static void Show(TMP_Text tip, bool shown, Vector3 at)
    {
        if (tip == null) return;
        if (shown) Place(tip, at);
        else if (tip.gameObject.activeSelf) tip.gameObject.SetActive(false);
    }

    // A tip in the same style for another script, centred on its point; null until the hints have woken
    public static TMP_Text Tip(string text)
    {
        if (instance == null) return null;
        TMP_Text tip = instance.CreateTip();
        tip.text = text;
        tip.alignment = TextAlignmentOptions.Center;
        tip.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        return tip;
    }

    TMP_Text CreateTip()
    {
        TextMeshPro tip = new GameObject("Hint", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
        // Text casts no shadow, or a light nearby throws its letters onto the walls
        tip.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tip.transform.SetParent(transform, false);
        if (font != null) tip.font = font;
        if (material != null) tip.fontSharedMaterial = material;
        tip.color = new Color(1f, 0.96f, 0.88f, 0.85f);
        tip.fontSize = fontSize;
        tip.alignment = TextAlignmentOptions.Left;
        tip.textWrappingMode = TextWrappingModes.NoWrap;
        tip.rectTransform.pivot = new Vector2(0f, 0.5f);
        tip.rectTransform.sizeDelta = new Vector2(0.3f, 0.06f);
        tip.gameObject.SetActive(false);
        return tip;
    }
}
