using UnityEngine;

/// <summary>
/// Glare fatigue (2026-10-27, user: "the pretty post effect hurts the eyes if you keep looking at it; after a few
/// seconds of looking it should stop showing"). Watches the game camera: while it keeps looking at the same place
/// (it barely turns or moves) a timer runs, and after <see cref="holdDelay"/> seconds the star-glare rays
/// (Hidden/DSEWA/StarGlare) fade out over <see cref="fadeTime"/>. As soon as the view moves they come back.
/// Drives the global _DSEWA_GlareDim (0 = full rays, 1 = gone); the glare material itself is not changed.
/// All numbers are my own values.
/// </summary>
public class GlareFatigue : MonoBehaviour
{
    [Tooltip("Seconds of looking at the same place before the rays start to fade")]
    public float holdDelay = 3f;
    [Tooltip("Seconds the fade-out takes")]
    public float fadeTime = 2f;
    [Tooltip("Seconds the rays take to come back once the view moves")]
    public float recoverTime = 0.6f;
    [Tooltip("Turning faster than this (degrees / s) counts as looking somewhere new")]
    public float turnThreshold = 8f;
    [Tooltip("Moving faster than this (m / s) counts as looking somewhere new")]
    public float moveThreshold = 0.8f;
    [Tooltip("How far the rays fade (1 = gone completely)")]
    [Range(0f, 1f)] public float maxDim = 1f;

    static readonly int DimId = Shader.PropertyToID("_DSEWA_GlareDim");
    Vector3 lastPos, lastFwd;
    Camera lastCam;
    float still, dim;

    void OnDisable() => Shader.SetGlobalFloat(DimId, 0f);

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) { Shader.SetGlobalFloat(DimId, 0f); return; }
        var t = cam.transform;
        float dt = Mathf.Clamp(Time.unscaledDeltaTime, 1e-4f, 0.1f);   // a long loading frame is not "looking"
        if (cam != lastCam) { lastCam = cam; lastPos = t.position; lastFwd = t.forward; still = 0f; }
        float turn = Vector3.Angle(lastFwd, t.forward) / dt;
        float move = Vector3.Distance(lastPos, t.position) / dt;
        lastPos = t.position; lastFwd = t.forward;

        if (turn > turnThreshold || move > moveThreshold) still = 0f; else still += dt;
        float target = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((still - holdDelay) / Mathf.Max(fadeTime, 0.01f))) * maxDim;
        // fading out follows the timer; coming back is quick
        dim = target < dim ? Mathf.MoveTowards(dim, target, dt / Mathf.Max(recoverTime, 0.01f)) : target;
        Shader.SetGlobalFloat(DimId, dim);
    }
}
