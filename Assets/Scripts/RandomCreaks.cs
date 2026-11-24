// Now and then, while the player is somewhere in its zone, a creak from a random spot nearby but out of reach, as if something moved a few aisles over.
// The zone is the box collider on this object; the sound plays from its own source, moved to each new spot.

using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class RandomCreaks : MonoBehaviour
{
    [Tooltip("The creak. Its source is moved about the zone to play each one.")]
    [SerializeField] AudioSource creak;

    [Tooltip("Seconds between creaks, at least and at most.")]
    [SerializeField] float minGap = 10f, maxGap = 30f;

    [Tooltip("How near the player a creak may be, and how far, in meters across the floor.")]
    [SerializeField] float nearest = 3f, farthest = 8f;

    [Tooltip("How high the creaks come from, in meters above the zone's floor.")]
    [SerializeField] float height = 1.4f;

    BoxCollider zone;
    float next;

    void Awake()
    {
        zone = GetComponent<BoxCollider>();
        next = Time.time + Random.Range(minGap, maxGap);
    }

    void Update()
    {
        Camera view = Camera.main;
        if (view == null || creak == null || Time.time < next) return;
        next = Time.time + Random.Range(minGap, maxGap);
        Vector3 head = view.transform.position;
        Bounds area = zone.bounds;
        if (head.x < area.min.x || head.x > area.max.x || head.z < area.min.z || head.z > area.max.z) return;

        // A few tries for a spot inside the zone at the right distance
        for (int i = 0; i < 8; i++)
        {
            Vector2 way = Random.insideUnitCircle.normalized * Random.Range(nearest, farthest);
            Vector3 spot = new(head.x + way.x, area.min.y + height, head.z + way.y);
            if (spot.x < area.min.x || spot.x > area.max.x || spot.z < area.min.z || spot.z > area.max.z) continue;
            creak.transform.position = spot;
            Sfx.Play(creak, 1f, 0.12f);
            return;
        }
    }
}
