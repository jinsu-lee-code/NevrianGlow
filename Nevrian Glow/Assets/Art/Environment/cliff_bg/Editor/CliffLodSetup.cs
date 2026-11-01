using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Rock LOD Groups (2026-11-01, dev note 1-2): puts env_cliff_bg_*_lod1 next to each rock prefab's Model and adds a
/// LODGroup that switches about SwitchMetres away (shadow distance). Safe to run again. Scene instances that override
/// the Model's materials get the same materials on the LOD1 (run with the background scene open).
/// A LOD Group switches on on-screen height, so the distance is exact only at scale 1 and the game's field of view.
/// </summary>
public static class CliffLodSetup
{
    const string Folder = "Assets/Art/Environment/cliff_bg/";
    const string Lod1Name = "Model_LOD1";
    const float SwitchMetres = 80f;
    const float PlayerFov = 65f;
    static readonly string[] Parts = { "a", "b", "c", "boulders" };

    [MenuItem("Glow Art/Rock LOD Groups")]
    static void Run()
    {
        foreach (var part in Parts)
        {
            var prefabPath = Folder + "pf_env_cliff_bg_" + part + ".prefab";
            var lodAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Models/env_cliff_bg_" + part + "_lod1.fbx");
            if (lodAsset == null) { Debug.LogError("[CliffLodSetup] no LOD1 model for " + part); continue; }
            using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
            {
                var root = scope.prefabContentsRoot;
                var model = root.transform.Find("Model");
                var r0 = model.GetComponentInChildren<MeshRenderer>(true);
                var old = root.transform.Find(Lod1Name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var lod1 = (GameObject)PrefabUtility.InstantiatePrefab(lodAsset, root.transform);
                lod1.name = Lod1Name;
                // the FBX root carries its own axis rotation and unit scale - keep them, only the position follows Model
                lod1.transform.SetLocalPositionAndRotation(model.localPosition, lodAsset.transform.localRotation);
                lod1.transform.localScale = lodAsset.transform.localScale;
                var r1 = lod1.GetComponentInChildren<MeshRenderer>(true);
                r1.sharedMaterials = r0.sharedMaterials;
                r1.shadowCastingMode = r0.shadowCastingMode;
                r1.receiveShadows = r0.receiveShadows;
                r1.renderingLayerMask = r0.renderingLayerMask;
                lod1.layer = r0.gameObject.layer;
                r1.gameObject.layer = r0.gameObject.layer;
                var group = root.GetComponent<LODGroup>();
                if (group == null) group = root.AddComponent<LODGroup>();
                group.fadeMode = LODFadeMode.None;
                group.SetLODs(new[] { new LOD(0.5f, new Renderer[] { r0 }), new LOD(0f, new Renderer[] { r1 }) });
                group.RecalculateBounds();
                float height = group.size * QualitySettings.lodBias / (SwitchMetres * 2f * Mathf.Tan(PlayerFov * 0.5f * Mathf.Deg2Rad));
                group.SetLODs(new[] { new LOD(Mathf.Clamp(height, 0.01f, 0.9f), new Renderer[] { r0 }), new LOD(0f, new Renderer[] { r1 }) });
                var mesh0 = r0.GetComponent<MeshFilter>().sharedMesh; var mesh1 = r1.GetComponent<MeshFilter>().sharedMesh;
                Debug.Log($"[CliffLodSetup] {part}: size {group.size:F1} height {height:F3} tris {mesh0.triangles.Length / 3} -> {mesh1.triangles.Length / 3} subs {mesh0.subMeshCount}/{mesh1.subMeshCount} bounds0 {r0.bounds.center}/{r0.bounds.size} bounds1 {r1.bounds.center}/{r1.bounds.size}");
            }
        }

        int fixedCount = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            bool changed = false;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var group in root.GetComponentsInChildren<LODGroup>(true))
                {
                    if (group.transform.Find(Lod1Name) == null) continue;
                    var lods = group.GetLODs();
                    if (lods.Length < 2 || lods[0].renderers.Length == 0 || lods[1].renderers.Length == 0) continue;
                    var a = lods[0].renderers[0]; var b = lods[1].renderers[0];
                    if (a == null || b == null) continue;
                    var ma = a.sharedMaterials; var mb = b.sharedMaterials; bool same = ma.Length == mb.Length;
                    for (int m = 0; same && m < ma.Length; m++) same = ma[m] == mb[m];
                    if (same) continue;
                    b.sharedMaterials = ma; EditorUtility.SetDirty(b); PrefabUtility.RecordPrefabInstancePropertyModifications(b);
                    fixedCount++; changed = true;
                }
            if (changed) EditorSceneManager.MarkSceneDirty(scene);
        }
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log($"[CliffLodSetup] done, {fixedCount} scene instances got their LOD0 materials on LOD1");
    }
}
