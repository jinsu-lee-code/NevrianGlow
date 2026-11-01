using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Rebuilds == Sky ==/Clouds3D: the Blender cloud meshes (shape + shade data) drawn by DSEWA/CloudPuff. The layout is
/// a composed list, not a ring: puffy clouds on the sun side with open sky between them, thin wisps on the night side.
/// Safe to run again: the group is replaced and the result is the same every time; its sun tint settings carry over.
/// </summary>
public static class Cloud3DSetup
{
    const string Folder = "Assets/Art/Environment/Sky/Clouds3D";
    const string WispMaterialPath = Folder + "/mat_env_sky_cloudwisp.mat";
    const string SkyRoot = "== Sky ==";
    const string GroupName = "Clouds3D";
    const string NightLightName = "CloudNightLight";
    const float NightLightElevation = 40f;
    const float DriftSpeed = 22f;    // m/s: the ring turns as one, so every cloud keeps its front to the middle of the map
    // per shape (1..3): shape map frame in object units, shell push
    static readonly Vector2[] Frames = { Vector2.zero, new Vector2(20.48f, 7.08f), new Vector2(8.83f, 2.38f), new Vector2(5.61f, 1.73f) };
    static readonly float[] Push = { 0f, 0.2f, 0.09f, 0.06f };

    struct Cloud
    {
        public int shape; public string wisp; public float azimuth, elevation, distance, scale, yaw, pitch; public bool mirror;
        public Cloud(int shape, float azimuth, float elevation, float distance, float scale, bool mirror = false, float yaw = 0f, float pitch = 0f)
        {
            this.shape = shape; wisp = null; this.azimuth = azimuth; this.elevation = elevation; this.distance = distance;
            this.scale = scale; this.mirror = mirror; this.yaw = yaw; this.pitch = pitch;
        }
        public Cloud(string wisp, float azimuth, float elevation, float distance, float scale, float yaw)
        {
            shape = 0; this.wisp = wisp; this.azimuth = azimuth; this.elevation = elevation; this.distance = distance;
            this.scale = scale; mirror = false; this.yaw = yaw; pitch = 0f;
        }
    }

    // shape 1 big, 2 medium, 3 small; azimuth in degrees from the sun, elevation in degrees to the cloud's base
    static readonly Cloud[] Layout =
    {
        new Cloud(1, -30f, 11f, 1500f, 125f),
        new Cloud(1, 62f, 9f, 1700f, 125f, true),
        new Cloud(1, -112f, 10f, 1600f, 118f),
        new Cloud(1, 158f, 9f, 1600f, 118f, true),
        new Cloud(2, 18f, 4f, 1800f, 165f, true),
        new Cloud(2, -72f, 21f, 1400f, 135f),
        new Cloud(2, 104f, 21f, 1400f, 130f, true),
        // these two ride at the sun's height, so it goes behind a cloud twice a turn; a relief seen from far below shows
        // only its transparent rim, so they bow to the middle of the map
        new Cloud(2, 42f, 31f, 1300f, 125f, pitch: 35f),
        new Cloud(2, -158f, 31f, 1300f, 125f, pitch: 35f),
        new Cloud(3, -8f, 29f, 1300f, 100f),
        new Cloud(3, 78f, 4f, 1900f, 160f),
        new Cloud(3, -52f, 4f, 1900f, 150f, true),
        new Cloud(3, 132f, 28f, 1300f, 100f, true),
        new Cloud("env_sky_cloudband_a", 148f, 22f, 1400f, 34f, 14f),
        new Cloud("env_sky_cloudband_b", 182f, 14f, 1500f, 38f, -12f),
        new Cloud("env_sky_cloudband_c", 214f, 26f, 1400f, 30f, 18f),
        new Cloud("env_sky_cloudband_a", -152f, 30f, 1400f, 28f, -16f),
    };

    [MenuItem("Glow Art/3D Clouds")]
    public static void Build()
    {
        var sky = GameObject.Find(SkyRoot);
        if (sky == null) { Debug.LogError($"[Cloud3DSetup] {SkyRoot} not found: open BackGroundScene first"); return; }

        var shapes = new Material[4];
        for (int shape = 1; shape <= 3; shape++)
        {
            var map = LoadMap($"{Folder}/tex_env_sky_cloudrelief_{shape}.png");
            int index = shape;
            shapes[shape] = EnsureMaterial($"{Folder}/mat_env_sky_cloudrelief_{shape}.mat", m =>
            {
                m.SetTexture("_Map", map);
                m.SetFloat("_Inflate", Push[index]);
                m.SetVector("_FrameSize", Frames[index]);
            });
        }
        var proxies = new Material[4];
        for (int shape = 1; shape <= 3; shape++) proxies[shape] = EnsureDepthProxy(shape, shapes[shape]);
        var wisp = EnsureMaterial(WispMaterialPath, m =>
        {
            m.SetFloat("_UseMap", 0f);
            m.SetFloat("_Opacity", 0.72f);
            m.SetVector("_BaseFade", new Vector4(0.1f, 0.6f, 0f, 0f));
            m.SetFloat("_RimFade", 0.75f);
            m.SetFloat("_StrokeScale", 0.012f);
            m.SetFloat("_NoiseAmount", 0.5f);
            m.SetFloat("_Spherize", 0f);
            m.SetFloat("_AOAmount", 0f);
        });

        var oldGroup = sky.transform.Find(GroupName);
        var group = new GameObject(GroupName);
        group.transform.SetParent(sky.transform, false);

        var sun = RenderSettings.sun;
        float sunAzimuth = sun != null ? Mathf.Atan2(-sun.transform.forward.x, -sun.transform.forward.z) * Mathf.Rad2Deg : 0f;
        int placed = 0;
        for (int i = 0; i < Layout.Length; i++)
        {
            var cloud = Layout[i];
            string meshName = cloud.wisp ?? $"env_sky_cloudlow_{cloud.shape}";
            var mesh = LoadMesh(meshName);
            if (mesh == null) { Debug.LogError($"[Cloud3DSetup] mesh not found: {meshName}"); continue; }

            float angle = (sunAzimuth + cloud.azimuth) * Mathf.Deg2Rad;
            float height = Mathf.Tan(cloud.elevation * Mathf.Deg2Rad) * cloud.distance;
            var position = new Vector3(Mathf.Sin(angle) * cloud.distance, height, Mathf.Cos(angle) * cloud.distance);

            var go = new GameObject($"{meshName}_{i:00}");
            go.transform.SetParent(group.transform, false);
            // local +Z is the relief's front: it faces the middle of the map
            go.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(cloud.pitch, sunAzimuth + cloud.azimuth + 180f + cloud.yaw, 0f));
            go.transform.localScale = new Vector3(cloud.mirror ? -cloud.scale : cloud.scale, cloud.scale, cloud.scale);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = cloud.wisp != null ? new[] { wisp } : new[] { shapes[cloud.shape], proxies[cloud.shape] };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            placed++;
        }

        // the sun tint and the slow drift move over from the group this one replaces, with the same settings
        var sunLight = group.AddComponent<CloudSunLight>();
        var drift = group.AddComponent<CloudDrift>();
        group.AddComponent<CloudSunCover>();
        if (oldGroup != null)
        {
            var oldSun = oldGroup.GetComponent<CloudSunLight>();
            var oldDrift = oldGroup.GetComponent<CloudDrift>();
            if (oldSun != null) EditorUtility.CopySerialized(oldSun, sunLight);
            if (oldDrift != null) EditorUtility.CopySerialized(oldDrift, drift);
            Object.DestroyImmediate(oldGroup.gameObject);
        }
        sunLight.driveRenderers = false;   // the shader reads the globals per pixel
        drift.windSpeed = DriftSpeed;
        drift.matchCloudShadows = false;   // DSEWA/CloudPuff blows its wind clockwise, the ring has to agree
        drift.direction = 1f;
        drift.scaleAmount = 0f;            // one steady wind: clouds travel and blow away, they do not bob on the spot
        drift.bobHeight = 0f;
        drift.swayDegrees = 0f;
        drift.previewInEditMode = true;

        // kept when it already exists, so a hand-placed direction and colour survive a rebuild
        if (sky.transform.Find(NightLightName) == null)
        {
            var night = new GameObject(NightLightName);
            night.transform.SetParent(sky.transform, false);
            night.transform.rotation = Quaternion.Euler(NightLightElevation, sunAzimuth, 0f);   // shines from opposite the sun
            night.AddComponent<CloudNightLight>();
        }

        EditorSceneManager.MarkSceneDirty(group.scene);
        Debug.Log($"[Cloud3DSetup] {placed} clouds placed under {SkyRoot}/{GroupName}");
    }

    static Mesh LoadMesh(string name)
    {
        string path = $"{Folder}/{name}.fbx";
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return null;
        bool dirty = importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.importAnimation
            || importer.importNormals != ModelImporterNormals.Import;
        if (dirty)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }

    static Texture2D LoadMap(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.sRGBTexture)
        {
            importer.sRGBTexture = false;                 // data, not colour
            importer.alphaIsTransparency = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // depth only copy of a cloud material: it follows the original's values on every build
    static Material EnsureDepthProxy(int shape, Material source)
    {
        var proxy = EnsureMaterial($"{Folder}/mat_env_sky_cloudrelief_{shape}_depth.mat", null);
        proxy.CopyPropertiesFromMaterial(source);
        proxy.SetShaderPassEnabled("SRPDefaultUnlit", false);
        proxy.SetShaderPassEnabled("UniversalForward", false);
        proxy.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        EditorUtility.SetDirty(proxy);
        return proxy;
    }

    static Material EnsureMaterial(string path, System.Action<Material> configure, string shader = "DSEWA/CloudPuff")
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find(shader));
        configure?.Invoke(material);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
