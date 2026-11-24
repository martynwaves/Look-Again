// A scare that plays a sound the first time the player's head enters a zone: a whisper behind them, static from the TV, a phone across the room.
// It can wait for the lever book to be pulled first, and stop after a while. The zone is the box collider on this object.

using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class ScareSound : MonoBehaviour
{
    [Tooltip("The sound to play. It plays from wherever its source is, so put the source on the thing that makes it.")]
    [SerializeField] AudioSource sound;

    [Tooltip("Moves the sound just behind the player's head as it plays, a little to one side, as if someone were there.")]
    [SerializeField] bool behindHead;

    [Tooltip("How far behind the head, and how far to the side, in meters.")]
    [SerializeField] float behind = 0.8f, aside = 0.35f;

    [Tooltip("Only after this lever book has been pulled. Leave empty to play the first time the zone is entered.")]
    [SerializeField] LeverBook afterLeverBook;

    [Tooltip("Seconds of delay after entering before it plays.")]
    [SerializeField] float delay;

    [Tooltip("Seconds it plays for before fading out, for a looping sound such as static. 0 plays the clip through once.")]
    [SerializeField] float playFor;

    public bool Played { get; private set; }

    BoxCollider zone;
    float enteredAt = -1f, startedAt = -1f;

    void Awake() => zone = GetComponent<BoxCollider>();

    void Update()
    {
        Camera view = Camera.main;
        if (view == null) return;
        Transform head = view.transform;

        if (startedAt >= 0f && playFor > 0f && sound != null)
        {
            // Fades out over the last second
            float left = startedAt + playFor - Time.time;
            if (left <= 0f) { sound.Stop(); startedAt = -1f; enabled = false; }
            else if (left < 1f) sound.volume = Mathf.Min(sound.volume, left);
            return;
        }
        if (Played) return;
        if (afterLeverBook != null && !afterLeverBook.Pulled) return;

        if (enteredAt < 0f)
        {
            // Inside the box across the floor; height doesn't matter, so crouching or a tall player still counts
            Vector3 local = transform.InverseTransformPoint(head.position) - zone.center;
            Vector3 half = zone.size * 0.5f;
            if (Mathf.Abs(local.x) > half.x || Mathf.Abs(local.z) > half.z) return;
            enteredAt = Time.time;
        }
        if (Time.time < enteredAt + delay) return;

        Played = true;
        if (sound == null) return;
        if (behindHead)
        {
            Vector3 back = Vector3.ProjectOnPlane(-head.forward, Vector3.up).normalized;
            Vector3 side = Vector3.ProjectOnPlane(head.right, Vector3.up).normalized * (Random.value < 0.5f ? -1f : 1f);
            sound.transform.position = head.position + back * behind + side * aside;
        }
        Sfx.Play(sound, 1f, 0f);
        if (playFor > 0f) startedAt = Time.time;
        else enabled = false;
    }
}
