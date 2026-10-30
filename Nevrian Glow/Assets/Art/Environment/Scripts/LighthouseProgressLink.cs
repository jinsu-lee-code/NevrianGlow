using System.Collections;
using NevrianGlowFarm.Core;
using UnityEngine;

/// <summary>
/// Level = lighthouse stages (lighthouse spec v2, 2026-10-30: floor 1 · floor 2 · heart). The level goes up at the
/// settlement, inside the shop, where the tower cannot be seen - so the level-up is only remembered, and the next
/// morning, a moment after the player arrives at the farm, the tower lights its next floor (read as "it lit up overnight").
/// Floor 1 at level 4 (day 2 settlement -> day 3 morning), floor 2 at level 9 (day 4 -> day 5 morning); the heart (orb)
/// waits for the ending (LighthouseGoal.PlayEnding). Lit floors split the window strip evenly (2 floors = halves).
/// A new game or a loaded save puts the tower at the current level straight away, without the climb or the sound.
/// Listens to the game's events only (LeveledUp, DayChanged, ZoneArrived) - no game code.
/// Floor levels 4 / 9 and the 1.5 s delay are the planner's values (game_settings lighthouse_floor_levels,
/// lighthouse_light_delay); the rest are my own.
/// </summary>
public class LighthouseProgressLink : MonoBehaviour
{
    [Tooltip("Empty = the lighthouse in the loaded scenes (LighthouseGoal.Active)")]
    public LighthouseGoal lighthouse;

    [Header("Levels")]
    [Tooltip("Level that lights each floor, in order (planner: lighthouse_floor_levels 4;9). The heart waits for the ending")]
    public int[] floorLevels = { 4, 9 };
    [Tooltip("Keep the orb dark until the ending even with every floor lit")] [Range(0f, 1f)] public float maxBeforeEnding = 0.99f;

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

    float ProgressOf(int level)
    {
        if (floorLevels == null || floorLevels.Length == 0) return 0f;
        int lit = 0;
        foreach (int need in floorLevels) if (level >= need) lit++;
        return Mathf.Min(lit / (float)floorLevels.Length, maxBeforeEnding);
    }

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
    void PreviewLevelUp() { var gm = GameManager.Instance; pendingLevel = (gm != null ? gm.Level : 1) + 1; }
}
