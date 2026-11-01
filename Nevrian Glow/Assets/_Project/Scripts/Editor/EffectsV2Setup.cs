#if UNITY_EDITOR
using System.Collections.Generic;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Systems.Interaction;
using NevrianGlowFarm.Systems.Rendering;
using NevrianGlowFarm.Systems.Zones;
using NevrianGlowFarm.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NevrianGlowFarm.Editor
{
    /// <summary>
    /// 연출 v2 (최종 기획서 v2.0 · 주요 변동사항 10/29, 아트 피드백 10/30 2번): 화면 가운데 E 꾹 진행 고리, 씨앗 가루 입자(개발 임시 — 아트 교체 대기),
    /// 농장 문 포털 연결, 화면 외곽선 거리(먼 배경 빼기) 연결. 여러 번 실행해도 결과가 같다.
    /// </summary>
    public static class EffectsV2Setup
    {
        private const string CorePath = "Assets/_Project/Scenes/Core.unity";
        private const string RingPath = "Assets/_Project/Art/UI/ui_hold_ring.png";
        private const string SeedDustPath = "Assets/_Project/Prefabs/Effects/vfx_seed_dust.prefab";
        private const string LightPuffPath = "Assets/Art/Environment/FX/light_puff/pf_vfx_light_puff.prefab";
        private const string VfxLibraryPath = "Assets/_Project/Data/Effects/VfxLibrary.asset";
        private const string ZoneDoorPath = "Assets/_Project/Prefabs/Zones/zone_door.prefab";
        private const string PortalPath = "Assets/Art/Environment/FX/portal/pf_vfx_portal.prefab";
        private const string OutlinePath = "Assets/Art/Environment/Materials_PP/mat_pp_screen_outline.mat";

        [MenuItem("Glow Farm/Setup/Apply Effects v2 (11-01)")]
        private static void Apply()
        {
            var notes = new List<string>();
            var ring = LoadOrMakeRing();
            notes.Add(MakeSeedDust());
            notes.Add(LinkPortal());

            var scene = EditorSceneManager.GetSceneByPath(CorePath);
            bool opened = false;
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(CorePath, OpenSceneMode.Additive);
                opened = true;
            }
            // 씬을 연 뒤 에셋을 다시 잡는다 (앞서 잡은 참조가 null로 저장되는 일 방지)
            ring = AssetDatabase.LoadAssetAtPath<Sprite>(RingPath);
            notes.Add(BuildHoldRing(scene, ring));
            notes.Add(LinkOutline(scene));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] 연출 v2: " + string.Join(" · ", notes));
        }

        // 고리 그림 128 × 128 (흰색 · 알파, 가장자리 부드럽게) — 없을 때만 만든다
        private static Sprite LoadOrMakeRing()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(RingPath) == null)
            {
                const int size = 128;
                const float outer = 60f, inner = 50f;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                        float a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                    }
                tex.SetPixels(pixels);
                tex.Apply();
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(RingPath));
                System.IO.File.WriteAllBytes(RingPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(RingPath);
            }
            if (AssetImporter.GetAtPath(RingPath) is TextureImporter importer
                && (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(RingPath);
        }

        // UICanvas/Crosshair/HoldRing: 흐린 바탕 고리 + 차오르는 고리(시계 방향, 위에서 시작)
        private static string BuildHoldRing(UnityEngine.SceneManagement.Scene scene, Sprite ring)
        {
            Transform canvas = null;
            InteractionRaycaster raycaster = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var c in root.GetComponentsInChildren<Canvas>(true))
                    if (c.name == "UICanvas")
                        canvas = c.transform;
                if (raycaster == null)
                    raycaster = root.GetComponentInChildren<InteractionRaycaster>(true);
            }
            var crosshair = canvas != null ? canvas.Find("Crosshair") : null;
            if (crosshair == null || raycaster == null)
                return "진행 고리 못 만듦 (Crosshair · InteractionRaycaster 없음)";

            var existing = crosshair.Find("HoldRing");
            string note = existing != null ? "진행 고리 유지" : "진행 고리 생성";
            RectTransform root2;
            if (existing == null)
            {
                root2 = new GameObject("HoldRing", typeof(RectTransform), typeof(HoldRingView)).GetComponent<RectTransform>();
                root2.SetParent(crosshair, false);
                new GameObject("Track", typeof(RectTransform), typeof(Image)).transform.SetParent(root2, false);
                new GameObject("Fill", typeof(RectTransform), typeof(Image)).transform.SetParent(root2, false);
                Undo.RegisterCreatedObjectUndo(root2.gameObject, "Create HoldRing");
            }
            else
                root2 = existing as RectTransform;

            root2.anchorMin = root2.anchorMax = new Vector2(0.5f, 0.5f);
            root2.pivot = new Vector2(0.5f, 0.5f);
            root2.anchoredPosition = Vector2.zero;
            root2.sizeDelta = new Vector2(58f, 58f);
            var track = root2.Find("Track").GetComponent<Image>();
            var fill = root2.Find("Fill").GetComponent<Image>();
            foreach (var img in new[] { track, fill })
            {
                var r = img.rectTransform;
                r.anchorMin = Vector2.zero;
                r.anchorMax = Vector2.one;
                r.offsetMin = r.offsetMax = Vector2.zero;
                img.sprite = ring;
                img.raycastTarget = false;
                EditorUtility.SetDirty(img);
            }
            track.type = Image.Type.Simple;
            track.color = new Color(1f, 1f, 1f, 0.22f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            fill.fillAmount = 0f;
            fill.color = new Color(0.55f, 0.95f, 1f, 0.95f); // 시안 = 상호작용 (색 규칙)

            var so = new SerializedObject(root2.GetComponent<HoldRingView>());
            so.FindProperty("raycaster").objectReferenceValue = raycaster;
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("track").objectReferenceValue = track;
            so.ApplyModifiedPropertiesWithoutUndo();
            return note;
        }

        // 씨앗 가루: 흙 위로 작은 빛가루가 0.25 ~ 0.4초 튀었다 가라앉는다 (재질은 아트 빛가루 효과의 것). 프리팹은 늘 다시 만든다
        private static string MakeSeedDust()
        {
            var puff = AssetDatabase.LoadAssetAtPath<GameObject>(LightPuffPath);
            var puffRenderer = puff != null ? puff.GetComponentInChildren<ParticleSystemRenderer>(true) : null;
            if (puffRenderer == null || puffRenderer.sharedMaterial == null)
                return "씨앗 가루 못 만듦 (아트 빛가루 효과 재질 없음)";

            var go = new GameObject("vfx_seed_dust");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.4f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.86f, 0.55f, 1f), new Color(0.6f, 0.95f, 1f, 1f));
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 32;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = 0.08f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // 위로
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = puffRenderer.sharedMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SeedDustPath));
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, SeedDustPath);
            Object.DestroyImmediate(go);

            // VFX 목록에 vfx_seed_dust (있으면 프리팹만 — 아트가 바꾼 것은 그대로)
            var library = AssetDatabase.LoadAssetAtPath<VfxLibraryData>(VfxLibraryPath);
            if (library == null)
                return "씨앗 가루 프리팹 · VFX 목록 없음";
            var so = new SerializedObject(library);
            var list = so.FindProperty("entries");
            SerializedProperty e = null;
            for (int i = 0; i < list.arraySize && e == null; i++)
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == Core.VfxCues.SeedDust)
                    e = list.GetArrayElementAtIndex(i);
            string note = "씨앗 가루 유지";
            if (e == null)
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                e = list.GetArrayElementAtIndex(list.arraySize - 1);
                e.FindPropertyRelative("id").stringValue = Core.VfxCues.SeedDust;
                e.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                e.FindPropertyRelative("lifetime").floatValue = 0.8f;
                e.FindPropertyRelative("scale").floatValue = 1f;
                note = "씨앗 가루 생성";
            }
            else if (e.FindPropertyRelative("prefab").objectReferenceValue == null)
                e.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            return note;
        }

        // 농장 문(zone_door)의 아트 문 모델 안 포털 빛을 ZoneDoor에 연결
        private static string LinkPortal()
        {
            var root = PrefabUtility.LoadPrefabContents(ZoneDoorPath);
            try
            {
                var door = root.GetComponentInChildren<ZoneDoor>(true);
                // 아트 포털 프리팹(pf_vfx_portal)을 끼운 자리 — 위에서부터 처음 만나는 것이 그 프리팹의 뿌리 (이름은 아트가 'Portal'로 바꿔 둠)
                Transform portal = null;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (portal == null && AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(t.gameObject)) == PortalPath)
                        portal = t;
                if (door == null || portal == null)
                    return "포털 연결 못 함 (문 · 포털 없음)";
                var so = new SerializedObject(door);
                so.FindProperty("portal").objectReferenceValue = portal;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, ZoneDoorPath);
                return "포털 연결";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string LinkOutline(UnityEngine.SceneManagement.Scene scene)
        {
            ViewLayers layers = null;
            foreach (var root in scene.GetRootGameObjects())
                if (layers == null)
                    layers = root.GetComponentInChildren<ViewLayers>(true);
            var outline = AssetDatabase.LoadAssetAtPath<Material>(OutlinePath);
            if (layers == null || outline == null)
                return "외곽선 연결 못 함 (ViewLayers · 아트 외곽선 재질 없음)";
            var so = new SerializedObject(layers);
            so.FindProperty("outlineMaterial").objectReferenceValue = outline;
            so.ApplyModifiedPropertiesWithoutUndo();
            return "외곽선 거리 연결";
        }
    }
}
#endif
