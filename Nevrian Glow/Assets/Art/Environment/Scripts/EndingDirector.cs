using System.Collections;
using System.Collections.Generic;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.UI;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Demo ending (lighthouse spec v2 section 7, 2026-10-30): after the last day's settlement -
///   cut 0  fade to black, then up on an outside view of the lighthouse (child "ShotTower")
///   cut 1  the remaining floors fill and the heart wakes (LighthouseGoal.PlayEnding - the ending resonance starts)
///   cut 2  the beam erupts at the clip's swell (LighthouseGoal.beamAtClipTime, 7.75 s)
///   cut 3  the last gold wave runs to the edge of the map and the grey lifts (WorldAwakening, after the beam)
///   cut 4  one loop over the restored map (MapFlyover, the same loop the intro was planned on)
///   cut 5  white fade -> grandma's pot in the shop (first object named Pot Name; skipped when the game scene has none)
///   cut 6  the demo end screen.
/// No line of dialogue (planner, 2026-10-30). Hooks in without game code: SettlementScreen raises DemoEndReached and
/// opens the demo end screen right after; this is a GameScreen opened on the event, so the demo end screen cannot open
/// until the ending closes it and opens it itself. The game HUD canvases are off while it plays.
/// Fade / hold times are my own values; the 7.75 s beam comes from the clip; the ~12 s loop from MapFlyover.
/// </summary>
public class EndingDirector : GameScreen
{
    [Header("Ending")]
    public LighthouseGoal lighthouse;
    public WorldAwakening world;
    [Tooltip("Lends its camera; cut 4 flies its loop")] public MapFlyover flyover;
    [Tooltip("Full-screen image on the panel (black / white fades)")] public Image fadeImage;
    [Tooltip("Pot to end on (cut 5): first scene object with this name")] public string potName = "pf_prop_pot_grandma";
    [Tooltip("Camera offset from the pot for cut 5 (pot's local space would be odd for a round pot - world offset)")]
    public Vector3 potCameraOffset = new Vector3(0f, 1.1f, -1.9f);

    [Header("Beam camera shake")]
    [Tooltip("Shake on the beam (degrees of camera jitter at the start, dies away) - turned, not moved, so it reads from far off")] public float shakeAmount = 3f;
    public float shakeSeconds = 1.6f;
    [Tooltip("While the light gathers the camera sits this much closer to the tower (m along the shot), then pulls back to the wide shot as the beam erupts")]
    public float pushIn = 28f;
    [Tooltip("Seconds of the pull back on the beam")] public float pullBackSeconds = 1.2f;
    public float shakeFrequency = 22f;

    [Header("Sky dissolve on the beam")]
    [Tooltip("Clouds burn away in a ring spreading from where the beam meets the sky (radians the ring reaches)")]
    public float skyDissolveRadius = 1.9f;
    public float skyDissolveSeconds = 5f;
    [Tooltip("Gold rim on the burning edge (rgb) and its width in radians (a)")]
    public Color skyDissolveEdge = new Color(3.2f, 2.1f, 0.9f, 0.18f);

    [Header("Shock dome on the last wave")]
    [Tooltip("Material for the dome (DSEWA/ShockDome). Empty = no dome")]
    public Material domeMaterial;
    [Tooltip("The dome runs this many metres ahead of the colour (its edge is the wave front)")]
    public float domeLead = 2f;

    [Header("Timing")]
    public float fadeOutSeconds = 1f;
    public float fadeInSeconds = 1.5f;
    [Tooltip("Hold on the restored tower after the last wave, before the loop")] public float holdAfterWave = 1.5f;
    public float whiteFadeSeconds = 1.2f;
    public float potHold = 4f;
    public float endFadeSeconds = 1.5f;
    public bool hideHud = true;

    /// <summary>True while the ending runs.</summary>
    public static bool IsPlaying { get; private set; }

    bool allowClose;
    readonly List<Canvas> hiddenCanvases = new List<Canvas>();

    void OnEnable() => GameEvents.DemoEndReached += OnDemoEnd;
    void OnDisable()
    {
        GameEvents.DemoEndReached -= OnDemoEnd;
        if (IsPlaying) { RestoreHud(); IsPlaying = false; }
        Shader.SetGlobalVector("_DSEWA_SkyDissolve", Vector4.zero);
    }

    void OnDemoEnd(int day)
    {
        if (IsPlaying || !Application.isPlaying) return;
        if (lighthouse == null) lighthouse = LighthouseGoal.Active;
        if (world == null) world = FindFirstObjectByType<WorldAwakening>();
        IsPlaying = true;
        SetFade(Color.black, 0f);
        Show();   // the settlement has just closed; holds the demo end screen back
        if (!IsOpen) { IsPlaying = false; return; }
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        // cut 0: to black, then up on the tower
        yield return Fade(Color.black, 0f, 1f, fadeOutSeconds);
        if (hideHud) HideHud();
        var cam = flyover != null ? flyover.TakeCamera() : null;
        var shot = transform.Find("ShotTower");
        if (cam != null && shot != null) cam.transform.SetPositionAndRotation(shot.position + shot.forward * pushIn, shot.rotation);

        // cut 1-2: heart, beam (LighthouseGoal times the beam to the clip)
        if (lighthouse != null) lighthouse.PlayEnding();
        yield return Fade(Color.black, 1f, 0f, fadeInSeconds);
        float t = 0f;
        bool skyOpened = false;
        Vector3 p0 = shot != null ? shot.position : (cam != null ? cam.transform.position : Vector3.zero);
        while (lighthouse != null && (!lighthouse.BeamUp || (lighthouse.BeamStartTime > 0f && Time.time - lighthouse.BeamStartTime < shakeSeconds)) && t < 25f)
        {
            t += Time.deltaTime;
            // the sky opens the moment the beam erupts: clouds dissolve outward from it (runs on under the wave and the loop)
            if (!skyOpened && lighthouse.BeamStartTime > 0f) { skyOpened = true; StartCoroutine(SkyDissolve(lighthouse.BeamStartTime)); }
            // close while the light gathers (a slow drift back), then a quick pull back to the wide shot on the beam
            Vector3 fwd = shot != null ? shot.forward : Vector3.forward;
            float near = pushIn - t * 0.25f;
            if (lighthouse.BeamStartTime > 0f)
                near *= 1f - Mathf.SmoothStep(0f, 1f, (Time.time - lighthouse.BeamStartTime) / Mathf.Max(pullBackSeconds, 0.01f));
            Vector3 pos = p0 + fwd * Mathf.Max(near, 0f);
            Quaternion rot = shot != null ? shot.rotation : Quaternion.identity;
            // shake as the beam erupts: a hard kick that dies away (the camera turns a little, so it reads from far off)
            if (lighthouse.BeamStartTime > 0f)
            {
                float s = Time.time - lighthouse.BeamStartTime;
                float amp = s < shakeSeconds ? shakeAmount * Mathf.Exp(-s * 3f / Mathf.Max(shakeSeconds, 0.01f)) * (1f - s / shakeSeconds) : 0f;
                float f = shakeFrequency;
                rot *= Quaternion.Euler((Mathf.PerlinNoise(s * f, 0.3f) - 0.5f) * 2f * amp, (Mathf.PerlinNoise(0.7f, s * f) - 0.5f) * 2f * amp, (Mathf.PerlinNoise(s * f * 0.7f, 5.1f) - 0.5f) * amp);
            }
            if (cam != null && shot != null) cam.transform.SetPositionAndRotation(pos, rot);
            yield return null;
        }

        if (!skyOpened && lighthouse != null) StartCoroutine(SkyDissolve(Time.time));
        // cut 3: the last wave (WorldAwakening starts it when the beam is up) with the shock dome riding its front
        yield return null;
        if (domeMaterial != null && world != null && lighthouse != null) StartCoroutine(Dome());
        for (t = 0f; world != null && world.WaveRunning && t < 15f; t += Time.deltaTime) yield return null;
        yield return new WaitForSeconds(holdAfterWave);

        // cut 4: one loop over the restored map
        if (flyover != null)
        {
            flyover.Release();
            bool flown = false;
            flyover.Play(() => flown = true, keepCamera: true);
            while (!flown) yield return null;
            cam = flyover.FlyCamera;
        }

        // cut 5: white -> grandma's pot
        yield return Fade(Color.white, 0f, 1f, whiteFadeSeconds);
        var pot = GameObject.Find(potName);
        if (pot != null && cam != null)
        {
            Vector3 c = pot.transform.position + Vector3.up * 0.6f;
            cam.transform.position = pot.transform.position + potCameraOffset;
            cam.transform.LookAt(c);
            var sleep = pot.GetComponentInChildren<GrandmaSleep>();
            if (sleep != null) sleep.enabled = true;
            yield return Fade(Color.white, 1f, 0f, whiteFadeSeconds);
            yield return new WaitForSeconds(potHold);
            yield return Fade(Color.black, 0f, 1f, endFadeSeconds);
        }
        else
        {
            yield return Fade(Color.white, 1f, 1f, 0.5f);
        }

        // cut 6: the demo end screen
        if (flyover != null) flyover.Release();
        Finish();
    }

    IEnumerator Dome()
    {
        for (float w = 0f; !world.HeartWaveRunning && w < 3f; w += Time.deltaTime) yield return null;
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "EndingShockDome";
        Destroy(go.GetComponent<Collider>());
        var r = go.GetComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        var mat = new Material(domeMaterial);
        r.sharedMaterial = mat;
        Vector3 c = lighthouse.transform.position;
        while (world.HeartWaveRunning)
        {
            float rad = world.CurrentRadius + domeLead;
            go.transform.position = c;
            go.transform.localScale = Vector3.one * rad * 2f;
            float k = world.WaveProgress;
            mat.SetFloat("_Fade", Mathf.Clamp01(k * 12f) * (1f - Mathf.SmoothStep(0.55f, 1f, k)));
            yield return null;
        }
        Destroy(go);
        Destroy(mat);
    }

    IEnumerator SkyDissolve(float from)
    {
        // centre: straight up the beam as seen from the ending shot
        Vector3 top = lighthouse.HeartPoint() + Vector3.up * lighthouse.beamHeight;
        var shot = transform.Find("ShotTower");
        Vector3 eye = shot != null ? shot.position : top - Vector3.forward * 60f;
        Vector3 dirToTop = (top - eye).normalized;
        Shader.SetGlobalVector("_DSEWA_SkyDissolveEdge", new Vector4(skyDissolveEdge.r, skyDissolveEdge.g, skyDissolveEdge.b, skyDissolveEdge.a));
        for (float t = Time.time - from; t < skyDissolveSeconds; t = Time.time - from)
        {
            float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / skyDissolveSeconds), 2.5f);   // fast burst, slow settle
            Shader.SetGlobalVector("_DSEWA_SkyDissolve", new Vector4(dirToTop.x, dirToTop.y, dirToTop.z, Mathf.Max(0.001f, k * skyDissolveRadius)));
            yield return null;
        }
        Shader.SetGlobalVector("_DSEWA_SkyDissolve", new Vector4(dirToTop.x, dirToTop.y, dirToTop.z, skyDissolveRadius));
        Shader.SetGlobalVector("_DSEWA_SkyDissolveEdge", new Vector4(skyDissolveEdge.r, skyDissolveEdge.g, skyDissolveEdge.b, 0f));
    }

    void Finish()
    {
        RestoreHud();
        IsPlaying = false;
        allowClose = true; Close(); allowClose = false;
        var end = DemoEndScreen.Instance;
        if (end != null && !end.IsOpen) end.Open();
    }

    protected override bool OnClosing() => allowClose || !IsPlaying;

    IEnumerator Fade(Color c, float from, float to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            SetFade(c, Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds)));
            yield return null;
        }
        SetFade(c, to);
    }

    void SetFade(Color c, float a)
    {
        if (fadeImage == null) return;
        c.a = a; fadeImage.color = c;
    }

    void HideHud()
    {
        hiddenCanvases.Clear();
        var mine = GetComponentInChildren<Canvas>(true);
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas || !c.enabled || c == mine || c.renderMode == RenderMode.WorldSpace) continue;
            c.enabled = false;
            hiddenCanvases.Add(c);
        }
    }

    void RestoreHud()
    {
        foreach (var c in hiddenCanvases) if (c != null) c.enabled = true;
        hiddenCanvases.Clear();
    }
}
