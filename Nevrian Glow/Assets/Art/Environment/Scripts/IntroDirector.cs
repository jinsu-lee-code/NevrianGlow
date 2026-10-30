using System.Collections;
using System.Collections.Generic;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Systems.Save;
using NevrianGlowFarm.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// New-game intro (lighthouse spec v2 section 3, reworked 2026-10-30, user: the flyover "looks like a battle intro" -
/// farming games open on a quiet arrival, so: fade out, fade in, slowly getting lighter). A few still shots of the grey
/// farm, each fading in from black, drifting a little and fading out (the shots are the child transforms named
/// "Shot.."; the camera looks along their blue axis) -> the last fade-in is slow and lands on the player's eyes ->
/// letter 1 opens. New game only; any key jumps to the last fade-in. No shots = only the slow fade-in on the player.
/// Hooks into the game without changing game code: it is a GameScreen, so while it plays the player input is locked
/// and no other screen opens - the title's "open letter 1" waits and is opened here at the end. Continue also raises
/// NewGameStarted (the game resets, then loads the save over it): a load finishing in that same frame cancels the intro.
/// The game HUD canvases are switched off while it plays and switched back on after. All times are my own values.
/// </summary>
public class IntroDirector : GameScreen
{
    [Header("Intro")]
    [Tooltip("Lends its camera for the still shots (MapFlyover.TakeCamera)")] public MapFlyover flyover;
    [Tooltip("Full-screen black image on the panel")] public Image blackout;
    [Tooltip("Black before the first shot (after the loading screen)")] public float blackHold = 1f;
    [Tooltip("Per shot: fade in from black")] public float shotFadeIn = 1.6f;
    [Tooltip("Per shot: held after the fade in")] public float shotHold = 2.2f;
    [Tooltip("Per shot: fade out to black")] public float shotFadeOut = 1.2f;
    [Tooltip("Metres each shot drifts along its view (a slow push, so the still is alive)")] public float shotDrift = 1.2f;
    [Tooltip("Last fade-in, on the player's eyes - slow, 'slowly getting lighter'")] public float finalFadeIn = 3f;
    public float fieldOfView = 50f;
    [Tooltip("Keys are ignored this long after the start, so the click that started the game does not skip it")] public float skipAfter = 0.6f;
    [Tooltip("Switch the game HUD off while the intro plays")] public bool hideHud = true;

    /// <summary>True while the intro runs (other art scripts can wait for it).</summary>
    public static bool IsPlaying { get; private set; }

    bool allowClose;
    int armedFrame = -1;
    Coroutine run;
    SaveSystem hookedSave;
    readonly List<Canvas> hiddenCanvases = new List<Canvas>();

    void OnEnable() => GameEvents.NewGameStarted += OnNewGame;

    void OnDisable()
    {
        GameEvents.NewGameStarted -= OnNewGame;
        if (IsPlaying) Abort();
        Unhook();
    }

    void OnNewGame()
    {
        if (IsPlaying || !Application.isPlaying) return;
        armedFrame = Time.frameCount;
        hookedSave = SaveSystem.Instance;
        if (hookedSave != null) hookedSave.Finished += OnSaveFinished;
        SetBlack(1f);
        IsPlaying = true;
        Show();   // the title lets itself close while starting; holds letter 1 back
        if (!IsOpen) { IsPlaying = false; Unhook(); return; }
        run = StartCoroutine(Run());
    }

    // a load in the same frame as the reset = Continue, not a new game (the new game's own save also lands in that
    // frame, so only a load counts - its result message says 불러오기 / 불러옴)
    void OnSaveFinished(bool ok, string message)
    {
        if (Time.frameCount == armedFrame && IsPlaying && message != null && message.Contains("불러")) Abort();
    }

    void Unhook()
    {
        if (hookedSave != null) hookedSave.Finished -= OnSaveFinished;
        hookedSave = null;
    }

    IEnumerator Run()
    {
        yield return null;
        Unhook();
        // the loading screen covers the start; begin once it is gone
        while (LoadingScreen.Instance != null && LoadingScreen.Instance.IsShowing) yield return null;
        if (hideHud) HideHud();
        SetBlack(1f);
        float started = Time.unscaledTime;
        bool Skipped() => Time.unscaledTime - started > skipAfter && AnyKey();

        // still shots of the grey farm: fade in, drift, fade out
        var shots = new List<Transform>();
        foreach (Transform c in transform) if (c.name.StartsWith("Shot")) shots.Add(c);
        var cam = shots.Count > 0 && flyover != null ? flyover.TakeCamera() : null;
        bool skip = false;
        for (float t = 0f; t < blackHold && !skip; t += Time.unscaledDeltaTime) { skip = Skipped(); yield return null; }
        if (cam != null)
        {
            cam.fieldOfView = fieldOfView;
            float len = shotFadeIn + shotHold + shotFadeOut;
            for (int i = 0; i < shots.Count && !skip; i++)
            {
                var s = shots[i];
                for (float t = 0f; t < len && !skip; t += Time.unscaledDeltaTime)
                {
                    cam.transform.SetPositionAndRotation(s.position + s.forward * shotDrift * (t / len), s.rotation);
                    float lit = Mathf.Min(Mathf.Clamp01(t / shotFadeIn), Mathf.Clamp01((len - t) / shotFadeOut));
                    SetBlack(1f - Mathf.SmoothStep(0f, 1f, lit));
                    skip = Skipped();
                    yield return null;
                }
            }
            SetBlack(1f);
            flyover.Release();
            yield return null;
        }

        // last: slowly getting lighter on the player's own eyes
        for (float t = 0f; t < finalFadeIn; t += Time.unscaledDeltaTime)
        {
            SetBlack(1f - Mathf.SmoothStep(0f, 1f, t / finalFadeIn));
            yield return null;
        }
        SetBlack(0f);
        Finish();
    }

    void Finish()
    {
        run = null;
        RestoreHud();
        IsPlaying = false;
        allowClose = true; Close(); allowClose = false;
        var letter = OpeningLetterScreen.Instance;
        if (letter != null && !letter.IsOpen) letter.Open();   // letter 1, which the title tried to open at the start
    }

    void Abort()
    {
        if (run != null) StopCoroutine(run);
        run = null;
        if (flyover != null) flyover.Stop();
        RestoreHud();
        SetBlack(0f);
        IsPlaying = false;
        allowClose = true; Close(); allowClose = false;
        Unhook();
    }

    protected override bool OnClosing() => allowClose || !IsPlaying;

    static bool AnyKey()
    {
        var kb = Keyboard.current; var mouse = Mouse.current; var pad = Gamepad.current;
        return (kb != null && kb.anyKey.wasPressedThisFrame)
            || (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
            || (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame));
    }

    void SetBlack(float a)
    {
        if (blackout == null) return;
        var c = blackout.color; c.a = a; blackout.color = c;
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
