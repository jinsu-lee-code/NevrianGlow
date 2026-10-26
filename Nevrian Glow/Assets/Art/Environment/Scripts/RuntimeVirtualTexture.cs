using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Runtime virtual texture of the terrain's colour (2026-10-27, user: "make a real RVT", after GroundColorBaker,
/// the single-rectangle stand-in). Same idea as Unreal's RVT:
///  - the whole terrain is a virtual texture split into pages on a quadtree (level 0 = finest pages)
///  - pages near the camera are wanted at fine levels, far ones at coarse levels
///  - only wanted pages are rendered, each into one slot of a physical tile atlas, and kept in an LRU cache
///  - an indirection texture (one texel per finest page) tells the shader which atlas slot covers that spot
///  - a few pages are rendered per frame (the coarse ones first, so something always covers the ground)
/// The pages store the ground albedo only (the terrain's GroundColorBake pass with _LitColor = white), so the
/// day / night colour change doesn't invalidate them; the lit colour is applied when sampling.
/// Pages are re-rendered when the terrain is painted (editor dirty count) or by Invalidate().
/// Globals for DSEWA/ToonFoliage (RuntimeVirtualTexture.hlsl):
///   _DSEWA_RVT_Atlas, _DSEWA_RVT_Indirection, _DSEWA_RVT_Rect (minX, minZ, 1/sizeX, 1/sizeZ), _DSEWA_RVT_Params
///   (indirection size, slot uv size, slot border uv, 0); plus the ground lit / shadow / ramp globals GroundColorBaker
///   used to publish. All numbers are my own values.
/// </summary>
[ExecuteAlways]
public class RuntimeVirtualTexture : MonoBehaviour
{
    public Terrain terrain;
    [Tooltip("Pages across the whole terrain at the finest level (power of two). 128 over 500 m = 3.9 m pages")]
    public int finestPages = 128;
    [Tooltip("Pixels per page (without the border)")]
    public int tilePixels = 128;
    [Tooltip("Border pixels round each page, so bilinear filtering never reads the neighbouring slot")]
    public int borderPixels = 4;
    [Tooltip("Atlas slots per side (slots = this squared)")]
    public int atlasSlots = 16;
    [Tooltip("A page splits into four finer ones when the camera is closer than its size times this")]
    public float lodFactor = 2.5f;
    [Tooltip("Pages rendered per frame at most")]
    public int pagesPerFrame = 12;
    [Tooltip("Show the atlas in the inspector (debug)")]
    public RenderTexture debugAtlas;

    const string PassName = "GroundColorBake";
    static readonly int AtlasId = Shader.PropertyToID("_DSEWA_RVT_Atlas");
    static readonly int IndirId = Shader.PropertyToID("_DSEWA_RVT_Indirection");
    static readonly int RectId = Shader.PropertyToID("_DSEWA_RVT_Rect");
    static readonly int ParamsId = Shader.PropertyToID("_DSEWA_RVT_Params");
    static readonly int GroundShadowId = Shader.PropertyToID("_DSEWA_GroundShadowColor");
    static readonly int GroundLitId = Shader.PropertyToID("_DSEWA_GroundLitColor");
    static readonly int GroundLightId = Shader.PropertyToID("_DSEWA_GroundLight");
    static readonly int LitColorId = Shader.PropertyToID("_LitColor");
    static readonly int ShadowColorId = Shader.PropertyToID("_ShadowColor");
    static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
    static readonly int SoftnessId = Shader.PropertyToID("_Softness");
    static readonly int AmbientId = Shader.PropertyToID("_AmbientShade");
    static readonly int ControlId = Shader.PropertyToID("_Control");
    static readonly int[] SplatIds = { Shader.PropertyToID("_Splat0"), Shader.PropertyToID("_Splat1"), Shader.PropertyToID("_Splat2"), Shader.PropertyToID("_Splat3") };
    static readonly int[] SplatStIds = { Shader.PropertyToID("_Splat0_ST"), Shader.PropertyToID("_Splat1_ST"), Shader.PropertyToID("_Splat2_ST"), Shader.PropertyToID("_Splat3_ST") };
    static readonly int[] MaskIds = { Shader.PropertyToID("_Mask0"), Shader.PropertyToID("_Mask1"), Shader.PropertyToID("_Mask2"), Shader.PropertyToID("_Mask3") };

    struct Page { public int level, x, y; }
    class Slot { public int index; public long key = -1; public int lastUsed; }

    RenderTexture atlas;
    Texture2D indirection;
    Color[] indirPixels;
    Mesh quad;
    CommandBuffer cmd;
    MaterialPropertyBlock props;
    readonly Dictionary<long, Slot> resident = new Dictionary<long, Slot>();
    readonly List<Slot> slots = new List<Slot>();
    readonly List<Page> wanted = new List<Page>();
    readonly List<Page> missing = new List<Page>();
    int frame, levels, dirtyStamp = int.MinValue;
    bool indirDirty;

    static long Key(int level, int x, int y) => ((long)level << 40) | ((long)x << 20) | (long)y;

    void OnEnable()
    {
#if UNITY_EDITOR
        AssemblyReloadEvents.beforeAssemblyReload += Release;
        EditorApplication.update += EditorTick;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        AssemblyReloadEvents.beforeAssemblyReload -= Release;
        EditorApplication.update -= EditorTick;
#endif
        Release();
    }

#if UNITY_EDITOR
    void EditorTick()
    {
        if (this == null || Application.isPlaying || !isActiveAndEnabled) return;
        Tick();
    }
#endif

    void Update() { if (Application.isPlaying) Tick(); }

    /// <summary>Drops every page; they are rendered again over the next frames.</summary>
    [ContextMenu("Invalidate")]
    public void Invalidate()
    {
        resident.Clear();
        foreach (var s in slots) { s.key = -1; s.lastUsed = int.MinValue; }
        indirDirty = true;
    }

    void Release()
    {
        Shader.SetGlobalVector(RectId, Vector4.zero);
        if (atlas != null) { atlas.Release(); DestroySafe(atlas); atlas = null; }
        DestroySafe(indirection); indirection = null;
        DestroySafe(quad); quad = null;
        cmd?.Release(); cmd = null;
        resident.Clear(); slots.Clear();
        debugAtlas = null;
    }

    static void DestroySafe(UnityEngine.Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
    }

    bool Ensure()
    {
        if (terrain == null) terrain = Terrain.activeTerrain;
        if (terrain == null || terrain.terrainData == null || terrain.materialTemplate == null) return false;
        if (terrain.materialTemplate.FindPass(PassName) < 0) return false;
        finestPages = Mathf.ClosestPowerOfTwo(Mathf.Clamp(finestPages, 8, 1024));
        levels = (int)Mathf.Log(finestPages, 2) + 1;
        int slotPx = tilePixels + borderPixels * 2;
        int atlasPx = slotPx * atlasSlots;
        if (atlas == null || !atlas.IsCreated() || atlas.width != atlasPx)
        {
            if (atlas != null) { atlas.Release(); DestroySafe(atlas); }
            atlas = new RenderTexture(atlasPx, atlasPx, GraphicsFormat.R8G8B8A8_SRGB, GraphicsFormat.None)
            { name = "_DSEWA_RVT_Atlas", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            atlas.Create();
            slots.Clear();
            for (int i = 0; i < atlasSlots * atlasSlots; i++) slots.Add(new Slot { index = i, lastUsed = int.MinValue });
            resident.Clear();
            indirDirty = true;
        }
        if (indirection == null || indirection.width != finestPages)
        {
            DestroySafe(indirection);
            indirection = new Texture2D(finestPages, finestPages, TextureFormat.RGBAFloat, false, true)
            { name = "_DSEWA_RVT_Indirection", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            indirPixels = new Color[finestPages * finestPages];
            indirDirty = true;
        }
        if (quad == null)
        {
            quad = new Mesh { name = "RVT Quad", hideFlags = HideFlags.HideAndDontSave };
            quad.vertices = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1), new Vector3(1, 0, 1) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            quad.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            quad.RecalculateBounds();
        }
        debugAtlas = atlas;
        return true;
    }

    Camera ViewCamera()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && SceneView.lastActiveSceneView != null) return SceneView.lastActiveSceneView.camera;
#endif
        return Camera.main;
    }

    void Tick()
    {
        try
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !Ensure()) { Shader.SetGlobalVector(RectId, Vector4.zero); return; }
            frame++;
            var td = terrain.terrainData;
#if UNITY_EDITOR
            int stamp = EditorUtility.GetDirtyCount(td);
            if (stamp != dirtyStamp) { if (dirtyStamp != int.MinValue) Invalidate(); dirtyStamp = stamp; }
#endif
            SelectPages();
            RenderMissing();
            if (indirDirty) BuildIndirection();
            Publish();
        }
        catch (Exception e)
        {
            Shader.SetGlobalVector(RectId, Vector4.zero);
            Debug.LogWarning("[RuntimeVirtualTexture] " + e.Message, this);
        }
    }

    // quadtree: split while the camera is closer than size * lodFactor
    void SelectPages()
    {
        wanted.Clear();
        var cam = ViewCamera();
        var td = terrain.terrainData; Vector3 p0 = terrain.GetPosition();
        Vector3 c = cam != null ? cam.transform.position : p0 + td.size * 0.5f;
        var stack = new Stack<Page>();
        stack.Push(new Page { level = levels - 1, x = 0, y = 0 });
        while (stack.Count > 0)
        {
            var pg = stack.Pop();
            int across = finestPages >> pg.level;
            float sx = td.size.x / across, sz = td.size.z / across;
            float minX = p0.x + pg.x * sx, minZ = p0.z + pg.y * sz;
            float dx = Mathf.Max(minX - c.x, 0, c.x - (minX + sx)), dz = Mathf.Max(minZ - c.z, 0, c.z - (minZ + sz));
            float dy = Mathf.Max(0, c.y - (terrain.SampleHeight(new Vector3(Mathf.Clamp(c.x, minX, minX + sx), 0, Mathf.Clamp(c.z, minZ, minZ + sz))) + p0.y));
            float dist = Mathf.Sqrt(dx * dx + dz * dz + dy * dy);
            if (pg.level > 0 && dist < Mathf.Max(sx, sz) * lodFactor)
            {
                int l = pg.level - 1;
                for (int j = 0; j < 2; j++) for (int i = 0; i < 2; i++) stack.Push(new Page { level = l, x = pg.x * 2 + i, y = pg.y * 2 + j });
            }
            else wanted.Add(pg);
        }
        // mark wanted pages and all their ancestors as used (ancestors cover them until they are rendered)
        missing.Clear();
        var seen = new HashSet<long>();
        foreach (var pg in wanted)
        {
            for (int l = pg.level, x = pg.x, y = pg.y; l < levels; l++, x >>= 1, y >>= 1)
            {
                long k = Key(l, x, y);
                if (!seen.Add(k)) break;
                if (resident.TryGetValue(k, out var s)) s.lastUsed = frame;
                else missing.Add(new Page { level = l, x = x, y = y });
            }
        }
        missing.Sort((a, b) => b.level.CompareTo(a.level));   // coarse first
    }

    void RenderMissing()
    {
        if (missing.Count == 0) return;
        var mat = terrain.materialTemplate; int pass = mat.FindPass(PassName);
        var td = terrain.terrainData; Vector3 p0 = terrain.GetPosition();
        cmd ??= new CommandBuffer { name = "DSEWA RVT pages" };
        cmd.Clear();
        cmd.SetRenderTarget(atlas);
        SetTerrainProps(td);
        int slotPx = tilePixels + borderPixels * 2;
        int n = Mathf.Min(pagesPerFrame, missing.Count);
        for (int m = 0; m < n; m++)
        {
            var pg = missing[m];
            Slot s = null;
            foreach (var cand in slots) if (cand.lastUsed != frame && (s == null || cand.lastUsed < s.lastUsed)) s = cand;   // least recently used
            if (s == null) break;   // atlas full of pages wanted this frame
            if (s.key >= 0) resident.Remove(s.key);
            s.key = Key(pg.level, pg.x, pg.y); s.lastUsed = frame; resident[s.key] = s;

            int across = finestPages >> pg.level;
            float sx = td.size.x / across, sz = td.size.z / across;
            float bx = sx * borderPixels / tilePixels, bz = sz * borderPixels / tilePixels;
            Vector3 centre = new Vector3(p0.x + (pg.x + 0.5f) * sx, p0.y, p0.z + (pg.y + 0.5f) * sz);
            Matrix4x4 view = Matrix4x4.TRS(centre + Vector3.up * 500f, Quaternion.Euler(90f, 0f, 0f), new Vector3(1f, 1f, -1f)).inverse;
            Matrix4x4 proj = Matrix4x4.Ortho(-sx * 0.5f - bx, sx * 0.5f + bx, -sz * 0.5f - bz, sz * 0.5f + bz, 0.01f, 1000f);
            int ix = s.index % atlasSlots, iy = s.index / atlasSlots;
            cmd.SetViewport(new Rect(ix * slotPx, iy * slotPx, slotPx, slotPx));
            cmd.SetViewProjectionMatrices(view, proj);
            cmd.DrawMesh(quad, Matrix4x4.TRS(p0, Quaternion.identity, new Vector3(td.size.x, 1f, td.size.z)), mat, 0, pass, props);
        }
        Graphics.ExecuteCommandBuffer(cmd);
        indirDirty = true;
    }

    void SetTerrainProps(TerrainData td)
    {
        props ??= new MaterialPropertyBlock();
        props.Clear();
        props.SetColor(LitColorId, Color.white);   // albedo only: the lit colour is applied when sampling
        var alph = td.alphamapTextures;
        if (alph != null && alph.Length > 0 && alph[0] != null) props.SetTexture(ControlId, alph[0]);
        var layers = td.terrainLayers ?? Array.Empty<TerrainLayer>();
        for (int i = 0; i < 4 && i < layers.Length; i++)
        {
            var l = layers[i]; if (l == null || l.diffuseTexture == null) continue;
            props.SetTexture(SplatIds[i], l.diffuseTexture);
            if (l.maskMapTexture != null) props.SetTexture(MaskIds[i], l.maskMapTexture);
            Vector2 t = l.tileSize, o = l.tileOffset; if (t.x <= 0 || t.y <= 0) t = Vector2.one;
            props.SetVector(SplatStIds[i], new Vector4(td.size.x / t.x, td.size.z / t.y, o.x / t.x, o.y / t.y));
        }
    }

    // one texel per finest page: atlas uv of the slot's page origin (xy) and pages across at that level (z)
    void BuildIndirection()
    {
        indirDirty = false;
        for (int i = 0; i < indirPixels.Length; i++) indirPixels[i] = Color.clear;
        int slotPx = tilePixels + borderPixels * 2;
        float atlasPx = slotPx * atlasSlots;
        var list = new List<KeyValuePair<long, Slot>>(resident);
        list.Sort((a, b) => (b.Key >> 40).CompareTo(a.Key >> 40));   // coarse first, finer pages overwrite
        foreach (var kv in list)
        {
            int level = (int)(kv.Key >> 40), x = (int)((kv.Key >> 20) & 0xFFFFF), y = (int)(kv.Key & 0xFFFFF);
            int span = 1 << level;
            int ix = kv.Value.index % atlasSlots, iy = kv.Value.index / atlasSlots;
            var c = new Color((ix * slotPx + borderPixels) / atlasPx, (iy * slotPx + borderPixels) / atlasPx, finestPages >> level, 1);
            for (int j = y * span; j < (y + 1) * span; j++)
                for (int i = x * span; i < (x + 1) * span; i++) indirPixels[j * finestPages + i] = c;
        }
        indirection.SetPixels(indirPixels);
        indirection.Apply(false, false);
    }

    void Publish()
    {
        var td = terrain.terrainData; Vector3 p0 = terrain.GetPosition();
        int slotPx = tilePixels + borderPixels * 2;
        float atlasPx = slotPx * atlasSlots;
        Shader.SetGlobalTexture(AtlasId, atlas);
        Shader.SetGlobalTexture(IndirId, indirection);
        Shader.SetGlobalVector(RectId, new Vector4(p0.x, p0.z, 1f / td.size.x, 1f / td.size.z));
        Shader.SetGlobalVector(ParamsId, new Vector4(finestPages, tilePixels / atlasPx, 0, 0));
        // the ground's lit / shadow colour and toon ramp, read live so day / night needs no re-render
        var m = terrain.materialTemplate;
        Shader.SetGlobalColor(GroundShadowId, m.HasProperty(ShadowColorId) ? m.GetColor(ShadowColorId) : Color.white);
        bool ramp = m.HasProperty(ThresholdId) && m.HasProperty(SoftnessId) && m.HasProperty(AmbientId) && m.HasProperty(LitColorId);
        Shader.SetGlobalColor(GroundLitId, ramp ? m.GetColor(LitColorId) : Color.white);
        Shader.SetGlobalVector(GroundLightId, ramp ? new Vector4(m.GetFloat(ThresholdId), m.GetFloat(SoftnessId), m.GetFloat(AmbientId), 1f) : Vector4.zero);
    }
}
