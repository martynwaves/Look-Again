// Folds a book open or shut by turning its right half over the left, bending the spine between the covers into a rounded back once shut.
// The belt is shown it shut, however open it is now.

using System.Collections.Generic;
using UnityEngine;

public class BookFold : MonoBehaviour, IStowedShape
{
    [Tooltip("The book's right half, a child of the book, which folds over the left to shut it. Its pivot is on the spine, level with the pages.")]
    [SerializeField] Transform foldingHalf;

    [Tooltip("Seconds the book takes to open or shut.")]
    [SerializeField] float foldTime = 0.6f;

    [Tooltip("Half the spine's width with the book open, out to where each cover starts, in the book's own units.")]
    [SerializeField] float spineHalfWidth = 0.28f;

    [Tooltip("How far below the pivot the outside of the covers is, in the book's own units.")]
    [SerializeField] float coverDepth = 0.617f;

    [Tooltip("How many strips the spine is cut into so it bends smoothly.")]
    [SerializeField] int spineStrips = 16;

    public bool Open { get; set; }

    // The shut book's middle, which its collider fits
    public Vector3 StowedMiddle => box != null ? box.center : Vector3.zero;

    // Faces the body like the slot, so it only turns a little to face the eyes when picked up
    public Quaternion StowedTurn => Quaternion.identity;

    // A half's own copy of its mesh, as it is open and as it's bent now
    class Half
    {
        public Mesh mesh, shut;
        public Vector3 offset;
        public Vector3[] open, openNormals, bent, bentNormals;
        public Vector4[] openTangents, bentTangents;
    }

    // How far the book is shut, from 0 (open) to 1 (shut)
    float fold = 1f, shownFold = -1f;
    BoxCollider box;
    Vector3 pivot;
    Half left, right;

    void Awake()
    {
        box = GetComponent<BoxCollider>();
        if (foldingHalf == null) return;
        pivot = foldingHalf.localPosition;
        left = Bendable(GetComponent<MeshFilter>(), Vector3.zero);
        right = Bendable(foldingHalf.GetComponent<MeshFilter>(), pivot);
        // A copy of each half bent shut, for the belt to show
        Bend(180f);
        if (left != null) left.shut = Instantiate(left.mesh);
        if (right != null) right.shut = Instantiate(right.mesh);
        Bend(Ease(fold) * 180f);
        shownFold = fold;
    }

    void OnDestroy()
    {
        foreach (Half half in new[] { left, right })
        {
            if (half == null) continue;
            Destroy(half.mesh);
            Destroy(half.shut);
        }
    }

    // Each book gets its own copy of a half's mesh, its spine cut into strips so it can bend
    Half Bendable(MeshFilter filter, Vector3 offset)
    {
        if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable) return null;
        float from = pivot.x - spineHalfWidth, step = 2f * spineHalfWidth / Mathf.Max(spineStrips, 1);
        List<float> cuts = new();
        for (int i = 1; i < spineStrips; i++) cuts.Add(from + i * step - offset.x);
        Mesh mesh = SliceAcrossX(filter.sharedMesh, cuts);
        mesh.MarkDynamic();
        filter.sharedMesh = mesh;
        Half half = new() { mesh = mesh, offset = offset, open = mesh.vertices, openNormals = mesh.normals, openTangents = mesh.tangents };
        half.bent = new Vector3[half.open.Length];
        half.bentNormals = new Vector3[half.openNormals.Length];
        half.bentTangents = new Vector4[half.openTangents.Length];
        return half;
    }

    void Update()
    {
        fold = Mathf.MoveTowards(fold, Open ? 0f : 1f, Time.deltaTime / Mathf.Max(foldTime, 0.001f));
        if (fold != shownFold) Bend(Ease(fold) * 180f);
        shownFold = fold;
    }

    // Eases in and out with no sudden start or stop
    static float Ease(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    static Quaternion Turn(float angle) => Quaternion.Euler(0f, -angle, 0f);

    // Turns a point (x, z) in the book's cross-section anticlockwise
    static Vector2 Rotate(Vector2 p, float radians) => new(p.x * Mathf.Cos(radians) - p.y * Mathf.Sin(radians), p.x * Mathf.Sin(radians) + p.y * Mathf.Cos(radians));

    // The spine runs between hinges at the covers' edges; it bends into an arc that turns as far as the right half has, so both ends stay on their covers.
    // Shut, it stands as tall as the book is thick, which puts the right half exactly over the left
    void Bend(float angle)
    {
        if (foldingHalf == null) return;
        float turn = angle * Mathf.Deg2Rad;
        float bottom = pivot.z - coverDepth;
        Vector2 hinge = new(pivot.x - spineHalfWidth, bottom), openEnd = new(pivot.x + spineHalfWidth, bottom);
        float chord = Mathf.Lerp(2f * spineHalfWidth, 2f * coverDepth, angle / 180f);
        Vector2 end = hinge + Rotate(Vector2.right, turn * 0.5f) * chord;
        float radius = turn > 1e-4f ? chord / (2f * Mathf.Sin(turn * 0.5f)) : 0f;

        Vector2 pivotNow = end + Rotate(new Vector2(pivot.x, pivot.z) - openEnd, turn);
        foldingHalf.SetLocalPositionAndRotation(new Vector3(pivotNow.x, pivot.y, pivotNow.y), Turn(angle));

        // Where a point of the open book's cross-section is now, and how far it has turned
        Vector2 Place(Vector2 p, out float turned)
        {
            if (p.x <= hinge.x) { turned = 0f; return p; }
            if (p.x >= openEnd.x) { turned = turn; return end + Rotate(p - openEnd, turn); }
            float along = (p.x - hinge.x) / (openEnd.x - hinge.x);
            turned = turn * along;
            Vector2 onArc = radius > 0f ? hinge + radius * new Vector2(Mathf.Sin(turned), 1f - Mathf.Cos(turned)) : hinge + new Vector2(chord * along, 0f);
            return onArc + Rotate(new Vector2(0f, p.y - bottom), turned);
        }

        // Each half's points are placed in the book, then put back in the half's own space as it's turned now
        void BendHalf(Half half, Vector3 at, float halfTurn)
        {
            if (half == null) return;
            Quaternion back = Quaternion.Inverse(Turn(halfTurn * Mathf.Rad2Deg));
            for (int i = 0; i < half.open.Length; i++)
            {
                Vector3 v = half.open[i] + half.offset;
                Vector2 placed = Place(new Vector2(v.x, v.z), out float turned);
                half.bent[i] = back * (new Vector3(placed.x, v.y, placed.y) - at);
                Quaternion spin = Turn((turned - halfTurn) * Mathf.Rad2Deg);
                if (i < half.bentNormals.Length) half.bentNormals[i] = spin * half.openNormals[i];
                if (i < half.bentTangents.Length)
                {
                    Vector4 t = half.openTangents[i];
                    Vector3 spun = spin * new Vector3(t.x, t.y, t.z);
                    half.bentTangents[i] = new Vector4(spun.x, spun.y, spun.z, t.w);
                }
            }
            half.mesh.vertices = half.bent;
            if (half.bentNormals.Length == half.bent.Length) half.mesh.normals = half.bentNormals;
            if (half.bentTangents.Length == half.bent.Length) half.mesh.tangents = half.bentTangents;
            half.mesh.RecalculateBounds();
        }

        BendHalf(left, Vector3.zero, 0f);
        BendHalf(right, foldingHalf.localPosition, turn);
    }

    // A copy of a mesh with every triangle cut where it crosses each plane x = cut, so it can bend there
    static Mesh SliceAcrossX(Mesh source, List<float> cuts)
    {
        List<Vector3> positions = new(), normals = new();
        List<Vector4> tangents = new();
        List<Vector2> uvs = new();
        source.GetVertices(positions);
        source.GetNormals(normals);
        source.GetTangents(tangents);
        source.GetUVs(0, uvs);
        Dictionary<(int, int, float), int> splits = new();

        // The point where an edge crosses a plane, made once and shared by the triangles on either side
        int Split(int a, int b, float cut)
        {
            var key = a < b ? (a, b, cut) : (b, a, cut);
            if (splits.TryGetValue(key, out int made)) return made;
            float t = (cut - positions[a].x) / (positions[b].x - positions[a].x);
            positions.Add(Vector3.Lerp(positions[a], positions[b], t));
            if (normals.Count > 0) normals.Add(Vector3.Lerp(normals[a], normals[b], t).normalized);
            if (tangents.Count > 0) tangents.Add(Vector4.Lerp(tangents[a], tangents[b], t));
            if (uvs.Count > 0) uvs.Add(Vector2.Lerp(uvs[a], uvs[b], t));
            return splits[key] = positions.Count - 1;
        }

        List<int>[] submeshes = new List<int>[source.subMeshCount];
        for (int s = 0; s < source.subMeshCount; s++)
        {
            List<int> tris = new(source.GetTriangles(s));
            foreach (float cut in cuts)
            {
                List<int> cutTris = new(tris.Count);
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    bool sa = positions[a].x >= cut, sb = positions[b].x >= cut, sc = positions[c].x >= cut;
                    if (sa == sb && sb == sc)
                    {
                        cutTris.Add(a); cutTris.Add(b); cutTris.Add(c);
                        continue;
                    }
                    // Turned so the corner alone on its side comes first, keeping the winding
                    if (sb != sa && sb != sc) (a, b, c) = (b, c, a);
                    else if (sc != sa && sc != sb) (a, b, c) = (c, a, b);
                    int ab = Split(a, b, cut), ac = Split(a, c, cut);
                    cutTris.AddRange(new[] { a, ab, ac, ab, b, c, ab, c, ac });
                }
                tris = cutTris;
            }
            submeshes[s] = tris;
        }

        Mesh mesh = new() { name = source.name };
        if (positions.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(positions);
        if (normals.Count == positions.Count) mesh.SetNormals(normals);
        if (tangents.Count == positions.Count) mesh.SetTangents(tangents);
        if (uvs.Count == positions.Count) mesh.SetUVs(0, uvs);
        mesh.subMeshCount = submeshes.Length;
        for (int s = 0; s < submeshes.Length; s++) mesh.SetTriangles(submeshes[s], s);
        mesh.RecalculateBounds();
        return mesh;
    }

    public bool ShowsStowed(Transform part) => part.TryGetComponent(out Renderer look) && look.enabled;

    // Each half bent shut, however far it's bent now
    public Mesh StowedMesh(MeshFilter part)
    {
        Mesh now = part.sharedMesh;
        if (left != null && now == left.mesh) return left.shut;
        if (right != null && now == right.mesh) return right.shut;
        return now;
    }

    public Matrix4x4 StowedPose(Transform part)
    {
        Matrix4x4 pose = transform.worldToLocalMatrix * part.localToWorldMatrix;
        if (foldingHalf == null || !part.IsChildOf(foldingHalf)) return pose;
        // The folding half, and anything on it, turned shut about its pivot
        Matrix4x4 now = Matrix4x4.TRS(foldingHalf.localPosition, foldingHalf.localRotation, foldingHalf.localScale);
        Matrix4x4 shut = Matrix4x4.TRS(pivot, Turn(180f), foldingHalf.localScale);
        return shut * now.inverse * pose;
    }
}
