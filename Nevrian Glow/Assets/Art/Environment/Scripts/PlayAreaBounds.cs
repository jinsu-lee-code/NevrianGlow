using UnityEngine;

/// <summary>
/// Keeps the player inside the farm area (2026-10-29, user: "restrict the player's area"). The corners (world x, z,
/// in order round the area) become a ring of invisible box walls, tall enough that the player's CharacterController
/// cannot step or jump over them; the walls follow the terrain height. Keep Out loops (the lake shore) get the same
/// walls, so the player cannot walk into them. Edit the points in the inspector and the walls rebuild.
/// Nothing renders; only the colliders (built at load, never saved). Points are my own values.
/// </summary>
[ExecuteAlways]
public class PlayAreaBounds : MonoBehaviour
{
    [System.Serializable]
    public class Loop { public Vector2[] points = new Vector2[0]; }

    [Tooltip("Area corners in world x / z, in order round the area")]
    public Vector2[] corners =
    {
        new Vector2(-18f, 24f), new Vector2(12f, 24f), new Vector2(14f, -28f), new Vector2(-20f, -28f)
    };
    [Tooltip("Areas inside that the player cannot enter (e.g. the lake), each a loop of world x / z points")]
    public Loop[] keepOut = new Loop[0];
    [Tooltip("Wall height above the ground (m)")]
    public float height = 6f;
    [Tooltip("Wall thickness (m)")]
    public float thickness = 1f;
    [Tooltip("Length of one wall piece (m) - shorter pieces follow the terrain better")]
    public float pieceLength = 4f;
    [Tooltip("Layer of the walls (Default collides with the player)")]
    public int layer;

    const string WallsName = "PlayAreaWalls";
    int builtHash;

    void OnEnable() => builtHash = 0;

    void Update()
    {
        int h = Hash();
        if (h == builtHash) return;
        builtHash = h;
        Build();
    }

    int Hash()
    {
        int h = 17;
        if (corners != null) foreach (var c in corners) h = h * 31 + c.GetHashCode();
        if (keepOut != null)
            foreach (var l in keepOut)
                if (l != null && l.points != null) foreach (var c in l.points) h = h * 31 + c.GetHashCode();
        h = h * 31 + height.GetHashCode();
        h = h * 31 + thickness.GetHashCode();
        h = h * 31 + pieceLength.GetHashCode();
        return h * 31 + layer;
    }

    void Build()
    {
        var old = transform.Find(WallsName);
        if (old != null) DestroyImmediate(old.gameObject);
        var root = new GameObject(WallsName) { hideFlags = HideFlags.DontSave };   // rebuilt on load, never saved
        root.transform.SetParent(transform, false);
        BuildLoop(root.transform, corners);
        if (keepOut != null)
            foreach (var l in keepOut) if (l != null) BuildLoop(root.transform, l.points);
    }

    void BuildLoop(Transform root, Vector2[] pts)
    {
        if (pts == null || pts.Length < 3) return;
        for (int i = 0; i < pts.Length; i++)
        {
            Vector2 a = pts[i], b = pts[(i + 1) % pts.Length];
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / Mathf.Max(pieceLength, 0.5f)));
            for (int k = 0; k < n; k++)
            {
                Vector2 p0 = Vector2.Lerp(a, b, k / (float)n), p1 = Vector2.Lerp(a, b, (k + 1) / (float)n);
                Vector2 mid = (p0 + p1) * 0.5f, d = p1 - p0;
                var go = new GameObject("Wall") { hideFlags = HideFlags.DontSave, layer = layer };
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(mid.x, Ground(mid) + height * 0.5f - 1f, mid.y);   // 1 m into the ground
                go.transform.rotation = Quaternion.LookRotation(new Vector3(d.x, 0f, d.y));
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(thickness, height + 1f, d.magnitude + thickness);   // overlap at the joints
            }
        }
    }

    static float Ground(Vector2 p)
    {
        var t = Terrain.activeTerrain;
        return t != null ? t.SampleHeight(new Vector3(p.x, 0f, p.y)) + t.GetPosition().y : 0f;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.9f);
        DrawLoop(corners);
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.9f);
        if (keepOut != null) foreach (var l in keepOut) if (l != null) DrawLoop(l.points);
    }

    void DrawLoop(Vector2[] pts)
    {
        if (pts == null || pts.Length < 2) return;
        for (int i = 0; i < pts.Length; i++)
        {
            Vector2 a = pts[i], b = pts[(i + 1) % pts.Length];
            Vector3 A = new Vector3(a.x, Ground(a), a.y), B = new Vector3(b.x, Ground(b), b.y);
            Gizmos.DrawLine(A, B);
            Gizmos.DrawLine(A + Vector3.up * height, B + Vector3.up * height);
            Gizmos.DrawLine(A, A + Vector3.up * height);
        }
    }
}
