using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Background tree Mesh LODs (2026-10-30, LOD plan step 2): the qt trees import with Unity's Generate Mesh LODs, but the
/// generator drops the leaf cards first (2-triangle quads: 1,920 -> 600 -> 0 by LOD2), so the canopy vanished and only
/// bare branches were left. The leaves are cheap anyway, so every LOD level keeps the full leaf submesh (LOD0 range);
/// bark, glow fruit and husk keep the generated reductions. Submesh 1 = leaves (ToonLeaf) in all ten qt trees.
/// </summary>
public class TreeBgMeshLodPostprocessor : AssetPostprocessor
{
    const string Folder = "/tree_bg/Models/qt/";
    const int LeafSubmesh = 1;

    void OnPostprocessModel(GameObject root)
    {
        if (!assetPath.Replace('\\', '/').Contains(Folder)) return;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = mf.sharedMesh;
            if (mesh == null || mesh.lodCount < 2 || mesh.subMeshCount <= LeafSubmesh) continue;
            var full = mesh.GetLod(LeafSubmesh, 0);
            for (int lod = 1; lod < mesh.lodCount; lod++)
                mesh.SetLod(LeafSubmesh, lod, full, MeshUpdateFlags.DontRecalculateBounds);
        }
    }
}
