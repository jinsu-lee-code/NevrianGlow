using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Sleeping grandma (2026-10-29, story: the last gardener spent her light and sleeps grey in the shop; she wakes when
/// the lighthouse is lit). Her grey lifts from the feet up as the lighthouse lights floor by floor, and at full light
/// she wakes (a small hop, onWake). Stand-in model for now: a ball (user: "make a ball to stand in for grandma").
/// Progress comes from the lighthouse when one is loaded (LighthouseGoal.Active), otherwise from Wake below (preview).
/// Writes only MaterialPropertyBlocks on DSEWA/ToonProp (reveal by height + _RevealGrey).
/// All numbers are my own values.
/// </summary>
[ExecuteAlways]
public class GrandmaSleep : MonoBehaviour
{
    [Tooltip("Renderers that turn from grey to colour (ToonProp materials)")]
    public Renderer[] body = new Renderer[0];
    [Tooltip("Preview / fallback when no lighthouse is loaded: 0 asleep, 1 awake")]
    [Range(0f, 1f)] public float wake;
    [Tooltip("Follow the lighthouse's lit progress when one is loaded")]
    public bool followLighthouse = true;

    [Header("Look")]
    [Tooltip("Asleep colour multiply (after desaturating)")] public Color greyColor = new Color(0.62f, 0.62f, 0.66f);
    [Range(0f, 1f)] public float desaturate = 1f;
    public float revealSoft = 0.15f;
    [Tooltip("Slow breathing while asleep (scale)")] public float breath = 0.025f;

    [Header("Waking")]
    [Tooltip("Wakes when progress reaches this")] [Range(0.5f, 1f)] public float wakeAt = 1f;
    public float hopHeight = 0.25f;
    public UnityEvent onWake;

    static readonly int RevealYId = Shader.PropertyToID("_RevealY");
    static readonly int RevealSoftId = Shader.PropertyToID("_RevealSoft");
    static readonly int RevealDarkId = Shader.PropertyToID("_RevealDark");
    static readonly int RevealGreyId = Shader.PropertyToID("_RevealGrey");
    static readonly int RevealFlickerId = Shader.PropertyToID("_RevealFlicker");

    MaterialPropertyBlock mpb;
    Vector3 baseScale, basePos;
    float awakeTime = -1f;

    public float Progress => followLighthouse && LighthouseGoal.Active != null ? LighthouseGoal.Active.ShownProgress : wake;
    public bool IsAwake => awakeTime >= 0f;

    void OnEnable()
    {
        mpb ??= new MaterialPropertyBlock();
        baseScale = transform.localScale;
        basePos = transform.localPosition;
        awakeTime = -1f;
    }

    void OnDisable()
    {
        foreach (var r in body) if (r != null) r.SetPropertyBlock(null);
        transform.localScale = baseScale;
        transform.localPosition = basePos;
    }

    void Update()
    {
        float p = Mathf.Clamp01(Progress / Mathf.Max(wakeAt, 0.01f));
        foreach (var r in body) if (r != null) Write(r, p);
        if (!Application.isPlaying) return;

        if (awakeTime < 0f && p >= 1f - 1e-4f) { awakeTime = Time.time; onWake?.Invoke(); }
        if (awakeTime < 0f)
        {
            // asleep: slow breathing
            float b = Mathf.Sin(Time.time * Mathf.PI * 2f / 4f) * breath;
            transform.localScale = Vector3.Scale(baseScale, new Vector3(1f + b, 1f - b, 1f + b));
        }
        else
        {
            // waking: one small hop with a squash on landing, then rest
            float t = Time.time - awakeTime;
            float hop = t < 0.6f ? Mathf.Sin(t / 0.6f * Mathf.PI) * hopHeight : 0f;
            float squash = t >= 0.6f && t < 0.9f ? Mathf.Sin((t - 0.6f) / 0.3f * Mathf.PI) * 0.12f : 0f;
            transform.localPosition = basePos + Vector3.up * hop;
            transform.localScale = Vector3.Scale(baseScale, new Vector3(1f + squash, 1f - squash, 1f + squash));
        }
    }

    void Write(Renderer r, float p)
    {
        var b = r.bounds;
        r.GetPropertyBlock(mpb);
        mpb.SetFloat(RevealYId, Mathf.Lerp(b.min.y - revealSoft, b.max.y + revealSoft, p));
        mpb.SetFloat(RevealSoftId, revealSoft);
        mpb.SetColor(RevealDarkId, greyColor);
        mpb.SetFloat(RevealGreyId, desaturate);
        mpb.SetFloat(RevealFlickerId, 0f);
        r.SetPropertyBlock(mpb);
    }

    [ContextMenu("Preview: Asleep")] void PreviewAsleep() { followLighthouse = false; wake = 0f; }
    [ContextMenu("Preview: Half")] void PreviewHalf() { followLighthouse = false; wake = 0.5f; }
    [ContextMenu("Preview: Awake")] void PreviewAwake() { followLighthouse = false; wake = 1f; }
}
