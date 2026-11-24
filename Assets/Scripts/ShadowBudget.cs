// Where the renderer can only fit so many shadow maps, as on a standalone headset, switches off the shadow-casting lights over that budget, the least needed first.
// A light out of view goes at once; one in view but hidden or far fades out, and fades back in once there's room for it. Does nothing where every shadow fits.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class ShadowBudget : MonoBehaviour
{
    static readonly RaycastHit[] hits = new RaycastHit[8];

    [Tooltip("Seconds a light in view takes to fade out, or back in.")]
    [SerializeField] float fadeTime = 0.5f;

    [Tooltip("How much nearer, in meters, a light must be than one that's lit to take its place, so they don't swap back and forth.")]
    [SerializeField] float stickiness = 2f;

    [Tooltip("How much further, in meters, a light counts as when a wall stands between it and the eyes.")]
    [SerializeField] float hiddenPenalty = 10f;

    [Tooltip("What blocks the view of a light.")]
    [SerializeField] LayerMask walls = 1;

    class Managed
    {
        public Light light;
        public float intensity, fade = 1f, score;
        public bool wanted = true, inView;
    }

    readonly List<Managed> lights = new();
    readonly List<Managed> order = new();
    readonly List<Light> lanterns = new();
    readonly Plane[] planes = new Plane[6];
    int budget;

    // The lantern's light is never switched off, so its shadows always have room; a light switched off in the scene is left off
    void Start()
    {
        budget = UniversalRenderPipeline.maxVisibleAdditionalLights;
        int total = 0;
        foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            if (light.type is not (LightType.Point or LightType.Spot) || light.shadows == LightShadows.None || !light.enabled) continue;
            total += Slices(light);
            if (light.GetComponentInParent<LanternLight>() != null) lanterns.Add(light);
            else lights.Add(new Managed { light = light, intensity = light.intensity });
        }
        if (total <= budget) enabled = false;
    }

    static int Slices(Light light) => light.type == LightType.Point ? 6 : 1;

    bool InView(Light light) => GeometryUtility.TestPlanesAABB(planes, new Bounds(light.transform.position, Vector3.one * (2f * light.range)));

    // Whether nothing fixed stands between the eyes and the light, short of its own fitting
    static bool Sees(Transform eye, Vector3 at, LayerMask walls)
    {
        Vector3 from = eye.position, to = at - from;
        float far = to.magnitude - 0.6f;
        if (far <= 0f) return true;
        int count = Physics.RaycastNonAlloc(from, to.normalized, hits, far, walls, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (hits[i].collider.attachedRigidbody == null && !hits[i].collider.transform.IsChildOf(eye.root)) return false;
        return true;
    }

    void LateUpdate()
    {
        Camera eye = Camera.main;
        if (eye == null) return;
        GeometryUtility.CalculateFrustumPlanes(eye.cullingMatrix, planes);
        Transform head = eye.transform;

        // Room left once the lantern is counted, handed out to the lights in view first, then nearest and in sight
        int drawn = 0;
        foreach (Light lamp in lanterns)
            if (lamp != null && lamp.isActiveAndEnabled && InView(lamp)) drawn += Slices(lamp);
        order.Clear();
        foreach (Managed m in lights)
        {
            m.wanted = false;
            if (m.light == null || !m.light.gameObject.activeInHierarchy) continue;
            Vector3 at = m.light.transform.position;
            m.inView = InView(m.light);
            m.score = Vector3.Distance(head.position, at) + (m.inView ? 0f : 1000f);
            if (!Sees(head, at, walls)) m.score += hiddenPenalty;
            if (m.light.enabled) m.score -= stickiness;
            order.Add(m);
        }
        order.Sort((a, b) => a.score.CompareTo(b.score));
        int room = budget - drawn;
        foreach (Managed m in order)
        {
            int slices = Slices(m.light);
            if (slices > room) continue;
            m.wanted = true;
            room -= slices;
        }

        // Lights going out make room before any comes back, so the renderer never has more than fits
        float step = Time.deltaTime / Mathf.Max(fadeTime, 0.001f);
        foreach (Managed m in lights)
        {
            if (m.light == null || !m.light.enabled || !m.light.gameObject.activeInHierarchy) continue;
            if (m.wanted)
            {
                if (m.inView) drawn += Slices(m.light);
                continue;
            }
            m.fade = m.inView ? Mathf.MoveTowards(m.fade, 0f, step) : 0f;
            if (m.fade > 0f) drawn += Slices(m.light);
            else m.light.enabled = false;
        }
        foreach (Managed m in lights)
        {
            if (m.light == null) continue;
            if (m.wanted && !m.light.enabled)
            {
                int slices = m.inView ? Slices(m.light) : 0;
                if (drawn + slices > budget) continue;
                drawn += slices;
                m.light.enabled = true;
                m.fade = m.inView ? 0f : 1f;
            }
            if (m.wanted) m.fade = Mathf.MoveTowards(m.fade, 1f, step);
            if (m.light.enabled) m.light.intensity = m.intensity * Blackout.Level * m.fade;
        }
    }
}
