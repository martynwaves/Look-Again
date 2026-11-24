// The evil spirit that comes out of the ghost orb. Once the player sets off from the start it drifts straight at them, through walls, freezing while seen through the camera.
// The nearer it is, the stronger it shows in the camera and the bluer and shakier the view's edges; touching it, even frozen, puts every light out and sends the player back to the start.
// Dispelling the orb ends it.

using System.Collections;
using UnityEngine;

public class EvilSpirit : MonoBehaviour
{
    static readonly int SilhouetteId = Shader.PropertyToID("_Silhouette");
    static readonly int ImageId = Shader.PropertyToID("_Image");
    static readonly int StrengthId = Shader.PropertyToID("_Strength");

    [Tooltip("The orb the spirit comes from and goes back to; dispelling it ends the spirit.")]
    [SerializeField] GhostOrb source;

    [Tooltip("The spirit's body, whose material shows it through the camera.")]
    [SerializeField] Renderer body;

    [Tooltip("The line drawn from the spirit back to its source.")]
    [SerializeField] LineRenderer trail;

    [Tooltip("The material for the blue creeping in at the view's edges.")]
    [SerializeField] Material dread;

    [Header("Hunting")]
    [Tooltip("Whether the spirit hunts at all. Untick it to test the rest of the game in peace; it works while playing too.")]
    [SerializeField] bool huntsPlayer = true;

    [Tooltip("How far the player must go from the start, in meters, before the spirit sets out.")]
    [SerializeField] float wakeDistance = 2f;

    [Tooltip("How fast it drifts when it sets out, and how fast at most once it's been hunting a while, in meters per second.")]
    [SerializeField] float startSpeed = 0.3f, topSpeed = 0.8f;

    [Tooltip("Seconds of moving it takes to reach top speed.")]
    [SerializeField] float speedUpTime = 90f;

    [Tooltip("How far below the eyes its middle drifts, in meters.")]
    [SerializeField] float belowEyes = 0.7f;

    [Tooltip("How near the eyes it must come across the floor to catch the player, in meters.")]
    [SerializeField] float catchDistance = 0.5f;

    [Tooltip("How far off it can be spotted through the camera, in meters, walls or not.")]
    [SerializeField] float spotDistance = 15f;

    [Header("Showing")]
    [Tooltip("How near it must be for its outline to show through the camera at full strength, and how far it still shows faintly, in meters.")]
    [SerializeField] float clearDistance = 2f, faintDistance = 15f;

    [Tooltip("How near it must come before the view's edges start turning blue, in meters.")]
    [SerializeField] float dreadDistance = 14f;

    [Header("Caught")]
    [Tooltip("Seconds the lights stay out after the player is caught.")]
    [SerializeField] float darkTime = 2f;

    [Tooltip("Seconds it waits at its source after the lights come back, before setting out again.")]
    [SerializeField] float restTime = 3f;

    [Header("Sound")]
    [Tooltip("A looping drone from the spirit while it hunts, higher as it nears. Heard through walls, as it moves through them.")]
    [SerializeField] AudioSource drone;

    [Tooltip("Played the moment it catches the player.")]
    [SerializeField] AudioSource caughtSound;

    Player player;
    Material look;
    Renderer dreadQuad;
    Material dreadLook;
    Vector3[] trailPoints;
    float hunted, restUntil, nextPulse, gone;
    bool awake, caught;

    void Awake()
    {
        if (body != null) look = body.material;
        trailPoints = new Vector3[24];
        if (trail != null)
        {
            trail.useWorldSpace = true;
            trail.positionCount = trailPoints.Length;
        }
    }

    void Start()
    {
        player = FindAnyObjectByType<Player>();
        Camera view = Camera.main;
        if (view != null && dread != null) dreadQuad = CreateDread(view.transform);
        GoHome();
        Show(false);
    }

    void OnDestroy()
    {
        if (look != null) Destroy(look);
        if (dreadLook != null) Destroy(dreadLook);
    }

    void Update()
    {
        Camera view = Camera.main;
        if (view == null || source == null || player == null) return;
        Transform head = view.transform;

        // Dispelling its source ends it, fading away
        if (source.Dispelled)
        {
            gone = Mathf.MoveTowards(gone, 1f, Time.deltaTime);
            LayOut(head, 1f - gone);
            if (gone >= 1f) gameObject.SetActive(false);
            return;
        }

        // Switched off, it keeps out of the way until switched back on
        if (!huntsPlayer)
        {
            Show(false);
            LayOut(head, 0f);
            return;
        }
        if (awake && body != null && !body.enabled) Show(true);

        // It sets out once the player has left the start
        if (!awake)
        {
            Vector3 fromStart = head.position - player.SpawnPoint;
            if (new Vector2(fromStart.x, fromStart.z).magnitude > wakeDistance)
            {
                awake = true;
                Show(true);
            }
        }

        if (awake && !caught && Time.time >= restUntil)
        {
            Vector3 middle = transform.position;
            if (!SpectralCamera.Sees(middle, spotDistance))
            {
                hunted += Time.deltaTime;
                float speed = Mathf.Lerp(startSpeed, topSpeed, hunted / Mathf.Max(speedUpTime, 0.001f));
                Vector3 target = head.position + Vector3.down * belowEyes;
                transform.position = Vector3.MoveTowards(middle, target, speed * Time.deltaTime);
                Vector3 facing = Vector3.ProjectOnPlane(head.position - transform.position, Vector3.up);
                if (facing.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(facing), 1f - Mathf.Exp(-3f * Time.deltaTime));
            }
            // Touching it catches the player, frozen or not
            Vector3 across = head.position - transform.position;
            if (new Vector2(across.x, across.z).magnitude < catchDistance) StartCoroutine(Catch());
        }
        LayOut(head, awake && !caught ? 1f : 0f);
    }

    // Lights out, the orb's dispelling undone, the player back at the start and the spirit at its source; then the lights come back, and it rests a moment
    IEnumerator Catch()
    {
        caught = true;
        Sfx.Play(caughtSound, 1f, 0f);
        Blackout.Fade(false);
        source.Undo();
        yield return new WaitForSeconds(0.35f);
        player.Respawn();
        GoHome();
        hunted = 0f;
        yield return new WaitForSeconds(Mathf.Max(darkTime - 0.35f, 0f));
        Blackout.Fade(true);
        restUntil = Time.time + 0.5f + restTime;
        caught = false;
    }

    void GoHome()
    {
        if (source != null) transform.position = source.Middle;
    }

    void Show(bool shown)
    {
        if (body != null) body.enabled = shown;
        if (trail != null) trail.enabled = shown;
    }

    // How strongly it shows in the camera, the trail back to its source, the blue at the view's edges and the controllers' shiver, all by how near it is
    void LayOut(Transform head, float presence)
    {
        float distance = Vector3.Distance(head.position, transform.position);
        float near = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(clearDistance, faintDistance, distance));
        if (look != null)
        {
            look.SetFloat(SilhouetteId, Mathf.Lerp(0.15f, 1.3f, near) * presence);
            look.SetFloat(ImageId, presence);
        }

        // Grows evenly the whole way in, so it's noticed well before it's alarming
        float dreadStrength = Mathf.Clamp01(Mathf.InverseLerp(dreadDistance, catchDistance, distance)) * presence;
        if (dreadLook != null)
        {
            dreadLook.SetFloat(StrengthId, dreadStrength);
            dreadQuad.enabled = dreadStrength > 0.001f;
        }
        Shiver(dreadStrength);
        Sfx.Loop(drone, presence, Mathf.Lerp(0.9f, 1.25f, dreadStrength), presence > 0f ? 2f : 12f);

        if (trail == null || !trail.enabled) return;
        Vector3 from = transform.position, to = source.Middle;
        Vector3 side = Vector3.Cross(to - from, Vector3.up).normalized;
        for (int i = 0; i < trailPoints.Length; i++)
        {
            float t = i / (trailPoints.Length - 1f);
            // A lazy wave along it that dies away at both ends
            float sway = Mathf.Sin(t * Mathf.PI) * (Mathf.Sin(t * 9f - Time.time * 1.3f) * 0.12f + Mathf.Sin(t * 4f + Time.time * 0.7f) * 0.18f);
            trailPoints[i] = Vector3.Lerp(from, to, t) + side * sway + Vector3.up * sway * 0.5f;
        }
        trail.SetPositions(trailPoints);
        trail.widthMultiplier = presence;
    }

    // In a headset, the controllers rumble faster and harder as it nears
    void Shiver(float strength)
    {
        if (strength < 0.15f || !UnityEngine.XR.XRSettings.isDeviceActive || Time.time < nextPulse) return;
        nextPulse = Time.time + Mathf.Lerp(0.9f, 0.18f, strength);
        float amplitude = Mathf.Lerp(0.05f, 0.5f, strength);
        UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand).SendHapticImpulse(0, amplitude, 0.06f);
        UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand).SendHapticImpulse(0, amplitude, 0.06f);
    }

    // A quad in front of the eyes whose shader fills each eye's view, drawn just under the black fade
    Renderer CreateDread(Transform head)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Spirit Dread";
        Destroy(quad.GetComponent<Collider>());
        quad.layer = LayerMask.NameToLayer("Viewfinder");
        quad.transform.SetParent(head, false);
        quad.transform.SetLocalPositionAndRotation(new Vector3(0f, 0f, 0.06f), Quaternion.identity);
        quad.transform.localScale = Vector3.one * 0.5f;
        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        dreadLook = new Material(dread) { renderQueue = 4400 };
        renderer.sharedMaterial = dreadLook;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.enabled = false;
        return renderer;
    }
}
