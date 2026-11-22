// Outlines an item while a hand's laser or reach is on it, the belt would hand it to a hand, or a hand holds it, so it can be found even in the dark.
// It starts at the same moment the laser takes on the hover look, and is off while the item is up at the eye to look through.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRGrabInteractable))]
public class HoverOutline : MonoBehaviour
{
    static readonly int ColorId = Shader.PropertyToID("_Color"), ShiftId = Shader.PropertyToID("_Shift"), NearerId = Shader.PropertyToID("_Nearer");
    static readonly int ZTestId = Shader.PropertyToID("_ZTest"), ColorMaskId = Shader.PropertyToID("_ColorMask");
    static readonly int StencilCompId = Shader.PropertyToID("_StencilComp"), StencilPassId = Shader.PropertyToID("_StencilPass");

    // How many ways the item is nudged to draw the line around it, and how much nearer that's drawn, as a fraction of its distance
    const int Ways = 8;
    const float Nearer = 0.008f;

    [Tooltip("The outline shader.")]
    [SerializeField] Shader shader;

    [Tooltip("The outline's colour: the ghostly blue of the orb and the spirit.")]
    [SerializeField] Color color = new(0.35f, 0.7f, 1f, 1f);

    [Tooltip("How thick the outline is per meter it is from the eye, so it looks the same size near or far.")]
    [SerializeField] float width = 0.0025f;

    XRGrabInteractable item;
    HeldItemMotion motion;
    Material mark, line, unmark;
    MaterialPropertyBlock[] nudges;
    readonly List<MeshFilter> parts = new();

    void Awake()
    {
        item = GetComponent<XRGrabInteractable>();
        motion = GetComponent<HeldItemMotion>();
        if (shader == null) return;
        // Marks the item, drawn first; then draws it nudged each way where it isn't marked; then wipes the mark for whatever draws later
        mark = Step(2011, CompareFunction.LessEqual, 0, CompareFunction.Always, StencilOp.Replace);
        line = Step(2012, CompareFunction.LessEqual, 15, CompareFunction.NotEqual, StencilOp.Keep);
        unmark = Step(2013, CompareFunction.Always, 0, CompareFunction.Always, StencilOp.Zero);
        nudges = new MaterialPropertyBlock[Ways];
        for (int i = 0; i < Ways; i++)
        {
            float angle = i * 2f * Mathf.PI / Ways;
            nudges[i] = new MaterialPropertyBlock();
            nudges[i].SetVector(ShiftId, new Vector4(Mathf.Cos(angle), Mathf.Sin(angle)) * width);
        }
    }

    Material Step(int queue, CompareFunction depth, int colorMask, CompareFunction stencil, StencilOp pass)
    {
        var step = new Material(shader) { renderQueue = queue, hideFlags = HideFlags.DontSave };
        step.SetColor(ColorId, color);
        step.SetFloat(NearerId, Nearer);
        step.SetFloat(ZTestId, (float)depth);
        step.SetFloat(ColorMaskId, colorMask);
        step.SetFloat(StencilCompId, (float)stencil);
        step.SetFloat(StencilPassId, (float)pass);
        return step;
    }

    void OnEnable() => Application.onBeforeRender += Draw;
    void OnDisable() => Application.onBeforeRender -= Draw;

    void OnDestroy()
    {
        Destroy(mark);
        Destroy(line);
        Destroy(unmark);
    }

    // Whether a hand is on the item or holds it, or the belt would hand it over
    bool Wanted()
    {
        if (motion != null && motion.EyePose.HasValue) return false;
        foreach (IXRSelectInteractor hand in item.interactorsSelecting)
            if (hand is NearFarInteractor) return true;
        foreach (IXRHoverInteractor hand in item.interactorsHovering)
            if (hand is NearFarInteractor) return true;
        return BeltPointer.Chooses(item);
    }

    // After the toolkit has moved held items for this frame, so the outline doesn't trail them
    [BeforeRenderOrder(XRInteractionUpdateOrder.k_BeforeRenderLineVisual + 1)]
    void Draw()
    {
        if (mark != null && Wanted()) DrawFor(null);
    }

    // Draws the outline for one camera, or for all
    void DrawFor(Camera camera)
    {
        GetComponentsInChildren(parts);
        foreach (MeshFilter part in parts)
        {
            Mesh mesh = part.sharedMesh;
            if (mesh == null || part.TryGetComponent(out TMP_Text _) || !part.TryGetComponent(out MeshRenderer look) || !look.enabled) continue;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                DrawStep(mark, null, part, mesh, i, camera);
                foreach (MaterialPropertyBlock nudge in nudges) DrawStep(line, nudge, part, mesh, i, camera);
                DrawStep(unmark, null, part, mesh, i, camera);
            }
        }
    }

    static void DrawStep(Material step, MaterialPropertyBlock nudge, MeshFilter part, Mesh mesh, int submesh, Camera camera)
    {
        RenderParams look = new(step) { layer = part.gameObject.layer, camera = camera, matProps = nudge, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
        Graphics.RenderMesh(look, mesh, submesh, part.transform.localToWorldMatrix);
    }
}
