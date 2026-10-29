using System.Collections;
using NevrianGlowFarm.Core;
using UnityEngine;

/// <summary>
/// Level = lighthouse floors (2026-10-29, demo story S3 + clash fix 1). The level goes up at the settlement, inside the
/// shop, where the tower cannot be seen - so the level-up is only remembered, and the next morning, a moment after the
/// player arrives at the farm, the tower lights one floor more (read as "it lit up overnight").
/// A new game or a loaded save puts the tower at the current level straight away, without the climb or the sound.
/// Listens to the game's events only (LeveledUp, DayChanged, ZoneArrived) - no game code.
/// Progress = (level - levelAtStart) / (levelAtTop - levelAtStart). All numbers are my own values.
/// </summary>
public class LighthouseProgressLink : MonoBehaviour
{
    [Tooltip("Empty = the lighthouse in the loaded scenes (LighthouseGoal.Active)")]
    public LighthouseGoal lighthouse;

    [Header("Levels")]
    [Tooltip("Level with the tower dark")] public int levelAtStart = 1;
    [Tooltip("Level with every window floor lit (the orb on top waits for the ending)")] public int levelAtTop = 6;
    [Tooltip("Keep the orb dark until the ending even at the top level")] [Range(0f, 1f)] public float maxBeforeEnding = 0.99f;

    [Header("Morning climb")]
    [Tooltip("Zone where the day starts and the tower is in view")] public string farmZoneId = "zone_farm";
    [Tooltip("Seconds after arriving at the farm before the floor lights (after the fade in)")] public float delaySeconds = 1.5f;

    int pendingLevel = -1;

    LighthouseGoal Target => lighthouse != null ? lighthouse : LighthouseGoal.Active;

    void OnEnable()
    {
        GameEvents.LeveledUp += OnLeveledUp;
        GameEvents.DayChanged += OnDayChanged;
        GameEvents.ZoneArrived += OnZoneArrived;
    }

    void OnDisable()
    {
        GameEvents.LeveledUp -= OnLeveledUp;
        GameEvents.DayChanged -= OnDayChanged;
        GameEvents.ZoneArrived -= OnZoneArrived;
    }

    void Start() => SyncNow();

    float ProgressOf(int level) =>
        Mathf.Min(Mathf.Clamp01((level - levelAtStart) / (float)Mathf.Max(1, levelAtTop - levelAtStart)), maxBeforeEnding);

    void OnLeveledUp(int newLevel, int gained) => pendingLevel = newLevel;

    // new game, loaded save or a new day: without a level-up waiting, just show where the tower should be
    void OnDayChanged(int day)
    {
        if (pendingLevel < 0) SyncNow();
    }

    void OnZoneArrived(string zoneId)
    {
        if (zoneId != farmZoneId || pendingLevel < 0) return;
        int level = pendingLevel;
        pendingLevel = -1;
        StartCoroutine(Climb(level));
    }

    IEnumerator Climb(int level)
    {
        yield return new WaitForSeconds(delaySeconds);
        var t = Target;
        if (t != null && !t.IsEnding) t.SetProgress(ProgressOf(level));
    }

    void SyncNow()
    {
        var t = Target; var gm = GameManager.Instance;
        if (t == null || gm == null || t.IsEnding) return;
        t.SetProgressImmediate(ProgressOf(gm.Level));
    }

    [ContextMenu("Preview: Level Up Now (climbs on next farm arrival)")]
    void PreviewLevelUp() { var gm = GameManager.Instance; pendingLevel = (gm != null ? gm.Level : levelAtStart) + 1; }
}
