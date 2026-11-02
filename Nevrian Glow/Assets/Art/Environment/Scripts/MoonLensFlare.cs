using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Lens flare for the moon. ToonLensFlareFade follows the directional light, but with Opposite Sun on the moon sits on
/// the other side of the sky, so it needs its own. Works the same way: a hidden, unsaved flare object is placed in front
/// of the moon right before every camera renders, so hills and trees hide the flare and the moon model does not. It
/// shows only while the moon does (MoonModel.visibleFrom), and it hands AerialPerspective the moon's direction for the
/// screen rays. Strengths / sizes are my own starting values.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MoonModel))]
public class MoonLensFlare : MonoBehaviour
{
    public LensFlareDataSRP flareData;
    [Range(0f, 3f)] public float intensity = 1f;
    [Range(0.1f, 10f)] public float scale = 4f;
    [Tooltip("Where the flare (and its occlusion test) sits, as a fraction of the camera far plane. Keep it in front of the moon model (it draws at 0.92 x far minus its radius).")]
    [Range(0.1f, 0.95f)] public float distance = 0.8f;
    [Tooltip("Occlusion test radius in metres at that distance")]
    public float occlusionRadius = 25f;
    public bool useOcclusion = true;

    static readonly int MoonDirId = Shader.PropertyToID("_DSEWA_MoonDir");
    MoonModel moon;
    GameObject flareGO;
    LensFlareComponentSRP flare;

    void OnEnable()
    {
        moon = GetComponent<MoonModel>();
        flareGO = new GameObject("MoonFlare (runtime)") { hideFlags = HideFlags.HideAndDontSave };
        flare = flareGO.AddComponent<LensFlareComponentSRP>();
        flare.allowOffScreen = false;
        flare.attenuationByLightShape = false;
        // the flare sits far away: switch off distance attenuation / scaling
        flare.maxAttenuationDistance = 1e6f;
        flare.maxAttenuationScale = 1e6f;
        flare.distanceAttenuationCurve = AnimationCurve.Constant(0f, 1f, 1f);
        flare.scaleByDistanceCurve = AnimationCurve.Constant(0f, 1f, 1f);
        flare.occlusionOffset = 0f;
        flare.sampleCount = 16;
        Apply();
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        Shader.SetGlobalVector(MoonDirId, Vector4.zero);
        if (flareGO) DestroyImmediate(flareGO);
        flareGO = null; flare = null;
    }

    void LateUpdate() => Apply();

    void Apply()
    {
        if (!flare) return;
        flare.lensFlareData = flareData;
        flare.scale = scale;
        flare.useOcclusion = useOcclusion;
        flare.occlusionRadius = occlusionRadius;
        bool visible = moon.nightVolume == null || moon.nightVolume.weight >= moon.visibleFrom;
        flare.intensity = visible ? intensity : 0f;
        // AerialPerspective throws its screen rays from here too
        Vector3 d = moon.SkyDirection();
        Shader.SetGlobalVector(MoonDirId, new Vector4(d.x, d.y, d.z, visible ? 1f : 0f));
    }

    void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
    {
        // same as ToonLensFlareFade: the mirrored water camera must not move the flare
        if (!flareGO || cam.cameraType == CameraType.Reflection || cam.cameraType == CameraType.Preview) return;
        flareGO.transform.position = cam.transform.position + moon.SkyDirection() * (cam.farClipPlane * distance);
    }
}
