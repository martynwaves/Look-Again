// A painting that slips off its nail once every vase spot is filled, drops to the floor and topples onto its face.
// The object's pivot is the painting's bottom front edge, its forward axis pointing away from the wall.

using UnityEngine;

public class FallingPainting : MonoBehaviour
{
    [Tooltip("The spots that must all be filled before the painting falls.")]
    [SerializeField] VaseSpot[] spots;

    [Tooltip("Seconds between the last vase snapping into place and the painting falling.")]
    [SerializeField] float delay = 1.2f;

    [Tooltip("Height of the floor it lands on.")]
    [SerializeField] float floorHeight = 0f;

    [Tooltip("Seconds the painting takes to topple over once it hits the floor.")]
    [SerializeField] float toppleTime = 0.55f;

    public bool Fallen { get; private set; }

    const float UnhookTime = 0.15f, UnhookDrop = 0.02f, Roll = 4f, LeanOut = 8f, Out = 0.05f, BounceTime = 0.2f, Bounce = 4f;

    Vector3 hung;
    Quaternion hungRotation;
    float elapsed;

    void Awake()
    {
        hung = transform.position;
        hungRotation = transform.rotation;
    }

    void Update()
    {
        if (!Fallen)
        {
            if (spots.Length == 0) return;
            foreach (VaseSpot spot in spots)
                if (spot == null || !spot.Filled) return;
            Fallen = true;
            elapsed = -delay;
            return;
        }

        if (elapsed > Duration) return;
        elapsed += Time.deltaTime;
        if (elapsed >= 0f) Pose(Mathf.Min(elapsed, Duration));
    }

    float DropTime => Mathf.Sqrt(2f * Mathf.Max(hung.y - UnhookDrop - floorHeight, 0f) / Physics.gravity.magnitude);
    float Duration => UnhookTime + DropTime + toppleTime + BounceTime;

    // Unhooks with a small slip and twist, falls under gravity leaning out, then topples face down with a little bounce
    void Pose(float t)
    {
        float drop, outward, lean;
        float roll = Roll * Mathf.SmoothStep(0f, 1f, t / UnhookTime);
        if (t < UnhookTime)
        {
            drop = UnhookDrop * Mathf.SmoothStep(0f, 1f, t / UnhookTime);
            outward = 0f;
            lean = 0f;
        }
        else if ((t -= UnhookTime) < DropTime)
        {
            float k = DropTime > 0f ? t / DropTime : 1f;
            drop = UnhookDrop + 0.5f * Physics.gravity.magnitude * t * t;
            outward = Out * k;
            lean = LeanOut * k;
        }
        else
        {
            t -= DropTime;
            drop = hung.y - floorHeight;
            outward = Out;
            if (t < toppleTime)
            {
                float k = t / toppleTime;
                lean = LeanOut + (90f - LeanOut) * k * k;
            }
            else
            {
                lean = 90f - Bounce * Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - toppleTime) / BounceTime));
            }
        }

        Vector3 forward = hungRotation * Vector3.forward;
        transform.SetPositionAndRotation(hung + Vector3.down * drop + forward * outward,
            hungRotation * Quaternion.AngleAxis(lean, Vector3.right) * Quaternion.AngleAxis(roll, Vector3.forward));
    }
}
