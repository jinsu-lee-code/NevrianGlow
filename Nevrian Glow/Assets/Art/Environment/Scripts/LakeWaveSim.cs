using UnityEngine;

/// <summary>
/// Real-time wave simulation for the lake (2026-10-26, user: "high quality waves spreading from the falls").
/// A height field over a square area (Hugo Elias' 2D water, run on the GPU: Hidden/DSEWA/LakeWaveSim, ping-pong
/// between two half-float textures, R = height now, G = height one step ago). The falling water at the foot of the falls keeps hitting the surface at slightly
/// different spots and strengths, so the rings interfere instead of repeating; land cells (baked once from the
/// terrain) are pinned flat so the waves reflect off the shore and bend round stones; damping lets them die out.
/// The result is published as globals for DSEWA/ToonLakeWater:
///   _DSEWA_WaveTex  (R = height)   _DSEWA_WaveRect (x0, z0, size, 1 / resolution)   _DSEWA_WaveOn (1 when running)
/// The simulation runs at a fixed step rate (wave speed = cells per step x steps per second).
/// All numbers are my own values.
/// </summary>
[ExecuteAlways]
public class LakeWaveSim : MonoBehaviour
{
    [Tooltip("Hidden/DSEWA/LakeWaveSim (referenced here so it is included in builds)")]
    public Shader simShader;
    [Header("Area (world XZ)")]
    public Vector2 center = new Vector2(2f, -56f);
    public float size = 64f;
    [Tooltip("Texels per side (512 over 64 m = 12.5 cm)")]
    public int resolution = 512;
    [Tooltip("Ground above this height is land (waves reflect there)")]
    public float waterLevel = -0.09f;

    [Header("Simulation")]
    [Tooltip("Steps per second: each step moves a wave about one texel, so this sets the wave speed")]
    public float stepsPerSecond = 18f;
    [Range(0.9f, 1f)] public float damping = 0.988f;
    [Tooltip("Never run more than this many steps in one frame (after a hitch)")]
    public int maxStepsPerFrame = 4;
    [Tooltip("Steps run at start so the lake is already full of waves (no empty lake on load)")]
    public int warmupSteps = 300;
    [Tooltip("Keep simulating in the editor outside Play mode (Scene view shows the waves moving)")]
    public bool runInEditor = true;

    [Header("Falls impacts")]
    public Transform source;
    [Tooltip("Impacts land anywhere in this radius round the source")]
    public float sourceRadius = 1.4f;
    public Vector2 impactRadius = new Vector2(0.25f, 0.6f);
    public Vector2 impactStrength = new Vector2(0.15f, 0.45f);
    [Tooltip("Impacts per step (fractional = random)")]
    public float impactsPerStep = 1.3f;

    [Header("Stray drops over the whole lake (life)")]
    public float straysPerSecond = 0.6f;
    public float strayStrength = 0.12f;

    Material mat;
    RenderTexture a, b;
    Texture2D mask;
    float acc;
    readonly Vector4[] drops = new Vector4[8];
    static readonly int TexId = Shader.PropertyToID("_DSEWA_WaveTex");
    static readonly int RectId = Shader.PropertyToID("_DSEWA_WaveRect");
    static readonly int OnId = Shader.PropertyToID("_DSEWA_WaveOn");

    void OnEnable()
    {
        Init();
#if UNITY_EDITOR
        // outside Play mode Update only runs on repaints, so the sim stalled and the waves died out:
        // drive it from the editor loop with real elapsed time instead
        lastEditorTime = UnityEditor.EditorApplication.timeSinceStartup;
        UnityEditor.EditorApplication.update += EditorTick;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= EditorTick;
#endif
        Release();
        Shader.SetGlobalFloat(OnId, 0);
    }

#if UNITY_EDITOR
    double lastEditorTime;
    void EditorTick()
    {
        if (this == null || Application.isPlaying || !runInEditor || !isActiveAndEnabled) return;
        double now = UnityEditor.EditorApplication.timeSinceStartup;
        float dt = Mathf.Min((float)(now - lastEditorTime), 0.1f);
        lastEditorTime = now;
        Tick(dt);
        UnityEditor.SceneView.RepaintAll();
    }
#endif

    void Init()
    {
        Release();
        var sh = simShader != null ? simShader : Shader.Find("Hidden/DSEWA/LakeWaveSim");
        if (sh == null) return;
        mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        a = NewRT(); b = NewRT();
        BakeMask();
        mat.SetTexture("_WaterMask", mask);
        Publish();
        Step(warmupSteps);
    }

    RenderTexture NewRT()
    {
        var rt = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBHalf)   // RGHalf sampled as 0 in the lake shader here
        { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        rt.Create();
        var prev = RenderTexture.active; RenderTexture.active = rt; GL.Clear(false, true, Color.clear); RenderTexture.active = prev;
        return rt;
    }

    /// <summary>Water mask from the terrain: 1 where the ground is under the water level, soft at the shore.</summary>
    public void BakeMask()
    {
        var ter = Terrain.activeTerrain;
        mask = new Texture2D(resolution, resolution, TextureFormat.R8, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color32[resolution * resolution];
        float x0 = center.x - size * 0.5f, z0 = center.y - size * 0.5f;
        for (int j = 0; j < resolution; j++)
            for (int i = 0; i < resolution; i++)
            {
                float x = x0 + (i + 0.5f) / resolution * size, z = z0 + (j + 0.5f) / resolution * size;
                float g = ter ? ter.SampleHeight(new Vector3(x, 0, z)) + ter.transform.position.y : -10f;
                byte v = (byte)(Mathf.Clamp01((waterLevel - g) / 0.08f) * 255);
                px[j * resolution + i] = new Color32(v, v, v, 255);
            }
        mask.SetPixels32(px); mask.Apply();
    }

    void Release()
    {
        if (a) { a.Release(); DestroyImmediate(a); }
        if (b) { b.Release(); DestroyImmediate(b); }
        if (mask) DestroyImmediate(mask);
        if (mat) DestroyImmediate(mat);
    }

    void Update()
    {
        if (Application.isPlaying) Tick(Time.deltaTime);
    }

    void Tick(float dt)
    {
        if (mat == null || a == null) { Init(); if (mat == null) return; }
        acc += dt * stepsPerSecond;
        int n = Mathf.Min((int)acc, maxStepsPerFrame);
        acc -= (int)acc;
        if (n > 0) Step(n);
    }

    /// <summary>Run n simulation steps now (also used to warm the lake up for screenshots).</summary>
    public void Step(int n)
    {
        if (mat == null) Init();
        if (mat == null) return;
        float x0 = center.x - size * 0.5f, z0 = center.y - size * 0.5f;
        for (int s = 0; s < n; s++)
        {
            int count = 0;
            if (source)
            {
                float k = impactsPerStep;
                while (k > 0 && count < 6)
                {
                    if (k < 1 && Random.value > k) break;
                    Vector2 off = Random.insideUnitCircle * sourceRadius;
                    Vector3 p = source.position + new Vector3(off.x, 0, off.y);
                    drops[count++] = new Vector4((p.x - x0) / size, (p.z - z0) / size,
                        Random.Range(impactRadius.x, impactRadius.y), Random.Range(impactStrength.x, impactStrength.y));
                    k -= 1;
                }
            }
            if (Random.value < straysPerSecond / Mathf.Max(stepsPerSecond, 1f) && count < 8)
                drops[count++] = new Vector4(Random.value, Random.value, 0.3f, strayStrength);
            mat.SetVectorArray("_Drops", drops);
            mat.SetInt("_DropCount", count);
            mat.SetFloat("_Damping", damping);
            mat.SetFloat("_SimSize", size);
            mat.SetVector("_SimTexel", new Vector4(1f / resolution, 1f / resolution, 0, 0));
            Graphics.Blit(a, b, mat, 0);
            var t = a; a = b; b = t;
        }
        Publish();
    }

    void Publish()
    {
        Shader.SetGlobalTexture(TexId, a);
        Shader.SetGlobalVector(RectId, new Vector4(center.x - size * 0.5f, center.y - size * 0.5f, size, 1f / resolution));
        Shader.SetGlobalFloat(OnId, 1);
    }
}
