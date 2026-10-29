using UnityEngine;

/// <summary>
/// Fake indirect light for an interior (2026-10-27, from the URP light-bake tutorial: a room is filled by baked bounce
/// light, but the shop lives in a team scene whose lighting data we cannot save and ToonSoft does not read lightmaps).
/// Publishes a world box (this object's BoxCollider) and two bounce colours; DSEWA/ToonSoft adds them to every pixel
/// inside the box: faces looking up get the ceiling bounce, faces looking down the floor bounce, fading out over
/// edgeFade metres at the box walls. Disable to remove it. All numbers are my own values.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(BoxCollider))]
public class InteriorFill : MonoBehaviour
{
    [Tooltip("Light bounced down from the ceiling (reaches faces looking up)")]
    public Color ceilingBounce = new Color(0.55f, 0.52f, 0.85f);
    [Tooltip("Light bounced up from the floor (reaches faces looking down / sideways)")]
    public Color floorBounce = new Color(0.72f, 0.50f, 0.36f);
    [Range(0f, 1f)] public float amount = 0.35f;
    [Header("Key (fake light from a point, e.g. the centre orb)")]
    public Transform keyPoint;
    public Color keyColor = new Color(0.82f, 0.80f, 1f);
    [Range(0f, 1.5f)] public float keyAmount = 0f;
    [Tooltip("Fade-out width at the box walls (m)")]
    public float edgeFade = 0.15f;

    static readonly int MinId = Shader.PropertyToID("_DSEWA_InteriorBoxMin");
    static readonly int MaxId = Shader.PropertyToID("_DSEWA_InteriorBoxMax");
    static readonly int UpId = Shader.PropertyToID("_DSEWA_InteriorFillUp");
    static readonly int DownId = Shader.PropertyToID("_DSEWA_InteriorFillDown");
    static readonly int KeyPosId = Shader.PropertyToID("_DSEWA_InteriorKeyPos");
    static readonly int KeyColId = Shader.PropertyToID("_DSEWA_InteriorKeyColor");

    void OnEnable() => Publish();
    void OnValidate() => Publish();
    void Update() => Publish();
    void OnDisable()
    {
        Shader.SetGlobalVector(UpId, Vector4.zero);
        Shader.SetGlobalVector(KeyPosId, Vector4.zero);
    }

    void Publish()
    {
        var box = GetComponent<BoxCollider>();
        Bounds b = box.bounds;
        Shader.SetGlobalVector(MinId, new Vector4(b.min.x, b.min.y, b.min.z, edgeFade));
        Shader.SetGlobalVector(MaxId, new Vector4(b.max.x, b.max.y, b.max.z, 0));
        Shader.SetGlobalVector(UpId, new Vector4(ceilingBounce.r, ceilingBounce.g, ceilingBounce.b, isActiveAndEnabled ? amount : 0));
        Shader.SetGlobalVector(DownId, new Vector4(floorBounce.r, floorBounce.g, floorBounce.b, 0));
        Vector3 k = keyPoint != null ? keyPoint.position : b.center + Vector3.up * b.extents.y * 0.6f;
        Shader.SetGlobalVector(KeyPosId, new Vector4(k.x, k.y, k.z, isActiveAndEnabled ? keyAmount : 0));
        Shader.SetGlobalVector(KeyColId, keyColor);
    }
}
