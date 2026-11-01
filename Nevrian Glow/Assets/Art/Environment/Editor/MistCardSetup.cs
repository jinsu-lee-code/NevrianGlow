using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Mist banks (2026-11-01): rebuilds == FX ==/MistCards in the open background scene from the list below, small mist volumes
/// at the waterfall's foot, round the lake and behind the tree lines (DSEWA/ToonMistCard marches through each box). They are
/// fixed to the world and do not turn with the camera. Safe to run again. Numbers are mine.
/// </summary>
public static class MistCardSetup
{
    const string MaterialPath = "Assets/Art/Environment/Materials/mat_env_toon_mist_card.mat";
    const float SinkMetres = 1f;   // the box starts this far under the ground
    const float Depth = 0.7f;      // the bank's depth as a fraction of its width

    // x, z, width, height (m); ground and water sit about y 0
    static readonly Vector4[] Cards =
    {
        new Vector4(6, -68, 34, 9), new Vector4(-9, -66, 26, 6), new Vector4(22, -66, 26, 6.5f),
        new Vector4(-13, -48, 30, 4), new Vector4(15, -44, 34, 4.5f), new Vector4(2, -36, 26, 3.5f),
        new Vector4(27, -54, 26, 4), new Vector4(-27, -56, 24, 4),
        new Vector4(-72, -12, 54, 8), new Vector4(-64, 30, 46, 7), new Vector4(72, 22, 54, 8), new Vector4(36, 70, 50, 7),
    };

    [MenuItem("Glow Art/Mist Cards")]
    static void Run()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        var fx = GameObject.Find("== FX ==");
        if (material == null || fx == null) { Debug.LogError("[MistCardSetup] needs the card material and the background scene open"); return; }
        var old = fx.transform.Find("MistCards");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var group = new GameObject("MistCards");
        group.transform.SetParent(fx.transform, false);
        for (int i = 0; i < Cards.Length; i++)
        {
            var c = Cards[i];
            var card = GameObject.CreatePrimitive(PrimitiveType.Cube);
            card.name = "MistCard_" + i.ToString("00");
            Object.DestroyImmediate(card.GetComponent<Collider>());
            card.transform.SetParent(group.transform, false);
            card.transform.SetPositionAndRotation(new Vector3(c.x, c.w * 0.5f - SinkMetres, c.y), Quaternion.Euler(0f, i * 47f, 0f));
            card.transform.localScale = new Vector3(c.z, c.w + SinkMetres, c.z * Depth);
            var r = card.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }
        EditorSceneManager.MarkSceneDirty(fx.scene);
        Debug.Log($"[MistCardSetup] {Cards.Length} banks");
    }
}
