using UnityEngine;

/// <summary>
/// Hands a point light to DSEWA/LightVolume, drawn on this box. The glow follows the light's position, colour and intensity,
/// so it dims with it. Goes on a box round the room, the Glow Art/Shop Chandelier menu does this.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer))]
public class LightVolume : MonoBehaviour
{
    [Tooltip("Light that fills the air")]
    public Light source;

    static readonly int PositionId = Shader.PropertyToID("_LightPos");
    static readonly int ColorId = Shader.PropertyToID("_LightColor");
    MeshRenderer view;
    MaterialPropertyBlock block;

    void OnEnable()
    {
        view = GetComponent<MeshRenderer>();
        block = new MaterialPropertyBlock();
        Apply();
    }

    void LateUpdate() => Apply();

    void Apply()
    {
        if (source == null) return;
        view.GetPropertyBlock(block);
        block.SetVector(PositionId, source.transform.position);
        block.SetVector(ColorId, source.isActiveAndEnabled ? (Vector4)(source.color * source.intensity) : Vector4.zero);
        view.SetPropertyBlock(block);
    }
}
