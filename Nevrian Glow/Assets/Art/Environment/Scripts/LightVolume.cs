using UnityEngine;

/// <summary>
/// Hands a point light to DSEWA/LightVolume, drawn on this box. The glow follows the light's position, colour and intensity,
/// so it dims with it. Goes on a box round the room, the Glow Art/Shop Chandelier menu does this. Without a light it glows
/// from the box's middle in its own colour (the shop crystals: no lamp, so no lighting cost).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer))]
public class LightVolume : MonoBehaviour
{
    [Tooltip("Light that fills the air")]
    public Light source;
    [Tooltip("Colour used when there is no light")]
    [ColorUsage(false, true)] public Color glow = Color.black;

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
        view.GetPropertyBlock(block);
        block.SetVector(PositionId, source != null ? source.transform.position : transform.position);
        Color color = source == null ? glow : source.isActiveAndEnabled ? source.color * source.intensity : Color.black;
        block.SetVector(ColorId, color);
        view.SetPropertyBlock(block);
    }
}
