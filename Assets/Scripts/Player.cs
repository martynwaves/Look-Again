// Walks the player with WASD (running while Shift is held), fades a headset's view to black while the head is in a wall or out of the play area,
// with a line in the middle saying why and how to get out. In a headset it holds the eyes at a set height and starts (and respawns) the player facing the welcome panel. Quits on Esc in builds.

using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

[RequireComponent(typeof(XROrigin), typeof(CharacterController))]
public class Player : MonoBehaviour
{
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    [Tooltip("Collider whose horizontal bounds define the walkable area (the floor).")]
    [SerializeField] Collider area;

    [Tooltip("WASD walking speed, in meters per second.")]
    [SerializeField] float moveSpeed = 1.5f;

    [Tooltip("Speed while Shift is held, in meters per second.")]
    [SerializeField] float runSpeed = 3f;

    [Header("Fade")]
    [Tooltip("How close the head may come to walls and furniture before the view fades, in meters.")]
    [SerializeField] float headRadius = 0.1f;
    [Tooltip("Seconds the view takes to fade out or back in.")]
    [SerializeField] float fadeTime = 0.2f;

    [Header("Fade Message")]
    [Tooltip("Font of the line saying why the view went dark.")]
    [SerializeField] TMP_FontAsset messageFont;
    [Tooltip("An overlay material for that font, so the line shows over walls and the dark.")]
    [SerializeField] Material messageMaterial;
    [Tooltip("Seconds the line takes to fade in once the view is dark.")]
    [SerializeField] float messageFadeTime = 0.6f;

    [Header("Start")]
    [Tooltip("What the player faces when the game starts in a headset, such as the welcome panel, however they stand in their room.")]
    [SerializeField] Transform startFacing;
    [Tooltip("Seconds the headset must track the head before the game turns the player to face the start target, so it has settled.")]
    [SerializeField] float settleTime = 0.4f;
    [Tooltip("How high the eyes stand above the floor in a headset, in meters, however tall the player or wherever the headset puts the floor. 0 keeps the real height.")]
    [SerializeField] float eyeHeight = 1.6f;

    XROrigin origin;
    CharacterController controller;
    MeshRenderer fade;
    TMP_Text message;
    MaterialPropertyBlock fadeBlock;
    GameObject comfortVignette;
    readonly Collider[] hits = new Collider[8];
    float faded, messageShown, startedAt, trackedSince = -1f, headsetSince = -1f, heightShift;
    Vector3 spawn;
    bool started, heightSet;
    readonly System.Collections.Generic.List<XRInputSubsystem> inputs = new();
    enum Blocked { None, Wall, OutOfArea }

    void Awake()
    {
        origin = GetComponent<XROrigin>();
        controller = GetComponent<CharacterController>();
        spawn = transform.position;
        if (origin.Camera != null)
        {
            fade = CreateFade(origin.Camera.transform);
            message = CreateMessage(origin.Camera.transform);
        }
        fadeBlock = new MaterialPropertyBlock();
        var vignette = GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort.TunnelingVignetteController>(true);
        if (vignette != null) comfortVignette = vignette.gameObject;
    }

    void Update()
    {
#if !UNITY_EDITOR
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Application.Quit();
#endif
        // The comfort vignette is sized for a headset's wide view, and on a monitor's narrower one it covers most of the screen
        bool headset = UnityEngine.XR.XRSettings.isDeviceActive;
        if (comfortVignette != null && comfortVignette.activeSelf != headset) comfortVignette.SetActive(headset);
        if (!started) Begin(headset);
        Walk();
    }

    void LateUpdate()
    {
        // Held at the set eye height every frame, as the rig resets its floor offset whenever tracking restarts
        if (heightSet && UnityEngine.XR.XRSettings.isDeviceActive)
        {
            Transform offset = origin.CameraFloorOffsetObject.transform;
            if (!Mathf.Approximately(offset.localPosition.y, heightShift)) offset.localPosition = new Vector3(offset.localPosition.x, heightShift, offset.localPosition.z);
        }
        if (fade == null) return;
        // On a monitor the body can't walk into walls, so only a real head can end up inside one; and not before the headset has settled at the start
        Blocked blocked = UnityEngine.XR.XRSettings.isDeviceActive && started && Time.time - startedAt > 0.5f ? HeadInside() : Blocked.None;
        faded = Mathf.MoveTowards(faded, blocked != Blocked.None ? 1f : 0f, Time.deltaTime / Mathf.Max(fadeTime, 0.001f));
        fade.enabled = faded > 0f;
        fadeBlock.SetFloat(AlphaId, faded);
        fade.SetPropertyBlock(fadeBlock);
        LayOutMessage(blocked);
    }

    // The line fades in slowly once the view is fully dark, so a brief brush with a wall doesn't flash it, and goes as the view clears
    void LayOutMessage(Blocked blocked)
    {
        if (message == null) return;
        bool wanted = blocked != Blocked.None && faded >= 1f;
        messageShown = wanted ? Mathf.MoveTowards(messageShown, 1f, Time.deltaTime / Mathf.Max(messageFadeTime, 0.001f)) : Mathf.Min(messageShown, faded);
        bool shown = messageShown > 0f;
        if (message.gameObject.activeSelf != shown) message.gameObject.SetActive(shown);
        if (!shown) return;
        if (wanted) message.text = MessageFor(blocked);
        message.alpha = Mathf.SmoothStep(0f, 1f, messageShown);
    }

    static string MessageFor(Blocked blocked)
    {
        return blocked == Blocked.Wall ? "Back up or turn away from the wall" : "Step back into the play area";
    }

    // Where the game starts, which the eyes are put back over after being caught
    public Vector3 SpawnPoint => spawn;

    // Moves the player back to the start, facing the start target again in a headset
    public void Respawn()
    {
        if (origin.Camera == null) return;
        if (UnityEngine.XR.XRSettings.isDeviceActive) FaceStart();
        else MoveToSpawn();
    }

    // A headset can take a few seconds to come up after the game starts, so this waits for it, however long, rather than settling on a monitor at the first frame.
    // Once it tracks the head and has settled, it sets the eye height and faces the start target on the spawn point; a recentre in the first moments is followed again
    void Begin(bool headset)
    {
        if (origin.Camera == null || !headset) return;
        if (headsetSince < 0f) headsetSince = Time.time;
        // Tracked once the head stands clear of the floor, or after a moment anyway, for a headset that tracks from eye level
        bool tracked = origin.Camera.transform.localPosition.y >= 0.5f || Time.time - headsetSince > 2f;
        if (!tracked)
        {
            trackedSince = -1f;
            return;
        }
        if (trackedSince < 0f) trackedSince = Time.time;
        if (Time.time - trackedSince < settleTime) return;
        started = true;
        startedAt = Time.time;
        MeasureHeight();
        FaceStart();
        SubsystemManager.GetSubsystems(inputs);
        foreach (XRInputSubsystem input in inputs) input.trackingOriginUpdated += OnRecentred;
    }

    void OnDestroy()
    {
        foreach (XRInputSubsystem input in inputs)
            if (input != null) input.trackingOriginUpdated -= OnRecentred;
    }

    void OnRecentred(XRInputSubsystem input)
    {
        if (Time.time - startedAt > 5f) return;
        MeasureHeight();
        FaceStart();
    }

    // How far to raise or lower the tracked head so the eyes stand at the set height
    void MeasureHeight()
    {
        if (eyeHeight <= 0f || origin.CameraFloorOffsetObject == null) return;
        heightShift = eyeHeight - origin.Camera.transform.localPosition.y;
        heightSet = true;
    }

    void FaceStart()
    {
        if (startFacing != null)
        {
            Vector3 toTarget = Vector3.ProjectOnPlane(startFacing.position - origin.Camera.transform.position, Vector3.up);
            if (toTarget.sqrMagnitude > 1e-4f) origin.MatchOriginUpCameraForward(Vector3.up, toTarget.normalized);
        }
        MoveToSpawn();
    }

    void MoveToSpawn()
    {
        Vector3 head = origin.Camera.transform.position;
        transform.position += new Vector3(spawn.x - head.x, 0f, spawn.z - head.z);
        Physics.SyncTransforms();
    }

    // Moves along the ground, relative to where the head faces
    void Walk()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || origin.Camera == null) return;

        Vector2 input = Vector2.zero;
        if (keyboard.wKey.isPressed) input.y += 1f;
        if (keyboard.sKey.isPressed) input.y -= 1f;
        if (keyboard.aKey.isPressed) input.x -= 1f;
        if (keyboard.dKey.isPressed) input.x += 1f;
        input = Vector2.ClampMagnitude(input, 1f);

        Transform head = origin.Camera.transform;
        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(head.right, Vector3.up).normalized;
        float speed = keyboard.shiftKey.isPressed ? runSpeed : moveSpeed;
        controller.Move((forward * input.y + right * input.x) * speed * Time.deltaTime);
    }

    // Whether the head has left the play area or gone into a wall or furniture (things that can be picked up don't count)
    Blocked HeadInside()
    {
        if (origin.Camera == null) return Blocked.None;
        Vector3 head = origin.Camera.transform.position;
        if (area != null)
        {
            Bounds b = area.bounds;
            if (head.x < b.min.x || head.x > b.max.x || head.z < b.min.z || head.z > b.max.z) return Blocked.OutOfArea;
        }
        int count = Physics.OverlapSphereNonAlloc(head, headRadius, hits, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (!hits[i].transform.IsChildOf(transform) && hits[i].attachedRigidbody == null) return Blocked.Wall;
        return Blocked.None;
    }

    // A black quad just in front of the eyes, drawn last in the viewfinder pass so it covers the tools too
    static MeshRenderer CreateFade(Transform head)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Fade";
        Destroy(quad.GetComponent<Collider>());
        quad.layer = LayerMask.NameToLayer("Viewfinder");
        quad.transform.SetParent(head, false);
        quad.transform.SetLocalPositionAndRotation(new Vector3(0f, 0f, 0.05f), Quaternion.identity);
        quad.transform.localScale = Vector3.one * 0.5f;

        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = new Material(Shader.Find("Look Again/HUD Icon")) { renderQueue = 4500 };
        renderer.sharedMaterial.SetColor(ColorId, Color.black);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.enabled = false;
        return renderer;
    }

    // Text in the middle of the view, far enough off to focus on, drawn over the black quad
    TMP_Text CreateMessage(Transform head)
    {
        if (messageFont == null) return null;
        TextMeshPro text = new GameObject("Fade Message", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
        text.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        text.gameObject.layer = LayerMask.NameToLayer("Viewfinder");
        text.transform.SetParent(head, false);
        text.transform.localPosition = new Vector3(0f, 0f, 0.8f);
        text.font = messageFont;
        if (messageMaterial != null) text.fontSharedMaterial = new Material(messageMaterial) { renderQueue = 4501 };
        text.color = new Color(1f, 0.96f, 0.88f);
        text.fontSize = 0.7f;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.rectTransform.sizeDelta = new Vector2(1f, 0.4f);
        text.gameObject.SetActive(false);
        return text;
    }
}
