// Folds a book open or shut by turning its right half over the left about the spine; fully shut, a whole closed book stands in, so the spine is whole.
// The belt is shown it shut, however open it is now.

using UnityEngine;

public class BookFold : MonoBehaviour, IStowedShape
{
    [Tooltip("The book's right half, a child of the book, which folds over the left to shut it. Its pivot is on the spine, level with the pages.")]
    [SerializeField] Transform foldingHalf;

    [Tooltip("A closed book fitted to the folded halves, shown instead of them once the book is fully shut. The halves split the spine, which shows when folded.")]
    [SerializeField] Renderer shutBook;

    [Tooltip("Seconds the book takes to open or shut.")]
    [SerializeField] float foldTime = 0.6f;

    public bool Open { get; set; }

    // The shut book's middle, which its collider fits
    public Vector3 StowedMiddle => box != null ? box.center : Vector3.zero;

    // Faces the body like the slot, so it only turns a little to face the eyes when picked up
    public Quaternion StowedTurn => Quaternion.identity;

    // How far the book is shut, from 0 (open) to 1 (shut)
    float fold = 1f;
    BoxCollider box;
    Renderer leftHalf, rightHalf;

    void Awake()
    {
        box = GetComponent<BoxCollider>();
        leftHalf = GetComponent<Renderer>();
        if (foldingHalf != null) rightHalf = foldingHalf.GetComponent<Renderer>();
    }

    void Update()
    {
        fold = Mathf.MoveTowards(fold, Open ? 0f : 1f, Time.deltaTime / Mathf.Max(foldTime, 0.001f));
        if (foldingHalf != null) foldingHalf.localRotation = Turn(fold);

        bool shut = fold >= 1f;
        if (shutBook == null || shutBook.enabled == shut) return;
        shutBook.enabled = shut;
        if (leftHalf != null) leftHalf.enabled = !shut;
        if (rightHalf != null) rightHalf.enabled = !shut;
    }

    // Eases in and out with no sudden start or stop
    static Quaternion Turn(float shut) => Quaternion.Euler(0f, -180f * shut * shut * shut * (shut * (shut * 6f - 15f) + 10f), 0f);

    // Stowed, only the closed book shows, if there is one
    public bool ShowsStowed(Transform part) => shutBook == null ? part.TryGetComponent(out Renderer look) && look.enabled : part == shutBook.transform;

    public Matrix4x4 StowedPose(Transform part)
    {
        Matrix4x4 pose = transform.worldToLocalMatrix * part.localToWorldMatrix;
        if (foldingHalf == null || !part.IsChildOf(foldingHalf)) return pose;
        // The folding half, and anything on it, turned shut about its pivot
        Matrix4x4 now = Matrix4x4.TRS(foldingHalf.localPosition, foldingHalf.localRotation, foldingHalf.localScale);
        Matrix4x4 shut = Matrix4x4.TRS(foldingHalf.localPosition, Turn(1f), foldingHalf.localScale);
        return shut * now.inverse * pose;
    }
}
