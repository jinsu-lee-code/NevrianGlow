using UnityEngine;

/// <summary>
/// A shaft of moonlight falling on one place (DSEWA/MoonBeam), drawn on a box laid along the moon's direction. Follows
/// MoonModel, shows only while the moon does, and dims when a 3D cloud crosses the line to the moon. A slow wander dims it
/// too, so it comes and goes even while no cloud is near. Numbers are mine.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class MoonBeam : MonoBehaviour
{
    public MoonModel moon;
    [Tooltip("Where it lands")]
    public Vector3 target;
    [Tooltip("Radius of the shaft (m)")]
    public float radius = 14f;
    [Tooltip("Length of the box up towards the moon (m)")]
    public float length = 320f;
    [Tooltip("Least it dims to under a cloud")]
    [Range(0f, 1f)] public float coverFloor = 0.15f;
    [Tooltip("Seconds for the slow wander to come and go")]
    public float wanderPeriod = 40f;
    [Range(0f, 1f)] public float wanderAmount = 0.6f;

    static readonly int TargetId = Shader.PropertyToID("_BeamTarget");
    static readonly int AxisId = Shader.PropertyToID("_BeamAxis");
    MeshRenderer view;
    MaterialPropertyBlock block;
    Renderer[] clouds;
    float seed;

    void OnEnable()
    {
        view = GetComponent<MeshRenderer>();
        block = new MaterialPropertyBlock();
        seed = Mathf.Abs(target.x * 0.37f + target.z * 0.11f) % 100f;
        var all = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var list = new System.Collections.Generic.List<Renderer>();
        foreach (var r in all)
            if (r.sharedMaterial != null && r.sharedMaterial.shader.name == "DSEWA/CloudPuff") list.Add(r);
        clouds = list.ToArray();
        Apply();
    }

    void LateUpdate() => Apply();

    void Apply()
    {
        if (moon == null) return;
        Vector3 axis = moon.SkyDirection().normalized;
        transform.SetPositionAndRotation(target + axis * (length * 0.5f - radius), Quaternion.LookRotation(axis));
        transform.localScale = new Vector3(radius * 2.2f, radius * 2.2f, length + radius * 4f);

        float time = Application.isPlaying ? Time.time : 0f;
        float wander = Mathf.PerlinNoise(time / Mathf.Max(wanderPeriod, 0.1f), seed);
        float cover = 1f - wanderAmount * Mathf.SmoothStep(0.35f, 0.7f, wander);
        cover = Mathf.Min(cover, CloudCover(axis));
        bool night = moon.nightVolume == null || moon.nightVolume.weight >= moon.visibleFrom;

        view.GetPropertyBlock(block);
        block.SetVector(TargetId, new Vector4(target.x, target.y, target.z, radius));
        block.SetVector(AxisId, new Vector4(axis.x, axis.y, axis.z, night ? Mathf.Max(cover, coverFloor) : 0f));
        view.SetPropertyBlock(block);
    }

    // 1 = clear, 0 = a cloud sits on the line from the target to the moon
    float CloudCover(Vector3 axis)
    {
        float clear = 1f;
        if (clouds == null) return clear;
        foreach (var r in clouds)
        {
            if (r == null) continue;
            Vector3 toCloud = r.bounds.center - target;
            float along = Vector3.Dot(toCloud, axis);
            if (along <= 0f) continue;
            float offLine = (toCloud - axis * along).magnitude;
            float size = r.bounds.extents.magnitude * 0.35f;   // the box corner reaches well past the cloud itself
            clear = Mathf.Min(clear, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(size * 0.5f, size, offLine)));
        }
        return clear;
    }
}
