using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Draws DSEWA/MoonHalo just behind the moon for every camera, the same way MoonModel draws the moon, and only while it
/// shows. Numbers are mine.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MoonModel))]
public class MoonHalo : MonoBehaviour
{
    public Material material;
    [Tooltip("Size of the glow as a multiple of the moon's")]
    [Range(1.2f, 8f)] public float size = 3f;

    static readonly int CentreId = Shader.PropertyToID("_HaloCentre");
    static readonly int MoonShareId = Shader.PropertyToID("_MoonShare");
    MoonModel moon;
    Mesh quad;
    MaterialPropertyBlock block;

    void OnEnable()
    {
        moon = GetComponent<MoonModel>();
        quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        block = new MaterialPropertyBlock();
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
    }

    void OnDisable() => RenderPipelineManager.beginCameraRendering -= OnBeginCamera;

    void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
    {
        if (material == null || quad == null || cam.cameraType == CameraType.Preview) return;
        if (moon.nightVolume != null && moon.nightVolume.weight < moon.visibleFrom) return;

        Vector3 eye = cam.transform.position;
        float away = Mathf.Min(moon.distance, cam.farClipPlane * 0.94f);   // the moon itself sits at 0.92 x far at most
        float radius = away * Mathf.Tan(moon.angularDiameter * 0.5f * Mathf.Deg2Rad) * size;
        Vector3 centre = eye + moon.SkyDirection() * away;

        block.SetVector(CentreId, new Vector4(centre.x, centre.y, centre.z, radius));
        block.SetFloat(MoonShareId, 0.8f / size);
        var rp = new RenderParams(material)
        {
            camera = cam,
            matProps = block,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            worldBounds = new Bounds(eye, Vector3.one * moon.distance * 4f),
            layer = gameObject.layer
        };
        Graphics.RenderMesh(rp, quad, 0, Matrix4x4.identity);
    }
}
