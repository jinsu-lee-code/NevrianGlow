using UnityEditor;
using UnityEngine;

/// <summary>
/// Seed dust (2026-11-01, dev handoff 1-3): builds pf_vfx_seed_dust, the art version of the sowing puff (game design: 0.4 s).
/// Glowing motes leap off the soil and settle, a few star flecks flash among them and a soft glow spreads flat on the
/// ground. Uses the existing light puff and sparkle materials. Safe to run again. Numbers are mine.
/// </summary>
public static class SeedDustSetup
{
    const string Folder = "Assets/Art/Environment/FX/seed_dust";
    const string PrefabPath = Folder + "/pf_vfx_seed_dust.prefab";
    const string PuffMaterialPath = "Assets/Art/Environment/FX/light_puff/mat_vfx_light_puff.mat";
    const string SparkleMaterialPath = "Assets/Art/Environment/FX/sparkle/mat_vfx_sparkle.mat";
    static readonly Color Cyan = new Color(0.55f, 2.2f, 2f, 1f);
    static readonly Color Gold = new Color(2.4f, 1.7f, 0.7f, 1f);
    const float Length = 0.45f;   // the longest particle has gone by then

    [MenuItem("Glow Art/Seed Dust VFX")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art/Environment/FX", "seed_dust");
        var puff = AssetDatabase.LoadAssetAtPath<Material>(PuffMaterialPath);
        var sparkle = AssetDatabase.LoadAssetAtPath<Material>(SparkleMaterialPath);
        if (puff == null || sparkle == null) { Debug.LogError("[SeedDustSetup] light puff or sparkle material not found"); return; }

        var root = new GameObject("pf_vfx_seed_dust");
        BuildMotes(root, puff);
        var flecks = new GameObject("Flecks");
        flecks.transform.SetParent(root.transform, false);
        BuildFlecks(flecks, sparkle);
        var glow = new GameObject("GroundGlow");
        glow.transform.SetParent(root.transform, false);
        glow.transform.localPosition = Vector3.up * 0.02f;
        BuildGroundGlow(glow, puff);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log($"[SeedDustSetup] saved {PrefabPath}");
    }

    static void BuildMotes(GameObject go, Material material)
    {
        var ps = NewSystem(go, material, 32, 22);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.42f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.9f, 2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(Cyan, Gold);
        main.gravityModifier = 1.1f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 32f;
        shape.radius = 0.2f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var drag = ps.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.limit = 10f;
        drag.drag = 2.5f;
        FadeOut(ps, 0.55f);
        Shrink(ps, 1f, 0.35f);
    }

    static void BuildFlecks(GameObject go, Material material)
    {
        var ps = NewSystem(go, material, 10, 6);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.32f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI);
        main.startColor = new Color(2.6f, 2.3f, 1.5f, 1f);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 45f;
        shape.radius = 0.18f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        FadeOut(ps, 0.4f);
        Shrink(ps, 0.4f, 1f);
    }

    static void BuildGroundGlow(GameObject go, Material material)
    {
        var ps = NewSystem(go, material, 1, 1);
        var main = ps.main;
        main.startLifetime = 0.38f;
        main.startSpeed = 0f;
        main.startSize = 1f;
        main.startColor = new Color(0.5f, 1.5f, 1.35f, 0.5f);

        var shape = ps.shape;
        shape.enabled = false;
        ps.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        FadeOut(ps, 0.15f);
        Shrink(ps, 0.45f, 1f);
    }

    static ParticleSystem NewSystem(GameObject go, Material material, int maxParticles, short burst)
    {
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = Length;
        main.loop = false;
        main.playOnAwake = true;
        main.maxParticles = maxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, burst) });

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return ps;
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

    static void Shrink(ParticleSystem ps, float from, float to)
    {
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, from, 1f, to));
    }
}
