using UnityEngine;

/// <summary>
/// The planet wakes up with the lighthouse (2026-10-28, user picked idea 1: "the world comes alive as you progress").
/// Reads the lighthouse's climbing light (LighthouseGoal, so the game still calls only SetProgress) and drives:
///   colour     - colour lives only round the farm and the lighthouse (a capsule from one to the other) and spreads
///                outward as the tower lights up; outside it the background shaders (terrain, grass, props / flowers,
///                leaves, leaf shells, cliffs - GroundFog.hlsl ApplyWorldWake) go grey, and the ToonProp glows with
///                Glow Follows The World Waking sleep. At full progress it switches off (everything as authored).
///   lake       - caustic / glint strength on the lake renderers (property block, scaled from the material)
///   creatures  - each group appears once the progress passes its threshold (play mode only)
///   particles  - ambient particle systems (lake mist, bubbles ...) emit more as it wakes
///   (2026-10-28, user: "isn't it odd that crops grow in a grey world?" - so the crops are the colour's source)
/// Disabled = everything as authored. All numbers are my own values.
/// </summary>
[ExecuteAlways]
public class WorldAwakening : MonoBehaviour
{
    [System.Serializable]
    public class Creature
    {
        public GameObject group;
        [Range(0f, 1f)] public float appearAt;
    }

    [Tooltip("Progress source (its climbing light). Empty = use Preview Progress")]
    public LighthouseGoal lighthouse;
    [Range(0f, 1f)] public float previewProgress = 1f;
    [Tooltip("Follow the progress in edit mode too (off = the scene stays fully awake while you edit it)")]
    public bool previewInEditor;

    [Header("Colour spreads from the farm")]
    [Tooltip("Farm centre (world); the coloured capsule runs from here to the lighthouse")]
    public Vector3 farmCenter = new Vector3(-1.5f, 0f, -2f);
    [Tooltip("Coloured radius round the capsule at 0 progress (m)")]
    public float startRadius = 48f;   // 2026-10-28 user: start where the old 35 % was
    [Tooltip("Radius just before full progress (m); at 1 the effect switches off")]
    public float endRadius = 260f;
    [Tooltip("Soft edge width (m)")]
    public float edge = 6f;
    [Tooltip("Light band on the edge")]
    [Range(0f, 2f)] public float edgeBand = 0.5f;

    [Header("Lake")]
    public Renderer[] lakeRenderers = new Renderer[0];
    public string[] lakeFloats = { "_CausticStrength", "_GlintEmission" };
    [Tooltip("Lake strength while asleep (x the authored value)")]
    [Range(0f, 1f)] public float lakeAsleep = 0.25f;

    [Header("Creatures")]
    public Creature[] creatures = new Creature[0];

    [Header("Ambient particles")]
    public ParticleSystem[] particles = new ParticleSystem[0];
    [Range(0f, 1f)] public float particlesAsleep = 0.2f;

    static readonly int WakeAId = Shader.PropertyToID("_DSEWA_WakeA");
    static readonly int WakeParamsId = Shader.PropertyToID("_DSEWA_WakeParams");
    MaterialPropertyBlock mpb;
    float[] particleRates;

    public float Progress
    {
        get
        {
            if (!Application.isPlaying && !previewInEditor) return 1f;
            return lighthouse != null ? lighthouse.ShownProgress : previewProgress;
        }
    }

    void OnEnable()
    {
        mpb ??= new MaterialPropertyBlock();
        particleRates = new float[particles.Length];
        for (int i = 0; i < particles.Length; i++)
            if (particles[i] != null) particleRates[i] = particles[i].emission.rateOverTimeMultiplier;
    }

    void OnDisable()
    {
        Shader.SetGlobalVector(WakeParamsId, Vector4.zero);
        foreach (var r in lakeRenderers) if (r != null) r.SetPropertyBlock(null);
        for (int i = 0; i < particles.Length; i++)
            if (particles[i] != null && particleRates != null && i < particleRates.Length) { var e = particles[i].emission; e.rateOverTimeMultiplier = particleRates[i]; }
        if (Application.isPlaying)
            foreach (var c in creatures) if (c.group != null) c.group.SetActive(true);
    }

    void Update()
    {
        float p = Mathf.Clamp01(Progress);
        float awake = p * p * (3f - 2f * p);   // eased
        // radius grows slowly at first (the farm and its edge), then sweeps the far hills
        Vector3 lh = lighthouse != null ? lighthouse.transform.position : farmCenter;
        Shader.SetGlobalVector(WakeAId, new Vector4(farmCenter.x, farmCenter.z, lh.x, lh.z));
        float radius = Mathf.Lerp(startRadius, endRadius, Mathf.Pow(p, 1.8f));
        Shader.SetGlobalVector(WakeParamsId, new Vector4(radius, edge, p < 0.999f ? 1f : 0f, edgeBand));

        foreach (var r in lakeRenderers)
        {
            if (r == null || r.sharedMaterial == null) continue;
            r.GetPropertyBlock(mpb);
            foreach (var prop in lakeFloats)
                if (r.sharedMaterial.HasProperty(prop))
                    mpb.SetFloat(prop, r.sharedMaterial.GetFloat(prop) * Mathf.Lerp(lakeAsleep, 1f, awake));
            r.SetPropertyBlock(mpb);
        }

        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] == null || particleRates == null || i >= particleRates.Length) continue;
            var e = particles[i].emission;
            e.rateOverTimeMultiplier = particleRates[i] * Mathf.Lerp(particlesAsleep, 1f, awake);
        }

        // switching scene objects on / off in edit mode would dirty the scene: play mode only
        if (Application.isPlaying)
            foreach (var c in creatures)
                if (c.group != null && c.group.activeSelf != (p >= c.appearAt - 1e-4f)) c.group.SetActive(p >= c.appearAt - 1e-4f);
    }
}
