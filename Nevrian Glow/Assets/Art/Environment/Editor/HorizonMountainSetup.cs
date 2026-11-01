using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Horizon mountains (2026-11-01, user: real rock mountains instead of HorizonRing, and they must read as far away):
/// rebuilds == Terrain ==/HorizonMountains in the open background scene as three painted layers of the faceted
/// env_horizon_mountain_* meshes: dark low ridges by the terrain edge, rock mountains behind them, pale far ranges at the
/// back (few facets, no contrast). Each layer has its own DSEWA/ToonMountain material; an existing material is left as it
/// is, so tuned values survive a rebuild. Switches the old HorizonSilhouettes off. Safe to run again (same seed, same
/// mountains). Numbers are mine.
/// </summary>
public static class HorizonMountainSetup
{
    const string Folder = "Assets/Art/Environment/horizon/";
    const int Seed = 7;
    const float FootY = -10f;
    const float FloorY = -5f;       // under the terrain's lowest point (y -4)
    const float FloorSize = 3400f;

    class Layer
    {
        public string name; public string[] parts;
        public float radius, jitter; public int count;
        public Vector2 width, height;   // scale ranges
        public float air, footAir, contrast;
        public Color foot, top;
    }

    static readonly Layer[] Layers =
    {
        new Layer { name = "near", parts = new[] { "b", "d" }, radius = 355f, jitter = 0.05f, count = 15,
            width = new Vector2(0.9f, 1.25f), height = new Vector2(0.1f, 0.16f), air = 0.12f, footAir = 0.25f, contrast = 1f,
            foot = new Color(0.15f, 0.15f, 0.33f), top = new Color(0.3f, 0.28f, 0.54f) },
        new Layer { name = "mid", parts = new[] { "a", "c", "e" }, radius = 790f, jitter = 0.09f, count = 15,
            width = new Vector2(1.7f, 2.4f), height = new Vector2(0.8f, 1.15f), air = 0.5f, footAir = 0.6f, contrast = 0.7f,
            foot = new Color(0.22f, 0.22f, 0.44f), top = new Color(0.44f, 0.43f, 0.72f) },
        new Layer { name = "far", parts = new[] { "a_far", "b_far", "c_far", "e_far" }, radius = 1450f, jitter = 0.1f, count = 15,
            width = new Vector2(3.4f, 4.6f), height = new Vector2(2f, 2.8f), air = 0.84f, footAir = 0.9f, contrast = 0.25f,
            foot = new Color(0.3f, 0.3f, 0.52f), top = new Color(0.5f, 0.5f, 0.76f) },
    };

    static readonly Layer Floor = new Layer { name = "floor", height = new Vector2(1f, 1f), air = 0.45f, footAir = 0f, contrast = 0f,
        foot = new Color(0.17f, 0.17f, 0.37f), top = new Color(0.17f, 0.17f, 0.37f) };

    [MenuItem("Glow Art/Horizon Mountains")]
    static void Run()
    {
        var terrain = GameObject.Find("== Terrain ==");
        if (terrain == null) { Debug.LogError("[HorizonMountainSetup] open the background scene"); return; }
        var old = terrain.transform.Find("HorizonMountains");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var group = new GameObject("HorizonMountains");
        group.transform.SetParent(terrain.transform, false);

        var rnd = new System.Random(Seed);
        float R() => (float)rnd.NextDouble();
        int made = 0;
        foreach (var layer in Layers)
        {
            var material = EnsureMaterial(layer);
            var parent = new GameObject("Layer_" + layer.name).transform;
            parent.SetParent(group.transform, false);
            for (int i = 0; i < layer.count; i++)
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "Models/env_horizon_mountain_" + layer.parts[rnd.Next(layer.parts.Length)] + ".fbx");
                if (mesh == null) { Debug.LogError("[HorizonMountainSetup] missing mesh in layer " + layer.name); return; }
                float az = (i + (R() - 0.5f) * 0.6f) / layer.count * Mathf.PI * 2f;
                float radius = layer.radius * (1f + (R() - 0.5f) * 2f * layer.jitter);
                float w = Mathf.Lerp(layer.width.x, layer.width.y, R());
                var go = new GameObject("Mountain_" + made.ToString("00"), typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(new Vector3(Mathf.Sin(az) * radius, FootY, Mathf.Cos(az) * radius), Quaternion.Euler(0f, R() * 360f, 0f));
                go.transform.localScale = new Vector3(w, Mathf.Lerp(layer.height.x, layer.height.y, R()), w);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                made++;
            }
        }
        // the land between the terrain's edge and the ranges: without it high views look through to the sky under the world
        var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
        floor.name = "Floor";
        Object.DestroyImmediate(floor.GetComponent<Collider>());
        floor.transform.SetParent(group.transform, false);
        floor.transform.SetPositionAndRotation(new Vector3(0f, FloorY, 0f), Quaternion.Euler(90f, 0f, 0f));
        floor.transform.localScale = new Vector3(FloorSize, FloorSize, 1f);
        var floorRenderer = floor.GetComponent<MeshRenderer>();
        floorRenderer.sharedMaterial = EnsureMaterial(Floor);
        floorRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        floorRenderer.receiveShadows = false;
        floorRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        floorRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        var ring = terrain.transform.Find("HorizonSilhouettes");
        if (ring != null) ring.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(terrain.scene);
        Debug.Log($"[HorizonMountainSetup] {made} mountains in {Layers.Length} layers");
    }

    static Material EnsureMaterial(Layer layer)
    {
        string path = Folder + "Materials/mat_env_horizon_mountain_" + layer.name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("DSEWA/ToonMountain"));
        m.SetColor("_FootColor", layer.foot);
        m.SetColor("_TopColor", layer.top);
        m.SetFloat("_Air", layer.air);
        m.SetFloat("_FootAir", layer.footAir);
        m.SetFloat("_Contrast", layer.contrast);
        m.SetVector("_HeightRange", new Vector4(FootY, 210f * layer.height.y * 1f, 0f, 0f));
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
