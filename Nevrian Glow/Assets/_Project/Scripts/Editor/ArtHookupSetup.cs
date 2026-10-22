#if UNITY_EDITOR
using System.Collections.Generic;
using NevrianGlowFarm.Data;
using UnityEditor;
using UnityEngine;

namespace NevrianGlowFarm.Editor
{
    /// <summary>
    /// 아트 납품 게임 연결 (2026-10-22, 아트 연결 체크사항 — BRANCH_ART 10.21). 아트 원본(`Assets/Art`)은 건드리지 않고
    /// `_Project` 쪽 래퍼 프리팹 · 데이터 · 씬에서 잇는다. 여러 번 실행해도 결과가 같다 (있으면 다시 만들지 않고 값만 맞춤).
    /// </summary>
    public static class ArtHookupSetup
    {
        private const string HeldFolder = "Assets/_Project/Prefabs/Held";
        private const string ToolsFolder = "Assets/_Project/Data/Tools";
        private const string ArtProps = "Assets/Art/Props";

        // ── 1 · 3. 도구 손 모델 ──

        // 괭이: 아트 피벗 = 손잡이 가운데, 날 +Y · 날 끝 +Z (그레이박스 괭이 기준과 같음) → 회전 없이 0.83 m를 손에 맞는 0.55 m로
        private const float HoeHeldScale = 0.66f;

        [MenuItem("Glow Farm/Setup/Connect Art (1) Held Tools")]
        private static void HeldToolsMenu()
        {
            var notes = new List<string>();
            // 괭이 1 · 2 · 3단계 (1단계도 함께 — 2 · 3단계만 아트면 등급이 섞여 보임)
            notes.Add(HeldSimple("held_hoe_worn", $"{ArtProps}/hoe/pf_tool_hoe_worn.prefab", HoeHeldScale, "tool_hoe_1"));
            notes.Add(HeldSimple("held_hoe_iron", $"{ArtProps}/hoe/pf_tool_hoe_iron.prefab", HoeHeldScale, "tool_hoe_2"));
            notes.Add(HeldSimple("held_hoe_glow", $"{ArtProps}/hoe/pf_tool_hoe_glow.prefab", HoeHeldScale, "tool_hoe_3"));
            // 대걸레 2 · 3단계: 1단계 래퍼(held_mop — 자세 · Grip · Contact 표시점)를 복제해 아트 원본만 바꾼다
            notes.Add(HeldSwapped("held_mop", "held_mop_good", $"{ArtProps}/mop/pf_tool_mop_good.prefab", "tool_mop_2"));
            notes.Add(HeldSwapped("held_mop", "held_mop_glow", $"{ArtProps}/mop/pf_tool_mop_glow.prefab", "tool_mop_3"));
            // 물뿌리개 4등급(밝은 다이아) 래퍼: 아트 bright 프리팹으로 바꾸고 _Project 변형 재질 덮어쓰기는 지운다 (재질 파일은 git에서 삭제)
            notes.Add(SwapSource("held_wateringcan_diamond_bright", $"{ArtProps}/wateringcan/pf_tool_wateringcan_bright.prefab", revertRenderers: true));
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] 도구 손 모델: " + string.Join(" · ", notes));
        }

        // 새 래퍼: 루트 + Model(아트 프리팹 인스턴스, 균일 크기)
        private static string HeldSimple(string name, string artPath, float scale, string toolId)
        {
            string path = $"{HeldFolder}/{name}.prefab";
            var art = AssetDatabase.LoadAssetAtPath<GameObject>(artPath);
            if (art == null)
                return $"{name}: 아트 프리팹 없음 ({artPath})";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                var root = new GameObject(name);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(art, root.transform);
                model.name = "Model";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one * scale;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
            }
            return $"{name} → {Link(toolId, path)}";
        }

        // 기존 래퍼를 복제해 Model의 아트 원본만 바꾼다 (자세 · 추가한 표시점 · 덮어쓴 값 유지)
        private static string HeldSwapped(string fromName, string name, string artPath, string toolId)
        {
            string path = $"{HeldFolder}/{name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                if (!AssetDatabase.CopyAsset($"{HeldFolder}/{fromName}.prefab", path))
                    return $"{name}: {fromName} 복사 실패";
                AssetDatabase.ImportAsset(path);
            }
            string swapped = SwapSource(name, artPath, revertRenderers: false);
            return $"{swapped} · {Link(toolId, path)}";
        }

        private static string SwapSource(string name, string artPath, bool revertRenderers)
        {
            string path = $"{HeldFolder}/{name}.prefab";
            var art = AssetDatabase.LoadAssetAtPath<GameObject>(artPath);
            if (art == null || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return $"{name}: 래퍼 또는 아트 프리팹 없음";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.name = name;
                // 바꾼 뒤 이름이 아트 프리팹 이름이 되므로 둘 다 찾는다 (다시 실행해도 같게)
                var model = root.transform.Find("Model") ?? root.transform.Find(art.name);
                if (model == null || !PrefabUtility.IsAnyPrefabInstanceRoot(model.gameObject))
                    return $"{name}: Model(프리팹 인스턴스) 없음";
                var current = PrefabUtility.GetCorrespondingObjectFromSource(model.gameObject);
                if (current != art)
                    PrefabUtility.ReplacePrefabAssetOfPrefabInstance(model.gameObject, art, InteractionMode.AutomatedAction);
                if (revertRenderers)
                    foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                        if (PrefabUtility.IsPartOfPrefabInstance(r))
                            PrefabUtility.RevertObjectOverride(r, InteractionMode.AutomatedAction);
                model.name = "Model";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return $"{name} ← {System.IO.Path.GetFileNameWithoutExtension(artPath)}";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ── 5 · 6. 작물 단계 ──

        private const string CropPrefabFolder = "Assets/_Project/Prefabs/Farm/Crops";
        private const string CropDataFolder = "Assets/_Project/Data/Crops";
        private const string ArtCrops = "Assets/Art/Crops";

        [MenuItem("Glow Farm/Setup/Connect Art (3) Crop Stages")]
        private static void CropStagesMenu()
        {
            var notes = new List<string>();
            // 버섯꽃: [0] 공용 둔덕 · [1] small · [2] mature. 거대(giant)는 거대 작물(4-4, P1)용으로 Variant만 — 완성 4칸 한가운데에 놓을 것
            var mushSmall = CropVariant("crop_mushroomflower_small", $"{ArtCrops}/mushroomflower/pf_crop_mushroomflower_small.prefab", notes);
            var mushMature = CropVariant("crop_mushroomflower_mature", $"{ArtCrops}/mushroomflower/pf_crop_mushroomflower_mature.prefab", notes);
            CropVariant("crop_mushroomflower_giant", $"{ArtCrops}/mushroomflower/pf_crop_mushroomflower_giant.prefab", notes);
            notes.Add(SetStages("crop_mushroomflower", new[] { (1, mushSmall), (2, mushMature) }, 3));
            // 등불 열매: [0] 공용 둔덕 · [1] sapling · [2] stem · [3] mature (재수확: Harvest · Regrow 트리거 — FarmCellView)
            var lanSapling = CropVariant("crop_lanternfruit_sapling", $"{ArtCrops}/lanternfruit/pf_crop_lanternfruit_sapling.prefab", notes);
            var lanStem = CropVariant("crop_lanternfruit_stem", $"{ArtCrops}/lanternfruit/pf_crop_lanternfruit_stem.prefab", notes);
            var lanMature = CropVariant("crop_lanternfruit_mature", $"{ArtCrops}/lanternfruit/pf_crop_lanternfruit_mature.prefab", notes);
            notes.Add(SetStages("crop_lanternfruit", new[] { (1, lanSapling), (2, lanStem), (3, lanMature) }, 4));
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] 작물 단계: " + string.Join(" · ", notes));
        }

        // 아트 프리팹의 Prefab Variant (_Project 쪽 — 아트 원본은 그대로). 같은 경로에 다른 것이 있으면 덮어쓴다(GUID 유지)
        private static GameObject CropVariant(string name, string artPath, List<string> notes)
        {
            string path = $"{CropPrefabFolder}/{name}.prefab";
            var art = AssetDatabase.LoadAssetAtPath<GameObject>(artPath);
            if (art == null)
            {
                notes.Add($"{name}: 아트 없음");
                return null;
            }
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && PrefabUtility.GetPrefabAssetType(existing) == PrefabAssetType.Variant
                && PrefabUtility.GetCorrespondingObjectFromSource(existing) == art)
                return existing;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(art);
            instance.name = name;
            var saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            notes.Add(existing != null ? $"{name} 바꿈" : $"{name} 만듦");
            return saved;
        }

        private static string SetStages(string cropId, (int Slot, GameObject Prefab)[] slots, int count)
        {
            var crop = AssetDatabase.LoadAssetAtPath<CropData>($"{CropDataFolder}/{cropId}.asset");
            if (crop == null)
                return $"{cropId} 없음";
            var so = new SerializedObject(crop);
            var list = so.FindProperty("stagePrefabs");
            if (list.arraySize < count)
                list.arraySize = count;
            foreach (var (slot, prefab) in slots)
                if (prefab != null)
                    list.GetArrayElementAtIndex(slot).objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            return $"{cropId} 단계 {list.arraySize}칸";
        }

        // ── 4. 농장 문 ──

        private const string ZoneDoorPrefab = "Assets/_Project/Prefabs/Zones/zone_door.prefab";
        private const string FarmDoorArt = "Assets/Art/Environment/farm_door/pf_env_farm_door.prefab";

        [MenuItem("Glow Farm/Setup/Connect Art (2) Farm Door")]
        private static void FarmDoorMenu()
        {
            var note = ReplaceModel(ZoneDoorPrefab, FarmDoorArt, root =>
            {
                // 문 열기 연출: 아트 문 루트의 Animator (Bool IsOpen) — ZoneDoor가 쓸 때 켠다
                var door = root.GetComponent<Systems.Zones.ZoneDoor>();
                var model = root.transform.Find("Model");
                var animator = model != null ? model.GetComponent<Animator>() : null;
                if (door == null || animator == null)
                    return "ZoneDoor 또는 문 Animator 없음";
                var so = new SerializedObject(door);
                so.FindProperty("doorAnimator").objectReferenceValue = animator;
                so.ApplyModifiedPropertiesWithoutUndo();
                return "Animator 연결";
            });
            Debug.Log("[Setup] 농장 문: " + note);
        }

        /// <summary>
        /// 프리팹의 'Model' 자식을 아트 프리팹 인스턴스로 바꾼다 (위치 · 회전 · 크기는 그대로). 이미 그 아트면 그대로 두고 after만 실행.
        /// </summary>
        private static string ReplaceModel(string prefabPath, string artPath, System.Func<GameObject, string> after)
        {
            var art = AssetDatabase.LoadAssetAtPath<GameObject>(artPath);
            if (art == null || AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                return $"프리팹 또는 아트 없음 ({prefabPath} · {artPath})";
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var old = root.transform.Find("Model");
                if (old == null)
                    return $"{prefabPath}에 Model 자식 없음";
                string note;
                bool already = PrefabUtility.IsAnyPrefabInstanceRoot(old.gameObject) && PrefabUtility.GetCorrespondingObjectFromSource(old.gameObject) == art;
                if (already)
                    note = "Model 유지";
                else
                {
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(art, root.transform);
                    model.name = "Model";
                    model.transform.SetSiblingIndex(old.GetSiblingIndex());
                    model.transform.localPosition = old.localPosition;
                    model.transform.localRotation = old.localRotation;
                    model.transform.localScale = old.localScale;
                    Object.DestroyImmediate(old.gameObject);
                    note = $"Model → {art.name}";
                }
                if (after != null)
                    note += " · " + after(root);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return note;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string Link(string toolId, string heldPath)
        {
            var tool = AssetDatabase.LoadAssetAtPath<ItemData>($"{ToolsFolder}/{toolId}.asset");
            var held = AssetDatabase.LoadAssetAtPath<GameObject>(heldPath);
            if (tool == null || held == null)
                return $"{toolId} 연결 실패";
            var so = new SerializedObject(tool);
            so.FindProperty("heldPrefab").objectReferenceValue = held;
            so.ApplyModifiedPropertiesWithoutUndo();
            return toolId;
        }
    }
}
#endif
