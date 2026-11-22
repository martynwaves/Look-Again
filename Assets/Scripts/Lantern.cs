// The crank lantern on a monitor: holding R takes it up in front of the eyes from the belt or a hand, where the right hand winds its cap;
// letting go puts it back on the belt. In a headset it's wound by twisting its cap by hand instead, with a tip by it until it's first wound a full turn.

using UnityEngine;

public class Lantern : HeldTool
{
    [Header("Lantern")]
    [Tooltip("Where the lantern stands while taken up from the keyboard to be wound.")]
    [SerializeField] Transform raisedPoint;

    [Tooltip("How the lantern is tipped while taken up, so its cap shows.")]
    [SerializeField] Vector3 raisedRotation = new(-28f, 0f, 0f);

    [Tooltip("How far below its spot the lantern starts as it's taken up, in meters.")]
    [SerializeField] float raiseDrop = 0.2f;

    [Header("Winding")]
    [Tooltip("The hand shown winding the cap, steered off its controller while R is held.")]
    [SerializeField] HandPoser hand;

    [Tooltip("Seconds one wind takes: a twist of the cap, then the hand turning back for the next.")]
    [SerializeField] float strokeTime = 0.55f;

    [Tooltip("Degrees the cap turns with each twist.")]
    [SerializeField] float strokeAngle = 120f;

    [Tooltip("How high the palm sits above the cap's pivot, in meters.")]
    [SerializeField] float gripHeight = 0.065f;

    // Of each stroke, the part spent gripping and twisting; the rest turns back
    const float Gripping = 0.65f;

    LanternLight lantern;
    TMPro.TMP_Text windTip;
    Vector3 raisedAt;
    float stroke, twisted;

    // Whether the lantern is alight
    public bool Lit => lantern != null && lantern.Charge > 0.02f;

    // Whether the player is holding the lantern out: in a hand, or on a monitor dragged by the mouse or taken up with its key
    public bool Ready => InHand || TakenOut;

    // Whether it's shining its light out, held out and lit
    public bool Shining => Lit && Ready;

    // Where its light comes from
    public Vector3 LightPosition => item != null ? item.transform.position : transform.position;

    public Lantern() : base("Lantern", "<Keyboard>/r") { }

    protected override Transform RaisedAnchor => raisedPoint;
    protected override Quaternion RaisedRotation => Quaternion.Euler(raisedRotation);

    protected override void Awake()
    {
        if (raisedPoint != null) raisedAt = raisedPoint.localPosition;
        base.Awake();
        if (item != null) lantern = item.GetComponent<LanternLight>();
        windTip = CreateTip("Grip the cap with your other hand\nand turn it to wind the lantern");
    }

    protected override void LayOut(float eased)
    {
        if (raisedPoint != null) raisedPoint.localPosition = raisedAt + Vector3.down * ((1f - eased) * raiseDrop);
        bool learning = Headset && InHand && lantern != null && lantern.Cap != null && lantern.TotalWound < 360f;
        PlaceTip(windTip, learning, learning ? lantern.Cap.position + Vector3.up * 0.12f : Vector3.zero);
        // Wound once it's up in front of the eyes
        if (TakenOut && eased > 0.9f && lantern != null && lantern.Cap != null) Wind();
        else
        {
            stroke = 0f;
            twisted = -strokeAngle * 0.5f;
        }
    }

    // Each stroke grips the cap and twists it, then lets go and turns back; the cap only turns while gripped
    void Wind()
    {
        stroke = Mathf.Repeat(stroke + Time.deltaTime / Mathf.Max(strokeTime, 0.01f), 1f);
        bool gripping = stroke < Gripping;
        float t = gripping ? stroke / Gripping : (stroke - Gripping) / (1f - Gripping);
        t = t * t * (3f - 2f * t);
        float twist = (gripping ? t - 0.5f : 0.5f - t) * strokeAngle;
        if (gripping) lantern.Wind(Mathf.Max(0f, twist - twisted));
        twisted = twist;
        if (hand == null || View == null) return;

        // From above, palm down on the cap, fingers pointing away and to the left as a right hand reaches across, lifting slightly while turning back
        Transform body = item.transform;
        Vector3 away = Vector3.ProjectOnPlane(body.InverseTransformDirection(View.transform.forward), Vector3.up).normalized;
        Vector3 fingers = Quaternion.AngleAxis(twist - 45f, Vector3.up) * away;
        float lift = gripping ? 0f : Mathf.Sin(t * Mathf.PI) * 0.012f;
        Vector3 palmAt = lantern.Cap.localPosition + Vector3.up * (gripHeight + lift);
        hand.Steer(body, palmAt, Vector3.down, fingers, gripping ? 0.62f : 0.3f);
    }
}
