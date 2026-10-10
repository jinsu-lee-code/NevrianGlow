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
        private const string FarmCellPath = "Assets/_Project/Prefabs/Farm/farm_cell.prefab";
        private const string InteractableLayer = "Interactable";
        private const float CellSize = 1.6f;

        [MenuItem("Glow Farm/Setup/Create Greybox Prefabs")]
        private static void CreateMenu()
        {
            Debug.Log("[Setup] 그레이박스 프리팹: " + Run());
        }

        public static string Run()
        {
            var material = EnsureMaterial(out bool materialCreated);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FarmCellPath) != null)
                return $"재질 {(materialCreated ? "생성" : "유지")} · farm_cell 유지";

            int layer = LayerMask.NameToLayer(InteractableLayer);
            if (layer < 0)
            {
                Debug.LogError($"[Setup] '{InteractableLayer}' 레이어가 없음 — Project Settings > Tags and Layers 확인");
                return "실패";
            }

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

            EnsureFolder(Path.GetDirectoryName(FarmCellPath).Replace('\\', '/'));
            PrefabUtility.SaveAsPrefabAsset(root, FarmCellPath);
            Object.DestroyImmediate(root);
            return $"재질 {(materialCreated ? "생성" : "유지")} · farm_cell 생성";
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

        private static Material EnsureMaterial(out bool created)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            created = material == null;
            if (material != null)
                return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader) { name = "mat_greybox_cell" };
            EnsureFolder(Path.GetDirectoryName(MaterialPath).Replace('\\', '/'));
            AssetDatabase.CreateAsset(material, MaterialPath);
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
