using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The game's goal (2026-10-28, user: "the goal is lighting the whole lighthouse - that restores the planet's turning").
/// Nevria stopped turning and is stuck in twilight; the old Lumian "turning beacon" is dark. As the player grows and
/// sells, its windows light up floor by floor from the foot; when the last floor is lit the orb on top wakes, a beam
/// shoots into the sky. (2026-10-30, lighthouse spec v2: no first sunrise - the planet does not start turning, the sky
/// stays fixed; Turn Sky At Ending keeps the old dawn turn as an option.)
///
/// Game hook-up (two calls):
///   SetProgress(level / targetLevel)   // 0..1, any time; the light climbs to it over climbSeconds
///   PlayEnding()                       // when the goal is reached; onEndingFinished fires at the end
///   SetProgressImmediate(value)        // no climb / sound (loaded save) - LighthouseProgressLink does all three from game events
/// Writes only MaterialPropertyBlocks (DSEWA/ToonProp reveal-by-height: _RevealY, _RevealFlash, _RevealFlicker),
/// so the shared materials stay untouched. Previews in the editor while you drag Progress.
/// All numbers are my own values.
/// </summary>
[ExecuteAlways]
public class LighthouseGoal : MonoBehaviour
{
    [Header("Parts")]
    [Tooltip("Window strip (lights up from the bottom)")] public Renderer windows;
    [Tooltip("Orb on top (lights up last)")] public Renderer orb;
    [Tooltip("Other glowing parts that follow the climbing light (optional)")] public Renderer[] extraGlow = new Renderer[0];
    [Tooltip("Burst played at the light front each time it climbs (optional, a particle prefab)")] public ParticleSystem climbBurst;

    [Header("Progress")]
    [Range(0f, 1f)] public float progress;
    [Tooltip("Seconds for the light to climb to a new progress")] public float climbSeconds = 2.5f;
    [Tooltip("Orb wakes when progress reaches this")] [Range(0.5f, 1f)] public float orbAt = 1f;

    [Header("Look")]
    [Tooltip("Unlit colour multiply")] public Color darkColor = new Color(0.10f, 0.10f, 0.18f);
    [Tooltip("Unlit multiply for the extra parts (ribs etc.) - white: the structure keeps its colour, only its glow is off")] public Color extraDarkColor = Color.white;
    [Tooltip("Faint flicker of the dark windows, so they read as 'can be lit'")] [Range(0f, 1f)] public float darkFlicker = 0.35f;
    [Tooltip("Bright band riding the climbing front")] public float frontFlash = 2.5f;
    public float revealSoft = 0.8f;

    [Header("Ending")]
    [Tooltip("Beam shooting into the sky (created if empty)")] public Transform beam;
    public Material beamMaterial;
    public float beamHeight = 400f;
    public float beamWidth = 2.2f;
    public float beamSeconds = 2.5f;
    [Tooltip("Seconds into the ending (the ending clip) when the beam erupts - sfx_lighthouse_ending swells hardest at 7.75 s")]
    public float beamAtClipTime = 7.75f;

    [Header("Ending: light gathering into the heart")]
    [Tooltip("Particle material for the gathering light (soft round glow, e.g. mat_vfx_crop_ready_halo). Empty = no gathering")]
    public Material gatherMaterial;
    [Tooltip("Seconds of gathering before the beam")] public float gatherSeconds = 4.5f;
    [Tooltip("Motes per second at the start / just before the beam")] public Vector2 gatherRate = new Vector2(40f, 260f);
    [Tooltip("Seconds a mote takes from where it rises to the heart")] public Vector2 gatherFlight = new Vector2(1.2f, 2.2f);
    [Tooltip("Where the motes rise from: radius round the tower axis (m) at the foot")] public Vector2 gatherRadius = new Vector2(3f, 12f);
    public Color gatherColor = new Color(1f, 0.82f, 0.45f, 1f);
    public float gatherSize = 1.6f;
    [Tooltip("Off (2026-10-30, lighthouse spec v2): the sky stays fixed at the twilight - no first sunrise. On: the sky turns to Dawn Hour after the beam")]
    public bool turnSkyAtEnding = false;
    [Tooltip("Sky turns to this hour (0..24) - dawn")] public float dawnHour = 5.5f;
    public float skyTurnSeconds = 10f;
    public DayNightBlend dayNight;
    public UnityEvent onEndingFinished;

    [Header("Sound (2026-10-29: resonance hums made in VARCO - the tower has no bell)")]
    [Tooltip("Plays when a floor starts to light (sfx_lighthouse_floor, 2-3 s)")] public AudioClip floorClip;
    [Tooltip("Plays when the ending starts, swelling into the beam (sfx_lighthouse_ending, 8-12 s)")] public AudioClip endingClip;
    [Range(0f, 1f)] public float volume = 0.8f;
    [Tooltip("Optional; a 2D source is added here when empty (the tower is far away but must be heard)")] public AudioSource audioSource;

    static readonly int RevealYId = Shader.PropertyToID("_RevealY");
    static readonly int RevealSoftId = Shader.PropertyToID("_RevealSoft");
    static readonly int RevealDarkId = Shader.PropertyToID("_RevealDark");
    static readonly int RevealFlashId = Shader.PropertyToID("_RevealFlash");
    static readonly int RevealFlickerId = Shader.PropertyToID("_RevealFlicker");

    MaterialPropertyBlock mpb;
    float shown = -1f;        // progress currently drawn (climbs toward progress)
    float flash;              // front band strength, fades after each climb
    float orbWake;            // 0..1 orb light
    float savedHour = -1f;
    Material[] touchedMats;   // editor play mode: every material asset DayNightBlend writes (sky list + property entries)
    Material[] savedMats;     // ... and copies of them from before the ending, restored on exit
    bool ending;

    /// <summary>The lighthouse in the loaded scenes (GrandmaSleep follows it from the shop scene).</summary>
    public static LighthouseGoal Active { get; private set; }
    public bool IsEnding => ending;
    /// <summary>Frame of the last SetProgressImmediate (a load / new game) - WorldAwakening snaps instead of sending a wave.</summary>
    public int ImmediateFrame { get; private set; } = -100;
    /// <summary>Time.time the ending beam started to rise (-1 = not yet) - EndingDirector shakes the camera on it.</summary>
    public float BeamStartTime { get; private set; } = -1f;
    /// <summary>The ending beam has finished rising (WorldAwakening sends the last wave after it).</summary>
    public bool BeamUp { get; private set; }
    /// <summary>The progress the climbing light has reached right now (WorldAwakening follows this).</summary>
    public float ShownProgress => shown < 0f ? progress : shown;

    /// <summary>World point at the top of the lit part of the tower (where harvest light sinks in).</summary>
    public Vector3 LightFront()
    {
        if (windows == null) return transform.position + Vector3.up * 10f;
        var b = windows.bounds;
        return new Vector3(b.center.x, Mathf.Lerp(b.min.y, b.max.y, Mathf.Clamp01(ShownProgress / Mathf.Max(orbAt, 0.01f))), b.center.z);
    }

    /// <summary>World point of the heart (orb on top) - harvest light sinks in here (lighthouse spec v2).</summary>
    public Vector3 HeartPoint()
    {
        if (orb != null) return orb.bounds.center;
        if (windows != null) { var b = windows.bounds; return new Vector3(b.center.x, b.max.y, b.center.z); }
        return transform.position + Vector3.up * 10f;
    }

    /// <summary>A brief flare along the lit front (a harvest's light arriving, HarvestLightWisp).</summary>
    public void Pulse() => flash = Mathf.Max(flash, 1f);

    /// <summary>Progress 0..1 (e.g. level / target level). The light climbs there over climbSeconds.</summary>
    public void SetProgress(float value)
    {
        float v = Mathf.Clamp01(value);
        if (v > progress + 1e-4f) { flash = 1f; Burst(v); if (!ending) PlaySound(floorClip); }
        progress = v;
    }

    /// <summary>Jump straight to a progress with no climb, flash or sound (a loaded save, a new game).</summary>
    public void SetProgressImmediate(float value)
    {
        ImmediateFrame = Time.frameCount;
        progress = Mathf.Clamp01(value);
        shown = progress; flash = 0f;
        if (!ending) orbWake = shown >= orbAt - 1e-4f ? 1f : 0f;
        Apply();
    }

    void PlaySound(AudioClip clip)
    {
        if (!Application.isPlaying || clip == null) return;
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
        audioSource.PlayOneShot(clip, volume);
    }

    /// <summary>Goal reached: any dark floors fill, the heart (orb) wakes, the beam goes up. The sky stays fixed unless Turn Sky At Ending.</summary>
    public void PlayEnding()
    {
        if (ending || !Application.isPlaying) return;
        StartCoroutine(Ending());
    }

    void OnEnable() { mpb ??= new MaterialPropertyBlock(); shown = Application.isPlaying ? 0f : progress; Active = this; }

    void OnDisable()
    {
        if (Active == this) Active = null;
        foreach (var r in All()) if (r != null) r.SetPropertyBlock(null);
        // DayNightBlend writes its listed material ASSETS: in the editor put back the exact values from before the
        // ending (re-applying the old hour is not enough - the saved assets need not match a preset exactly)
        if (savedHour >= 0f && dayNight != null) dayNight.hour = savedHour;
        savedHour = -1f;
        if (savedMats != null && touchedMats != null)
        {
            for (int i = 0; i < savedMats.Length; i++)
                if (savedMats[i] != null && touchedMats[i] != null) { touchedMats[i].CopyPropertiesFromMaterial(savedMats[i]); DestroyImmediate(savedMats[i]); }
            savedMats = null; touchedMats = null;
        }
    }

    void Update()
    {
        if (!Application.isPlaying)
        {
            // editor: follow the slider straight away
            shown = progress; flash = 0f; orbWake = shown >= orbAt - 1e-4f ? 1f : 0f;
            Apply();
            return;
        }
        float dt = Time.deltaTime;
        shown = Mathf.MoveTowards(shown < 0 ? 0f : shown, progress, dt / Mathf.Max(climbSeconds, 0.01f));
        flash = Mathf.MoveTowards(flash, Mathf.Abs(shown - progress) > 1e-4f ? 1f : 0f, dt * 0.8f);
        if (!ending) orbWake = Mathf.MoveTowards(orbWake, shown >= orbAt - 1e-4f ? 1f : 0f, dt * 0.5f);
        Apply();
    }

    void Apply()
    {
        mpb ??= new MaterialPropertyBlock();
        if (windows != null)
        {
            var b = windows.bounds;
            float y = Mathf.Lerp(b.min.y - revealSoft, b.max.y + revealSoft, Mathf.Clamp01(shown / Mathf.Max(orbAt, 0.01f)));
            Write(windows, y, frontFlash * flash, darkColor);
            foreach (var r in extraGlow) if (r != null) Write(r, y, frontFlash * flash, extraDarkColor);
        }
        if (orb != null)
        {
            var b = orb.bounds;
            Write(orb, Mathf.Lerp(b.min.y - revealSoft, b.max.y + revealSoft, orbWake), frontFlash * Mathf.Sin(orbWake * Mathf.PI), darkColor);
        }
    }

    void Write(Renderer r, float revealY, float flashAmount, Color dark)
    {
        r.GetPropertyBlock(mpb);
        mpb.SetFloat(RevealYId, revealY);
        mpb.SetFloat(RevealSoftId, revealSoft);
        mpb.SetColor(RevealDarkId, dark);
        mpb.SetFloat(RevealFlashId, flashAmount);
        mpb.SetFloat(RevealFlickerId, darkFlicker);
        r.SetPropertyBlock(mpb);
    }

    void Burst(float target)
    {
        if (!Application.isPlaying || climbBurst == null || windows == null) return;
        var b = windows.bounds;
        var p = new Vector3(b.center.x, Mathf.Lerp(b.min.y, b.max.y, Mathf.Clamp01(target / Mathf.Max(orbAt, 0.01f))), b.center.z);
        var fx = Instantiate(climbBurst, p, Quaternion.identity);
        fx.Play();
        Destroy(fx.gameObject, 6f);
    }

    IEnumerator Ending()
    {
        ending = true;
        float endingStart = Time.time;
        PlaySound(endingClip);
        SetProgress(1f);
        while (shown < 1f - 1e-4f) yield return null;
        // orb charges up
        for (float t = 0; t < 1.5f; t += Time.deltaTime) { orbWake = Mathf.SmoothStep(0, 1, t / 1.5f); yield return null; }
        orbWake = 1f;
        // light gathers from the tower foot up the pillar into the heart, swelling toward the beam
        float gatherFrom = Mathf.Max(Time.time, endingStart + beamAtClipTime - gatherSeconds);
        while (Time.time < gatherFrom) yield return null;
        if (gatherMaterial != null) StartCoroutine(Gather(endingStart + beamAtClipTime));
        // beam: wait for the swell of the ending clip
        while (Time.time - endingStart < beamAtClipTime) yield return null;
        BeamStartTime = Time.time;
        flash = 1f;
        if (beam == null) beam = MakeBeam();
        beam.gameObject.SetActive(true);
        Vector3 top = orb != null ? orb.bounds.center : transform.position;
        beam.position = top;
        for (float t = 0; t < beamSeconds; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0, 1, t / beamSeconds);
            beam.localScale = new Vector3(beamWidth * (1f + 0.3f * Mathf.Sin(t * 9f) * (1 - k)), beamHeight * k * 0.5f, beamWidth);
            beam.position = top + Vector3.up * beamHeight * k * 0.5f;
            yield return null;
        }
        BeamUp = true;
        // the sky starts turning: night -> first dawn (hours wrap past midnight)
        if (turnSkyAtEnding && dayNight != null)
        {
            savedHour = dayNight.hour;
            if (Application.isEditor)
            {
                touchedMats = dayNight.TouchedMaterials().ToArray();
                savedMats = new Material[touchedMats.Length];
                for (int i = 0; i < touchedMats.Length; i++) savedMats[i] = new Material(touchedMats[i]);
            }
            float from = dayNight.hour, to = dawnHour < from ? dawnHour + 24f : dawnHour;
            for (float t = 0; t < skyTurnSeconds; t += Time.deltaTime)
            {
                dayNight.hour = Mathf.Repeat(Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, t / skyTurnSeconds)), 24f);
                dayNight.Apply();   // the component sits disabled in the scene (fixed night), so drive it directly
                yield return null;
            }
            dayNight.hour = Mathf.Repeat(to, 24f);
            dayNight.Apply();
        }
        onEndingFinished?.Invoke();
    }

    // gathering motes: each rises from round the tower foot / along the pillar and curls up into the heart (a quadratic
    // curve: up the pillar first, then in). Driven by hand through SetParticles so every mote lands on the heart.
    IEnumerator Gather(float until)
    {
        var go = new GameObject("EndingGather");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f; main.startLifetime = 100f; main.maxParticles = 1500; main.playOnAwake = false; main.loop = false;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = gatherMaterial;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();

        Vector3 heart = HeartPoint();
        Vector3 foot = new Vector3(heart.x, transform.position.y, heart.z);
        var starts = new System.Collections.Generic.List<Vector3>();
        var ctrls = new System.Collections.Generic.List<Vector3>();
        var ages = new System.Collections.Generic.List<float>();
        var lives = new System.Collections.Generic.List<float>();
        var buf = new ParticleSystem.Particle[1500];
        float start = Time.time, carry = 0f;
        while (Time.time < until + gatherFlight.y)
        {
            float dt = Time.deltaTime;
            if (Time.time < until)
            {
                float k = Mathf.InverseLerp(start, until, Time.time);
                carry += Mathf.Lerp(gatherRate.x, gatherRate.y, k * k) * dt;
                while (carry >= 1f && starts.Count < buf.Length)
                {
                    carry -= 1f;
                    float a = Random.value * Mathf.PI * 2f;
                    bool onPillar = Random.value < 0.45f;
                    float r = onPillar ? Random.Range(2.5f, 4f) : Random.Range(gatherRadius.x, gatherRadius.y);
                    float y = onPillar ? Random.Range(0f, (heart.y - foot.y) * 0.6f) : Random.Range(0f, 2f);
                    var p0 = foot + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                    starts.Add(p0);
                    ctrls.Add(new Vector3(foot.x + Mathf.Cos(a) * r * 0.35f, Mathf.Lerp(p0.y, heart.y, 0.8f), foot.z + Mathf.Sin(a) * r * 0.35f));
                    ages.Add(0f);
                    lives.Add(Random.Range(gatherFlight.x, gatherFlight.y));
                }
            }
            int n = 0;
            for (int i = starts.Count - 1; i >= 0; i--)
            {
                ages[i] += dt;
                float u = ages[i] / lives[i];
                if (u >= 1f) { starts.RemoveAt(i); ctrls.RemoveAt(i); ages.RemoveAt(i); lives.RemoveAt(i); flash = Mathf.Max(flash, 0.35f); continue; }
                float e = u * u;   // slow off the ground, quick into the heart
                Vector3 pos = (1 - e) * (1 - e) * starts[i] + 2 * (1 - e) * e * ctrls[i] + e * e * heart;
                var c = gatherColor; c.a = Mathf.Clamp01(u * 4f) * (1f - Mathf.Clamp01((u - 0.9f) * 10f));
                buf[n].position = pos;
                buf[n].startColor = c;
                buf[n].startSize = gatherSize * Mathf.Lerp(0.7f, 1.2f, u);
                buf[n].remainingLifetime = 10f; buf[n].startLifetime = 100f;
                n++;
            }
            ps.SetParticles(buf, n);
            yield return null;
        }
        Destroy(go);
    }

    Transform MakeBeam()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "EndingBeam";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, true);
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = beamMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        go.transform.localScale = new Vector3(beamWidth, 0f, beamWidth);
        return go.transform;
    }

    System.Collections.Generic.IEnumerable<Renderer> All()
    {
        yield return windows; yield return orb;
        foreach (var r in extraGlow) yield return r;
    }

    [ContextMenu("Preview: Next Floor (+0.5)")]
    void PreviewNext() => SetProgress(progress + 0.5f);

    [ContextMenu("Preview: Play Ending")]
    void PreviewEnding() => PlayEnding();

    [ContextMenu("Preview: Reset")]
    void PreviewReset() { progress = 0f; shown = 0f; orbWake = 0f; if (beam != null) beam.gameObject.SetActive(false); }
}
