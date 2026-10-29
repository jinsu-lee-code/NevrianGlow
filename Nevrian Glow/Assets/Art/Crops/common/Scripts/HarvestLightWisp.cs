using UnityEngine;

/// <summary>
/// Harvest light (2026-10-29, user: "harvesting alone should send light flying"): a warm wisp of light rises from the
/// harvested crop, arcs over the farm and sinks into the lighthouse at its lit front, which flares once
/// (LighthouseGoal.Pulse). Spawned by CropReadyGlow when a ready crop is harvested. If the crop's cell is gone by the
/// next frame (the farm scene unloading, not a harvest) the wisp removes itself without flying.
/// All numbers are my own values.
/// </summary>
public class HarvestLightWisp : MonoBehaviour
{
    [Tooltip("Seconds of the first rise before it turns to the lighthouse")]
    public float riseTime = 0.6f;
    public float riseHeight = 1.6f;
    [Tooltip("Travel speed (m/s) and the minimum flight time (s)")]
    public float speed = 14f;
    public float minFlight = 1.6f;
    [Tooltip("Arc height over the straight line, as a fraction of the distance")]
    public float arc = 0.25f;
    [Tooltip("Played where it sinks in (child particle system, not playing on awake)")]
    public ParticleSystem arriveBurst;
    [Tooltip("Played as it leaves the crop (child particle system, not playing on awake)")]
    public ParticleSystem departBurst;

    static LighthouseGoal lighthouse;
    Transform anchor;
    bool checkedAnchor;
    Vector3 start, lift, target;
    float t, flight;
    bool arrived;

    /// <summary>Spawn a wisp at a harvested crop. anchor = the crop's cell (checked next frame).</summary>
    public static void Spawn(GameObject prefab, Vector3 from, Transform anchor)
    {
        if (prefab == null || !Application.isPlaying) return;
        var go = Instantiate(prefab, from, Quaternion.identity);
        var w = go.GetComponent<HarvestLightWisp>();
        if (w != null) w.anchor = anchor;
    }

    void Start()
    {
        start = transform.position;
        lift = start + Vector3.up * riseHeight;
        if (lighthouse == null) lighthouse = FindFirstObjectByType<LighthouseGoal>();
        target = lighthouse != null ? lighthouse.LightFront() : start + Vector3.up * 30f;
        flight = Mathf.Max(minFlight, Vector3.Distance(lift, target) / Mathf.Max(speed, 0.1f));
        // the glow and its trail run for the whole flight; the bursts only play on cue
        foreach (var ps in GetComponentsInChildren<ParticleSystem>())
        {
            if (IsBurst(ps)) continue;
            ps.Play(false);
        }
        if (departBurst != null) departBurst.Play();
    }

    bool IsBurst(ParticleSystem ps) =>
        (departBurst != null && ps.transform.IsChildOf(departBurst.transform)) ||
        (arriveBurst != null && ps.transform.IsChildOf(arriveBurst.transform));

    void Update()
    {
        if (!checkedAnchor)
        {
            checkedAnchor = true;
            if (anchor == null) { Destroy(gameObject); return; }   // unloaded with its cell, not harvested
        }
        if (arrived) return;
        t += Time.deltaTime;
        if (t < riseTime)
        {
            float k = t / riseTime;
            transform.position = Vector3.Lerp(start, lift, 1f - (1f - k) * (1f - k));
            return;
        }
        float u = Mathf.Clamp01((t - riseTime) / flight);
        float e = u * u * (3f - 2f * u);
        Vector3 p = Vector3.Lerp(lift, target, e);
        p += Vector3.up * Mathf.Sin(u * Mathf.PI) * Vector3.Distance(lift, target) * arc;
        transform.position = p;
        if (u >= 1f) Arrive();
    }

    void Arrive()
    {
        arrived = true;
        if (lighthouse != null) lighthouse.Pulse();
        if (arriveBurst != null) { arriveBurst.transform.SetParent(null, true); arriveBurst.Play(); Destroy(arriveBurst.gameObject, 3f); }
        foreach (var ps in GetComponentsInChildren<ParticleSystem>())
            if (!IsBurst(ps)) { var em = ps.emission; em.enabled = false; }
        Destroy(gameObject, 1.2f);   // let the trail and the last sparks fade
    }
}
