// A level-design marker: a spot where the watcher can stand, or a zone a scare trigger will use later.
// It only draws itself in the Scene view and does nothing in play, so scripts can be hooked up to it whenever they're ready.

using UnityEngine;

public class LayoutMarker : MonoBehaviour
{
    public enum Kind { WatcherSpot, TriggerZone }

    [SerializeField] Kind kind;

    [Tooltip("What happens here, as a reminder for whoever scripts it.")]
    [SerializeField, TextArea] string note;

    public Kind MarkerKind => kind;
    public string Note => note;

#if UNITY_EDITOR
    static readonly Color WatcherColor = new(0.9f, 0.2f, 0.2f, 0.9f);
    static readonly Color ZoneColor = new(1f, 0.35f, 0.3f, 0.12f);

    void OnDrawGizmos()
    {
        if (kind == Kind.WatcherSpot)
        {
            // A rough standing figure and the way it looks
            Gizmos.color = WatcherColor;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(new Vector3(0f, 0.95f, 0f), new Vector3(0.5f, 1.9f, 0.35f));
            Gizmos.DrawWireSphere(new Vector3(0f, 1.75f, 0f), 0.13f);
            Gizmos.DrawLine(new Vector3(0f, 1.75f, 0f), new Vector3(0f, 1.75f, 1.5f));
            Gizmos.matrix = Matrix4x4.identity;
        }
        else if (TryGetComponent(out BoxCollider box))
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = ZoneColor;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = WatcherColor;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.1f, name);
    }
#endif
}
