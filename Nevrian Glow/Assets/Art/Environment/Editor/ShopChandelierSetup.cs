using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Puts the shop chandelier in pf_env_shop_decor in place of the hanging bulbs: the wall lamp (pf_prop_lamp_glow_wall, curled
/// arm with a crystal lantern) repeated round a rope, backs together, so it follows that prefab. A warm key light with the
/// air volume fills the room, three lanterns add breathing colour, and the shop's dust and sparks come with it. Safe to run again: the chandelier prefab is rebuilt, bulbs already gone stay gone.
/// </summary>
public static class ShopChandelierSetup
{
    const string Folder = "Assets/Art/Environment/shop_chandelier";
    const string Decor = "Assets/Art/Environment/shop_decor/pf_env_shop_decor.prefab";
    const string Lamp = "Assets/Art/Props/lamp_glow/pf_prop_lamp_glow_wall.prefab";
    const string RopeMaterial = "Assets/Art/Props/lamp_glow/Materials/mat_prop_lamp_glow_wall_bracket.mat";
    const int Arms = 6;
    const float ArmScale = 1.9f;
    const float ArmHeight = 3f;       // height of the arms' feet above the floor (m)
    const float ArmReach = 0.46f;     // from the rope to an arm's pivot (m): the wall plates close up round the rope
    const string DustMaterial = "Assets/Art/Environment/FX/air/mat_vfx_air_shopdust.mat";
    const float ChandelierGlowSize = 0.75f;   // width of a lantern's ball of glow (m)
    const float WallLampGlowSize = 0.7f;
    static readonly Vector3 RoomSize = new Vector3(12f, 3.6f, 10f);
    static readonly Color[] Accents = { new Color(0.55f, 1f, 0.84f), new Color(0.5f, 0.9f, 1f), new Color(1f, 0.94f, 0.62f) };   // mint, cyan, lemon: they stand out of the warm room; soft, stronger reads as a club
    const string CrystalModel = "Assets/Art/Environment/Crystal/Models/env_crystal_shards.fbx";
    const string CrystalMaterials = "Assets/Art/Environment/Crystal/Materials";
    const string CornersGroup = "DarkCorners";
    const float WallLampScale = 1.7f;
    static readonly Vector3 Mount = new Vector3(0f, 5.8f, 0f);
    // wall lamps sit on the free pillars beside the windows; their back (+X) goes to the wall
    static readonly (Vector3 position, float yaw)[] WallLamps =
    {
        (new Vector3(-5.36f, 2.891f, 1.55f), 180f),
        (new Vector3(5.36f, 2.891f, 1.55f), 0f),
    };
    // piece of the crystal asset, position on the floor, size, turn, mat_env_crystal_<colour>, mat_env_glowpool_<pool>
    static readonly (string piece, Vector3 position, float scale, float yaw, string colour, string pool)[] Corners =
    {
        ("Crystal_Cluster_02", new Vector3(-5.4f, 0f, 0.1f), 2.2f, 40f, "mint", "aqua"),
        ("Crystal_Cluster_01", new Vector3(5.4f, 0f, 0.1f), 2.2f, 200f, "cyan", "sky"),
        ("Crystal_Cluster_02", new Vector3(-5.4f, 0f, -1.7f), 1.7f, 120f, "aqua", "aqua"),
    };
    static readonly string[] Bulbs = { "OrbLamp_1", "OrbLamp_5", "OrbLamp_Displays", "OrbLamp_Counter" };
    static readonly string[] SceneBulbs = { "OrbLamp_1 (1)", "OrbLamp_1 (2)" };   // in Shop.unity, a dev scene

    [MenuItem("Glow Art/Shop Chandelier")]
    public static void Build()
    {
        var lamp = AssetDatabase.LoadAssetAtPath<GameObject>(Lamp);
        if (lamp == null) { Debug.LogError($"[ShopChandelierSetup] {Lamp} not found"); return; }

        var chandelier = new GameObject("pf_env_shop_chandelier");
        float feet = ArmHeight - Mount.y;
        var glowMaterial = EnsureMaterial("mat_env_shop_chandelier_lantern_glow", "DSEWA/LanternGlow");
        var ball = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");

        // the back of a wall lamp is its +X side, so turning the lamp outwards leaves the plates round the rope
        for (int i = 0; i < Arms; i++)
        {
            var turn = Quaternion.Euler(0f, i * 360f / Arms, 0f);
            var arm = (GameObject)PrefabUtility.InstantiatePrefab(lamp, chandelier.transform);
            arm.name = $"Arm_{i + 1}";
            arm.transform.SetLocalPositionAndRotation(turn * new Vector3(-ArmReach, feet, 0f), turn);
            arm.transform.localScale = Vector3.one * ArmScale;
            foreach (var view in arm.GetComponentsInChildren<Renderer>(true)) view.shadowCastingMode = ShadowCastingMode.Off;   // the key light hangs inside them

            var lantern = arm.transform.Find("Model/Lantern_Scale");
            var bounds = lantern.GetComponentInChildren<Renderer>().bounds;
            foreach (var view in lantern.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(view.bounds);
            var glow = chandelier.transform.InverseTransformPoint(bounds.center);
            LanternGlow(chandelier.transform, $"LanternGlow_{i + 1}", glow, ChandelierGlowSize, glowMaterial, ball);
            if (i % 2 == 0)
            {
                // every other lantern tints the room round it, three lights taking turns to breathe
                int accent = i / 2;
                var tint = new GameObject($"LanternLight_{accent + 1}");
                tint.transform.SetParent(chandelier.transform, false);
                tint.transform.localPosition = glow;
                var lit = tint.AddComponent<Light>();
                lit.type = LightType.Point;
                lit.color = Accents[accent];
                lit.intensity = 2.8f;
                lit.range = 5.5f;
                lit.shadows = LightShadows.None;
                var breath = tint.AddComponent<LampBreath>();
                breath.period = 14f;
                breath.low = 0.8f;
                breath.phase = accent / 3f;
            }
        }

        var rope = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rope.name = "Rope";
        Object.DestroyImmediate(rope.GetComponent<CapsuleCollider>());
        rope.transform.SetParent(chandelier.transform, false);
        float top = feet + 0.9f;
        rope.transform.localPosition = new Vector3(0f, top * 0.5f, 0f);
        rope.transform.localScale = new Vector3(0.05f, -top * 0.5f, 0.05f);
        var ropeView = rope.GetComponent<MeshRenderer>();
        ropeView.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(RopeMaterial);

        var lightObject = new GameObject("ChandelierLight");
        lightObject.transform.SetParent(chandelier.transform, false);
        lightObject.transform.localPosition = new Vector3(0f, feet - 0.1f, 0f);   // below the ledge, so its light reaches the walls under it
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.74f, 0.56f);
        light.intensity = 24f;
        light.range = 14f;
        light.shadows = LightShadows.Soft;

        // the one light also fills the air of the room
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "LightVolume";
        Object.DestroyImmediate(box.GetComponent<BoxCollider>());
        box.transform.SetParent(chandelier.transform, false);
        box.transform.localPosition = new Vector3(0f, -Mount.y * 0.5f, 0f);
        box.transform.localScale = new Vector3(12f, Mount.y, 10f);
        var boxView = box.GetComponent<MeshRenderer>();
        boxView.sharedMaterial = EnsureVolume();
        boxView.shadowCastingMode = ShadowCastingMode.Off;
        boxView.receiveShadows = false;
        box.AddComponent<LightVolume>().source = light;

        // lives here, not in the background scene, so it is there whenever the shop is
        Motes(chandelier.transform, "ShopDust", AssetDatabase.LoadAssetAtPath<Material>(DustMaterial), 360, new Vector3(0f, 1.8f - Mount.y, 0f), RoomSize)
            .AddComponent<RoomDust>().roomSize = RoomSize;
        Motes(chandelier.transform, "FallingSparks", EnsureMaterial("mat_env_shop_chandelier_sparks", "DSEWA/OrbitMotes"), 80, new Vector3(0f, feet + 0.7f, 0f), Vector3.one * 8f);

        CheckCamera(chandelier.transform);

        chandelier.AddComponent<HideSceneLamps>().names = SceneBulbs;
        var prefab = PrefabUtility.SaveAsPrefabAsset(chandelier, $"{Folder}/pf_env_shop_chandelier.prefab");
        Object.DestroyImmediate(chandelier);

        var decor = PrefabUtility.LoadPrefabContents(Decor);
        var hanging = decor.transform.Find("Hanging");
        var lights = decor.transform.Find("Lights");
        foreach (string bulb in Bulbs)
        {
            var old = hanging.Find(bulb);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }
        var placed = hanging.Find(prefab.name);
        if (placed != null) Object.DestroyImmediate(placed.gameObject);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, hanging);
        instance.transform.localPosition = Mount;

        BuildCorners(decor.transform, lamp, EnsureMaterial("mat_env_shop_chandelier_lantern_glow", "DSEWA/LanternGlow"));

        // one light for the whole room: the old helpers stay in the prefab, switched off
        lights.gameObject.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(decor, Decor);
        PrefabUtility.UnloadPrefabContents(decor);
        AssetDatabase.SaveAssets();
        Debug.Log("[ShopChandelierSetup] placed");
    }

    // for playing the shop scene alone: lives here because Shop.unity is a dev scene; it switches itself off in the game
    static void CheckCamera(Transform parent)
    {
        var go = new GameObject("CheckCamera_Shop") { tag = "MainCamera" };
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(-3.5f, 1.7f - Mount.y, -3.2f);
        go.transform.localRotation = Quaternion.Euler(-8f, 55f, 0f);
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 2500f;
        cam.fieldOfView = 65f;
        cam.allowHDR = true;
        cam.allowMSAA = true;
        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.None;
        data.volumeLayerMask = 1;
        go.AddComponent<AudioListener>();
        var check = go.AddComponent<BackgroundCheckCamera>();
        check.alsoLoad = "BackGroundScene";
        check.roomAmbient = new Color(0.36f, 0.29f, 0.36f, 1f);   // zone_shop's room light
    }

    // small glows for the corners the chandelier does not reach: no real lights, only emissive parts
    static void BuildCorners(Transform decor, GameObject lamp, Material glowMaterial)
    {
        var old = decor.Find(CornersGroup);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var group = new GameObject(CornersGroup).transform;
        group.SetParent(decor, false);
        var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        foreach (var (position, yaw) in WallLamps)
        {
            var wallLamp = (GameObject)PrefabUtility.InstantiatePrefab(lamp, group);
            wallLamp.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            wallLamp.transform.localScale = Vector3.one * WallLampScale;
            var lantern = wallLamp.transform.Find("Model/Lantern_Scale");
            var bounds = lantern.GetComponentInChildren<Renderer>().bounds;
            foreach (var view in lantern.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(view.bounds);
            LanternGlow(group, "LanternGlow", group.InverseTransformPoint(bounds.center), WallLampGlowSize, glowMaterial, Resources.GetBuiltinResource<Mesh>("Sphere.fbx"));
        }

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(CrystalModel);
        foreach (var (piece, position, scale, yaw, colour, poolColour) in Corners)
        {
            var source = model.transform.Find(piece);
            var crystal = new GameObject(piece);
            crystal.transform.SetParent(group, false);
            crystal.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f) * source.localRotation);
            crystal.transform.localScale = source.localScale * scale;
            crystal.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
            var view = crystal.AddComponent<MeshRenderer>();
            view.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{CrystalMaterials}/mat_env_crystal_{colour}.mat");

            var pool = new GameObject("GlowPool");
            pool.transform.SetParent(group, false);
            pool.transform.SetLocalPositionAndRotation(position + Vector3.up * 0.02f, Quaternion.Euler(90f, 0f, 0f));
            pool.transform.localScale = Vector3.one * 3f;
            pool.AddComponent<MeshFilter>().sharedMesh = quad;
            var poolView = pool.AddComponent<MeshRenderer>();
            poolView.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{CrystalMaterials}/mat_env_glowpool_{poolColour}.mat");
            poolView.shadowCastingMode = ShadowCastingMode.Off;
            poolView.receiveShadows = false;
        }
    }

    static GameObject Motes(Transform parent, string name, Material material, int count, Vector3 position, Vector3 bounds)
    {
        var go = new GameObject(name);
        go.SetActive(false);   // AirMotes builds its mesh on enable: the count has to be set first
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        var view = go.AddComponent<MeshRenderer>();
        view.sharedMaterial = material;
        view.shadowCastingMode = ShadowCastingMode.Off;
        view.receiveShadows = false;
        var motes = go.AddComponent<AirMotes>();
        motes.count = count;
        motes.wakeAtStart = 1f;
        motes.boundsSize = bounds;
        go.SetActive(true);
        return go;
    }

    static void LanternGlow(Transform parent, string name, Vector3 position, float size, Material material, Mesh ball)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = Vector3.one * size;
        go.AddComponent<MeshFilter>().sharedMesh = ball;
        var view = go.AddComponent<MeshRenderer>();
        view.sharedMaterial = material;
        view.shadowCastingMode = ShadowCastingMode.Off;
        view.receiveShadows = false;
    }

    static Material EnsureMaterial(string name, string shader)
    {
        string path = $"{Folder}/Materials/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find(shader));
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static Material EnsureVolume()
    {
        string path = $"{Folder}/Materials/mat_env_shop_chandelier_volume.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("DSEWA/LightVolume"));
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
