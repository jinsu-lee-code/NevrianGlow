using UnityEditor;
using UnityEngine;

/// <summary>
/// Fertilizer growth (2026-11-01, dev handoff 1-5): builds pf_vfx_fertilizer_grow, the art version of the 0.8 s instant
/// growth. A ring runs out over the soil, motes spiral up round the crop, and when the crop reaches its overshoot
/// (FarmCellView.GrowBurst, about 0.45 s) a soft flash and star flecks go off. Plays at the soil, the crop's own scale
/// stays in code. Uses the existing ring, light puff and sparkle materials. Safe to run again. Numbers are mine.
/// </summary>
public static class FertilizerGrowSetup
{
    const string Folder = "Assets/Art/Environment/FX/fertilizer_grow";
    const string PrefabPath = Folder + "/pf_vfx_fertilizer_grow.prefab";
    const string RingMaterialPath = "Assets/Art/Environment/FX/ring_gold/mat_vfx_ring_gold.mat";
    const string PuffMaterialPath = "Assets/Art/Environment/FX/light_puff/mat_vfx_light_puff.mat";
    const string SparkleMaterialPath = "Assets/Art/Environment/FX/sparkle/mat_vfx_sparkle.mat";
    static readonly Color Teal = new Color(0.45f, 2f, 1.6f, 1f);
    static readonly Color Gold = new Color(2.4f, 1.8f, 0.75f, 1f);
    const float PopTime = 0.42f;      // just before the crop's overshoot peak
    const float RiseTime = 0.4f;
    const float CropHeight = 0.4f;

    [MenuItem("Glow Art/Fertilizer Grow VFX")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art/Environment/FX", "fertilizer_grow");
        var ring = AssetDatabase.LoadAssetAtPath<Material>(RingMaterialPath);
        var puff = AssetDatabase.LoadAssetAtPath<Material>(PuffMaterialPath);
        var sparkle = AssetDatabase.LoadAssetAtPath<Material>(SparkleMaterialPath);
        if (ring == null || puff == null || sparkle == null) { Debug.LogError("[FertilizerGrowSetup] ring, puff or sparkle material not found"); return; }

        var root = new GameObject("pf_vfx_fertilizer_grow");
        BuildRising(root, puff);
        BuildRing(Child(root, "GroundRing", 0.02f), ring);
        BuildFlash(Child(root, "PopFlash", CropHeight), puff);
        BuildFlecks(Child(root, "PopFlecks", CropHeight), sparkle);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log($"[FertilizerGrowSetup] saved {PrefabPath}");
    }

    static GameObject Child(GameObject root, string name, float height)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = Vector3.up * height;
        return go;
    }

    // motes rise from a circle round the crop and turn around it
    static void BuildRising(GameObject go, Material material)
    {
        var ps = NewSystem(go, material, 40, RiseTime);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.38f, 0.6f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(Teal, Gold);

        var emission = ps.emission;
        emission.rateOverTime = 90f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.42f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(1f, 1.7f);
        velocity.orbitalY = 5f;
        velocity.radial = -0.45f;
        FadeOut(ps, 0.6f);
        Size(ps, 1f, 0.4f);
    }

    static void BuildRing(GameObject go, Material material)
    {
        var ps = NewSystem(go, material, 1, 0.1f);
        Burst(ps, 1, 0f);
        var main = ps.main;
        main.startLifetime = 0.55f;
        main.startSpeed = 0f;
        main.startSize = 1.7f;
        main.startColor = new Color(0.55f, 1.7f, 1.35f, 0.9f);
        go.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        FadeOut(ps, 0.3f);
        Size(ps, 0.2f, 1f);
    }

    static void BuildFlash(GameObject go, Material material)
    {
        var ps = NewSystem(go, material, 1, 0.1f);
        Burst(ps, 1, 0f);
        var main = ps.main;
        main.startDelay = PopTime;
        main.startLifetime = 0.3f;
        main.startSpeed = 0f;
        main.startSize = 1.1f;
        main.startColor = new Color(1.5f, 1.35f, 0.8f, 0.75f);
        FadeOut(ps, 0.1f);
        Size(ps, 0.45f, 1f);
    }

    static void BuildFlecks(GameObject go, Material material)
    {
        var ps = NewSystem(go, material, 12, 0.1f);
        Burst(ps, 10, 0f);
        var main = ps.main;
        main.startDelay = PopTime;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.36f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI);
        main.startColor = new Color(2.6f, 2.3f, 1.4f, 1f);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;
        FadeOut(ps, 0.45f);
        Size(ps, 0.5f, 1f);
    }

    static ParticleSystem NewSystem(GameObject go, Material material, int maxParticles, float duration)
    {
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = duration;
        main.loop = false;
        main.playOnAwake = true;
        main.maxParticles = maxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        var shape = ps.shape;
        shape.enabled = false;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return ps;
    }

    static void Burst(ParticleSystem ps, short count, float time)
    {
        var emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(time, count) });
    }

    static void FadeOut(ParticleSystem ps, float holdUntil)
    {
        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, holdUntil), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
    }

    static void Size(ParticleSystem ps, float from, float to)
    {
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, from, 1f, to));
    }
}
