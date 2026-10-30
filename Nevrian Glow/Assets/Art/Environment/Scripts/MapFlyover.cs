using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Map flyover (lighthouse spec v2, 2026-10-30): one loop over the map at about 28 m in about 12 s, slowing down in
/// front of the lighthouse. Shared by the new-game intro (grey map, dark tower) and the ending (restored map, lit tower).
/// The path is a closed curve through the child waypoints (drag them in the scene; "Make Ring Waypoints" lays out a
/// fresh ring). Plays on its own camera and switches the player camera off meanwhile (no Cinemachine, no game code):
///   flyover.Play(() => { /* done or skipped */ });   flyover.Skip();
/// 28 m / 12 s are the planner's values (flyover_height, intro_flyover_time); the rest are my own.
/// </summary>
[ExecuteAlways]
public class MapFlyover : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Seconds for the whole loop (planner: intro_flyover_time 12)")] public float duration = 12f;
    [Tooltip("Ease in at the start and out at the end of the loop (0 = constant speed)")] [Range(0f, 0.3f)] public float easeEnds = 0.08f;

    [Header("Look")]
    [Tooltip("Point the camera looks at for most of the loop (map centre on the ground)")] public Vector3 lookCenter = new Vector3(-3f, 6f, -6f);
    [Tooltip("Lighthouse: the camera slows down and turns to it nearby (empty = LighthouseGoal.Active)")] public LighthouseGoal lighthouse;
    [Tooltip("Height on the lighthouse the camera looks at near it (0 = foot, 1 = heart on top)")] [Range(0f, 1f)] public float lighthouseLookHeight = 0.7f;
    [Tooltip("Speed near the lighthouse, as a fraction of the normal speed")] [Range(0.1f, 1f)] public float slowSpeed = 0.3f;
    [Tooltip("Ground distance from the lighthouse where the slow-down and the turn start")] public float slowRadius = 40f;
    public float fieldOfView = 55f;

    [Header("Ring (Make Ring Waypoints)")]
    [Tooltip("Flight height (planner: flyover_height 28)")] public float height = 28f;
    public Vector2 ringRadius = new Vector2(58f, 46f);
    [Tooltip("Waypoints closer than this (ground distance) to the lighthouse are pushed out, so the tower never fills the frame")] public float keepAway = 34f;
    [Tooltip("Where the loop starts, degrees clockwise from north (90 = east; the lighthouse in the north then comes at 3/4)")] public float startAngle = 90f;
    [Range(4, 16)] public int ringPoints = 8;

    [Header("Editor preview")]
    [Tooltip("Moves the preview camera gizmo along the loop (0..1 of the time)")] [Range(0f, 1f)] public float preview;

    public bool IsPlaying { get; private set; }

    const int Samples = 600;
    Vector3[] pts; float[] timeAt;   // sampled curve and its normalised time (slow-down included)
    Camera cam, hiddenMain;
    bool skip;

    // ---------- playback ----------

    /// <summary>Fly the loop. keepCamera: stay on the flyover camera when done (the caller moves it on and calls Release).</summary>
    public void Play(Action onDone = null, bool keepCamera = false)
    {
        if (!Application.isPlaying || IsPlaying) return;
        running = StartCoroutine(Run(onDone, keepCamera));
    }

    /// <summary>Stop where it is (e.g. any key in the intro). onDone still fires.</summary>
    public void Skip() => skip = true;

    /// <summary>The flyover camera (while playing or kept) and the player camera it switched off.</summary>
    public Camera FlyCamera => cam;
    public Camera HiddenMain => hiddenMain;

    /// <summary>Switch to the flyover camera without flying (for still shots, e.g. the intro); Release() gives it back.</summary>
    public Camera TakeCamera()
    {
        if (!Application.isPlaying) return null;
        BeginCamera();
        return cam;
    }

    /// <summary>Back to the player camera after Play(.., keepCamera: true) or TakeCamera().</summary>
    public void Release() => EndCamera();

    /// <summary>Cut off at once, no onDone, player camera back.</summary>
    public void Stop()
    {
        if (running != null) StopCoroutine(running);
        running = null;
        EndCamera();
        IsPlaying = false;
    }

    Coroutine running;

    IEnumerator Run(Action onDone, bool keepCamera)
    {
        IsPlaying = true; skip = false;
        Bake();
        BeginCamera();
        for (float t = 0f; t < duration && !skip; t += Time.unscaledDeltaTime)
        {
            Pose(t / duration, out var p, out var r);
            cam.transform.SetPositionAndRotation(p, r);
            yield return null;
        }
        if (!keepCamera) EndCamera();
        IsPlaying = false;
        running = null;
        onDone?.Invoke();
    }

    void BeginCamera()
    {
        if (cam == null)
        {
            var go = new GameObject("FlyoverCamera");
            go.transform.SetParent(transform, false);
            cam = go.AddComponent<Camera>();
        }
        hiddenMain = Camera.main;
        if (hiddenMain != null && hiddenMain != cam)
        {
            cam.CopyFrom(hiddenMain);
            hiddenMain.enabled = false;   // its AudioListener stays on
        }
        var src = hiddenMain != null ? hiddenMain.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() : null;
        var dst = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>()
                  ?? cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        dst.renderPostProcessing = true;
        if (src != null)
        {
            dst.volumeLayerMask = src.volumeLayerMask;
            dst.antialiasing = src.antialiasing;
        }
        cam.fieldOfView = fieldOfView;
        cam.farClipPlane = Mathf.Max(cam.farClipPlane, 3000f);
        cam.enabled = true;
    }

    void EndCamera()
    {
        if (cam != null) cam.enabled = false;
        if (hiddenMain != null) hiddenMain.enabled = true;
        hiddenMain = null;
    }

    void OnDisable()
    {
        if (IsPlaying || hiddenMain != null) { EndCamera(); IsPlaying = false; }
    }

    // ---------- path ----------

    LighthouseGoal Tower => lighthouse != null ? lighthouse : LighthouseGoal.Active;

    Vector3 SlowPoint()
    {
        var t = Tower;
        if (t == null) return new Vector3(-5f, 0f, 32f);
        var top = t.HeartPoint();
        var foot = new Vector3(top.x, t.transform.position.y, top.z);
        return Vector3.Lerp(foot, top, lighthouseLookHeight);
    }

    float Near(Vector3 p)
    {
        var s = SlowPoint();
        float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(s.x, s.z));
        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(slowRadius * 0.35f, slowRadius, d));
    }

    static Vector3 CatmullRom(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2f * b + (c - a) * t + (2f * a - 5f * b + 4f * c - d) * t2 + (3f * b - a - 3f * c + d) * t3);
    }

    bool Bake()
    {
        int n = transform.childCount;
        var way = new System.Collections.Generic.List<Vector3>();
        for (int i = 0; i < n; i++) { var c = transform.GetChild(i); if (c.name.StartsWith("WP")) way.Add(c.position); }
        if (way.Count < 3) { pts = null; return false; }
        int m = way.Count;
        pts = new Vector3[Samples + 1];
        for (int i = 0; i <= Samples; i++)
        {
            float u = i / (float)Samples * m;
            int k = Mathf.Min((int)u, m - 1);
            pts[i] = CatmullRom(way[(k - 1 + m) % m], way[k], way[(k + 1) % m], way[(k + 2) % m], u - k);
        }
        // time cost of each step = length / speed; slower near the lighthouse
        timeAt = new float[Samples + 1];
        for (int i = 1; i <= Samples; i++)
        {
            float speed = Mathf.Lerp(1f, slowSpeed, Near((pts[i] + pts[i - 1]) * 0.5f));
            timeAt[i] = timeAt[i - 1] + Vector3.Distance(pts[i], pts[i - 1]) / speed;
        }
        float total = Mathf.Max(timeAt[Samples], 1e-4f);
        for (int i = 0; i <= Samples; i++) timeAt[i] /= total;
        return true;
    }

    float Ease(float x)
    {
        if (easeEnds <= 0f) return x;
        // constant speed in the middle, smooth start and stop over easeEnds of the time
        float e = easeEnds, v = 1f / (1f - e);
        if (x < e) return v * x * x / (2f * e);
        if (x > 1f - e) { float y = 1f - x; return 1f - v * y * y / (2f * e); }
        return v * (x - e * 0.5f);
    }

    /// <summary>Camera pose at a normalised time (0..1) of the loop.</summary>
    public void Pose(float time01, out Vector3 pos, out Quaternion rot)
    {
        if (pts == null && !Bake()) { pos = transform.position; rot = transform.rotation; return; }
        float x = Ease(Mathf.Clamp01(time01));
        int lo = 0, hi = Samples;
        while (hi - lo > 1) { int mid = (lo + hi) / 2; if (timeAt[mid] < x) lo = mid; else hi = mid; }
        float f = Mathf.InverseLerp(timeAt[lo], timeAt[hi], x);
        pos = Vector3.Lerp(pts[lo], pts[hi], f);
        var look = Vector3.Lerp(lookCenter, SlowPoint(), Near(pos));
        rot = Quaternion.LookRotation((look - pos).normalized, Vector3.up);
    }

    // ---------- authoring ----------

    [ContextMenu("Make Ring Waypoints")]
    void MakeRing()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            if (transform.GetChild(i).name.StartsWith("WP")) DestroyImmediate(transform.GetChild(i).gameObject);
        for (int i = 0; i < ringPoints; i++)
        {
            float a = (startAngle + 360f * i / ringPoints) * Mathf.Deg2Rad;
            var wp = new GameObject("WP" + i.ToString("00")).transform;
            wp.SetParent(transform, false);
            var p = new Vector3(lookCenter.x + Mathf.Sin(a) * ringRadius.x, height, lookCenter.z + Mathf.Cos(a) * ringRadius.y);
            var s = SlowPoint(); var away = new Vector3(p.x - s.x, 0f, p.z - s.z);
            if (away.magnitude < keepAway) p += away.normalized * (keepAway - away.magnitude);
            wp.position = p;
        }
        pts = null;
    }

    [ContextMenu("Preview: Play (play mode)")]
    void PreviewPlay() => Play();

    void OnValidate() => pts = null;

    void OnDrawGizmos()
    {
        if (!Bake()) return;
        Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.9f);
        for (int i = 1; i <= Samples; i += 2)
        {
            Gizmos.color = Color.Lerp(new Color(1f, 0.8f, 0.3f), new Color(1f, 0.3f, 0.2f), Near(pts[i]));
            Gizmos.DrawLine(pts[i - 2 < 0 ? 0 : i - 2], pts[i]);
        }
        Pose(preview, out var p, out var r);
        Gizmos.color = Color.white;
        Gizmos.matrix = Matrix4x4.TRS(p, r, Vector3.one);
        Gizmos.DrawFrustum(Vector3.zero, fieldOfView, 12f, 0.3f, 16f / 9f);
        Gizmos.matrix = Matrix4x4.identity;
    }
}
