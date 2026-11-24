// In a headset, shows a steady flat view on the monitor for recording: it follows the head smoothly, level with the horizon, instead of one eye's shaky image.
// It renders into its own texture laid over the monitor window, so the headset's view is left alone. Turn the object off to get the plain eye mirror back.

using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

[RequireComponent(typeof(Camera))]
public class SpectatorView : MonoBehaviour
{
    [Tooltip("The headset camera to follow.")]
    [SerializeField] Camera head;

    [Tooltip("The view's vertical field of view, in degrees.")]
    [SerializeField] float fieldOfView = 65f;

    [Tooltip("How quickly the view catches up with the head's position and turning. Lower is smoother but lags more.")]
    [SerializeField] float followPosition = 12f, followTurning = 5f;

    [Tooltip("The view's height in pixels; its width follows the window.")]
    [SerializeField] int height = 720;

    [Tooltip("Renders every this many frames, to keep the headset's frame rate up.")]
    [SerializeField] int renderEvery = 2;

    Camera view;
    RenderTexture image;
    Canvas overlay;
    RawImage shown;
    bool placed;

    void Awake()
    {
        view = GetComponent<Camera>();
        view.enabled = false;
        view.stereoTargetEye = StereoTargetEyeMask.None;
        view.fieldOfView = fieldOfView;
        if (TryGetComponent(out UniversalAdditionalCameraData data)) data.allowXRRendering = false;

        var canvasObject = new GameObject("Spectator Overlay", typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);
        overlay = canvasObject.GetComponent<Canvas>();
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = short.MaxValue;
        var imageObject = new GameObject("View", typeof(RawImage));
        imageObject.transform.SetParent(canvasObject.transform, false);
        shown = imageObject.GetComponent<RawImage>();
        shown.raycastTarget = false;
        var rect = shown.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        overlay.enabled = false;
    }

    void OnDisable()
    {
        view.enabled = false;
        if (overlay != null) overlay.enabled = false;
        placed = false;
    }

    void OnDestroy()
    {
        if (image != null) image.Release();
    }

    void LateUpdate()
    {
        bool headset = UnityEngine.XR.XRSettings.isDeviceActive && head != null;
        overlay.enabled = headset;
        view.enabled = headset && Time.frameCount % Mathf.Max(1, renderEvery) == 0;
        if (!headset)
        {
            placed = false;
            return;
        }
        FitImage();
        view.cullingMask = head.cullingMask;

        Transform eye = head.transform;
        Quaternion level = Quaternion.LookRotation(eye.forward, Vector3.up);
        if (!placed)
        {
            transform.SetPositionAndRotation(eye.position, level);
            placed = true;
            return;
        }
        float dt = Time.unscaledDeltaTime;
        transform.SetPositionAndRotation(
            Vector3.Lerp(transform.position, eye.position, 1f - Mathf.Exp(-followPosition * dt)),
            Quaternion.Slerp(transform.rotation, level, 1f - Mathf.Exp(-followTurning * dt)));
    }

    // Keeps the texture shaped like the window
    void FitImage()
    {
        int h = Mathf.Max(64, height), w = Mathf.Max(64, Mathf.RoundToInt(h * (float)Screen.width / Mathf.Max(1, Screen.height)));
        if (image != null && image.width == w && image.height == h) return;
        if (image != null) image.Release();
        image = new RenderTexture(w, h, 24) { name = "Spectator View" };
        view.targetTexture = image;
        view.aspect = (float)w / h;
        shown.texture = image;
    }
}
