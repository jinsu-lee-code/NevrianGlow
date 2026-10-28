using System;
using UnityEngine;

/// <summary>
/// Terrain layers spawn the grass (Unreal "Landscape Grass Type" style): the detail density of each cell is computed
/// from the painted terrain-layer weights, so painting a layer (e.g. the path) also adds / removes grass.
/// count = Σ_layers remap(weight) × rule density × patch mask, dithered to whole instances.
/// Faint brush edges get fewer instances (weight remap), the bush shape comes from a world-space patch noise.
/// Edge shrinking and the darker ground under grass come from GroundColorBaker's coverage map (it re-bakes when
/// the details change). Painting the terrain texture regenerates that region automatically (auto update);
/// hand-painted details inside the spawn area are overwritten.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Terrain))]
public class LayerGrassSpawner : MonoBehaviour
{
    [Serializable]
    public class LayerRule
    {
        public TerrainLayer layer;
        [Tooltip("Max instances of each detail prototype per detail cell at full layer weight (index = detail prototype)")]
        public float[] density = { 0f, 0f };
        [Tooltip("0 = uniform carpet, 1 = bushes only where the patch noise is high")]
        [Range(0f, 1f)] public float patchiness = 0.7f;
        [Tooltip("Tall prototypes (index >= 1) only grow in the dense middle of patches: mask power")]
        public float tallPatchPower = 2f;
    }

    public LayerRule[] rules = Array.Empty<LayerRule>();

    [Header("Weight remap (thin brush edges -> sparse grass)")]
    [Range(0f, 1f)] public float weightMin = 0.15f;
    [Range(0f, 1f)] public float weightMax = 0.7f;

    [Header("Soft field edges (2026-10-27, review: grass stops like a cut carpet)")]
    [Tooltip("Layer weights are averaged over this radius (m), so the grass thins out over a band instead of a line")]
    public float edgeBlur = 1.5f;
    [Tooltip("Tall blades (prototype 1+) follow weight^this, so a field edge keeps only short clumps")]
    public float tallEdgePower = 2.5f;

    [Header("Patch noise (bush shapes)")]
    public float patchScale = 7f;
    [Range(0f, 1f)] public float patchThreshold = 0.45f;
    [Range(0.01f, 0.5f)] public float patchSoftness = 0.18f;
    public int seed = 7;

    [Header("Spawn area (performance)")]
    [Tooltip("World centre of the grass area")]
    public Vector3 center = Vector3.zero;
    [Tooltip("Radius in metres; 0 = whole terrain")]
    public float radius = 40f;
    [Tooltip("Width of the soft fade at the area border (metres)")]
    public float borderFade = 10f;
    [Range(1, 32)] public int maxPerCell = 16;

    [Header("Obstacles (rocks): bare under them, a denser ring round the foot")]
    [Tooltip("Roots whose meshes block grass (e.g. RocksBG). Each mesh's real foot outline is measured from its vertices")]
    public Transform[] obstacleRoots = Array.Empty<Transform>();
    [Tooltip("Width of the grass ring round each foot (m)")]
    public float ringWidth = 0.9f;
    [Tooltip("Density multiplier at the foot (fades out over the ring)")]
    public float ringBoost = 2f;
    [Tooltip("Extra instances per cell at the foot, so the ring also grows in bare patches (index = detail prototype)")]
    public float[] ringAdd = { 2f, 1f };
    [Tooltip("Grass within this distance of a rock foot leans away from it (m); read by DSEWA/ToonFoliage (_ObstacleLean)")]
    public float leanReach = 1.4f;
    [Tooltip("Obstacle field texel size (m)")]
    public float fieldTexel = 0.25f;

    [Header("Tree shade (no grass under the crowns)")]
    [Tooltip("Roots of the trees (e.g. TreesBG). Each child's crown radius comes from its renderer bounds")]
    public Transform[] shadeRoots = Array.Empty<Transform>();
    [Tooltip("Bare out to this fraction of the crown radius")]
    [Range(0f, 1f)] public float shadeBare = 0.45f;
    [Tooltip("Grass back to full at this fraction of the crown radius")]
    [Range(0f, 1.5f)] public float shadeEdge = 0.95f;

    [Header("Grass rhythm (2026-10-27, layout pass)")]
    [Tooltip("Tall blades (prototype 1+) x (1 + this) in the band where the tree shade fades out, i.e. round the crown edge")]
    public float shadeEdgeTall = 1.6f;
    [Tooltip("Tall blades x (1 + this) along path edges (path layers at partial weight)")]
    public float pathEdgeTall = 1.4f;
    [Tooltip("Path layer indices counted as path for the edge band (terrain layer order)")]
    public int[] pathLayers = { 3, 4 };
    [Tooltip("No grass on the path itself (2026-10-28, user: clear the grass growing on the paths): fades out between " +
             "these path weights (x = starts thinning, y = gone), so the blurred field edge does not creep onto the path")]
    public Vector2 pathClear = new Vector2(0.3f, 0.55f);
    [Tooltip("Ridges: where the ground stands this much (m) above its surroundings (ridgeRadius) the grass is short and sparse")]
    public float ridgeHeight = 0.6f;
    public float ridgeRadius = 6f;
    [Range(0f, 1f)] public float ridgeKeep = 0.55f;
    [Tooltip("Flowers under this root keep a low patch round them so they show above the grass")]
    public Transform flowerRoot;
    public float flowerClear = 0.9f;

    Texture2D obstacleField;
    [Tooltip("Obstacle field baked in the editor and used in play mode / builds, where the rock meshes cannot be read (2026-10-27)")]
    public Texture2D bakedField;
    [HideInInspector] public Vector4 bakedRect;
    const string BakedFieldPath = "Assets/Art/Environment/DeepBlue/tex_env_grass_obstacle_field.asset";

    struct Crown { public Vector2 c; public float r; }

    System.Collections.Generic.List<Crown> BuildCrowns()
    {
        var list = new System.Collections.Generic.List<Crown>();
        if (shadeRoots == null) return list;
        foreach (var root in shadeRoots)
        {
            if (root == null) continue;
            foreach (Transform t in root)                                   // each direct child is one tree
            {
                if (!t.gameObject.activeInHierarchy) continue;
                var rs = t.GetComponentsInChildren<MeshRenderer>();
                if (rs.Length == 0) continue;
                Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                list.Add(new Crown { c = new Vector2(t.position.x, t.position.z), r = Mathf.Max(1f, (b.extents.x + b.extents.z) * 0.5f) });
            }
        }
        return list;
    }
    static readonly int FieldId = Shader.PropertyToID("_DSEWA_ObstacleField");
    static readonly int FieldRectId = Shader.PropertyToID("_DSEWA_ObstacleRect");

    [Header("Update")]
    public bool autoUpdate = true;

    Terrain terrain;

    // foot outline of one obstacle: centre + radius in 24 directions (vertices within FootHeight of the ground)
    struct Foot { public Vector2 c; public float[] r; public float maxR; }
    const int FootBins = 24;
    const float FootHeight = 0.3f;

    System.Collections.Generic.List<Foot> BuildFeet()
    {
        var feet = new System.Collections.Generic.List<Foot>();
        if (obstacleRoots == null) return feet;
        Vector3 tp = terrain.GetPosition();
        foreach (var root in obstacleRoots)
        {
            if (root == null) continue;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf.sharedMesh; var mr = mf.GetComponent<MeshRenderer>();
                if (mesh == null || mr == null || !mr.enabled) continue;
                if (Application.isPlaying && !mesh.isReadable) continue;     // only readable in the editor
                if (mr.sharedMaterial != null && mr.sharedMaterial.shader.name.Contains("Foliage")) continue;   // grass / plants are not obstacles
                Bounds b = mr.bounds;
                var c = new Vector2(b.center.x, b.center.z);
                float ground = terrain.SampleHeight(b.center) + tp.y;
                if (b.min.y > ground + FootHeight) continue;                 // not touching the ground
                var r = new float[FootBins];
                Matrix4x4 m = mf.transform.localToWorldMatrix;
                foreach (var v in mesh.vertices)
                {
                    Vector3 w = m.MultiplyPoint3x4(v);
                    if (w.y > ground + FootHeight) continue;
                    float a = Mathf.Atan2(w.z - c.y, w.x - c.x);
                    int bin = (int)((a + Mathf.PI) / (2f * Mathf.PI) * FootBins) % FootBins;
                    float d = Vector2.Distance(new Vector2(w.x, w.z), c);
                    if (d > r[bin]) r[bin] = d;
                }
                float maxR = 0f, fill = Mathf.Min(b.extents.x, b.extents.z) * 0.7f;
                for (int i = 0; i < FootBins; i++) { if (r[i] <= 0f) r[i] = fill; maxR = Mathf.Max(maxR, r[i]); }
                feet.Add(new Foot { c = c, r = r, maxR = maxR });
            }
        }
        return feet;
    }

    // signed distance from a foot outline (negative inside), with the radius interpolated between the 24 directions
    static float FootDistance(in Foot f, float wx, float wz)
    {
        float dx = wx - f.c.x, dz = wz - f.c.y;
        float d = Mathf.Sqrt(dx * dx + dz * dz);
        float t = (Mathf.Atan2(dz, dx) + Mathf.PI) / (2f * Mathf.PI) * FootBins - 0.5f;
        int i0 = ((int)Mathf.Floor(t) % FootBins + FootBins) % FootBins, i1 = (i0 + 1) % FootBins;
        float rad = Mathf.Lerp(f.r[i0], f.r[i1], t - Mathf.Floor(t));
        return d - rad;
    }

    void OnEnable()
    {
        terrain = GetComponent<Terrain>();
        TerrainCallbacks.textureChanged += OnTextureChanged;
        if (Application.isPlaying && bakedField != null)
        {
            // play mode / build: the rock meshes are not readable there, use the field baked in the editor
            Shader.SetGlobalTexture(FieldId, bakedField);
            Shader.SetGlobalVector(FieldRectId, bakedRect);
            return;
        }
        BakeObstacleField(BuildFeet());
    }

    void OnDisable()
    {
        TerrainCallbacks.textureChanged -= OnTextureChanged;
        Shader.SetGlobalVector(FieldRectId, Vector4.zero);
        if (obstacleField != null) { if (Application.isPlaying) Destroy(obstacleField); else DestroyImmediate(obstacleField); obstacleField = null; }
    }

    // editor: keep a copy of the field as an asset for play mode / builds (written only when it changed)
    void SaveBakedField(Color32[] px, int w, int h, Vector4 rect)
    {
#if UNITY_EDITOR
        if (Application.isPlaying) return;
        var tex = bakedField != null ? bakedField : UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(BakedFieldPath);
        if (tex != null && tex.width == w && tex.height == h && bakedRect == rect)
        {
            var old = tex.GetPixels32();
            bool same = old.Length == px.Length;
            for (int i = 0; same && i < px.Length; i++) same = old[i].r == px[i].r && old[i].g == px[i].g && old[i].b == px[i].b;
            if (same) { if (bakedField != tex) { bakedField = tex; UnityEditor.EditorUtility.SetDirty(this); } return; }
        }
        if (tex == null || tex.width != w || tex.height != h)
        {
            var t2 = new Texture2D(w, h, TextureFormat.RGBA32, false, true) { name = "tex_env_grass_obstacle_field", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t2.SetPixels32(px); t2.Apply(false, false);
            if (tex == null) UnityEditor.AssetDatabase.CreateAsset(t2, BakedFieldPath);
            else { UnityEditor.EditorUtility.CopySerialized(t2, tex); DestroyImmediate(t2); }
            tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(BakedFieldPath);
        }
        else { tex.SetPixels32(px); tex.Apply(false, false); }
        UnityEditor.EditorUtility.SetDirty(tex);
        bakedField = tex; bakedRect = rect;
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    // Per texel over the grass area: direction away from the nearest rock foot (rg) and closeness (b: 1 at / inside the
    // foot, 0 at leanReach). Feet are bucketed on an 8 m grid so each texel only checks its neighbours.
    void BakeObstacleField(System.Collections.Generic.List<Foot> feet)
    {
        if (terrain == null || terrain.terrainData == null) return;
        if (feet == null || feet.Count == 0 || leanReach <= 0f) { Shader.SetGlobalVector(FieldRectId, Vector4.zero); return; }
        TerrainData td = terrain.terrainData; Vector3 tp = terrain.GetPosition();
        float minX = tp.x, minZ = tp.z, sizeX = td.size.x, sizeZ = td.size.z;
        if (radius > 0f)
        {
            float rr = radius + borderFade;
            minX = Mathf.Max(tp.x, center.x - rr); minZ = Mathf.Max(tp.z, center.z - rr);
            sizeX = Mathf.Min(tp.x + td.size.x, center.x + rr) - minX; sizeZ = Mathf.Min(tp.z + td.size.z, center.z + rr) - minZ;
        }
        int w = Mathf.Clamp(Mathf.CeilToInt(sizeX / Mathf.Max(fieldTexel, 0.05f)), 8, 2048);
        int h = Mathf.Clamp(Mathf.CeilToInt(sizeZ / Mathf.Max(fieldTexel, 0.05f)), 8, 2048);
        const float Bucket = 8f;
        int bw = Mathf.CeilToInt(sizeX / Bucket) + 1, bh = Mathf.CeilToInt(sizeZ / Bucket) + 1;
        var buckets = new System.Collections.Generic.List<int>[bw * bh];
        for (int i = 0; i < feet.Count; i++)
        {
            float reach = feet[i].maxR + leanReach;
            int bx0 = Mathf.Max(0, (int)((feet[i].c.x - reach - minX) / Bucket)), bx1 = Mathf.Min(bw - 1, (int)((feet[i].c.x + reach - minX) / Bucket));
            int by0 = Mathf.Max(0, (int)((feet[i].c.y - reach - minZ) / Bucket)), by1 = Mathf.Min(bh - 1, (int)((feet[i].c.y + reach - minZ) / Bucket));
            for (int by = by0; by <= by1; by++) for (int bx = bx0; bx <= bx1; bx++)
                (buckets[by * bw + bx] ??= new System.Collections.Generic.List<int>()).Add(i);
        }
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float wx = minX + (x + 0.5f) * sizeX / w, wz = minZ + (y + 0.5f) * sizeZ / h;
            var list = buckets[Mathf.Min(bh - 1, (int)((wz - minZ) / Bucket)) * bw + Mathf.Min(bw - 1, (int)((wx - minX) / Bucket))];
            float best = float.MaxValue; Vector2 dir = Vector2.zero;
            if (list != null)
                foreach (int i in list)
                {
                    float sd = FootDistance(feet[i], wx, wz);
                    if (sd < best) { best = sd; dir = new Vector2(wx - feet[i].c.x, wz - feet[i].c.y); }
                }
            float near = best == float.MaxValue ? 0f : 1f - Mathf.Clamp01(best / leanReach);
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.zero;
            px[y * w + x] = new Color32((byte)(dir.x * 127.5f + 127.5f), (byte)(dir.y * 127.5f + 127.5f), (byte)(near * 255f), 255);
        }
        if (obstacleField == null || obstacleField.width != w || obstacleField.height != h)
        {
            if (obstacleField != null) { if (Application.isPlaying) Destroy(obstacleField); else DestroyImmediate(obstacleField); }
            obstacleField = new Texture2D(w, h, TextureFormat.RGBA32, false, true)
            { name = "_DSEWA_ObstacleField", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        }
        obstacleField.SetPixels32(px); obstacleField.Apply(false, false);
        Shader.SetGlobalTexture(FieldId, obstacleField);
        Shader.SetGlobalVector(FieldRectId, new Vector4(minX, minZ, 1f / sizeX, 1f / sizeZ));
        SaveBakedField(px, w, h, new Vector4(minX, minZ, 1f / sizeX, 1f / sizeZ));
    }

    void OnTextureChanged(Terrain t, string textureName, RectInt region, bool synced)
    {
        if (!autoUpdate || t != terrain || textureName != TerrainData.AlphamapTextureName) return;
        // alphamap region -> detail region (+1 cell margin)
        TerrainData td = t.terrainData;
        float s = (float)td.detailResolution / td.alphamapResolution;
        var r = new RectInt(Mathf.FloorToInt(region.xMin * s) - 1, Mathf.FloorToInt(region.yMin * s) - 1,
                            Mathf.CeilToInt(region.width * s) + 3, Mathf.CeilToInt(region.height * s) + 3);
        Generate(r);
    }

    [ContextMenu("Regenerate All")]
    public void GenerateAll()
    {
        if (terrain == null) terrain = GetComponent<Terrain>();
        int res = terrain.terrainData.detailResolution;
        Generate(new RectInt(0, 0, res, res));
    }

    public void Generate(RectInt region)
    {
        if (terrain == null) terrain = GetComponent<Terrain>();
        TerrainData td = terrain.terrainData;
        if (td == null || rules == null || rules.Length == 0) return;
        int res = td.detailResolution;
        int protoCount = td.detailPrototypes.Length;
        if (protoCount == 0) return;

        // clip to the terrain and, if set, to the spawn area
        int x0 = Mathf.Max(0, region.xMin), y0 = Mathf.Max(0, region.yMin);
        int x1 = Mathf.Min(res, region.xMax), y1 = Mathf.Min(res, region.yMax);
        Vector3 tp = terrain.GetPosition();
        float cell = td.size.x / res;
        if (radius > 0f)
        {
            float rr = radius + borderFade;
            x0 = Mathf.Max(x0, Mathf.FloorToInt((center.x - rr - tp.x) / cell));
            x1 = Mathf.Min(x1, Mathf.CeilToInt((center.x + rr - tp.x) / cell));
            y0 = Mathf.Max(y0, Mathf.FloorToInt((center.z - rr - tp.z) / cell));
            y1 = Mathf.Min(y1, Mathf.CeilToInt((center.z + rr - tp.z) / cell));
        }
        if (x1 <= x0 || y1 <= y0) return;
        int w = x1 - x0, h = y1 - y0;

        // layer index of each rule
        TerrainLayer[] layers = td.terrainLayers;
        var ruleLayer = new int[rules.Length];
        for (int r = 0; r < rules.Length; r++) ruleLayer[r] = rules[r].layer == null ? -1 : Array.IndexOf(layers, rules[r].layer);

        // alphamap block covering the region
        float a = (float)td.alphamapResolution / res;
        // (with a margin of edgeBlur so the averaging window is complete at the region border)
        int margin = Mathf.CeilToInt(Mathf.Max(0f, edgeBlur) / (td.size.x / td.alphamapWidth));
        int ax0 = Mathf.Clamp(Mathf.FloorToInt(x0 * a) - margin, 0, td.alphamapWidth - 1), ay0 = Mathf.Clamp(Mathf.FloorToInt(y0 * a) - margin, 0, td.alphamapHeight - 1);
        int ax1 = Mathf.Clamp(Mathf.CeilToInt(x1 * a) + margin, ax0 + 1, td.alphamapWidth), ay1 = Mathf.Clamp(Mathf.CeilToInt(y1 * a) + margin, ay0 + 1, td.alphamapHeight);
        float[,,] alpha = td.GetAlphamaps(ax0, ay0, ax1 - ax0, ay1 - ay0);
        int aw = ax1 - ax0, ah = ay1 - ay0;
        // summed-area table per rule layer for the blurred weights
        var sat = new float[rules.Length][,];
        for (int r = 0; r < rules.Length; r++)
        {
            int li = ruleLayer[r];
            if (li < 0 || li >= td.alphamapLayers) continue;
            var s = new float[ah + 1, aw + 1];
            for (int yy = 0; yy < ah; yy++)
            {
                float row = 0f;
                for (int xx = 0; xx < aw; xx++) { row += alpha[yy, xx, li]; s[yy + 1, xx + 1] = s[yy, xx + 1] + row; }
            }
            sat[r] = s;
        }

        var outLayers = new int[protoCount][,];
        for (int p = 0; p < protoCount; p++) outLayers[p] = new int[h, w];
        float ox = seed * 17.13f, oz = seed * 31.71f;
        var feet = BuildFeet();
        // tree crowns bucketed on a 16 m grid over the region
        var crowns = BuildCrowns();
        const float CB = 16f;
        float rminX = tp.x + x0 * cell, rminZ = tp.z + y0 * cell;
        int cbw = Mathf.CeilToInt(w * cell / CB) + 1, cbh = Mathf.CeilToInt(h * cell / CB) + 1;
        var crownBuckets = new System.Collections.Generic.List<int>[cbw * cbh];
        for (int i = 0; i < crowns.Count; i++)
        {
            float rr = crowns[i].r * shadeEdge;
            int bx0 = Mathf.Max(0, (int)((crowns[i].c.x - rr - rminX) / CB)), bx1 = Mathf.Min(cbw - 1, (int)((crowns[i].c.x + rr - rminX) / CB));
            int by0 = Mathf.Max(0, (int)((crowns[i].c.y - rr - rminZ) / CB)), by1 = Mathf.Min(cbh - 1, (int)((crowns[i].c.y + rr - rminZ) / CB));
            for (int by = by0; by <= by1; by++) for (int bx = bx0; bx <= bx1; bx++)
                (crownBuckets[by * cbw + bx] ??= new System.Collections.Generic.List<int>()).Add(i);
        }
        // flowers bucketed on the same grid
        System.Collections.Generic.List<Vector2> flowerPts = null;
        var flowerBuckets = new System.Collections.Generic.List<int>[cbw * cbh];
        if (flowerRoot != null)
        {
            flowerPts = new System.Collections.Generic.List<Vector2>();
            foreach (Transform f in flowerRoot) if (f.gameObject.activeInHierarchy) flowerPts.Add(new Vector2(f.position.x, f.position.z));
            for (int i = 0; i < flowerPts.Count; i++)
            {
                int bx0 = Mathf.Max(0, (int)((flowerPts[i].x - flowerClear - rminX) / CB)), bx1 = Mathf.Min(cbw - 1, (int)((flowerPts[i].x + flowerClear - rminX) / CB));
                int by0 = Mathf.Max(0, (int)((flowerPts[i].y - flowerClear - rminZ) / CB)), by1 = Mathf.Min(cbh - 1, (int)((flowerPts[i].y + flowerClear - rminZ) / CB));
                for (int by = by0; by <= by1; by++) for (int bx = bx0; bx <= bx1; bx++)
                    (flowerBuckets[by * cbw + bx] ??= new System.Collections.Generic.List<int>()).Add(i);
            }
        }

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int dx = x0 + x, dy = y0 + y;
            float wx = tp.x + (dx + 0.5f) * cell, wz = tp.z + (dy + 0.5f) * cell;

            float area = 1f;
            if (radius > 0f)
            {
                float d = Vector2.Distance(new Vector2(wx, wz), new Vector2(center.x, center.z));
                area = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(radius, radius + borderFade, d));
                if (area <= 0f) continue;
            }

            // average the alphamap texels that fall in this detail cell
            int sx = Mathf.Clamp(Mathf.FloorToInt(dx * a) - ax0 - margin, 0, aw - 1), sy = Mathf.Clamp(Mathf.FloorToInt(dy * a) - ay0 - margin, 0, ah - 1);
            int ex = Mathf.Clamp(Mathf.CeilToInt((dx + 1) * a) - ax0 + margin, sx + 1, aw), ey = Mathf.Clamp(Mathf.CeilToInt((dy + 1) * a) - ay0 + margin, sy + 1, ah);

            float n = Mathf.PerlinNoise(wx / patchScale + ox, wz / patchScale + oz) * 0.7f
                    + Mathf.PerlinNoise(wx / (patchScale * 0.37f) + oz, wz / (patchScale * 0.37f) + ox) * 0.3f;
            float patch = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(patchThreshold - patchSoftness, patchThreshold + patchSoftness, n));

            // obstacles (2026-10-26, user: grass at the foot of the rocks, done by the terrain grass instead of hand-placed
            // clumps): nothing grows under a rock, and a ring round its foot gets denser, fading out over ringWidth
            bool bare = false; float ring = 0f;
            foreach (var f in feet)
            {
                if (Mathf.Abs(wx - f.c.x) > f.maxR + ringWidth || Mathf.Abs(wz - f.c.y) > f.maxR + ringWidth) continue;
                float sd = FootDistance(f, wx, wz);
                if (sd < -cell * 0.5f) { bare = true; break; }
                if (sd < ringWidth) ring = Mathf.Max(ring, 1f - Mathf.Max(sd, 0f) / ringWidth);
            }
            if (bare) continue;

            // tree shade (2026-10-26, user: no grass round the trees, they get no sun): bare near the trunk, back to full
            // grass at the crown edge
            float shade = 1f;
            var cl = crownBuckets[Mathf.Clamp((int)((wz - rminZ) / CB), 0, cbh - 1) * cbw + Mathf.Clamp((int)((wx - rminX) / CB), 0, cbw - 1)];
            if (cl != null)
                foreach (int i in cl)
                {
                    float d = Vector2.Distance(new Vector2(wx, wz), crowns[i].c);
                    shade = Mathf.Min(shade, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(crowns[i].r * shadeBare, crowns[i].r * shadeEdge, d)));
                }
            if (shade <= 0.001f) continue;

            // rhythm: tall band round crown edges and path edges, short sparse grass on ridges, low patch round flowers
            float shadeBand = shade < 0.999f ? 4f * shade * (1f - shade) : 0f;
            float pathW = 0f;
            {
                int cx = Mathf.Clamp(Mathf.FloorToInt((dx + 0.5f) * a) - ax0, 0, aw - 1), cy = Mathf.Clamp(Mathf.FloorToInt((dy + 0.5f) * a) - ay0, 0, ah - 1);
                foreach (int li in pathLayers) if (li >= 0 && li < td.alphamapLayers) pathW += alpha[cy, cx, li];
            }
            float pathBand = Mathf.Clamp01(pathW * 4f) * Mathf.Clamp01((0.6f - pathW) * 4f);
            float ridge = 0f;
            if (ridgeHeight > 0f)
            {
                float h0 = terrain.SampleHeight(new Vector3(wx, 0, wz));
                float hs = (terrain.SampleHeight(new Vector3(wx + ridgeRadius, 0, wz)) + terrain.SampleHeight(new Vector3(wx - ridgeRadius, 0, wz))
                          + terrain.SampleHeight(new Vector3(wx, 0, wz + ridgeRadius)) + terrain.SampleHeight(new Vector3(wx, 0, wz - ridgeRadius))) * 0.25f;
                ridge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ridgeHeight * 0.5f, ridgeHeight * 1.5f, h0 - hs));
            }
            float flower = 0f;
            if (flowerPts != null)
            {
                var fl = flowerBuckets[Mathf.Clamp((int)((wz - rminZ) / CB), 0, cbh - 1) * cbw + Mathf.Clamp((int)((wx - rminX) / CB), 0, cbw - 1)];
                if (fl != null) foreach (int i in fl) { float d = Vector2.Distance(new Vector2(wx, wz), flowerPts[i]); flower = Mathf.Max(flower, 1f - Mathf.SmoothStep(0f, 1f, d / flowerClear)); }
            }

            for (int p = 0; p < protoCount; p++)
            {
                float count = 0f;
                for (int r = 0; r < rules.Length; r++)
                {
                    int li = ruleLayer[r];
                    if (li < 0 || li >= td.alphamapLayers) continue;
                    LayerRule rule = rules[r];
                    float dens = p < rule.density.Length ? rule.density[p] : 0f;
                    if (dens <= 0f) continue;
                    var s = sat[r];
                    float wsum = s[ey, ex] - s[sy, ex] - s[ey, sx] + s[sy, sx]; int nn = (ex - sx) * (ey - sy);
                    float weight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(weightMin, weightMax, wsum / nn));
                    float mask = Mathf.Lerp(1f, patch, rule.patchiness);
                    if (p > 0) { mask = Mathf.Pow(mask, rule.tallPatchPower); weight = Mathf.Pow(weight, tallEdgePower); }
                    count += weight * dens * mask;
                }
                if (ring > 0f) count = count * (1f + ringBoost * ring) + (p < ringAdd.Length ? ringAdd[p] : 0f) * ring;
                count *= area * shade;
                if (pathClear.y > pathClear.x) count *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(pathClear.x, pathClear.y, pathW));
                if (p > 0) count *= (1f + shadeEdgeTall * shadeBand + pathEdgeTall * pathBand) * (1f - ridge) * (1f - flower);
                else count *= Mathf.Lerp(1f, ridgeKeep, ridge) * (1f - 0.6f * flower);
                // dither the fraction with a stable per-cell hash so regeneration is deterministic
                float hash = Frac(Mathf.Sin((dx * 12.9898f + dy * 78.233f + p * 37.719f + seed) ) * 43758.5453f);
                outLayers[p][y, x] = Mathf.Clamp(Mathf.FloorToInt(count + hash), 0, maxPerCell);
            }
        }

        for (int p = 0; p < protoCount; p++) td.SetDetailLayer(x0, y0, p, outLayers[p]);
        BakeObstacleField(feet);
    }

    static float Frac(float v) => v - Mathf.Floor(v);
}
