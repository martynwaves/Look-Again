// Puts every light in the level out, glowing lamps included, and brings them back, each as a quick fade. The lantern dims itself by the same level.

using UnityEngine;

public class Blackout : MonoBehaviour
{
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    static Blackout instance;

    // How lit the level is, from 0 (every light out) to 1 (as it was)
    public static float Level => instance != null ? instance.shown : 1f;

    [Tooltip("Seconds the lights take to go out.")]
    [SerializeField] float outTime = 0.3f;

    [Tooltip("Seconds they take to come back.")]
    [SerializeField] float inTime = 0.5f;

    Light[] lights;
    float[] intensities;
    Renderer[] glowing;
    Color[] glows;
    MaterialPropertyBlock block;
    float level = 1f, target = 1f, shown = 1f;

    void Awake()
    {
        instance = this;
        block = new MaterialPropertyBlock();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // Every light and glowing surface but the lantern's, which follows the level by itself, and spectral things, which aren't lit at all
    void Start()
    {
        var found = new System.Collections.Generic.List<Light>();
        foreach (Light light in FindObjectsByType<Light>())
            if (light.GetComponentInParent<LanternLight>() == null && light.type != LightType.Directional) found.Add(light);
        lights = found.ToArray();
        intensities = System.Array.ConvertAll(lights, l => l.intensity);

        var lit = new System.Collections.Generic.List<Renderer>();
        var colors = new System.Collections.Generic.List<Color>();
        int spectral = LayerMask.NameToLayer("Spectral");
        foreach (Renderer part in FindObjectsByType<Renderer>())
        {
            Material look = part.sharedMaterial;
            if (look == null || part.gameObject.layer == spectral || part.GetComponentInParent<LanternLight>() != null) continue;
            if (!look.IsKeywordEnabled("_EMISSION") || !look.HasProperty(EmissionId)) continue;
            Color glow = look.GetColor(EmissionId);
            if (glow.maxColorComponent < 0.01f) continue;
            lit.Add(part);
            colors.Add(glow);
        }
        glowing = lit.ToArray();
        glows = colors.ToArray();
    }

    // Starts the lights going out, or coming back
    public static void Fade(bool on)
    {
        if (instance != null) instance.target = on ? 1f : 0f;
    }

    void Update()
    {
        if (level == target || lights == null) return;
        level = Mathf.MoveTowards(level, target, Time.deltaTime / Mathf.Max(target < level ? outTime : inTime, 0.001f));
        shown = level * level * (3f - 2f * level);
        for (int i = 0; i < lights.Length; i++)
            if (lights[i] != null) lights[i].intensity = intensities[i] * shown;
        for (int i = 0; i < glowing.Length; i++)
        {
            if (glowing[i] == null) continue;
            glowing[i].GetPropertyBlock(block);
            block.SetColor(EmissionId, glows[i] * shown);
            glowing[i].SetPropertyBlock(block);
        }
    }
}
