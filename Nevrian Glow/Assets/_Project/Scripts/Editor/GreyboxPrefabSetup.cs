#if UNITY_EDITOR
using System.IO;
using NevrianGlowFarm.Systems.Farm;
using UnityEditor;
using UnityEngine;

namespace NevrianGlowFarm.Editor
{
    /// <summary>
    /// 그레이박스 프리팹을 만든다 (색만 다른 기본 도형, CLAUDE 아트 규칙). 여러 번 실행해도 결과가 같다 — 이미 있으면 건드리지 않는다.
    /// 프리팹 구조(루트 / 모델 자식)는 모델 교체(5-2) 때 자식만 바꾸도록 지금 정해 둔다.
    /// </summary>
    public static class GreyboxPrefabSetup
    {
        private const string MaterialPath = "Assets/_Project/Art/Materials/mat_greybox_cell.mat";
        private const string StorageMaterialPath = "Assets/_Project/Art/Materials/mat_greybox_storage.mat";
        private const string DoorMaterialPath = "Assets/_Project/Art/Materials/mat_greybox_door.mat";
        private const string DoorPath = "Assets/_Project/Prefabs/Zones/zone_door.prefab";
        private const string StainMaterialPath = "Assets/_Project/Art/Materials/mat_greybox_stain.mat";
        private const string TrashBinMaterialPath = "Assets/_Project/Art/Materials/mat_greybox_trashbin.mat";
        private const string StainPath = "Assets/_Project/Prefabs/Cleaning/stain.prefab";
        private const string TrashBinPath = "Assets/_Project/Prefabs/Cleaning/trash_bin.prefab";
        private const string ShelfMaterialPath = "Assets/_Project/Art/Materials/mat_greybox_shelf.mat";
        private const string HologramMaterialPath = "Assets/_Project/Art/Materials/mat_hologram_placement.mat";
        private const string HologramShaderPath = "Assets/Holograms/Shader Graphs/Examples/Basic_Scanline_Hologram.shadergraph";
        private const string ShelfItemPath = "Assets/_Project/Prefabs/Shop/shelf_item.prefab";
        private const string ShelfPath = "Assets/_Project/Prefabs/Shop/shelf.prefab";
        private const string FarmCellPath = "Assets/_Project/Prefabs/Farm/farm_cell.prefab";
        private const string WorldItemPath = "Assets/_Project/Prefabs/Items/world_item.prefab";
        private const string StoragePointPath = "Assets/_Project/Prefabs/Storage/storage_point.prefab";
        private const string InteractableLayer = "Interactable";
        private const float CellSize = 1.6f;

        [MenuItem("Glow Farm/Setup/Create Greybox Prefabs")]
        private static void CreateMenu()
        {
            Debug.Log("[Setup] 그레이박스 프리팹: " + Run());
        }

        public static string Run()
        {
            var material = EnsureMaterial(MaterialPath, Color.white, out bool materialCreated);
            int layer = LayerMask.NameToLayer(InteractableLayer);
            if (layer < 0)
            {
                Debug.LogError($"[Setup] '{InteractableLayer}' 레이어가 없음 — Project Settings > Tags and Layers 확인");
                return "실패";
            }

            string result = $"재질 {(materialCreated ? "생성" : "유지")}";
            result += Ensure(FarmCellPath, () => BuildFarmCell(material, layer));
            result += Ensure(WorldItemPath, () => BuildWorldItem(material, layer));
            result += Ensure(StoragePointPath, () => BuildStoragePoint(material, layer));
            result += Ensure(DoorPath, () => BuildDoor(layer));
            result += Ensure(StainPath, () => BuildStain(layer));
            result += Ensure(TrashBinPath, () => BuildTrashBin(layer));
            result += Ensure(ShelfItemPath, () => BuildShelfItem(material));
            result += Ensure(ShelfPath, () => BuildShelf(material, layer));
            return result;
        }

        // 진열된 작물 하나: 콜라이더 없는 작은 구 (색은 작물 그레이박스 색 — 진열대가 칠한다).
        private static GameObject BuildShelfItem(Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "shelf_item";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        // 진열대: 루트 조준 콜라이더 + ShelfDisplay / Body 상자 / Hologram 구 (Holograms 패키지 셰이더 재질). 로컬 +Z = 손님 쪽.
        private static GameObject BuildShelf(Material material, int layer)
        {
            var root = new GameObject("shelf") { layer = layer };
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(6.2f, 1.3f, 0.9f);
            box.center = new Vector3(0f, 0.65f, 0f);

            var wood = EnsureMaterial(ShelfMaterialPath, new Color(0.35f, 0.3f, 0.45f), out _);
            var body = CreateVisual(PrimitiveType.Cube, "Body", root.transform, wood);
            body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            body.transform.localScale = new Vector3(6.2f, 1f, 0.8f);

            var holo = CreateVisual(PrimitiveType.Sphere, "Hologram", root.transform, EnsureHologramMaterial());
            holo.transform.localScale = Vector3.one * 0.24f;
            holo.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            holo.SetActive(false);

            var shelf = root.AddComponent<Systems.Shop.ShelfDisplay>();
            var so = new SerializedObject(shelf);
            so.FindProperty("settings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Data.InventorySettingsData>("Assets/_Project/Data/Stats/InventorySettings.asset");
            so.FindProperty("database").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Data.ContentDatabase>("Assets/_Project/Data/ContentDatabase.asset");
            so.FindProperty("body").objectReferenceValue = body.transform;
            so.FindProperty("aimCollider").objectReferenceValue = box;
            so.FindProperty("hologram").objectReferenceValue = holo.GetComponent<Renderer>();
            so.FindProperty("itemPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Renderer>(ShelfItemPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // 진열 홀로그램 재질: Holograms 패키지의 스캔라인 셰이더 그래프로 만든다 (패키지 파일은 건드리지 않는다).
        // 값은 패키지 예제 재질(Scanline_Hologram_Empty — 용량 정리로 지움, 2-5)에서 옮긴 것. 색은 진열대가 실행 중 바꾼다.
        private static Material EnsureHologramMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(HologramMaterialPath);
            if (material != null)
                return material;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(HologramShaderPath);
            if (shader == null)
            {
                Debug.LogError($"[Setup] 홀로그램 셰이더가 없음: {HologramShaderPath} - Holograms 패키지 확인");
                return EnsureTransparentMaterial(StainMaterialPath);
            }
            material = new Material(shader) { name = Path.GetFileNameWithoutExtension(HologramMaterialPath) };
            material.SetColor("_Hologram_Color", new Color(0.38f, 0.95f, 0.88f, 0.35f));
            material.SetColor("_Texture_Tint_Color", new Color(0f, 0.374f, 0.443f, 0.298f));
            material.SetFloat("_Emission_Scale", 10f);
            material.SetFloat("_Fresnel_Power", 1.5f);
            material.SetFloat("_Pattern_Speed", 5f);
            material.SetFloat("_Pattern_Density", 250f);
            material.SetVector("_Pattern_Direction", new Vector4(0f, -1f, 0f, 0f));
            material.SetFloat("_Pattern_Edge_A", 0.1f);
            material.SetFloat("_Pattern_Edge_B", 0.5f);
            material.SetFloat("_Pattern_Boost", 0.5f);
            material.SetFloat("_Pattern_Noise_Scale", 250f);
            material.SetFloat("_Displacement_Speed", 0.5f);
            material.SetFloat("_Displacement_Density", 0.2f);
            material.SetVector("_Displacement_Noise_Direction", new Vector4(0f, 1f, 0f, 0f));
            EnsureFolder(Path.GetDirectoryName(HologramMaterialPath).Replace('\\', '/'));
            AssetDatabase.CreateAsset(material, HologramMaterialPath);
            return material;
        }

        // 얼룩: 조준용 얇은 트리거(Interactable) + Stain / Model 반투명 원판 (투명도 = 남은 양). URP Decal 교체는 비주얼 단계.
        private static GameObject BuildStain(int layer)
        {
            var root = new GameObject("stain") { layer = layer };
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.9f, 0.06f, 0.9f);
            box.center = new Vector3(0f, 0.03f, 0f);

            var translucent = EnsureTransparentMaterial(StainMaterialPath);
            var model = CreateVisual(PrimitiveType.Cylinder, "Model", root.transform, translucent);
            model.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            model.transform.localScale = new Vector3(0.9f, 0.005f, 0.9f);

            var stain = root.AddComponent<Systems.Cleaning.Stain>();
            var so = new SerializedObject(stain);
            so.FindProperty("modelRenderer").objectReferenceValue = model.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // 쓰레기통: 루트 콜라이더 + TrashBin / Model 통 (짙은 초록).
        private static GameObject BuildTrashBin(int layer)
        {
            var root = new GameObject("trash_bin") { layer = layer };
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(0.7f, 0.9f, 0.7f);
            box.center = new Vector3(0f, 0.45f, 0f);

            var green = EnsureMaterial(TrashBinMaterialPath, new Color(0.15f, 0.4f, 0.3f), out _);
            var model = CreateVisual(PrimitiveType.Cylinder, "Model", root.transform, green);
            model.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            model.transform.localScale = new Vector3(0.7f, 0.45f, 0.7f);

            var bin = root.AddComponent<Systems.Cleaning.TrashBin>();
            var so = new SerializedObject(bin);
            so.FindProperty("settings").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Data.CleaningSettingsData>("Assets/_Project/Data/Stats/CleaningSettings.asset");
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // URP Unlit 반투명 재질 (알파는 MaterialPropertyBlock으로 칸마다 바꾼다).
        private static Material EnsureTransparentMaterial(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = Path.GetFileNameWithoutExtension(path) };
            material.SetFloat("_Surface", 1f);   // Transparent
            material.SetFloat("_Blend", 0f);     // Alpha
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetColor("_BaseColor", new Color(0.25f, 0.18f, 0.32f, 0.85f));
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // 지역 문: 루트 콜라이더 + ZoneDoor / Model 문짝 (금빛 — 그레이박스 구분용). 앞쪽(+Z)이 플레이어 쪽.
        private static GameObject BuildDoor(int layer)
        {
            var root = new GameObject("zone_door") { layer = layer };
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(1.4f, 2.4f, 0.4f);
            box.center = new Vector3(0f, 1.2f, 0f);

            var gold = EnsureMaterial(DoorMaterialPath, new Color(1f, 0.82f, 0.48f), out _); // 금빛 #FFD27A
            var model = CreateVisual(PrimitiveType.Cube, "Model", root.transform, gold);
            model.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            model.transform.localScale = new Vector3(1.4f, 2.4f, 0.3f);

            root.AddComponent<Systems.Zones.ZoneDoor>();
            return root;
        }

        private static string Ensure(string path, System.Func<GameObject> build)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                return $" · {name} 유지";
            var root = build();
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return $" · {name} 생성";
        }

        // 바닥 아이템: 루트 콜라이더(줍기 조준) + WorldItem / Model 작은 상자 (아이템 색).
        private static GameObject BuildWorldItem(Material material, int layer)
        {
            var root = new GameObject("world_item") { layer = layer };
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true; // 조준(트리거도 맞음)만 하고 몸은 통과 — 쓰레기에 걸려 넘어가지 못하지 않게
            box.size = new Vector3(0.45f, 0.35f, 0.45f);
            box.center = new Vector3(0f, 0.175f, 0f);

            var model = CreateVisual(PrimitiveType.Cube, "Model", root.transform, material);
            model.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            model.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

            var item = root.AddComponent<Systems.Items.WorldItem>();
            var so = new SerializedObject(item);
            so.FindProperty("modelRenderer").objectReferenceValue = model.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // 창고 접근 지점: 루트 콜라이더 + StorageAccessPoint / Model 상자 1 m (나무 상자 자리).
        private static GameObject BuildStoragePoint(Material material, int layer)
        {
            var root = new GameObject("storage_point") { layer = layer };
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(1.2f, 1f, 0.8f);
            box.center = new Vector3(0f, 0.5f, 0f);

            // 프리팹에는 MaterialPropertyBlock이 저장되지 않으므로 색은 전용 재질로 (보라 발광 #A08BFF — 그레이박스 구분용).
            var purple = EnsureMaterial(StorageMaterialPath, new Color(0.63f, 0.55f, 1f), out _);
            var model = CreateVisual(PrimitiveType.Cube, "Model", root.transform, purple);
            model.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            model.transform.localScale = new Vector3(1.2f, 1f, 0.8f);

            var point = root.AddComponent<Systems.Storage.StorageAccessPoint>();
            var so = new SerializedObject(point);
            so.FindProperty("settings").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Data.InventorySettingsData>("Assets/_Project/Data/Stats/InventorySettings.asset");
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static GameObject BuildFarmCell(Material material, int layer)
        {
            // 루트: 콜라이더(조준 대상) + FarmCellView. 크기는 칸 2 m 간격 중 흙 1.6 m (FarmGrid cellFootprint와 같게)
            // — BALANCE 9-1 안쪽 줄 칸 중심 z ±1.8에서 가장자리가 길 경계 z ±1.0에 맞는 크기.
            var root = new GameObject("farm_cell") { layer = layer };
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(CellSize, 0.3f, CellSize);
            box.center = new Vector3(0f, 0.15f, 0f);

            var soil = CreateVisual(PrimitiveType.Cube, "Soil", root.transform, material);
            soil.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            soil.transform.localScale = new Vector3(CellSize, 0.1f, CellSize);

            var crop = CreateVisual(PrimitiveType.Sphere, "Crop", root.transform, material);
            crop.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            crop.SetActive(false);

            var view = root.AddComponent<FarmCellView>();
            var so = new SerializedObject(view);
            so.FindProperty("soilRenderer").objectReferenceValue = soil.GetComponent<Renderer>();
            so.FindProperty("cropRenderer").objectReferenceValue = crop.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // 모델 자식에는 콜라이더를 두지 않는다 — 조준은 루트 콜라이더 하나로.
        private static GameObject CreateVisual(PrimitiveType type, string name, Transform parent, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static Material EnsureMaterial(string path, Color color, out bool created)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            created = material == null;
            if (material != null)
                return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            material.SetColor("_BaseColor", color);
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
#endif
