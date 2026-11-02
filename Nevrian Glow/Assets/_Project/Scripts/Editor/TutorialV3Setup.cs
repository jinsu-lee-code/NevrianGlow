#if UNITY_EDITOR
using System.Collections.Generic;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Tutorial;
using NevrianGlowFarm.Systems.Tutorial;
using NevrianGlowFarm.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NevrianGlowFarm.Editor
{
    /// <summary>
    /// 튜토리얼 v3 (기획 10/29, 디스코드 10/30): 단계 · 편지는 기획 CSV(tutorial_steps · letters)를 다시 가져오고,
    /// 3 ~ 6일차 오늘의 목표 · 상점 추천(v3 5절 표 — daily_goals.csv 대기)을 데이터로, Core 씬에 DailyGoalSystem과 목표 칸(HUD 아래)을 만든다.
    /// 목표는 이 메뉴가 늘 v3 문구로 다시 쓴다(기획 CSV가 오면 그쪽으로). 여러 번 실행해도 결과가 같다.
    /// </summary>
    public static class TutorialV3Setup
    {
        private const string CorePath = "Assets/_Project/Scenes/Core.unity";
        private const string FontPath = "Assets/_Project/Art/Fonts/font_maplestory_bold_sdf.asset";
        private const string PanelSprite = "Assets/Art/UI/Panels/ui_panel_base.png";
        private const string GoalsPath = "Assets/_Project/Data/Tutorial/DailyGoals.asset";
        private const string StartSettingsPath = "Assets/_Project/Data/Stats/GameStartSettings.asset";

        private sealed class GoalRow
        {
            public string Id, Text, Condition;
            public bool Core;
            public GoalRow(string id, string text, string condition, bool core) { Id = id; Text = text; Condition = condition; Core = core; }
        }

        private sealed class DayRow
        {
            public int Day;
            public string Letter;
            public GoalRow[] Goals;
            public string[] Recommended;
        }

        // v3 5절 표 (굵은 글씨 = 핵심 목표). 2일차 편지는 단계형 튜토리얼(D2-01)이 연다
        private static readonly DayRow[] Days =
        {
            new DayRow { Day = 3, Letter = "letter_3",
                Goals = new[]
                {
                    new GoalRow("d3_iron_hoe", "철 괭이로 바꾸기", "bought:shop_hoe_upgrade:1", true),
                    new GoalRow("d3_full_field", "밭 32칸을 모두 심기", "planted_total:32", true),
                    new GoalRow("d3_mushroom", "버섯꽃 심기", "planted:crop_mushroomflower", false),
                },
                Recommended = new[] { "shop_hoe_upgrade", "shop_seed_mushroomflower" } },
            new DayRow { Day = 4, Letter = "letter_4",
                Goals = new[]
                {
                    new GoalRow("d4_plot_expand", "밭 넓히기 (밭 확장권)", "bought:shop_plot_expand:1", true),
                    new GoalRow("d4_promo_basket", "매장 홍보 또는 더 큰 장바구니", "bought_any:shop_store_promo,shop_big_basket:1", false),
                    new GoalRow("d4_fertilize", "등불 열매에 비료 주기", "fertilized:crop_lanternfruit", true),
                    new GoalRow("d4_site_planted", "새 밭에 심기", "site_planted", false),
                },
                Recommended = new[] { "shop_plot_expand", "shop_store_promo", "shop_big_basket", "shop_fertilizer" } },
            new DayRow { Day = 5, Letter = "letter_5",
                Goals = new[]
                {
                    new GoalRow("d5_mop_watering", "대걸레 · 물뿌리개 바꾸기", "bought:shop_mop_upgrade,shop_watering_upgrade:1", true),
                    new GoalRow("d5_bag_hotbar", "가방 · 핫바 넓히기", "bought:shop_bag_expand,shop_hotbar_expand:1", true),
                    new GoalRow("d5_full_field", "밭 64칸을 모두 심기", "planted_total:64", true),
                },
                Recommended = new[] { "shop_mop_upgrade", "shop_watering_upgrade", "shop_bag_expand", "shop_hotbar_expand" } },
            new DayRow { Day = 6, Letter = "letter_6",
                Goals = new[]
                {
                    new GoalRow("d6_lantern_harvest", "등불 열매 첫 수확", "harvested:crop_lanternfruit", true),
                    new GoalRow("d6_tier2", "2단계 업그레이드 하나 이상 또는 밭 확장 2",
                        "bought_any:shop_hoe_upgrade,shop_mop_upgrade,shop_watering_upgrade:2 || bought:shop_plot_expand:2", false),
                    new GoalRow("d6_lighthouse", "장사를 마치고 등대를 보기", "sales_done", false),
                },
                Recommended = new[] { "shop_hoe_upgrade", "shop_mop_upgrade", "shop_watering_upgrade", "shop_plot_expand" } },
        };

        [MenuItem("Glow Farm/Setup/Apply Tutorial v3 (10-30)")]
        private static void Apply()
        {
            var notes = new List<string>();

            // 1) 단계 (기획 CSV) · 편지
            int imported = PlanCsvImporter.ImportTutorialStepsFromRepo();
            notes.Add(imported < 0 ? "단계 CSV 없음" : $"단계 {imported}줄");
            int letters = PlanCsvImporter.ImportLettersFromRepo();
            notes.Add(letters < 0 ? "편지 CSV 없음" : $"편지 {letters}통");

            // 2) 오늘의 목표 데이터
            var goals = AssetDatabase.LoadAssetAtPath<DailyGoalsData>(GoalsPath);
            if (goals == null)
            {
                goals = ScriptableObject.CreateInstance<DailyGoalsData>();
                AssetDatabase.CreateAsset(goals, GoalsPath);
            }
            notes.Add(WriteGoals(goals));

            // 3) Core 씬: 목표 칸 + DailyGoalSystem
            var scene = EditorSceneManager.GetSceneByPath(CorePath);
            bool opened = false;
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(CorePath, OpenSceneMode.Additive);
                opened = true;
            }
            // 씬을 연 뒤에 에셋을 다시 잡는다 (앞서 잡은 참조가 null로 저장되는 일 방지)
            goals = AssetDatabase.LoadAssetAtPath<DailyGoalsData>(GoalsPath);
            var start = AssetDatabase.LoadAssetAtPath<GameStartSettingsData>(StartSettingsPath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Transform canvas = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var c in root.GetComponentsInChildren<Canvas>(true))
                    if (c.name == "UICanvas")
                        canvas = c.transform;
            if (canvas == null)
            {
                Debug.LogError("[Setup] 튜토리얼 v3: Core 씬에 UICanvas가 없음");
                return;
            }
            var letter = canvas.GetComponentInChildren<OpeningLetterScreen>(true);
            var (panel, title, body, hudNote) = BuildHud(canvas, font);
            notes.Add(hudNote);

            DailyGoalSystem system = null;
            foreach (var root in scene.GetRootGameObjects())
                if (system == null)
                    system = root.GetComponentInChildren<DailyGoalSystem>(true);
            if (system == null)
            {
                var go = new GameObject("DailyGoals");
                EditorSceneManager.MoveGameObjectToScene(go, scene);
                system = go.AddComponent<DailyGoalSystem>();
                Undo.RegisterCreatedObjectUndo(go, "Create DailyGoalSystem");
                notes.Add("DailyGoalSystem 생성");
            }
            var so = new SerializedObject(system);
            so.FindProperty("data").objectReferenceValue = goals;
            so.FindProperty("startSettings").objectReferenceValue = start;
            so.FindProperty("letterScreen").objectReferenceValue = letter;
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("bodyLabel").objectReferenceValue = body;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (letter == null || start == null || goals == null)
                notes.Add("참조 빠짐 - 편지 화면 · 시작 설정 · 목표 데이터 확인");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] 튜토리얼 v3: " + string.Join(" · ", notes));
        }

        private static string WriteGoals(DailyGoalsData data)
        {
            var so = new SerializedObject(data);
            var days = so.FindProperty("days");
            days.arraySize = Days.Length;
            int goalCount = 0;
            var bad = new List<string>();
            for (int d = 0; d < Days.Length; d++)
            {
                var row = Days[d];
                var e = days.GetArrayElementAtIndex(d);
                e.FindPropertyRelative("day").intValue = row.Day;
                e.FindPropertyRelative("letterId").stringValue = row.Letter;
                var goals = e.FindPropertyRelative("goals");
                goals.arraySize = row.Goals.Length;
                for (int g = 0; g < row.Goals.Length; g++)
                {
                    var src = row.Goals[g];
                    var ge = goals.GetArrayElementAtIndex(g);
                    ge.FindPropertyRelative("id").stringValue = src.Id;
                    ge.FindPropertyRelative("text").stringValue = src.Text;
                    ge.FindPropertyRelative("condition").stringValue = src.Condition;
                    ge.FindPropertyRelative("core").boolValue = src.Core;
                    if (!GoalConditions.IsValid(src.Condition))
                        bad.Add(src.Id);
                    goalCount++;
                }
                var rec = e.FindPropertyRelative("recommended");
                rec.arraySize = row.Recommended.Length;
                for (int r = 0; r < row.Recommended.Length; r++)
                    rec.GetArrayElementAtIndex(r).stringValue = row.Recommended[r];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            if (bad.Count > 0)
                Debug.LogError("[Setup] 튜토리얼 v3: 목표 조건 문법 오류 " + string.Join(", ", bad));
            return $"목표 {Days.Length}일 · {goalCount}개";
        }

        // 왼쪽 위 HUD(400 × 142) 바로 아래: 아트 패널(작은 패널 테두리 반) + 제목 + 목표 줄 — 줄 수에 맞춰 높이가 늘어난다
        private static (GameObject panel, TMP_Text title, TMP_Text body, string note) BuildHud(Transform canvas, TMP_FontAsset font)
        {
            var existing = canvas.Find("DailyGoalHud");
            string note = "목표 칸 유지";
            RectTransform root;
            if (existing == null)
            {
                root = new GameObject("DailyGoalHud", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
                root.SetParent(canvas, false);
                MakeLabel(root, "Title", font);
                MakeLabel(root, "Body", font);
                Undo.RegisterCreatedObjectUndo(root.gameObject, "Create DailyGoalHud");
                note = "목표 칸 생성";
            }
            else
                root = existing as RectTransform;

            // 자리 · 모양은 늘 다시 맞춘다
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            var hud = canvas.Find("HUD") as RectTransform;
            float top = hud != null ? hud.anchoredPosition.y - hud.sizeDelta.y - 12f : -178f;
            root.anchoredPosition = new Vector2(24f, top);
            root.sizeDelta = new Vector2(400f, root.sizeDelta.y);
            var image = root.GetComponent<Image>();
            image.raycastTarget = false;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSprite);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 2f; // 작은 패널 — 모서리 장식 반 (TidyHud와 같게)
                image.color = Color.white;
            }
            else
                image.color = new Color(0.02f, 0.09f, 0.12f, 0.92f);
            var layout = root.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(26, 26, 16, 18);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fit = root.GetComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var title = root.Find("Title").GetComponent<TMP_Text>();
            title.fontSize = 24f;
            title.color = new Color(0.85f, 0.95f, 1f, 1f);
            title.text = "오늘의 목표";
            var body = root.Find("Body").GetComponent<TMP_Text>();
            body.fontSize = 21f;
            body.lineSpacing = 8f;
            body.color = new Color(0.96f, 0.98f, 1f, 1f);
            body.text = string.Empty;
            foreach (var t in new[] { title, body })
                EditorUtility.SetDirty(t);
            EditorUtility.SetDirty(root);

            // HUD 바로 뒤 (창 · 툴팁 · 페이드보다 아래)
            if (hud != null)
                root.SetSiblingIndex(hud.GetSiblingIndex() + 1);
            return (root.gameObject, title, body, note);
        }

        private static void MakeLabel(Transform parent, string name, TMP_FontAsset font)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(parent, false);
            if (font != null)
                label.font = font;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            label.richText = true;
        }
    }
}
#endif
