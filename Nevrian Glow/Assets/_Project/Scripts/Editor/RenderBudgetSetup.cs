#if UNITY_EDITOR
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Systems.Rendering;
using NevrianGlowFarm.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace NevrianGlowFarm.Editor
{
    /// <summary>
    /// 최적화 (2026-10-30) 준비 메뉴 — 여러 번 실행해도 결과가 같다 (있으면 연결만 다시).
    /// · Create Render Budget: 그래픽 품질 데이터(없을 때만 기본 3단계) + Core 씬 RenderBudget + 설정 패널(타이틀 · 일시정지)의 '그래픽 품질' 줄.
    /// · Create Loading Screen: Core UICanvas 로딩 화면 + 타이틀 연결.
    /// · Create View Layers: 근경 · 중경 · 원경 데이터(없을 때만) + RenderBudget 오브젝트에 ViewLayers (아트 효과 재질 · 렌더러 연결).
    /// </summary>
    public static class RenderBudgetSetup
    {
        internal const string QualityPath = "Assets/_Project/Data/Stats/GraphicsQuality.asset";
        private const string ViewLayersPath = "Assets/_Project/Data/Stats/ViewLayers.asset";
        private const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        private const string PaintBrushPath = "Assets/Art/Environment/Materials_PP/mat_pp_paint_brush.mat";
        private const string AerialPath = "Assets/Art/Environment/Materials_PP/mat_pp_aerial_perspective.mat";
        private const string FontPath = "Assets/_Project/Art/Fonts/font_maplestory_bold_sdf.asset";
        private const string LogoPath = "Assets/_Project/Art/UI/ui_logo.png";
        private const string GlowPath = "Assets/_Project/Art/UI/ui_glow_soft.png";
        private const string BarBackPath = "Assets/Art/UI/Panels/ui_bar_xp_bg.png";
        private const string BarFillPath = "Assets/Art/UI/Panels/ui_bar_xp_fill.png";

        // 설정 패널: 해상도 줄(y −380) · 전체 화면(−444) 아래에 품질 줄, '뒤로' 버튼 자리만큼 패널을 늘린다
        private const float QualityRowY = -510f;
        private const float SettingsHeight = 680f;

        // ── 그리기 예산 ──

        [MenuItem("Glow Farm/Setup/Create Render Budget")]
        private static void CreateRenderBudgetMenu()
        {
            var quality = EnsureQualityData(out bool created);
            var cam = FindViewCamera();
            if (cam == null)
            {
                Debug.LogError("[Setup] Core 씬의 플레이어 카메라(MainCamera 태그)를 찾지 못함 - Core 씬을 연다");
                return;
            }
            var go = GameObject.Find("RenderBudget");
            if (go == null || go.scene != cam.gameObject.scene)
            {
                go = new GameObject("RenderBudget");
                if (go.scene != cam.gameObject.scene)
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, cam.gameObject.scene);
                Undo.RegisterCreatedObjectUndo(go, "Create RenderBudget");
            }
            if (!go.TryGetComponent<RenderBudget>(out var budget))
                budget = go.AddComponent<RenderBudget>();
            var so = new SerializedObject(budget);
            so.FindProperty("quality").objectReferenceValue = quality;
            so.FindProperty("viewCamera").objectReferenceValue = cam;
            so.ApplyModifiedPropertiesWithoutUndo();

            int rows = 0;
            foreach (var view in Object.FindObjectsByType<SettingsView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (AddQualityRow(view, quality))
                    rows++;
            EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
            Debug.Log($"[Setup] 그리기 예산: 품질 데이터 {(created ? "생성" : "유지")} ({quality.Tiers.Count}단계) · RenderBudget 연결 · 설정 패널 품질 줄 새로 {rows}개");
        }

        // ── 근경 · 중경 · 원경 (2026-10-31) ──

        [MenuItem("Glow Farm/Setup/Create View Layers")]
        private static void CreateViewLayersMenu()
        {
            var budget = Object.FindFirstObjectByType<RenderBudget>(FindObjectsInactive.Include);
            if (budget == null)
            {
                Debug.LogError("[Setup] Core 씬에 RenderBudget이 없음 - Core 씬을 열고 Create Render Budget을 먼저 실행");
                return;
            }
            var data = AssetDatabase.LoadAssetAtPath<ViewLayersData>(ViewLayersPath);
            bool created = data == null;
            if (created)
            {
                data = ScriptableObject.CreateInstance<ViewLayersData>();
                AssetDatabase.CreateAsset(data, ViewLayersPath);
                AssetDatabase.SaveAssets();
            }
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var brush = AssetDatabase.LoadAssetAtPath<Material>(PaintBrushPath);
            var aerial = AssetDatabase.LoadAssetAtPath<Material>(AerialPath);
            if (renderer == null || brush == null || aerial == null)
                Debug.LogWarning($"[Setup] 근경 · 중경 · 원경: 렌더러 {(renderer != null ? "있음" : "없음")} · 붓 터치 재질 {(brush != null ? "있음" : "없음")} · 거리 안개 재질 {(aerial != null ? "있음" : "없음")} - 없는 효과는 건너뜀");

            var go = budget.gameObject;
            if (!go.TryGetComponent<ViewLayers>(out var layers))
                layers = Undo.AddComponent<ViewLayers>(go);
            var so = new SerializedObject(layers);
            so.FindProperty("layers").objectReferenceValue = data;
            so.FindProperty("rendererData").objectReferenceValue = renderer;
            so.FindProperty("paintBrushMaterial").objectReferenceValue = brush;
            so.FindProperty("aerialMaterial").objectReferenceValue = aerial;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log($"[Setup] 근경 · 중경 · 원경: 데이터 {(created ? "생성" : "유지")} · {go.name}에 ViewLayers 연결");
        }

        private static Camera FindViewCamera()
        {
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (cam.CompareTag("MainCamera") && cam.gameObject.scene.name == "Core")
                    return cam;
            return null;
        }

        // 기본 3단계 (가벼운 것부터) — 높음 = 아트가 정한 값(URP 에셋 10/27: 그림자 120 m · 4096 · 4단계, 풀 아트 값 그대로)
        private static GraphicsQualityData EnsureQualityData(out bool created)
        {
            var data = AssetDatabase.LoadAssetAtPath<GraphicsQualityData>(QualityPath);
            created = data == null;
            if (!created)
                return data;
            data = ScriptableObject.CreateInstance<GraphicsQualityData>();
            AssetDatabase.CreateAsset(data, QualityPath);
            var so = new SerializedObject(data);
            var tiers = so.FindProperty("tiers");
            tiers.arraySize = 3;
            SetTier(tiers.GetArrayElementAtIndex(0), "낮음", 50f, 2048, 2, 0.4f, 0.5f, false, 0.3f, false, 0.8f, false);
            SetTier(tiers.GetArrayElementAtIndex(1), "보통", 100f, 2048, 4, 0.7f, 0.75f, true, 0.4f, false, 1f, true);
            SetTier(tiers.GetArrayElementAtIndex(2), "높음", 120f, 4096, 4, 1f, 1f, true, 0.5f, true, 1f, true);
            so.FindProperty("defaultTier").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return data;
        }

        private static void SetTier(SerializedProperty t, string name, float shadowDistance, int shadowResolution, int cascades,
            float grassDistance, float grassDensity, bool lake, float windowScale, bool windowShadows, float renderScale, bool smaa)
        {
            t.FindPropertyRelative("displayName").stringValue = name;
            t.FindPropertyRelative("shadowDistance").floatValue = shadowDistance;
            t.FindPropertyRelative("shadowResolution").intValue = shadowResolution;
            t.FindPropertyRelative("shadowCascades").intValue = cascades;
            t.FindPropertyRelative("grassDistanceScale").floatValue = grassDistance;
            t.FindPropertyRelative("grassDensityScale").floatValue = grassDensity;
            t.FindPropertyRelative("lakeReflection").boolValue = lake;
            t.FindPropertyRelative("windowViewScale").floatValue = windowScale;
            t.FindPropertyRelative("windowViewShadows").boolValue = windowShadows;
            t.FindPropertyRelative("renderScale").floatValue = renderScale;
            t.FindPropertyRelative("smaa").boolValue = smaa;
        }

        /// <summary>
        /// 설정 패널에 '그래픽 품질' 줄 (해상도 줄을 복제해 모양을 맞춤) — 이미 있으면 연결만. 새로 만들었으면 true.
        /// Create Meta Screens가 새 패널을 만들 때도 부른다.
        /// </summary>
        internal static bool AddQualityRow(SettingsView view, GraphicsQualityData quality)
        {
            var panel = (RectTransform)view.transform;
            bool created = false;
            var title = panel.Find("QualityTitle");
            if (title == null)
            {
                var sources = new[] { "ResolutionLabel", "ResolutionPrev", "Resolution", "ResolutionNext" };
                var names = new[] { "QualityTitle", "QualityPrev", "Quality", "QualityNext" };
                for (int i = 0; i < sources.Length; i++)
                {
                    var src = panel.Find(sources[i]);
                    if (src == null)
                    {
                        Debug.LogError($"[Setup] 설정 패널 '{panel.name}'에 '{sources[i]}'가 없어 품질 줄을 만들지 못함", view);
                        return false;
                    }
                    var copy = (RectTransform)Object.Instantiate(src, panel);
                    copy.name = names[i];
                    copy.anchoredPosition = new Vector2(copy.anchoredPosition.x, QualityRowY);
                    Undo.RegisterCreatedObjectUndo(copy.gameObject, "Add Quality Row");
                }
                title = panel.Find("QualityTitle");
                title.GetComponent<TMP_Text>().text = "그래픽 품질";
                panel.Find("Quality").GetComponent<TMP_Text>().text = "보통";
                panel.sizeDelta = new Vector2(panel.sizeDelta.x, Mathf.Max(panel.sizeDelta.y, SettingsHeight));
                created = true;
            }
            var so = new SerializedObject(view);
            so.FindProperty("quality").objectReferenceValue = quality;
            so.FindProperty("qualityLabel").objectReferenceValue = panel.Find("Quality").GetComponent<TMP_Text>();
            so.FindProperty("qualityPrev").objectReferenceValue = panel.Find("QualityPrev").GetComponent<Button>();
            so.FindProperty("qualityNext").objectReferenceValue = panel.Find("QualityNext").GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return created;
        }

        // ── 로딩 화면 ──

        [MenuItem("Glow Farm/Setup/Create Loading Screen")]
        private static void CreateLoadingScreenMenu()
        {
            var canvas = GameObject.Find("UICanvas");
            var boot = Object.FindFirstObjectByType<SceneBootstrapper>();
            var title = Object.FindFirstObjectByType<TitleScreen>(FindObjectsInactive.Include);
            if (canvas == null || boot == null)
            {
                Debug.LogError("[Setup] UICanvas · SceneBootstrapper가 없음 - Core 씬을 연다");
                return;
            }
            var screen = canvas.transform.Find("LoadingScreen")?.GetComponent<LoadingScreen>();
            bool created = screen == null;
            if (created)
                screen = BuildLoadingScreen(canvas.transform, boot);
            // 장소 이동 페이드는 늘 맨 위, 로딩 화면은 그 바로 아래
            screen.transform.SetAsLastSibling();
            var fader = canvas.transform.Find("ScreenFader");
            if (fader != null)
                fader.SetAsLastSibling();
            if (title != null)
            {
                var so = new SerializedObject(title);
                so.FindProperty("loadingScreen").objectReferenceValue = screen;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(canvas.scene);
            Debug.Log($"[Setup] 로딩 화면 {(created ? "생성" : "유지")} · 타이틀 연결 {(title != null ? "함" : "못 함(TitleScreen 없음)")}");
        }

        private static LoadingScreen BuildLoadingScreen(Transform canvas, SceneBootstrapper boot)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var root = Stretch(new GameObject("LoadingScreen", typeof(RectTransform)).GetComponent<RectTransform>(), canvas);
            var panel = Stretch(NewImage("Panel", root, new Color(0.02f, 0.05f, 0.09f, 1f), null), root);
            var group = panel.gameObject.AddComponent<CanvasGroup>();

            var glow = NewImage("Glow", panel, new Color(0.45f, 0.85f, 1f, 0.35f), Sprite(GlowPath));
            Place(glow, new Vector2(0f, 90f), new Vector2(980f, 980f));
            var logo = NewImage("Logo", panel, Color.white, Sprite(LogoPath));
            logo.GetComponent<Image>().preserveAspect = true;
            Place(logo, new Vector2(0f, 90f), new Vector2(760f, 380f));

            var bar = NewImage("Bar", panel, Color.white, Sprite(BarBackPath));
            bar.GetComponent<Image>().type = Image.Type.Sliced;
            Place(bar, new Vector2(0f, -170f), new Vector2(560f, 22f));
            var fill = Stretch(NewImage("Fill", bar, Color.white, Sprite(BarFillPath)), bar);
            fill.offsetMin = new Vector2(3f, 3f);
            fill.offsetMax = new Vector2(-3f, -3f);
            var fillImage = fill.GetComponent<Image>();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 0f;

            var message = MakeText("Message", panel, "불러오는 중.", 28, new Color(0.85f, 0.95f, 1f), TextAlignmentOptions.Left, font);
            Place(message.rectTransform, new Vector2(-80f, -212f), new Vector2(400f, 40f));
            var percent = MakeText("Percent", panel, "0%", 24, new Color(0.55f, 0.95f, 1f), TextAlignmentOptions.Right, font);
            Place(percent.rectTransform, new Vector2(200f, -212f), new Vector2(160f, 40f));

            var screen = root.gameObject.AddComponent<LoadingScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("bootstrapper").objectReferenceValue = boot;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("progressFill").objectReferenceValue = fillImage;
            so.FindProperty("messageLabel").objectReferenceValue = message;
            so.FindProperty("percentLabel").objectReferenceValue = percent;
            so.ApplyModifiedPropertiesWithoutUndo();
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Create LoadingScreen");
            return screen;
        }

        // ── 부품 ──

        private static Sprite Sprite(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null)
                Debug.LogWarning($"[Setup] 그림 없음: {path} — 단색으로 만듦");
            return s;
        }

        private static RectTransform NewImage(string name, Transform parent, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            img.raycastTarget = name == "Panel"; // 덮는 동안 뒤 화면 클릭을 막는 건 바탕 한 장만
            return go.GetComponent<RectTransform>();
        }

        private static TextMeshProUGUI MakeText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null)
                t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        private static RectTransform Stretch(RectTransform rt, Transform parent)
        {
            if (rt.parent != parent)
                rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static void Place(RectTransform rt, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }
    }
}
#endif
