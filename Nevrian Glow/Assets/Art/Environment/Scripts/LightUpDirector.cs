using System;
using System.Collections;
using System.Collections.Generic;
using NevrianGlowFarm.UI;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Floor light-up cutscene (2026-10-30, requested: "when the lighthouse lights, a top view where the area widens"): when
/// a floor lights in the morning (LighthouseProgressLink, days 3 and 5), the view dips to black, opens on a high view
/// looking down on the lighthouse and the farm - high enough that the stage's whole coloured circle fits - the floor
/// fills, the gold wave rolls out and the colour spreads, a short hold, then back to the player.
/// Waits until no other screen (letter, shop ...) is open; a GameScreen itself, so the player is held meanwhile and
/// nothing else opens on top. Loads / new games set the tower without a climb, so they never play this.
/// All numbers are my own values.
/// </summary>
public class LightUpDirector : GameScreen
{
    public static LightUpDirector Instance { get; private set; }

    public LighthouseGoal lighthouse;
    public WorldAwakening world;
    [Tooltip("Lends its camera")] public MapFlyover flyover;
    [Tooltip("Full-screen black image on the panel")] public Image fadeImage;
    [Tooltip("Camera height = the stage's coloured radius x this")] public float heightPerRadius = 2.3f;
    [Tooltip("Camera pitch (90 = straight down; a little less keeps the tower's side in view)")] public float pitch = 72f;
    public float fieldOfView = 50f;
    public float minHeight = 90f;
    [Tooltip("Height cap: the floor-2 circle (110 m) would put the camera at ~250 m, deep in the haze - the circle may run off the frame edge instead")]
    public float maxHeight = 150f;
    [Tooltip("Ground fog and aerial haze during the top view (1 = as in play, 0 = none)")] [Range(0f, 1f)] public float topViewFog = 0.7f;
    public float fadeSeconds = 0.45f;
    public float holdAfterWave = 1.2f;
    [Tooltip("Give up waiting for other screens after this long (the floor then lights without the cutscene)")] public float maxWaitForScreens = 20f;
    public bool hideHud = true;

    public static bool IsPlaying { get; private set; }

    bool allowClose;
    readonly List<Canvas> hiddenCanvases = new List<Canvas>();

    protected override void Awake() { base.Awake(); Instance = this; }
    protected override void OnDestroy() { base.OnDestroy(); if (Instance == this) Instance = null; }
    void OnDisable() { TopViewFog(false); if (IsPlaying) { RestoreHud(); IsPlaying = false; } }

    static readonly int HazeCutId = Shader.PropertyToID("_DSEWA_HazeCut");
    void TopViewFog(bool on)
    {
        GroundFog.CutsceneScale = on ? topViewFog : 1f;
        Shader.SetGlobalFloat(HazeCutId, on ? 1f - topViewFog : 0f);
    }

    /// <summary>Plays the cutscene and calls startClimb at the right moment (it lights the floor).</summary>
    public void Play(Action startClimb, float targetProgress)
    {
        if (!Application.isPlaying || IsPlaying) { startClimb?.Invoke(); return; }
        StartCoroutine(Run(startClimb, targetProgress));
    }

    IEnumerator Run(Action startClimb, float targetProgress)
    {
        if (lighthouse == null) lighthouse = LighthouseGoal.Active;
        if (world == null) world = FindFirstObjectByType<WorldAwakening>();
        // wait for a free screen (the morning letter, the shop ...)
        for (float w = 0f; (GameScreen.Current != null || IntroDirector.IsPlaying || EndingDirector.IsPlaying) && w < maxWaitForScreens; w += Time.unscaledDeltaTime)
            yield return null;
        if (GameScreen.Current != null || lighthouse == null || flyover == null) { startClimb?.Invoke(); yield break; }

        IsPlaying = true;
        SetBlack(0f);
        Show();
        if (!IsOpen) { IsPlaying = false; startClimb?.Invoke(); yield break; }
        yield return Fade(0f, 1f);
        if (hideHud) HideHud();

        // high view over the tower: the whole circle of this stage fits
        float radius = world != null && world.stageRadii.Length > 0
            ? world.stageRadii[Mathf.Clamp(targetProgress >= 0.985f ? 2 : 1, 0, world.stageRadii.Length - 1)] : 60f;
        Vector3 c = lighthouse.transform.position;
        float h = Mathf.Clamp(radius * heightPerRadius, minHeight, Mathf.Max(minHeight, maxHeight));
        var cam = flyover.TakeCamera();
        if (cam != null)
        {
            cam.fieldOfView = fieldOfView;
            var rot = Quaternion.Euler(pitch, 0f, 0f);   // looking north, tilted down
            Vector3 pos = c + Vector3.up * h - (rot * Vector3.forward) * 0f;
            pos -= Vector3.forward * h / Mathf.Tan(pitch * Mathf.Deg2Rad);   // step back so the tower sits mid-frame
            cam.transform.SetPositionAndRotation(pos, rot);
            TopViewFog(true);
        }
        yield return Fade(1f, 0f);

        // the floor fills, then WorldAwakening rolls the wave out
        startClimb?.Invoke();
        bool started = false;
        for (float t = 0f; t < 12f; t += Time.deltaTime)
        {
            if (world != null && world.WaveRunning) started = true;
            if (started && world != null && !world.WaveRunning) break;
            if (cam != null) cam.transform.position += Vector3.up * Time.deltaTime * 0.8f;   // a slow rise
            yield return null;
        }
        yield return new WaitForSeconds(holdAfterWave);

        // back to the player
        yield return Fade(0f, 1f);
        flyover.Release();
        TopViewFog(false);
        RestoreHud();
        yield return Fade(1f, 0f);
        IsPlaying = false;
        allowClose = true; Close(); allowClose = false;
    }

    protected override bool OnClosing() => allowClose || !IsPlaying;

    IEnumerator Fade(float from, float to)
    {
        for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
        {
            SetBlack(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / fadeSeconds)));
            yield return null;
        }
        SetBlack(to);
    }

    void SetBlack(float a)
    {
        if (fadeImage == null) return;
        var c = Color.black; c.a = a; fadeImage.color = c;
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
