using UnityEngine;

/// <summary>
/// One toon cloud (2026-10-30): a quad that DSEWA/ToonCloudVolume turns to face the camera and ray-marches into a
/// cloud (see the shader). Presets are materials (mat_env_sky_cloud_toon_*). The object's scale is the cloud's radii (x, y, z in metres); its position
/// is the centre. The mesh is built at load and never saved - only these numbers live in the scene. All numbers mine.
/// Cost (2026-10-30, measured: one cloud over 35 % of a 1080p screen took +60 ms when the shape was computed per
/// pixel per step): the shape is baked once per material into a 96^3 3D texture (cloud field + big-lump field) and the
/// shader only reads it. The bake reruns when the material's values change (CRC check). Drift needs the live path.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ToonCloud : MonoBehaviour
{
    [Tooltip("Quads (the shader ray-marches from one; keep 1)")] [Range(1, 4)] public int slices = 1;

    // per-cloud shape (2026-10-30, user: "various shapes"): -1 keeps the material's value. Each distinct combination is
    // baked once (clouds with the same material and values share the texture).
    [Header("Shape (-1 = use the material)")]
    [Tooltip("0..100")] public float seed = -1;
    [Tooltip("5..16")] public int lumpCount = -1;
    [Tooltip("0 wide and low .. 1 tall")] public float tower = -1;
    [Tooltip("0..1, height of the flat underside")] public float baseFlat = -1;
    [Tooltip("0 flat underside .. 1 lumpy all round")] public float baseRound = -1;
    [Tooltip("0.05..0.3 of the radius")] public float puffSize = -1;
    [Tooltip("0..1")] public float puffAmount = -1;
    [Tooltip("0..1, 1 = puffs only on top")] public float puffTopBias = -1;

    // motion (2026-10-30, user: "make it deform"): Play only, so the transform stays editable in the editor. The shape
    // itself wobbles in the shader (material Wobble values) in the editor too.
    [Header("Ground shadow")]
    [Tooltip("This cloud casts a shadow on the ground (up to 4 clouds)")] public bool castShadow = true;
    [Tooltip("Shadow strength on the ground (0..1)")] [Range(0f, 1f)] public float shadowStrength = 0.35f;

    [Header("Motion (Play only)")]
    [Tooltip("Travels round the map centre at this speed (m/s, + = counter-clockwise seen from above, 0 = stays). Orbiting keeps far clouds far: no wrap-around pop")]
    public float orbitSpeed = 2.5f;
    [Tooltip("Centre of the orbit (world x, z)")] public Vector2 orbitCentre = Vector2.zero;
    [Tooltip("Preview the motion in the editor too (the transform then moves while you edit)")] public bool previewInEditor = false;
    [Tooltip("Sways back and forth along this world direction (m each way), on top of the orbit")] public Vector3 driftRange = new Vector3(0f, 4f, 0f);
    [Tooltip("Seconds for one sway there and back")] public float driftPeriod = 90f;
    [Tooltip("Breathing: scale grows and shrinks by this fraction")] [Range(0f, 0.2f)] public float breathe = 0.04f;
    [Tooltip("Seconds per breath")] public float breathePeriod = 25f;

    Mesh mesh;
    MaterialPropertyBlock block;
    Vector3 restPos, restScale;
    bool haveRest;

    const int BakeRes = 96;
    static readonly System.Collections.Generic.Dictionary<string, (int crc, RenderTexture rt)> baked =
        new System.Collections.Generic.Dictionary<string, (int, RenderTexture)>();
    static readonly int BakedId = Shader.PropertyToID("_BakedField"), UseBakedId = Shader.PropertyToID("_UseBaked"),
        SliceId = Shader.PropertyToID("_BakeSlice"), ResId = Shader.PropertyToID("_BakeRes");

    // every enabled cloud, published to shaders as _DSEWA_Clouds (centre, radius) so AerialPerspective leaves them alone
    static readonly System.Collections.Generic.List<ToonCloud> all = new System.Collections.Generic.List<ToonCloud>();
    static readonly Vector4[] packed = new Vector4[16];
    static readonly int CloudsId = Shader.PropertyToID("_DSEWA_Clouds"), CloudCountId = Shader.PropertyToID("_DSEWA_CloudCount");

    void OnEnable() { Build(); if (!all.Contains(this)) all.Add(this); Publish(); ApplyBake(); }
    void OnDisable() { all.Remove(this); Publish(); }
    float editorStart;
    void Update()
    {
        bool live = Application.isPlaying || previewInEditor;
        if (!live) { if (haveRest) { transform.position = restPos; transform.localScale = restScale; haveRest = false; } return; }
        if (!haveRest) { restPos = transform.position; restScale = transform.localScale; haveRest = true; editorStart = Time.realtimeSinceStartup; }
        float ph = Mathf.Abs(GetInstanceID() % 997) * 0.37f;   // clouds out of step with each other
        float t = Application.isPlaying ? Time.time : Time.realtimeSinceStartup - editorStart;
        // orbit: turn the rest position round the centre (same wind for every cloud)
        Vector3 c = new Vector3(orbitCentre.x, 0f, orbitCentre.y);
        Vector3 rel = restPos - c;
        float radius = new Vector2(rel.x, rel.z).magnitude;
        Vector3 pos = restPos;
        if (orbitSpeed != 0f && radius > 1f)
        {
            float ang = orbitSpeed * t / radius;
            pos = c + Quaternion.Euler(0f, -ang * Mathf.Rad2Deg, 0f) * rel;
        }
        transform.position = pos + driftRange * Mathf.Sin((t / Mathf.Max(driftPeriod, 1f) + ph) * Mathf.PI * 2f);
        transform.localScale = restScale * (1f + breathe * Mathf.Sin((t / Mathf.Max(breathePeriod, 1f) + ph * 1.3f) * Mathf.PI * 2f));
    }

    void LateUpdate()
    {
        if (transform.hasChanged) { transform.hasChanged = false; Publish(); }
        ApplyBake();
        if (all.Count > 0 && all[0] == this) PublishShadows();   // once per frame, by the first cloud
    }

    // ---- ground shadows: each casting cloud bakes its silhouette seen from the sun (128^2), rebaked when the sun,
    // the shape or the scale changes; the shaders project it along the light (CloudShadow.hlsl)
    const int ShadowRes = 128;
    RenderTexture shadowRT;
    int shadowKey;
    static readonly int[] ShadowMapIds = { Shader.PropertyToID("_DSEWA_CloudShadowMap0"), Shader.PropertyToID("_DSEWA_CloudShadowMap1"),
        Shader.PropertyToID("_DSEWA_CloudShadowMap2"), Shader.PropertyToID("_DSEWA_CloudShadowMap3") };
    static readonly Vector4[] shadowCentres = new Vector4[4];

    static void PublishShadows()
    {
        var sunLight = RenderSettings.sun;
        if (sunLight == null) foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional && l.isActiveAndEnabled) { sunLight = l; break; }
        Vector3 L = sunLight != null ? -sunLight.transform.forward : Vector3.up;
        Vector3 U = Vector3.Cross(L, Vector3.up); if (U.sqrMagnitude < 1e-4f) U = Vector3.right; U.Normalize();
        Vector3 V = Vector3.Cross(U, L).normalized;
        int n = 0; float strength = 0;
        foreach (var c in all)
        {
            if (c == null || !c.castShadow || n >= 4) continue;
            var rt = c.BakeShadow(L, U, V);
            if (rt == null) continue;
            Shader.SetGlobalTexture(ShadowMapIds[n], rt);
            Vector3 s = c.transform.lossyScale;
            shadowCentres[n] = new Vector4(c.transform.position.x, c.transform.position.y, c.transform.position.z, Mathf.Max(s.x, Mathf.Max(s.y, s.z)) * 1.4f);
            strength = Mathf.Max(strength, c.shadowStrength);
            n++;
        }
        Shader.SetGlobalVectorArray("_DSEWA_CloudShadowCentre", shadowCentres);
        Shader.SetGlobalVector("_DSEWA_CloudShadowU", U);
        Shader.SetGlobalVector("_DSEWA_CloudShadowV", V);
        Shader.SetGlobalVector("_DSEWA_CloudShadowL", L);
        Shader.SetGlobalVector("_DSEWA_CloudShadowParams", new Vector4(n, strength, 0, 0));
    }

    RenderTexture BakeShadow(Vector3 L, Vector3 U, Vector3 V)
    {
        var mr = GetComponent<MeshRenderer>();
        var mat = mr != null ? mr.sharedMaterial : null;
        if (mat == null) return null;
        var field = GetBaked(mat, this);
        if (field == null) return null;
        Vector3 s = transform.lossyScale;
        Vector3 ks = haveRest ? restScale : transform.localScale;   // breathing must not rebake every frame
        int key = (L.ToString("F3") + ks.ToString("F0") + transform.eulerAngles.y.ToString("F0") + OverrideKey() + mat.ComputeCRC()).GetHashCode();
        if (shadowRT != null && shadowRT.IsCreated() && key == shadowKey) return shadowRT;
        int pass = mat.FindPass("BakeShadow");
        if (pass < 0) return null;
        if (shadowRT == null || !shadowRT.IsCreated())
        {
            shadowRT = new RenderTexture(ShadowRes, ShadowRes, 0, RenderTextureFormat.R8)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave, name = "ToonCloudShadow_" + name };
            shadowRT.Create();
        }
        var m = new Material(mat) { hideFlags = HideFlags.HideAndDontSave };
        m.SetTexture(BakedId, field); m.SetFloat(UseBakedId, 1f);
        if (seed >= 0) m.SetFloat("_Seed", seed);
        m.SetVector("_ShadowCentre", new Vector4(transform.position.x, transform.position.y, transform.position.z, Mathf.Max(s.x, Mathf.Max(s.y, s.z)) * 1.4f));
        m.SetVector("_ShadowU", U); m.SetVector("_ShadowV", V); m.SetVector("_ShadowL", L);
        m.SetFloat("_ShadowRes", ShadowRes);
        m.SetVector("_ShadowRadii", new Vector4(s.x, s.y, s.z, 0));
        m.SetMatrix("_ShadowWorldToObject", transform.worldToLocalMatrix);
        var prev = RenderTexture.active;
        Graphics.SetRenderTarget(shadowRT);
        GL.PushMatrix(); GL.LoadOrtho();
        m.SetPass(pass);
        GL.Begin(GL.QUADS); GL.Vertex3(0, 0, 0); GL.Vertex3(1, 0, 0); GL.Vertex3(1, 1, 0); GL.Vertex3(0, 1, 0); GL.End();
        GL.PopMatrix();
        RenderTexture.active = prev;
        if (Application.isPlaying) Destroy(m); else DestroyImmediate(m);
        shadowKey = key;
        return shadowRT;
    }

    void ApplyBake()
    {
        var mr = GetComponent<MeshRenderer>();
        var mat = mr != null ? mr.sharedMaterial : null;
        if (mat == null) return;
        var rt = GetBaked(mat, this);
        if (block == null) block = new MaterialPropertyBlock();
        mr.GetPropertyBlock(block);
        if (rt != null) { block.SetTexture(BakedId, rt); block.SetFloat(UseBakedId, 1f); }
        else block.SetFloat(UseBakedId, 0f);
        if (seed >= 0) block.SetFloat("_Seed", seed);
        mr.SetPropertyBlock(block);
    }

    string OverrideKey() => $"{seed}|{lumpCount}|{tower}|{baseFlat}|{baseRound}|{puffSize}|{puffAmount}|{puffTopBias}";

    static RenderTexture GetBaked(Material mat, ToonCloud c)
    {
        int crc = mat.ComputeCRC();
        string key = mat.GetInstanceID() + "|" + c.OverrideKey();
        if (baked.TryGetValue(key, out var e) && e.rt != null && e.rt.IsCreated() && e.crc == crc) return e.rt;
        int pass = mat.FindPass("Bake");
        if (pass < 0) return null;
        // a temporary copy carries this cloud's shape values into the bake
        var bakeMat = new Material(mat) { hideFlags = HideFlags.HideAndDontSave };
        if (c.seed >= 0) bakeMat.SetFloat("_Seed", c.seed);
        if (c.lumpCount >= 0) bakeMat.SetFloat("_LumpCount", c.lumpCount);
        if (c.tower >= 0) bakeMat.SetFloat("_Tower", c.tower);
        if (c.baseFlat >= 0) bakeMat.SetFloat("_BaseFlat", c.baseFlat);
        if (c.baseRound >= 0) bakeMat.SetFloat("_BaseRound", c.baseRound);
        if (c.puffSize >= 0) bakeMat.SetFloat("_PuffSize", c.puffSize);
        if (c.puffAmount >= 0) bakeMat.SetFloat("_PuffAmount", c.puffAmount);
        if (c.puffTopBias >= 0) bakeMat.SetFloat("_PuffTopBias", c.puffTopBias);
        var rt = e.rt;
        if (rt == null || !rt.IsCreated())
        {
            rt = new RenderTexture(BakeRes, BakeRes, 0, RenderTextureFormat.RGHalf)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Tex3D, volumeDepth = BakeRes,
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
                name = "ToonCloudField_" + mat.name + "_" + c.name,
            };
            rt.Create();
        }
        var prev = RenderTexture.active;
        Shader.SetGlobalFloat(ResId, BakeRes);
        GL.PushMatrix();
        GL.LoadOrtho();
        for (int z = 0; z < BakeRes; z++)
        {
            Shader.SetGlobalFloat(SliceId, z);
            Graphics.SetRenderTarget(rt, 0, CubemapFace.Unknown, z);
            bakeMat.SetPass(pass);
            GL.Begin(GL.QUADS);
            GL.Vertex3(0, 0, 0); GL.Vertex3(1, 0, 0); GL.Vertex3(1, 1, 0); GL.Vertex3(0, 1, 0);
            GL.End();
        }
        GL.PopMatrix();
        RenderTexture.active = prev;
        if (Application.isPlaying) Destroy(bakeMat); else DestroyImmediate(bakeMat);
        baked[key] = (crc, rt);
        return rt;
    }

    static void Publish()
    {
        int n = 0;
        foreach (var c in all)
        {
            if (c == null || n >= packed.Length) continue;
            Vector3 s = c.transform.lossyScale;
            packed[n++] = new Vector4(c.transform.position.x, c.transform.position.y, c.transform.position.z, Mathf.Max(s.x, Mathf.Max(s.y, s.z)) * 1.4f);
        }
        Shader.SetGlobalVectorArray(CloudsId, packed);
        Shader.SetGlobalFloat(CloudCountId, n);
    }
#if UNITY_EDITOR
    void OnValidate() { UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Build(); }; }
#endif
    void OnDestroy()
    {
        if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        if (shadowRT != null) { shadowRT.Release(); if (Application.isPlaying) Destroy(shadowRT); else DestroyImmediate(shadowRT); }
    }

    void Build()
    {
        int n = Mathf.Max(slices, 1);   // one quad = one ray march per pixel (2 drew it twice)
        var v = new Vector3[n * 4];
        var uv = new Vector3[n * 4];
        var tri = new int[n * 6];
        for (int s = 0; s < n; s++)
        {
            float z = Mathf.Lerp(-1f, 1f, (s + 0.5f) / n);
            int b = s * 4;
            uv[b] = new Vector3(-1, -1, z); uv[b + 1] = new Vector3(1, -1, z);
            uv[b + 2] = new Vector3(1, 1, z); uv[b + 3] = new Vector3(-1, 1, z);
            for (int k = 0; k < 4; k++) v[b + k] = Vector3.zero;   // placed by the shader
            int t = s * 6;
            tri[t] = b; tri[t + 1] = b + 2; tri[t + 2] = b + 1;
            tri[t + 3] = b; tri[t + 4] = b + 3; tri[t + 5] = b + 2;
        }
        if (mesh == null) mesh = new Mesh { name = "ToonCloudSlices", hideFlags = HideFlags.DontSave };
        mesh.Clear();
        mesh.vertices = v;
        mesh.SetUVs(0, uv);
        mesh.triangles = tri;
        // the shader moves the vertices out to the radii: bounds in object space, a little over the unit sphere
        Vector3 sc = transform.lossyScale;
        float ratio = Mathf.Max(sc.x, Mathf.Max(sc.y, sc.z)) / Mathf.Max(Mathf.Min(sc.x, Mathf.Min(sc.y, sc.z)), 0.01f);
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2.6f * ratio);   // the stack spans the largest radius on every axis
        GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }
}
