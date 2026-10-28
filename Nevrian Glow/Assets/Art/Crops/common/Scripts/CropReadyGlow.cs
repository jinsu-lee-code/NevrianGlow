using UnityEngine;

/// <summary>
/// Ready-to-harvest signal (2026-10-28). Sits on the last stage prefab of a crop, which the farm shows only when the
/// crop can be harvested. After the grow-up animation a soft warm-yellow effect fades in round the plant, standing out
/// against the violet world (user: "an effect - ready crops glow gently yellow against the purple"):
///   effect  - pf_vfx_crop_ready_glow: a soft breathing halo round the plant and a few warm motes drifting up
///   light   - a small warm point light at its foot pools gold on the soil (Ready Light, no shadows)
/// The plant's own materials are left as they are. It also becomes a CreatureLure, so the night creatures come and
/// hover round it. Fruit Probe (re-harvest crops): ready only while that fruit is grown (lantern fruit shows buds after
/// a harvest). No game code needed. All numbers are my own values.
/// </summary>
public class CropReadyGlow : MonoBehaviour
{
    [Tooltip("Seconds after the prefab appears (grow-up animation) before the glow starts")]
    public float delay = 1.6f;
    [Tooltip("Seconds for the glow to fade in")]
    public float fadeIn = 2.5f;
    [Tooltip("Breath period of the light (s)")]
    public float period = 3.2f;
    [Range(0f, 1f)] public float breathDepth = 0.25f;

    [Tooltip("Ready effect (child particle systems: halo + motes)")]
    public ParticleSystem effect;
    [Header("Light on the soil")]
    public Light readyLight;
    public float lightIntensity = 1.6f;

    [Tooltip("Optional: ready only while this fruit is grown (scale above half of its starting scale)")]
    public Transform fruitProbe;

    ParticleSystem[] parts = new ParticleSystem[0];
    CreatureLure lure;
    float born, k, probeScale;

    void OnEnable()
    {
        born = Time.time;
        k = 0f;
        if (fruitProbe != null) probeScale = Mathf.Max(fruitProbe.localScale.x, 1e-4f);
        lure = GetComponent<CreatureLure>();
        if (lure == null) lure = gameObject.AddComponent<CreatureLure>();
        lure.enabled = false;
        if (readyLight != null) { readyLight.intensity = 0f; readyLight.enabled = false; }
        parts = effect != null ? effect.GetComponentsInChildren<ParticleSystem>(true) : new ParticleSystem[0];
        foreach (var p in parts) { var e = p.emission; e.enabled = false; }
    }

    void Update()
    {
        bool ready = Time.time - born > delay && (fruitProbe == null || fruitProbe.localScale.x > probeScale * 0.5f);
        k = Mathf.MoveTowards(k, ready ? 1f : 0f, Time.deltaTime / Mathf.Max(fadeIn, 0.01f));
        float e = k * k * (3f - 2f * k);
        float breath = 1f - breathDepth * (0.5f + 0.5f * Mathf.Sin((Time.time - born) * Mathf.PI * 2f / Mathf.Max(period, 0.1f)));

        if (readyLight != null)
        {
            readyLight.enabled = e > 0.01f;
            readyLight.intensity = lightIntensity * e * breath;
        }
        bool on = k > 0.3f;
        foreach (var p in parts)
        {
            var em = p.emission;
            if (em.enabled == on) continue;
            em.enabled = on;
            if (on && !p.isPlaying) p.Play();
        }
        if (lure != null && lure.enabled != (k > 0.5f)) lure.enabled = k > 0.5f;
    }
}
