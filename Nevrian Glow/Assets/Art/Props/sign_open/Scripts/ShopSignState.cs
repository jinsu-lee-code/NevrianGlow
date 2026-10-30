using NevrianGlowFarm.Core;
using NevrianGlowFarm.Logic.Game;
using UnityEngine;

/// <summary>
/// Hanging OPEN / CLOSED sign in the shop (2026-10-30, user: "the player flips the sign, so the player has to read it").
/// The side facing the player tells the shop state: CLOSED until the player opens the shop (E on the sign, the game's
/// OpenSign), OPEN while selling, CLOSED again after. Reads GameManager's phase only - no game code calls this - and drives
/// ac_prop_sign_open's IsClosed (the board flips with @open / @close). On load / scene start it snaps to the right side
/// without the flip. The idle sway is a small turn of the swing pivot done here (the @sway clip as an additive layer
/// also re-posed the baked ropes and left them twisted on the OPEN side).
/// </summary>
public class ShopSignState : MonoBehaviour
{
    [Tooltip("Animator on the sign model (ac_prop_sign_open)")] public Animator animator;
    [Tooltip("Phases that show OPEN")] public DayPhase[] openPhases = { DayPhase.Sales };
    [Tooltip("Swing pivot (prop_sign_open_swing); its local Y turns for the idle sway")] public Transform swing;
    [Tooltip("Idle sway (degrees each way) and its period (s)")] public float swayDegrees = 1f;
    public float swayPeriod = 3f;

    static readonly int IsClosedId = Animator.StringToHash("IsClosed");
    bool known, closed;
    Quaternion swingRest;
    bool haveRest;

    void OnEnable() => known = false;

    void Update()
    {
        if (animator == null) return;
        var game = GameManager.Instance;
        if (game == null || !game.IsStarted) return;
        bool nowClosed = System.Array.IndexOf(openPhases, game.Phase) < 0;
        if (known && nowClosed == closed) return;
        animator.SetBool(IsClosedId, nowClosed);
        if (!known)   // first look (load, entering the shop): show the right side at once
            animator.Play(nowClosed ? "Close" : "Opened", 0, nowClosed ? 1f : 0f);
        closed = nowClosed;
        known = true;
    }

    // The clips leave the swing alone (its constant curve is stripped on import), so the sway is set from the rest pose
    // each frame instead of added on - adding would pile up.
    void LateUpdate()
    {
        if (swing == null || swayDegrees <= 0f) return;
        if (!haveRest) { swingRest = swing.localRotation; haveRest = true; }
        float a = Mathf.Sin(Time.time * Mathf.PI * 2f / Mathf.Max(swayPeriod, 0.1f)) * swayDegrees;
        swing.localRotation = swingRest * Quaternion.Euler(0f, a, 0f);
    }
}
