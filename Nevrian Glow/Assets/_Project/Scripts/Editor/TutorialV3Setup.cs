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
    /// 튜토리얼 v3 (기획 10/29, 디스코드 10/30): 단계는 기획 CSV를 다시 가져오고, 아침 편지 6통(v3 6절 문구)을 덮어쓰고,
    /// 3 ~ 6일차 오늘의 목표 · 상점 추천(v3 5절 표 — daily_goals.csv 대기)을 데이터로, Core 씬에 DailyGoalSystem과 목표 칸(HUD 아래)을 만든다.
    /// 편지 · 목표는 이 메뉴가 늘 v3 문구로 다시 쓴다(기획 CSV가 오면 그쪽으로). 여러 번 실행해도 결과가 같다.
    /// </summary>
    public static class TutorialV3Setup
    {
        private const string CorePath = "Assets/_Project/Scenes/Core.unity";
        private const string FontPath = "Assets/_Project/Art/Fonts/font_maplestory_bold_sdf.asset";
        private const string PanelSprite = "Assets/Art/UI/Panels/ui_panel_base.png";
        private const string GoalsPath = "Assets/_Project/Data/Tutorial/DailyGoals.asset";
        private const string StartSettingsPath = "Assets/_Project/Data/Stats/GameStartSettings.asset";

        // 편지 강조(v3 굵은 글씨 = 힌트 대상) — 종이 바탕에 읽히는 짙은 주황
        private static string H(string s) => $"<color=#8A3B12>{s}</color>";

        // v3 6절 (게임 화면 글자라 '—'는 '-'로). 편지 ①의 "창고 상자에 여섯 개"는 기획 CSV D1-09(옆 밭에서 6개 수확) 흐름에 맞춰 "옆 밭에 키워 둔"으로 — 기획 확인 대기
        private static readonly (string id, string title, string body)[] Letters =
        {
            ("letter_1", "얘야, 농장에 온 첫날 열어 보렴",
                "사랑하는 우리 얘야,\n\n" +
                "이 편지를 읽고 있다면, 할머니 대신 네가 글로우 팜에 와 주었구나. 정말 고맙다.\n" +
                "오랫동안 아무도 돌보지 못해서 밭도 가게도, 저 등대도 많이 쓸쓸해졌을 거야.\n\n" +
                H("괭이로 굳은 흙을 부드럽게 일구고") + ", 월광 뿌리를 심어 보렴. 물만 잘 주면 하룻밤 사이에 예쁘게 빛난단다.\n" +
                H("옆 밭에 월광 뿌리 여섯 개") + "를 키워 두었어. 오래 두어도 상하지 않거든. 가게에 내놓으면 반가운 손님이 올 거야.\n\n" +
                "천천히, 네 속도대로 하면 돼.\n" +
                "언제나 네 편인, 네아 할머니가"),
            ("letter_2", "옆집 피피의 편지",
                "안녕하세요! 옆집 피피예요.\n\n" +
                H("어제 할머니의 가게가 다시 열려서 미루랑 와 봤어요~") + " 월광 뿌리 정말 반짝반짝하고 맛있었어요!\n" +
                "엄마가 그러는데, 옛날에 할머니 가게에는 " + H("버섯꽃이랑 등불 열매") + "도 있었대요. 오늘은 상점에 들어왔을까요?\n" +
                "버섯꽃은 물을 안 줘도 쑥쑥 자란대요. 신기하죠?\n\n" +
                "또 놀러 갈게요! - 피피 (미루도 안녕이래요)"),
            ("letter_3", "오르 아주머니의 편지",
                "새 정원지기님께,\n\n" +
                "어젯밤 창밖을 보다가 깜짝 놀랐어요. " + H("등대 아래층에 불이 들어왔더군요.") + " 몇 해 만인지 몰라요.\n" +
                "오늘 아침엔 밭 둘레 풀이 초록빛이더라고요. 네아 할머니가 계실 때처럼요.\n" +
                "할머니는 늘 " + H("튼튼한 철 괭이") + "로 밭을 일구셨어요. " + H("밭을 빈 곳 없이 채우면") + " 등대도 더 밝아질 거예요.\n\n" +
                "이웃 오르 드림"),
            ("letter_4", "시장 상인 바루의 편지",
                "글로우 팜 주인장에게,\n\n" +
                "소문 들었소. 버섯꽃이 다 자랐다면서? 오늘은 손님이 꽤 몰릴 거요.\n" +
                "장사꾼으로서 한마디 하자면, 이런 날엔 " + H("밭을 넓혀 두는 게") + " 남는 장사요. " + H("간판을 다시 달아 홍보") + "하거나 " +
                H("장바구니를 큰 걸로") + " 바꾸면 손님이 더 많이 사 가지.\n" +
                "그리고 할머니가 아끼던 " + H("비료") + ", 등불 열매 나무에 한 줌 주면 금방 열매가 맺힌다더군.\n\n" +
                "시장 거리의 바루"),
            ("letter_5", "피피의 편지",
                "정원지기 언니(오빠)! 피피예요!\n\n" +
                H("등대 2층에도 불이 켜졌어요!") + " 나무에 꽃이 피고, 저녁엔 반딧불이가 날아다녀요. 새도 돌아왔어요!\n" +
                "미루가 그러는데, 할머니는 " + H("반짝이는 대걸레랑 큰 물뿌리개") + "를 쓰셨대요. " + H("가방이 크면") + " 수확한 걸 한 번에 많이 옮길 수 있겠죠?\n\n" +
                "오늘도 가게에 갈게요~ - 피피"),
            ("letter_6", "등대 아래층이 다 밝아진 날 열어 보렴",
                "우리 얘야,\n\n" +
                "이 편지를 열었다면, 등대에 다시 불이 들어오기 시작했구나. 등불 열매 나무에도 " + H("첫 열매") + "가 맺혔을 거야.\n" +
                "오늘 장사를 마치면, " + H("등대를 꼭 올려다보렴.") + " 할머니가 오래 기다린 순간이란다.\n\n" +
                "이제 글로우 팜은 네 농장이야. 네 마음 가는 대로 가꿔 주렴.\n" +
                "네아 할머니가"),
        };

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
            var steps = PlanCsvImporter.LoadOrCreateTutorialAsset();
            notes.Add($"편지 {WriteLetters(steps)}통");

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

        // 같은 id는 문구를 덮어쓰고, 없으면 끝에 더한다
        private static int WriteLetters(TutorialStepsData data)
        {
            var so = new SerializedObject(data);
            var list = so.FindProperty("letters");
            foreach (var (id, title, body) in Letters)
            {
                SerializedProperty e = null;
                for (int i = 0; i < list.arraySize && e == null; i++)
                    if (list.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == id)
                        e = list.GetArrayElementAtIndex(i);
                if (e == null)
                {
                    list.InsertArrayElementAtIndex(list.arraySize);
                    e = list.GetArrayElementAtIndex(list.arraySize - 1);
                    e.FindPropertyRelative("id").stringValue = id;
                }
                e.FindPropertyRelative("title").stringValue = title;
                e.FindPropertyRelative("body").stringValue = body;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            return Letters.Length;
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
