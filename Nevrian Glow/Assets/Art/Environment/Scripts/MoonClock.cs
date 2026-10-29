using NevrianGlowFarm.Core;
using NevrianGlowFarm.Logic.Game;
using UnityEngine;

/// <summary>
/// The moon is the day's clock (2026-10-29, story: the planet is tidally locked, so the sun never moves - a "day" is
/// one trip of the moon across the sky). It rises in the morning shop phase, stands high while farming, sinks during
/// the sales and sets below the horizon for the night (settlement); the next day it rises again.
/// Listens to the game's phase events (GameEvents.PhaseChanged) and only moves MoonModel's placement
/// (oppositeElevation / oppositeAzimuthOffset) - no game code, the sun and the sky stay as they are.
/// Path: t = 0 rising edge .. 0.5 highest .. 1 setting edge; below 0 / above 1 = under the horizon.
/// All numbers are my own values.
/// </summary>
[ExecuteAlways]
public class MoonClock : MonoBehaviour
{
    public MoonModel moon;

    [Header("Path across the sky")]
    [Tooltip("Azimuth offset (deg, MoonModel) where the moon rises / sets")]
    public float riseAzimuth = -70f;
    public float setAzimuth = 70f;
    [Tooltip("Height at the top of the path (deg)")]
    public float peakElevation = 38f;
    [Tooltip("Height when it is under the horizon (deg, negative)")]
    public float belowElevation = -10f;

    [Header("Where the moon is in each phase (0 rise .. 1 set)")]
    public float shop = 0.08f;
    public float farming = 0.32f;
    public float cleanDisplay = 0.55f;
    [Tooltip("Sales: it sinks slowly from Clean/Display towards this while the customers come")]
    public float sales = 0.88f;
    [Tooltip("Settlement and skills: the night, under the horizon")]
    public float night = 1.12f;

    [Header("Motion")]
    [Tooltip("Seconds to glide to a new phase position")]
    public float glideSeconds = 5f;
    [Tooltip("Seconds of the slow sink during the sales")]
    public float salesSeconds = 60f;

    [Header("Editor preview")]
    [Tooltip("Move the moon along its path in the editor (writes MoonModel's placement while on)")]
    public bool preview;
    [Range(-0.2f, 1.2f)] public float previewT = 0.32f;

    float t = -1f, target, speed;
    float savedElevation, savedAzimuth;
    bool saved, wasPreview;

    void OnEnable()
    {
        GameEvents.PhaseChanged += OnPhaseChanged;
        if (moon != null && !saved) { savedElevation = moon.oppositeElevation; savedAzimuth = moon.oppositeAzimuthOffset; saved = true; }
    }

    void OnDisable()
    {
        GameEvents.PhaseChanged -= OnPhaseChanged;
        // editor: give the moon back its authored placement
        if (!Application.isPlaying && wasPreview && moon != null && saved) { moon.oppositeElevation = savedElevation; moon.oppositeAzimuthOffset = savedAzimuth; }
    }

    void Start()
    {
        if (!Application.isPlaying) return;
        var gm = GameManager.Instance;
        t = target = PositionOf(gm != null ? gm.Phase : DayPhase.Shop);
        speed = 1f / Mathf.Max(glideSeconds, 0.01f);
        Place(t);
    }

    void OnPhaseChanged(PhaseChange change)
    {
        if (change.DayChanged) t = -Mathf.Abs(night - 1f);   // a new day: it comes back up from under the rising edge
        target = PositionOf(change.Phase);
        bool sinking = change.Phase == DayPhase.Sales;
        speed = Mathf.Abs(target - t) / Mathf.Max(sinking ? salesSeconds : glideSeconds, 0.01f);
    }

    float PositionOf(DayPhase phase)
    {
        switch (phase)
        {
            case DayPhase.Shop: return shop;
            case DayPhase.Farming: return farming;
            case DayPhase.CleanDisplay: return cleanDisplay;
            case DayPhase.Sales: return sales;
            default: return night;
        }
    }

    void Update()
    {
        if (moon == null) return;
        if (!Application.isPlaying)
        {
            if (preview) Place(previewT);
            else if (wasPreview && saved) { moon.oppositeElevation = savedElevation; moon.oppositeAzimuthOffset = savedAzimuth; }
            if (!preview && !wasPreview && moon != null) { savedElevation = moon.oppositeElevation; savedAzimuth = moon.oppositeAzimuthOffset; saved = true; }
            wasPreview = preview;
            return;
        }
        if (t < -0.9f) return;   // not started yet
        t = Mathf.MoveTowards(t, target, speed * Time.deltaTime);
        Place(t);
    }

    /// <summary>Moon at path position t (0 rise .. 1 set; outside = under the horizon).</summary>
    public void Place(float pathT)
    {
        if (moon == null) return;
        float k = Mathf.Clamp01(pathT);
        moon.oppositeAzimuthOffset = Mathf.LerpUnclamped(riseAzimuth, setAzimuth, pathT);
        // a smooth arc: up from the horizon, highest in the middle, down again; under the horizon past the ends
        float over = pathT < 0f ? -pathT : pathT > 1f ? pathT - 1f : 0f;
        moon.oppositeElevation = over > 0f
            ? Mathf.Lerp(0f, belowElevation, Mathf.Clamp01(over / 0.1f))
            : Mathf.Sin(k * Mathf.PI) * peakElevation;
    }
}
