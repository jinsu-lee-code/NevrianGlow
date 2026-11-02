#if UNITY_EDITOR
using System.Collections.Generic;
using NevrianGlowFarm.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NevrianGlowFarm.Editor
{
    /// <summary>
    /// 아트 '개발에 부탁할 것 (11/01)' 연결 (mapping.md · BRANCH_ART 11/01 ~ 11/02). 아트 원본은 그대로, `_Project` 데이터 · 씬만. 여러 번 실행해도 같다.
    /// 쓰레기 아이콘 · 씨앗 가루 아트판 · 비료 성장 아트판(VFX 목록) · 가게 씬 옛 램프 2개 삭제 · 가게 환경광 색.
    /// </summary>
    public static class ArtHookup1102Setup
    {
        private const string VfxLibraryPath = "Assets/_Project/Data/Effects/VfxLibrary.asset";
        private const string ShopScenePath = "Assets/_Project/Scenes/Shop.unity";
        private const string ShopZonePath = "Assets/_Project/Data/Zones/zone_shop.asset";
        // 보라 기가 적은 라벤더 그레이 (아트 요청 — 예전 0.36, 0.29, 0.36). 값은 개발이 정함
        private static readonly Color ShopAmbient = new Color(0.33f, 0.31f, 0.35f, 1f);

        [MenuItem("Glow Farm/Setup/Connect Art (12) 11-02 Requests")]
        private static void Menu()
        {
            var notes = new List<string>
            {
                TrashIcon(),
                Vfx(Core.VfxCues.SeedDust, "Assets/Art/Environment/FX/seed_dust/pf_vfx_seed_dust.prefab", 0.8f),
                Vfx(Core.VfxCues.FertilizerGrow, "Assets/Art/Environment/FX/fertilizer_grow/pf_vfx_fertilizer_grow.prefab", 1.2f),
                ShopAmbientColor(),
                ShopLamps(),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] 아트 11/02 요청: " + string.Join(" · ", notes));
        }

        private static string TrashIcon()
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/_Project/Data/Items/item_trash.asset");
            var icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Icons/icon_trash.png");
            if (item == null || icon == null)
                return "쓰레기 아이콘 못 넣음";
            var so = new SerializedObject(item);
            so.FindProperty("icon").objectReferenceValue = icon;
            so.ApplyModifiedPropertiesWithoutUndo();
            return "쓰레기 아이콘";
        }

        // VFX 목록에 id → 아트 프리팹 (있으면 프리팹 · 지우는 시간만 바꿈)
        private static string Vfx(string id, string prefabPath, float lifetime)
        {
            var library = AssetDatabase.LoadAssetAtPath<VfxLibraryData>(VfxLibraryPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (library == null || prefab == null)
                return $"{id} 못 넣음";
            var so = new SerializedObject(library);
            var list = so.FindProperty("entries");
            SerializedProperty entry = null;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == id)
                    entry = list.GetArrayElementAtIndex(i);
            if (entry == null)
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                entry = list.GetArrayElementAtIndex(list.arraySize - 1);
                entry.FindPropertyRelative("id").stringValue = id;
                entry.FindPropertyRelative("scale").floatValue = 1f;
            }
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("lifetime").floatValue = lifetime;
            so.ApplyModifiedPropertiesWithoutUndo();
            return $"{id} ← {prefab.name}";
        }

        private static string ShopAmbientColor()
        {
            var zone = AssetDatabase.LoadAssetAtPath<ZoneProfileData>(ShopZonePath);
            if (zone == null)
                return "zone_shop 없음";
            var so = new SerializedObject(zone);
            so.FindProperty("ambientColor").colorValue = ShopAmbient;
            so.ApplyModifiedPropertiesWithoutUndo();
            return "가게 환경광 라벤더 그레이";
        }

        // 아트가 샹들리에로 바꾸며 지운 옛 전구 중 개발 씬에 남은 2개 (그 전까지 아트 HideSceneLamps가 숨김)
        private static string ShopLamps()
        {
            var scene = EditorSceneManager.GetSceneByPath(ShopScenePath);
            bool opened = false;
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(ShopScenePath, OpenSceneMode.Additive);
                opened = true;
            }
            int removed = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t != null && (t.name == "OrbLamp_1 (1)" || t.name == "OrbLamp_1 (2)"))
                    {
                        Object.DestroyImmediate(t.gameObject);
                        removed++;
                    }
            if (removed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
            return $"가게 옛 램프 {removed}개 삭제";
        }
    }
}
#endif
