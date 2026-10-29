using UnityEngine;

/// <summary>
/// Terrain layers 5-6 for DSEWA/ToonTerrain (2026-10-27, user: "I'll paint the paths myself: stone path, grass path").
/// The toon terrain shader has no add-pass, so Unity would not draw layers past the fourth. This pushes Unity's second
/// alphamap texture and the 5th / 6th layers' diffuse + tiling into the terrain material; the base pass then blends
/// all six. Painting in the Terrain tool updates the alphamap texture in place, so strokes show at once.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Terrain))]
public class TerrainExtraLayers : MonoBehaviour
{
    static readonly int Control1 = Shader.PropertyToID("_Control1");
    static readonly int Splat4 = Shader.PropertyToID("_Splat4"), Splat5 = Shader.PropertyToID("_Splat5");
    static readonly int Normal4 = Shader.PropertyToID("_Normal4"), Normal5 = Shader.PropertyToID("_Normal5");
    static readonly int Mask4 = Shader.PropertyToID("_Mask4"), Mask5 = Shader.PropertyToID("_Mask5");
    static readonly int Splat4ST = Shader.PropertyToID("_Splat4_ST"), Splat5ST = Shader.PropertyToID("_Splat5_ST");

    Terrain terrain;

    void OnEnable() { terrain = GetComponent<Terrain>(); Push(); }
    void Update() => Push();

    void Push()
    {
        if (terrain == null) terrain = GetComponent<Terrain>();
        var td = terrain != null ? terrain.terrainData : null;
        var mat = terrain != null ? terrain.materialTemplate : null;
        if (td == null || mat == null) return;
        var maps = td.alphamapTextures;
        var layers = td.terrainLayers;
        mat.SetTexture(Control1, maps.Length > 1 ? maps[1] : Texture2D.blackTexture);
        Set(mat, layers, 4, Splat4, Splat4ST, Normal4, Mask4, td.size);
        Set(mat, layers, 5, Splat5, Splat5ST, Normal5, Mask5, td.size);
    }

    static void Set(Material mat, TerrainLayer[] layers, int i, int texId, int stId, int normalId, int maskId, Vector3 size)
    {
        if (layers.Length <= i || layers[i] == null) return;
        var l = layers[i];
        mat.SetTexture(texId, l.diffuseTexture);
        mat.SetTexture(normalId, l.normalMapTexture != null ? l.normalMapTexture : Texture2D.normalTexture);
        mat.SetTexture(maskId, l.maskMapTexture != null ? l.maskMapTexture : Texture2D.whiteTexture);
        mat.SetVector(stId, new Vector4(size.x / Mathf.Max(l.tileSize.x, 0.01f), size.z / Mathf.Max(l.tileSize.y, 0.01f),
                                         l.tileOffset.x / Mathf.Max(l.tileSize.x, 0.01f), l.tileOffset.y / Mathf.Max(l.tileSize.y, 0.01f)));
    }
}
