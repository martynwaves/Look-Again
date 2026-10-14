// Walks the player with WASD, fades the view to black while their head is in a wall or out of the play area,
// and quits on Esc in builds.

using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(XROrigin), typeof(CharacterController))]
public class Player : MonoBehaviour
{
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    [Tooltip("Collider whose horizontal bounds define the walkable area (the floor).")]
    [SerializeField] Collider area;

    [Tooltip("WASD walking speed, in meters per second.")]
    [SerializeField] float moveSpeed = 1.5f;

    [Header("Fade")]
    [Tooltip("How close the head may come to walls and furniture before the view fades, in meters.")]
    [SerializeField] float headRadius = 0.1f;
    [Tooltip("Seconds the view takes to fade out or back in.")]
    [SerializeField] float fadeTime = 0.2f;

    XROrigin origin;
    CharacterController controller;
    MeshRenderer fade;
    MaterialPropertyBlock fadeBlock;
    GameObject comfortVignette;
    readonly Collider[] hits = new Collider[8];
    float faded;

    void Awake()
    {
        origin = GetComponent<XROrigin>();
        controller = GetComponent<CharacterController>();
        if (origin.Camera != null) fade = CreateFade(origin.Camera.transform);
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
        Walk();
    }

    void LateUpdate()
    {
        if (fade == null) return;
        faded = Mathf.MoveTowards(faded, HeadInside() ? 1f : 0f, Time.deltaTime / Mathf.Max(fadeTime, 0.001f));
        fade.enabled = faded > 0f;
        fadeBlock.SetFloat(AlphaId, faded);
        fade.SetPropertyBlock(fadeBlock);
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
        controller.Move((forward * input.y + right * input.x) * moveSpeed * Time.deltaTime);
    }

    // Whether the head has left the play area or gone into a wall or furniture (things that can be picked up don't count)
    bool HeadInside()
    {
        if (origin.Camera == null) return false;
        Vector3 head = origin.Camera.transform.position;
        if (area != null)
        {
            Bounds b = area.bounds;
            if (head.x < b.min.x || head.x > b.max.x || head.z < b.min.z || head.z > b.max.z) return true;
        }
        int count = Physics.OverlapSphereNonAlloc(head, headRadius, hits, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (!hits[i].transform.IsChildOf(transform) && hits[i].attachedRigidbody == null) return true;
        return false;
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
}
