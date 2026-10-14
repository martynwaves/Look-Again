// Makes the ghost orb's body bob and drift. The floor light under it stays still.

using UnityEngine;

public class GhostOrb : MonoBehaviour
{
    [Tooltip("The part that hovers: the orb itself, its halo and the motes it sheds.")]
    [SerializeField] Transform body;

    [Tooltip("How far the body bobs up and down, and drifts sideways, in meters.")]
    [SerializeField] float bob = 0.06f, drift = 0.03f;

    [Tooltip("Bobs per second.")]
    [SerializeField] float speed = 0.12f;

    Vector3 rest;

    void Awake() => rest = body.localPosition;

    void Update()
    {
        float t = Time.time * speed * 2f * Mathf.PI;
        body.localPosition = rest + new Vector3(Mathf.Sin(t * 0.63f) * drift, Mathf.Sin(t) * bob, Mathf.Cos(t * 0.41f) * drift);
    }
}
