// Where a vase once stood, marked by a silhouette seen only through the spectral camera.
// Bringing the vase close snaps it into place for good, and the silhouette and its dust fade away.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class VaseSpot : MonoBehaviour
{
    static readonly int BrightnessId = Shader.PropertyToID("_Brightness");

    [Tooltip("The vase that belongs here.")]
    [SerializeField] XRGrabInteractable vase;

    [Tooltip("How close the middle of the vase must come to the middle of its silhouette to snap into place, in meters.")]
    [SerializeField] float snapRadius = 0.3f;

    [Tooltip("Seconds the vase takes to settle into place.")]
    [SerializeField] float settleTime = 0.3f;

    [Tooltip("The silhouette, faded out once the vase is back.")]
    [SerializeField] Renderer silhouette;

    [Tooltip("Seconds the silhouette takes to fade.")]
    [SerializeField] float fadeTime = 1.5f;

    [Tooltip("Ghostly dust around the silhouette, which stops once the vase is back.")]
    [SerializeField] ParticleSystem dust;

    public bool Filled { get; private set; }

    Rigidbody body;
    Renderer vaseRenderer;
    MaterialPropertyBlock block;
    Vector3 middle, fromPosition;
    Quaternion fromRotation;
    float brightness, settled, faded;

    void Awake()
    {
        body = vase != null ? vase.GetComponent<Rigidbody>() : null;
        vaseRenderer = vase != null ? vase.GetComponentInChildren<Renderer>() : null;
        block = new MaterialPropertyBlock();
        if (silhouette != null) brightness = silhouette.sharedMaterial.GetFloat(BrightnessId);
        middle = silhouette != null ? silhouette.bounds.center : transform.position;
    }

    void Update()
    {
        if (!Filled)
        {
            // Middles rather than bases, so a vase held tilted over its silhouette still counts
            if (vase != null)
            {
                Vector3 vaseMiddle = vaseRenderer != null ? vaseRenderer.bounds.center : vase.transform.position;
                if ((vaseMiddle - middle).sqrMagnitude <= snapRadius * snapRadius) Snap();
            }
            return;
        }

        if (settled < 1f)
        {
            settled = Mathf.MoveTowards(settled, 1f, Time.deltaTime / Mathf.Max(settleTime, 0.001f));
            float eased = Mathf.SmoothStep(0f, 1f, settled);
            vase.transform.SetPositionAndRotation(Vector3.Lerp(fromPosition, transform.position, eased), Quaternion.Slerp(fromRotation, transform.rotation, eased));
        }

        if (silhouette != null && faded < 1f)
        {
            faded = Mathf.MoveTowards(faded, 1f, Time.deltaTime / Mathf.Max(fadeTime, 0.001f));
            silhouette.GetPropertyBlock(block);
            block.SetFloat(BrightnessId, brightness * (1f - faded));
            silhouette.SetPropertyBlock(block);
            if (faded >= 1f) silhouette.enabled = false;
        }
    }

    // Takes the vase out of the player's hand if held, and fixes it here
    void Snap()
    {
        Filled = true;
        if (vase.isSelected) vase.interactionManager.CancelInteractableSelection((IXRSelectInteractable)vase);
        vase.enabled = false;
        if (body != null)
        {
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
        }
        fromPosition = vase.transform.position;
        fromRotation = vase.transform.rotation;
        if (dust != null) dust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
