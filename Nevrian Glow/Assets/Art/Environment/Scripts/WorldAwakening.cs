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
/// 2026-10-30, lighthouse spec v2: three stages and a light wave. Colour now spreads from the lighthouse (Centre On
/// Lighthouse), in steps that follow the lit floors: none -> floor 1 (fields, paths, fences, bushes) -> floor 2 (trees,
/// flowers, shop, hills) -> heart (everything; the effect then switches off). A stage's colour does not creep in with
/// the climbing light any more: once the floor has finished filling, a gold wave rides out from the tower over
/// Wave Seconds and colour returns behind it. A load / new game (LighthouseGoal.SetProgressImmediate) snaps, no wave.
/// Disabled = everything as authored. Stage radii, wave time and band values are my own; the 3 s wave is the planner's
/// lighthouse_restore_wave_time.
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

    [Header("Stages + light wave (spec v2)")]
    [Tooltip("Colour spreads from the lighthouse alone (on) instead of the farm-to-lighthouse capsule (off)")]
    public bool centerOnLighthouse = true;
    [Tooltip("Coloured radius round the lighthouse per stage: dark / floor 1 / floor 2 (m). The heart wave runs to Full Radius")]
    public float[] stageRadii = { 0f, 50f, 110f };
    [Tooltip("Heart (ending): the last wave runs this far, then the effect switches off")]
    public float fullRadius = 360f;
    [Tooltip("Seconds for a wave to reach its stage's edge (planner: lighthouse_restore_wave_time 3)")]
    public float waveSeconds = 3f;
    [Tooltip("Seconds for the last (heart) wave to reach Full Radius")]
    public float heartWaveSeconds = 5f;
    [Tooltip("Gold band strength while a wave travels")]
    [Range(0f, 6f)] public float waveBand = 2.5f;
    [Tooltip("Band width while a wave travels (x the soft edge)")]
    [Range(0.5f, 6f)] public float waveBandWidth = 2.2f;
    public Color bandColor = new Color(1f, 0.8f, 0.45f);

    [Header("Lake")]
    public Renderer[] lakeRenderers = new Renderer[0];
    public string[] lakeFloats = { "_CausticStrength", "_GlintEmission" };
    [Tooltip("Lake strength while asleep (x the authored value)")]
    [Range(0f, 1f)] public float lakeAsleep = 0.25f;

    [Header("Creatures")]
    public Creature[] creatures = new Creature[0];

    [Header("VFX only in the coloured area")]
    [Tooltip("Roots whose VFX (VisualEffect, ParticleSystem) show only inside the coloured area and appear as the wave passes them. Empty = the scene's '== FX ==' and '== Water ==' roots")]
    public Transform[] vfxRoots = new Transform[0];
    [Tooltip("A VFX counts as inside once the coloured edge is this far past its centre (m)")]
    public float vfxMargin = 3f;

    [Header("Ambient particles")]
    public ParticleSystem[] particles = new ParticleSystem[0];
    [Range(0f, 1f)] public float particlesAsleep = 0.2f;

    static readonly int WakeAId = Shader.PropertyToID("_DSEWA_WakeA");
    static readonly int WakeParamsId = Shader.PropertyToID("_DSEWA_WakeParams");
    static readonly int WakeBandId = Shader.PropertyToID("_DSEWA_WakeBand");

    // area-gated VFX (2026-10-30, user: "the VFX were not hidden in the grey zone")
    struct AreaFx { public GameObject go; public ParticleSystem ps; public float appearAt; public bool shown; }
    readonly System.Collections.Generic.List<AreaFx> areaFx = new System.Collections.Generic.List<AreaFx>();
    bool areaFxBuilt;

    void BuildAreaFx()
    {
        areaFxBuilt = true;
        areaFx.Clear();
        var roots = new System.Collections.Generic.List<Transform>();
        foreach (var r in vfxRoots) if (r != null) roots.Add(r);
        if (roots.Count == 0)
            foreach (var g in gameObject.scene.GetRootGameObjects())
                if (g.name == "== FX ==" || g.name == "== Water ==") roots.Add(g.transform);
        var seen = new System.Collections.Generic.HashSet<GameObject>();
        foreach (var c in creatures)
            if (c.group != null && seen.Add(c.group)) areaFx.Add(new AreaFx { go = c.group, appearAt = c.appearAt, shown = true });
        foreach (var root in roots)
        {
            foreach (var v in root.GetComponentsInChildren<UnityEngine.VFX.VisualEffect>(true))
                if (seen.Add(v.gameObject)) areaFx.Add(new AreaFx { go = v.gameObject, shown = true });
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps.transform.parent != null && ps.transform.parent.GetComponentInParent<ParticleSystem>() != null) continue;   // sub-emitters follow their parent
                if (seen.Add(ps.gameObject)) areaFx.Add(new AreaFx { go = ps.gameObject, ps = ps, shown = true });
            }
        }
    }

    void ShowAreaFx(ref AreaFx f, bool show)
    {
        if (f.shown == show) return;
        f.shown = show;
        if (f.ps != null)
        {
            if (show) f.ps.Play(true); else f.ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);   // particles in the air fade out
        }
        else f.go.SetActive(show);
    }
    float radiusNow = -1f, waveFrom, waveTo, waveT = 1f;
    int stageNow = -1;

    /// <summary>True while a light wave is travelling (EndingDirector waits for the heart wave).</summary>
    public bool WaveRunning => waveT < 1f;
    /// <summary>True while the last (heart) wave is travelling - EndingDirector's shock dome rides it.</summary>
    public bool HeartWaveRunning => stageNow >= 3 && waveT < 1f;
    /// <summary>Radius of the coloured area right now (m) and how far the running wave has got (0..1).</summary>
    public float CurrentRadius => radiusNow;
    public float WaveProgress => waveT;

    // stage from the tower's lit part: 0 dark, 1 = floor 1, 2 = floor 2 (both floors, orb still dark), 3 = heart
    // in the ending the heart stage waits for the beam (spec v2: heart -> beam -> last wave)
    int StageOf(float shown)
    {
        int st = shown >= 0.999f ? 3 : shown >= 0.985f ? 2 : shown >= 0.49f ? 1 : 0;
        if (st == 3 && lighthouse != null && lighthouse.IsEnding && !lighthouse.BeamUp) st = 2;
        return st;
    }
    float RadiusOf(int stage) => stage >= 3 ? fullRadius : stageRadii.Length == 0 ? 0f : stageRadii[Mathf.Clamp(stage, 0, stageRadii.Length - 1)];
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
        Shader.SetGlobalVector(WakeBandId, Vector4.zero);
        radiusNow = -1f; stageNow = -1; waveT = 1f;
        foreach (var r in lakeRenderers) if (r != null) r.SetPropertyBlock(null);
        for (int i = 0; i < particles.Length; i++)
            if (particles[i] != null && particleRates != null && i < particleRates.Length) { var e = particles[i].emission; e.rateOverTimeMultiplier = particleRates[i]; }
        if (Application.isPlaying)
        {
            for (int i = 0; i < areaFx.Count; i++) { var f = areaFx[i]; if (f.go != null) ShowAreaFx(ref f, true); areaFx[i] = f; }
            foreach (var c in creatures) if (c.group != null) c.group.SetActive(true);
        }
        areaFxBuilt = false;
    }

    void Update()
    {
        float p = Mathf.Clamp01(Progress);
        float awake = p * p * (3f - 2f * p);   // eased
        Vector3 lh = lighthouse != null ? lighthouse.transform.position : farmCenter;
        Vector3 from = centerOnLighthouse ? lh : farmCenter;
        Shader.SetGlobalVector(WakeAId, new Vector4(from.x, from.z, lh.x, lh.z));
        float band = edgeBand, bandWidth = 1f;
        bool on;
        if (centerOnLighthouse)
        {
            // stages: the wave starts once the floor has finished filling (the stage reads the lit part, not the target)
            int stage = StageOf(p);
            bool snap = radiusNow < 0f || !Application.isPlaying
                        || (lighthouse != null && Time.frameCount - lighthouse.ImmediateFrame <= 2)
                        || stage < stageNow;
            if (stage != stageNow)
            {
                if (snap) { radiusNow = RadiusOf(stage); waveT = 1f; }
                else { waveFrom = radiusNow; waveTo = RadiusOf(stage); waveT = 0f; }
                stageNow = stage;
            }
            if (waveT < 1f)
            {
                waveT = Mathf.Min(1f, waveT + Time.deltaTime / Mathf.Max(stageNow >= 3 ? heartWaveSeconds : waveSeconds, 0.01f));
                float e = 1f - (1f - waveT) * (1f - waveT);   // ease out: fast from the tower, settling at the edge
                radiusNow = Mathf.Lerp(waveFrom, waveTo, e);
                float travel = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, waveT));   // full while travelling, fades as it settles
                band = Mathf.Lerp(edgeBand, waveBand, travel);
                bandWidth = Mathf.Lerp(1f, waveBandWidth, travel);
            }
            on = !(stageNow >= 3 && waveT >= 1f);
        }
        else
        {
            // old behaviour: radius grows slowly at first (the farm and its edge), then sweeps the far hills
            radiusNow = Mathf.Lerp(startRadius, endRadius, Mathf.Pow(p, 1.8f));
            on = p < 0.999f;
        }
        Shader.SetGlobalVector(WakeParamsId, new Vector4(radiusNow, edge, on ? 1f : 0f, band));
        Shader.SetGlobalVector(WakeBandId, new Vector4(bandColor.r, bandColor.g, bandColor.b, bandWidth));

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

        // switching scene objects on / off in edit mode would dirty the scene: play mode only.
        // Creatures and the scene VFX show only inside the coloured area (they appear as the wave passes) and, for
        // creatures, once the progress passes their threshold.
        if (Application.isPlaying)
        {
            if (!areaFxBuilt) BuildAreaFx();
            bool gated = on && centerOnLighthouse;
            for (int i = 0; i < areaFx.Count; i++)
            {
                var f = areaFx[i];
                if (f.go == null) continue;
                bool show = p >= f.appearAt - 1e-4f;
                if (show && gated)
                {
                    Vector3 fp = f.go.transform.position;
                    show = Vector2.Distance(new Vector2(fp.x, fp.z), new Vector2(lh.x, lh.z)) <= radiusNow - vfxMargin;
                }
                ShowAreaFx(ref f, show);
                areaFx[i] = f;
            }
        }
    }
}
