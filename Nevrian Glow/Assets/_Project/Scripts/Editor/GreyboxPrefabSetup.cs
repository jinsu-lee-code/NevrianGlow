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
            return result;
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
