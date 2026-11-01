using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Rebuilds the air in == FX ==: light dust round the camera, glowing spores rising on the night side, rare shooting stars.
/// Safe to run again: the objects are replaced, materials that already exist keep their hand-tuned values.
/// </summary>
public static class AirParticlesSetup
{
    const string Folder = "Assets/Art/Environment/FX/air";
    const string FxRoot = "== FX ==";

    [MenuItem("Glow Art/Air Particles")]
    public static void Build()
    {
        var fx = GameObject.Find(FxRoot);
        if (fx == null) { Debug.LogError($"[AirParticlesSetup] {FxRoot} not found: open BackGroundScene first"); return; }
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art/Environment/FX", "air");

        var dust = EnsureMaterial("mat_vfx_air_dust", "DSEWA/AirMotes", null);
        var spores = EnsureMaterial("mat_vfx_air_spore", "DSEWA/AirMotes", m =>
        {
            m.SetColor("_ColorA", new Color(0.25f, 0.9f, 1f));
            m.SetColor("_ColorB", new Color(0.95f, 0.35f, 0.9f));
            m.SetFloat("_Intensity", 1.6f);
            m.SetFloat("_LightFollow", 0f);
            m.SetFloat("_BoxWidth", 46f);
            m.SetFloat("_BoxHeight", 9f);
            m.SetFloat("_FloorOffset", -2f);
            m.SetVector("_Size", new Vector4(0.03f, 0.07f));
            m.SetFloat("_Wind", 0.15f);
            m.SetFloat("_Rise", 0.3f);
            m.SetFloat("_Wobble", 0.5f);
            m.SetFloat("_Twinkle", 0.7f);
            m.SetFloat("_SunGlow", 0f);
            m.SetFloat("_DayAmount", 0.3f);
            m.SetFloat("_NightAmount", 1f);
            m.SetFloat("_AsleepAmount", 0f);    // a glow: it sleeps in the grey area
        });
        var stars = EnsureMaterial("mat_vfx_air_shootingstar", "DSEWA/ShootingStar", null);
        EnsureMaterial("mat_vfx_air_shopdust", "DSEWA/RoomDust", null);   // placed by Glow Art/Shop Chandelier

        Place(fx.transform, "AirDust", dust, 350, 0.25f);
        Place(fx.transform, "AirSpores", spores, 260, 0.1f);
        Place(fx.transform, "ShootingStars", stars, 3, 1f);

        EditorSceneManager.MarkSceneDirty(fx.scene);
        Debug.Log($"[AirParticlesSetup] placed under {FxRoot}");
    }

    static GameObject Place(Transform parent, string name, Material material, int count, float wakeAtStart, Vector3? bounds = null)
    {
        var old = parent.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var go = new GameObject(name);
        go.SetActive(false);   // AirMotes builds its mesh on enable: the count has to be set first
        go.transform.SetParent(parent, false);
        var view = go.AddComponent<MeshRenderer>();
        view.sharedMaterial = material;
        view.shadowCastingMode = ShadowCastingMode.Off;
        view.receiveShadows = false;
        view.lightProbeUsage = LightProbeUsage.Off;
        view.reflectionProbeUsage = ReflectionProbeUsage.Off;
        var motes = go.AddComponent<AirMotes>();
        motes.count = count;
        motes.wakeAtStart = wakeAtStart;
        if (bounds.HasValue) motes.boundsSize = bounds.Value;
        go.SetActive(true);
        return go;
    }

    static Material EnsureMaterial(string name, string shader, System.Action<Material> configure)
    {
        string path = $"{Folder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find(shader));
        configure?.Invoke(material);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
