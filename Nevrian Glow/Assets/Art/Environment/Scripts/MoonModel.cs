using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Draws a moon model (any mesh, e.g. a Blender FBX) in the sky along the moonlight direction (-light.forward),
/// or with Opposite Sun on, on the far side of the sky from it (the sky shader keeps its sun disc on the light),
/// at a real distance, sized by its angular diameter. Drawn per camera right before it renders (Graphics.RenderMesh),
/// so it also shows in the Scene view and no scene object moves (the scene does not get dirty).
/// Shown only at night: the night post volume's weight (driven by DayNightBlend) must reach Visible From.
/// Material DSEWA/ToonMoon: the component feeds _PhaseDir (artistic phase light, the real light sits behind the moon)
/// and _CameraShrink (fit inside the far plane). To use your own model: assign its mesh to Mesh.
/// </summary>
[ExecuteAlways]
public class MoonModel : MonoBehaviour
{
    public Mesh mesh;
    public Material material;
    public Light moonLight;
    [Tooltip("Night post volume (its weight = how much it is night)")]
    public Volume nightVolume;
    [Range(0f, 1f)] public float visibleFrom = 0.6f;

    [Header("Placement")]
    [Tooltip("Real distance in metres (the shader shrinks it inside the camera's far plane)")]
    public float distance = 4000f;
    [Tooltip("Size on screen: angular diameter in degrees (the real moon is about 0.5)")]
    public float angularDiameter = 6f;
    [Tooltip("Model size in its own units that should fill that diameter (1 for a Unity sphere)")]
    public float modelDiameter = 1f;
    [Tooltip("Put the moon on the far side of the sky from the light (opposite azimuth), so the sky's sun disc and " +
             "the moon are both visible - sun low on one horizon, moon on the other")]
    public bool oppositeSun = false;
    [Tooltip("Height of the moon above the horizon when Opposite Sun is on, degrees")]
    [Range(5f, 80f)] public float oppositeElevation = 25f;
    [Tooltip("Turn the moon this many degrees away from the exact opposite azimuth. Exactly opposite, the sun lights " +
             "the moon head-on (a flat full moon); turned aside, it is lit from the side and the terminator shows")]
    [Range(-90f, 90f)] public float oppositeAzimuthOffset = 0f;
    [Tooltip("Extra rotation of the model (which face looks at the camera)")]
    public Vector3 modelRotation = Vector3.zero;
    [Header("Animation (subtle)")]
    [Tooltip("Spin around the moon's own vertical axis, degrees per second (craters drift across the face)")]
    public float spinSpeed = 2f;
    [Tooltip("Floating: up / down drift in degrees on screen")]
    public float bobAmplitude = 0.15f;
    [Tooltip("Floating period in seconds")]
    public float bobPeriod = 8f;
    [Tooltip("Limb glow breathing: +- fraction of the glow")]
    [Range(0f, 1f)] public float glowPulse = 0.25f;
    [Tooltip("Glow breathing period in seconds")]
    public float glowPeriod = 5f;
    [Tooltip("Animate in the editor too (Scene / Game view), not only in play mode")]
    public bool animateInEditor = true;

    [Header("Sky clouds in front")]
    [Tooltip("How much the sky's painted clouds cover the moon (1 = fully, like the planets; lower = it glows through)")]
    [Range(0f, 1f)] public float cloudCover = 0.75f;

    [Header("Phase")]
    [Tooltip("With Opposite Sun on: shade the moon from the real light direction (the sun), so the terminator follows " +
             "the light and not the camera. Off = the artistic phase below")]
    public bool phaseFromLight = true;
    [Tooltip("Artistic phase (used when the light sits behind the moon): 0 = full moon, 90 = half, 150 = thin crescent")]
    [Range(0f, 175f)] public float phaseAngle = 35f;
    [Tooltip("Direction of the lit side on screen, degrees (0 = right, 90 = up)")]
    public float phaseRotation = 160f;

    static readonly int PhaseId = Shader.PropertyToID("_PhaseDir");
    static readonly int ShrinkId = Shader.PropertyToID("_CameraShrink");
    static readonly int GlowId = Shader.PropertyToID("_GlowMul");
    static readonly int CloudLowId = Shader.PropertyToID("_PanoCloudLow");
    static readonly int CloudHighId = Shader.PropertyToID("_PanoCloudHigh");
    static readonly int CloudParamsId = Shader.PropertyToID("_SkyCloudParams");
    static readonly int CloudSpeedId = Shader.PropertyToID("_PanoCloudSpeed");
    static readonly int CloudTintId = Shader.PropertyToID("_SkyCloudTint");
    // moon on screen for the light-shaft pass (DSEWA/LightShafts): xy = viewport position, z = 1 while the moon is drawn
    static readonly int MoonScreenId = Shader.PropertyToID("_DSEWA_MoonScreen");
    MaterialPropertyBlock mpb;

    static float EditorTime()
    {
#if UNITY_EDITOR
        return (float)UnityEditor.EditorApplication.timeSinceStartup;
#else
        return Time.time;
#endif
    }

#if UNITY_EDITOR
    // keep the editor views repainting so the animation is visible outside play mode
    void Update()
    {
        if (!Application.isPlaying && animateInEditor) UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
    }
#endif

    void OnEnable() => RenderPipelineManager.beginCameraRendering += OnBeginCamera;
    void OnDisable() => RenderPipelineManager.beginCameraRendering -= OnBeginCamera;

    void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
    {
        Shader.SetGlobalVector(MoonScreenId, Vector4.zero);   // no shafts unless the moon is drawn for this camera
        if (mesh == null || material == null || moonLight == null || cam.cameraType == CameraType.Preview) return;
        if (nightVolume != null && nightVolume.weight < visibleFrom) return;

        Vector3 d = -moonLight.transform.forward;
        if (oppositeSun)
        {
            Vector3 flat = new Vector3(-d.x, 0f, -d.z);
            if (flat.sqrMagnitude < 1e-6f) flat = Vector3.forward;
            flat = Quaternion.AngleAxis(oppositeAzimuthOffset, Vector3.up) * flat;
            float e = oppositeElevation * Mathf.Deg2Rad;
            d = flat.normalized * Mathf.Cos(e) + Vector3.up * Mathf.Sin(e);
        }
        Vector3 eye = cam.transform.position;
        float size = 2f * distance * Mathf.Tan(angularDiameter * 0.5f * Mathf.Deg2Rad) / Mathf.Max(modelDiameter, 1e-4f);
        float time = Application.isPlaying ? Time.time : (animateInEditor ? EditorTime() : 0f);
        // floating: tilt the direction a little around the horizontal axis
        Vector3 bobAxis = Vector3.Cross(Vector3.up, d);
        if (bobAxis.sqrMagnitude < 1e-6f) bobAxis = cam.transform.right;
        float bob = bobAmplitude * Mathf.Sin(time * 2f * Mathf.PI / Mathf.Max(bobPeriod, 0.1f));
        Vector3 dPos = Quaternion.AngleAxis(bob, bobAxis.normalized) * d;
        // spin around the moon's own up axis (craters drift across the face)
        Quaternion rot = Quaternion.LookRotation(-d, Vector3.up) * Quaternion.Euler(0, time * spinSpeed, 0) * Quaternion.Euler(modelRotation);
        Matrix4x4 m = Matrix4x4.TRS(eye + dPos * distance, rot, Vector3.one * size) * Matrix4x4.Translate(-mesh.bounds.center);
        Vector3 vp = cam.WorldToViewportPoint(eye + dPos * 1000f);
        Shader.SetGlobalVector(MoonScreenId, new Vector4(vp.x, vp.y, vp.z > 0f ? 1f : 0f, 0f));
        float glow = 1f + glowPulse * Mathf.Sin(time * 2f * Mathf.PI / Mathf.Max(glowPeriod, 0.1f));

        // phase light: from the camera side, tilted towards the lit-side direction on screen
        Vector3 right = Vector3.Cross(Vector3.up, -d).normalized;
        if (right.sqrMagnitude < 1e-6f) right = cam.transform.right;
        Vector3 up = Vector3.Cross(-d, right);
        float pa = phaseAngle * Mathf.Deg2Rad, pr = phaseRotation * Mathf.Deg2Rad;
        Vector3 phase = (Mathf.Cos(pa) * -d + Mathf.Sin(pa) * (Mathf.Cos(pr) * right + Mathf.Sin(pr) * up)).normalized;
        // real light: the sun is on the other side of the sky, so it lights the moon like the ringed planet is lit
        if (oppositeSun && phaseFromLight) phase = -moonLight.transform.forward;

        if (mpb == null) mpb = new MaterialPropertyBlock();
        mpb.SetVector(PhaseId, phase);
        mpb.SetFloat(ShrinkId, Mathf.Min(1f, cam.farClipPlane * 0.92f / distance));
        mpb.SetFloat(GlowId, glow);
        CopySkyClouds();
        var rp = new RenderParams(material)
        {
            camera = cam,
            matProps = mpb,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            worldBounds = new Bounds(eye, Vector3.one * distance * 4f),   // never culled by the real (far) position
            layer = gameObject.layer
        };
        for (int s = 0; s < mesh.subMeshCount; s++) Graphics.RenderMesh(rp, mesh, s, m);
    }

    // the skybox's painted cloud layers (DSEWA/ToonSky), so the moon sits behind them like the sky's own planets
    // (2026-10-28). Read every frame: DayNightBlend changes the sky material.
    void CopySkyClouds()
    {
        var sky = RenderSettings.skybox;
        if (sky == null || !sky.HasProperty(CloudLowId) || sky.GetTexture(CloudLowId) == null)
        {
            mpb.SetVector(CloudParamsId, Vector4.zero);
            return;
        }
        mpb.SetTexture(CloudLowId, sky.GetTexture(CloudLowId));
        mpb.SetTexture(CloudHighId, sky.GetTexture(CloudHighId) != null ? sky.GetTexture(CloudHighId) : Texture2D.blackTexture);
        mpb.SetVector(CloudParamsId, new Vector4(sky.GetFloat("_PanoAmount"), sky.GetFloat("_PanoHorizon"), sky.GetFloat("_PanoRotation"),
                                                 (sky.HasProperty("_PanoCloudFront") ? sky.GetFloat("_PanoCloudFront") : 1f) * cloudCover));
        mpb.SetVector(CloudSpeedId, sky.GetVector("_PanoCloudSpeed"));
        Color tint = sky.GetColor("_PanoTint");
        if (sky.HasProperty("_OverallColor")) tint *= sky.GetColor("_OverallColor");
        mpb.SetColor(CloudTintId, tint);
    }
}
