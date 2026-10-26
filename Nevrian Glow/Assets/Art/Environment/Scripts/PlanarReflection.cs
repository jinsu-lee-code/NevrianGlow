using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Planar reflection for the lake (2026-10-27, user picked "A" after the probe test: probes cannot mirror the cliffs
/// and the falls on a wide flat lake). Before each game / scene-view camera renders, a hidden camera mirrored about
/// the water plane renders the scene (minus the water layer) into a half-resolution texture with an oblique near plane
/// at the water, so nothing under the surface leaks in. Published for DSEWA/ToonLakeWater:
///   _DSEWA_PlanarReflTex (screen-space mirrored image)   _DSEWA_PlanarOn (1 while valid)
/// The water shader bends the lookup by the wave slope and cuts it into toon steps.
/// Same idea as Unity's Boat Attack planar reflections. All numbers are my own values.
/// </summary>
[ExecuteAlways]
public class PlanarReflection : MonoBehaviour
{
    [Tooltip("World height of the water plane")]
    public float waterLevel = -0.09f;
    [Range(0.1f, 1f)] public float resolutionScale = 0.5f;
    [Tooltip("Layers seen in the reflection (leave the water itself out)")]
    public LayerMask reflectLayers = ~(1 << 4);
    [Tooltip("Reflection camera far clip (m)")]
    public float farClip = 400f;
    [Tooltip("Push the clip plane a little under the surface so the shore line has no gap")]
    public float clipPlaneOffset = 0.05f;

    static readonly int TexId = Shader.PropertyToID("_DSEWA_PlanarReflTex");
    static readonly int OnId = Shader.PropertyToID("_DSEWA_PlanarOn");
    Camera reflCam;
    RenderTexture rt;
    bool rendering;

    void OnEnable() { RenderPipelineManager.beginCameraRendering += OnBeginCamera; }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        Shader.SetGlobalFloat(OnId, 0);
        if (reflCam) DestroyImmediate(reflCam.gameObject);
        if (rt) { if (RenderTexture.active == rt) RenderTexture.active = null; rt.Release(); DestroyImmediate(rt); }
    }

    void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
    {
        if (rendering || cam == reflCam) return;
        if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;
        rendering = true;
        try { Render(ctx, cam); }
        finally { rendering = false; }
    }

    void Render(ScriptableRenderContext ctx, Camera cam)
    {
        // guards (2026-10-27, "Screen position out of view frustum"): a camera at / under the water, orthographic, or
        // with a tiny view gives a degenerate oblique projection; skip the reflection then (the shader falls back to
        // the reflection probe)
        if (cam.orthographic || cam.pixelWidth < 32 || cam.pixelHeight < 32 || cam.transform.position.y <= waterLevel + clipPlaneOffset + 0.05f)
        {
            Shader.SetGlobalFloat(OnId, 0);
            return;
        }
        int w = Mathf.Max(16, (int)(cam.pixelWidth * resolutionScale)), h = Mathf.Max(16, (int)(cam.pixelHeight * resolutionScale));
        if (rt == null || rt.width != w || rt.height != h)
        {
            if (rt) { if (RenderTexture.active == rt) RenderTexture.active = null; rt.Release(); DestroyImmediate(rt); }
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR) { name = "_DSEWA_PlanarReflTex", hideFlags = HideFlags.HideAndDontSave };
            rt.Create();
        }
        if (reflCam == null)
        {
            var go = new GameObject("PlanarReflectionCamera") { hideFlags = HideFlags.HideAndDontSave };
            reflCam = go.AddComponent<Camera>();
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = true;
            data.requiresDepthTexture = false;
            data.requiresColorTexture = false;
            reflCam.enabled = false;
        }
        reflCam.CopyFrom(cam);
        reflCam.cameraType = CameraType.Reflection;
        reflCam.cullingMask = reflectLayers;
        reflCam.farClipPlane = Mathf.Min(farClip, cam.farClipPlane);
        reflCam.targetTexture = rt;
        reflCam.clearFlags = CameraClearFlags.Skybox;

        // mirror about the plane y = waterLevel
        Vector3 n = Vector3.up;
        float d = -waterLevel - clipPlaneOffset;
        Matrix4x4 mirror = Matrix4x4.identity;
        mirror.m00 = 1 - 2 * n.x * n.x; mirror.m01 = -2 * n.x * n.y; mirror.m02 = -2 * n.x * n.z; mirror.m03 = -2 * d * n.x;
        mirror.m10 = -2 * n.y * n.x; mirror.m11 = 1 - 2 * n.y * n.y; mirror.m12 = -2 * n.y * n.z; mirror.m13 = -2 * d * n.y;
        mirror.m20 = -2 * n.z * n.x; mirror.m21 = -2 * n.z * n.y; mirror.m22 = 1 - 2 * n.z * n.z; mirror.m23 = -2 * d * n.z;
        reflCam.worldToCameraMatrix = cam.worldToCameraMatrix * mirror;
        Vector3 camPos = cam.transform.position;
        reflCam.transform.position = new Vector3(camPos.x, 2 * waterLevel - camPos.y, camPos.z);

        // oblique near plane on the water so the underwater part is clipped
        Vector3 pos = new Vector3(0, waterLevel + clipPlaneOffset, 0);
        Matrix4x4 m = reflCam.worldToCameraMatrix;
        Vector3 cpos = m.MultiplyPoint(pos);
        Vector3 cnormal = m.MultiplyVector(n).normalized;
        Vector4 clipPlane = new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
        Matrix4x4 proj = cam.CalculateObliqueMatrix(clipPlane);
        for (int k = 0; k < 16; k++)
            if (float.IsNaN(proj[k]) || float.IsInfinity(proj[k])) { Shader.SetGlobalFloat(OnId, 0); return; }
        reflCam.projectionMatrix = proj;

        bool oldInvert = GL.invertCulling;
        GL.invertCulling = true;
#pragma warning disable CS0618
        UniversalRenderPipeline.RenderSingleCamera(ctx, reflCam);
#pragma warning restore CS0618
        GL.invertCulling = oldInvert;

        Shader.SetGlobalTexture(TexId, rt);
        Shader.SetGlobalFloat(OnId, 1);
    }
}
