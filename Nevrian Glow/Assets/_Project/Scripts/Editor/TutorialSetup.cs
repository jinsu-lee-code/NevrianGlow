#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Logic.Tutorial;
using NevrianGlowFarm.Systems.Tutorial;
using NevrianGlowFarm.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NevrianGlowFarm.Editor
{
    /// <summary>
    /// 튜토리얼 설치 (TASKS 5-7): 데이터 에셋(단계는 기획 CSV, 편지는 기획 튜토리얼_플로우 3절 문구), Core 씬의 TutorialSystem,
    /// UICanvas 위쪽 "오늘 할 일" 한 줄(TutorialHud, 아트 패널), 단계 버튼에 "장사 시작 [Enter]" · 튜토리얼 잠금 이름, 편지 E 닫기, 타이틀 중 숨김.
    /// 여러 번 실행해도 결과가 같다.
    /// </summary>
    public static class TutorialSetup
    {
        private const string CorePath = "Assets/_Project/Scenes/Core.unity";
        private const string InputPath = "Assets/InputSystem_Actions.inputactions";
        private const string FontPath = "Assets/_Project/Art/Fonts/font_maplestory_bold_sdf.asset";
        private const string PanelSprite = "Assets/Art/UI/Panels/ui_panel_base.png";

        // 기획 튜토리얼_플로우_1-2일차 3절 (2026-10-22) — 게임 화면 글자라 "—" · "…"는 쓰지 않는다. 문구는 에셋에서 바꾼다 (한 번 채운 뒤에는 건드리지 않음)
        private static readonly TutorialStepsData.Letter[] DefaultLetters =
        {
            new TutorialStepsData.Letter("letter_1", "얘야, 농장에 온 첫날 열어 보렴",
                "사랑하는 우리 얘야,\n\n" +
                "이 편지를 읽고 있다면, 할머니 대신 네가 글로우 팜에 와 주었구나. 정말 고맙다.\n" +
                "아마 오랫동안 아무도 돌보지 못해서 밭도 가게도 많이 쓸쓸해졌을 거야.\n\n" +
                "그래도 걱정 말렴. 이곳 흙은 손길만 닿으면 금방 다시 빛난단다.\n" +
                "괭이로 굳은 흙을 부드럽게 일구고, 월광 뿌리를 심어 보렴. 물만 잘 주면 하룻밤 사이에 예쁘게 빛날 거야.\n" +
                "창고 상자에 월광 뿌리 두 개를 남겨 두었어. 월광 뿌리는 오래 두어도 상하지 않거든. 가게에 내놓으면 반가운 손님이 올 거야.\n\n" +
                "가게는... 먼지가 꽤 쌓였겠지? 천천히, 네 속도대로 하면 돼.\n\n" +
                "언제나 네 편인, 네아 할머니가\n" +
                "추신. 옆집 꼬마 피피가 우리 가게를 참 좋아했단다."),
            new TutorialStepsData.Letter("letter_2", "첫 수확 날 열어 보렴",
                "얘야, 밤사이 밭에서 빛이 올라왔니?\n\n" +
                "그럼 월광 뿌리가 다 자란 거야. 손으로 살짝 뽑아 거두고, 비어 있는 밭은 다시 일궈서 새로 심으렴.\n" +
                "가게가 다시 열렸다는 소문이 나면, 손님이 하나둘 돌아올 거야. 할머니 때처럼.\n\n" +
                "네아 할머니가"),
            new TutorialStepsData.Letter("letter_3", "새 씨앗을 고른 날 열어 보렴",
                "우리 얘야, 벌써 새 씨앗을 고를 줄 알게 됐구나!\n\n" +
                "버섯꽃은 물을 주지 않아도 혼자 잘 자라는 착한 꽃이야. 내일 아침부터 상점에 들어올 거란다.\n" +
                "이제 글로우 팜은 네 농장이야. 네 마음 가는 대로 가꿔 주렴. 다시 빛나는 우리 농장을, 할머니는 늘 응원하고 있을게.\n\n" +
                "네아 할머니가"),
        };

        [MenuItem("Glow Farm/Setup/Create Tutorial HUD (5-7)")]
        private static void CreateMenu()
        {
            var notes = new List<string>();

            // 1) 데이터: 단계는 기획 CSV, 편지는 빈 것만 기본 문구
            var data = PlanCsvImporter.LoadOrCreateTutorialAsset();
            int imported = PlanCsvImporter.ImportTutorialStepsFromRepo();
            notes.Add(imported < 0 ? "단계 CSV 없음 (CSV/plan/data_csv/tutorial_steps.csv)" : $"단계 {imported}줄");
            notes.Add($"편지 {FillLetters(data)}개 채움");

            // 2) Core 씬
            var scene = EditorSceneManager.GetSceneByPath(CorePath);
            bool opened = false;
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(CorePath, OpenSceneMode.Additive);
                opened = true;
            }
            Transform canvas = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var c in root.GetComponentsInChildren<Canvas>(true))
                    if (c.name == "UICanvas")
                        canvas = c.transform;
            if (canvas == null)
            {
                Debug.LogError("[Setup] 튜토리얼: Core 씬에 UICanvas가 없음");
                return;
            }
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var refs = AssetDatabase.LoadAllAssetsAtPath(InputPath).OfType<InputActionReference>().ToList();
            InputActionReference Ref(string map, string action) => refs.FirstOrDefault(r => r.action != null && r.action.actionMap.name == map && r.action.name == action);

            var letter = canvas.GetComponentInChildren<OpeningLetterScreen>(true);
            notes.Add(BuildSystem(scene, data, letter));
            notes.Add(BuildHud(canvas, font));
            notes.Add(BuildLevelUpBanner(canvas, font));
            notes.Add(FixPhaseButtons(canvas, Ref("Player", "EndPhase")));
            notes.Add(AddLetterCloseKey(letter, Ref("Player", "Interact")));

            var fader = canvas.Find("ScreenFader");
            if (fader != null)
                fader.SetAsLastSibling();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] 튜토리얼 5-7: " + string.Join(" · ", notes));
        }

        // 편지 기본 문구 — 같은 id가 없을 때만 넣는다 (기획이 바꾼 문구를 덮지 않게)
        private static int FillLetters(TutorialStepsData data)
        {
            var so = new SerializedObject(data);
            var list = so.FindProperty("letters");
            int added = 0;
            foreach (var letter in DefaultLetters)
            {
                if (data.TryGetLetter(letter.Id, out _))
                    continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                var e = list.GetArrayElementAtIndex(list.arraySize - 1);
                e.FindPropertyRelative("id").stringValue = letter.Id;
                e.FindPropertyRelative("title").stringValue = letter.Title;
                e.FindPropertyRelative("body").stringValue = letter.Body;
                so.ApplyModifiedPropertiesWithoutUndo();
                added++;
            }
            if (added > 0)
                EditorUtility.SetDirty(data);
            return added;
        }

        // Core 씬 루트 "Tutorial" + TutorialSystem (데이터 · 편지 화면 참조)
        private static string BuildSystem(UnityEngine.SceneManagement.Scene scene, TutorialStepsData data, OpeningLetterScreen letter)
        {
            TutorialSystem system = null;
            foreach (var root in scene.GetRootGameObjects())
                system ??= root.GetComponentInChildren<TutorialSystem>(true);
            string note = "TutorialSystem 유지";
            if (system == null)
            {
                var go = new GameObject("Tutorial");
                EditorSceneManager.MoveGameObjectToScene(go, scene);
                system = go.AddComponent<TutorialSystem>();
                Undo.RegisterCreatedObjectUndo(go, "Create TutorialSystem");
                note = "TutorialSystem 생성";
            }
            var so = new SerializedObject(system);
            so.FindProperty("data").objectReferenceValue = data;
            if (letter != null)
                so.FindProperty("letterScreen").objectReferenceValue = letter;
            so.ApplyModifiedPropertiesWithoutUndo();
            return note + (letter == null ? " (편지 화면 없음 - Create P-7 UI 먼저)" : string.Empty);
        }

        // 화면 위쪽 가운데 한 줄: 아트 패널(작은 패널 테두리 반) + 글자. 툴팁 · 페이드보다 아래, 다른 화면보다 위
        private static string BuildHud(Transform canvas, TMP_FontAsset font)
        {
            var existing = canvas.Find("TutorialHud");
            string note = "TutorialHud 유지";
            RectTransform root;
            if (existing == null)
            {
                root = new GameObject("TutorialHud", typeof(RectTransform), typeof(TutorialHudView)).GetComponent<RectTransform>();
                root.SetParent(canvas, false);

                var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup)).GetComponent<RectTransform>();
                panel.SetParent(root, false);
                panel.anchorMin = Vector2.zero;
                panel.anchorMax = Vector2.one;
                panel.offsetMin = panel.offsetMax = Vector2.zero;
                var image = panel.GetComponent<Image>();
                image.raycastTarget = false;
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSprite);
                if (sprite != null)
                {
                    image.sprite = sprite;
                    image.type = Image.Type.Sliced;
                    image.pixelsPerUnitMultiplier = 2f; // 작은 패널 — 모서리 장식 반 (5-4a와 같게)
                    image.color = Color.white;
                }
                else
                    image.color = new Color(0.02f, 0.09f, 0.12f, 0.92f);
                var group = panel.GetComponent<CanvasGroup>();
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;

                var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                label.rectTransform.SetParent(panel, false);
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(28f, 6f);
                label.rectTransform.offsetMax = new Vector2(-28f, -6f);
                if (font != null)
                    label.font = font;
                label.fontSize = 26f;
                label.fontSizeMin = 16f;
                label.fontSizeMax = 26f;
                label.enableAutoSizing = true;
                label.color = new Color(0.96f, 0.98f, 1f, 1f);
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.Normal;
                label.raycastTarget = false;
                label.richText = true;
                label.text = string.Empty;

                var view = root.GetComponent<TutorialHudView>();
                var so = new SerializedObject(view);
                so.FindProperty("group").objectReferenceValue = group;
                so.FindProperty("label").objectReferenceValue = label;
                so.ApplyModifiedPropertiesWithoutUndo();
                Undo.RegisterCreatedObjectUndo(root.gameObject, "Create TutorialHud");
                note = "TutorialHud 생성";
            }
            else
                root = existing as RectTransform;

            // 자리 · 크기는 늘 다시 맞춘다: 위 가운데, HUD(왼쪽 460) · 오늘의 정리(오른쪽 460) 사이에 들어가는 폭 (기준 해상도 1920)
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -18f);
            root.sizeDelta = new Vector2(900f, 64f);
            EditorUtility.SetDirty(root);

            // 순서: 툴팁 바로 아래 (없으면 페이드 바로 아래) — 상점 · 정산 창 위에 보이게
            var tooltip = canvas.Find("Tooltip");
            var fader = canvas.Find("ScreenFader");
            var anchor = tooltip != null ? tooltip : fader;
            if (anchor != null)
                root.SetSiblingIndex(anchor.GetSiblingIndex() - (anchor.GetSiblingIndex() > root.GetSiblingIndex() ? 1 : 0));

            // 타이틀 중 숨김
            var title = canvas.Find("TitleScreen")?.GetComponent<TitleScreen>();
            if (title != null)
            {
                var tso = new SerializedObject(title);
                var arr = tso.FindProperty("hideWhileOpen");
                bool has = false;
                for (int i = 0; i < arr.arraySize; i++)
                    has |= arr.GetArrayElementAtIndex(i).objectReferenceValue == root.gameObject;
                if (!has)
                {
                    arr.arraySize++;
                    arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = root.gameObject;
                    tso.ApplyModifiedPropertiesWithoutUndo();
                    note += " · 타이틀 중 숨김";
                }
            }
            return note;
        }

        // 레벨업 배너: 화면 위쪽(튜토리얼 줄 아래) 아트 패널 + 큰 글자, 평소 숨김 — LeveledUp에 3초
        private static string BuildLevelUpBanner(Transform canvas, TMP_FontAsset font)
        {
            var existing = canvas.Find("LevelUpBanner");
            string note = "레벨업 배너 유지";
            RectTransform root;
            if (existing == null)
            {
                root = new GameObject("LevelUpBanner", typeof(RectTransform), typeof(LevelUpBanner)).GetComponent<RectTransform>();
                root.SetParent(canvas, false);
                var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup)).GetComponent<RectTransform>();
                panel.SetParent(root, false);
                panel.anchorMin = Vector2.zero;
                panel.anchorMax = Vector2.one;
                panel.offsetMin = panel.offsetMax = Vector2.zero;
                var image = panel.GetComponent<Image>();
                image.raycastTarget = false;
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSprite);
                if (sprite != null)
                {
                    image.sprite = sprite;
                    image.type = Image.Type.Sliced;
                    image.pixelsPerUnitMultiplier = 1.4f;
                    image.color = Color.white;
                }
                else
                    image.color = new Color(0.02f, 0.09f, 0.12f, 0.92f);
                var group = panel.GetComponent<CanvasGroup>();
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;

                var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                label.rectTransform.SetParent(panel, false);
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(30f, 10f);
                label.rectTransform.offsetMax = new Vector2(-30f, -10f);
                if (font != null)
                    label.font = font;
                label.fontSize = 40f;
                label.color = new Color(0.55f, 0.95f, 1f, 1f);
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;
                label.text = string.Empty;

                var so = new SerializedObject(root.GetComponent<LevelUpBanner>());
                so.FindProperty("group").objectReferenceValue = group;
                so.FindProperty("label").objectReferenceValue = label;
                so.ApplyModifiedPropertiesWithoutUndo();
                Undo.RegisterCreatedObjectUndo(root.gameObject, "Create LevelUpBanner");
                note = "레벨업 배너 생성";
            }
            else
                root = existing as RectTransform;

            root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -110f);
            root.sizeDelta = new Vector2(640f, 110f);
            EditorUtility.SetDirty(root);

            var tooltip = canvas.Find("Tooltip");
            var fader = canvas.Find("ScreenFader");
            var anchor = tooltip != null ? tooltip : fader;
            if (anchor != null)
                root.SetSiblingIndex(anchor.GetSiblingIndex() - (anchor.GetSiblingIndex() > root.GetSiblingIndex() ? 1 : 0));
            if (AddTitleHide(canvas, root.gameObject))
                note += " · 타이틀 중 숨김";
            return note;
        }

        // 타이틀이 떠 있는 동안 숨길 목록에 더한다 (이미 있으면 false)
        private static bool AddTitleHide(Transform canvas, GameObject target)
        {
            var title = canvas.Find("TitleScreen")?.GetComponent<TitleScreen>();
            if (title == null)
                return false;
            var tso = new SerializedObject(title);
            var arr = tso.FindProperty("hideWhileOpen");
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == target)
                    return false;
            arr.arraySize++;
            arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = target;
            tso.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // 단계 버튼: 농사(end_farming) · 청소·진열 "장사 시작 [Enter]"(open_sales, 새로) · 장사
        private static string FixPhaseButtons(Transform canvas, InputActionReference endPhase)
        {
            var hud = canvas.GetComponentInChildren<PhaseActionHud>(true);
            if (hud == null)
                return "단계 버튼 없음 (Create P-7 UI 먼저)";
            var so = new SerializedObject(hud);
            var entries = so.FindProperty("entries");
            int farming = -1, tidy = -1, changed = 0;
            for (int i = 0; i < entries.arraySize; i++)
            {
                var phase = (DayPhase)entries.GetArrayElementAtIndex(i).FindPropertyRelative("phase").enumValueIndex;
                if (phase == DayPhase.Farming) farming = i;
                if (phase == DayPhase.CleanDisplay) tidy = i;
            }
            if (farming >= 0 && SetLock(entries.GetArrayElementAtIndex(farming), TutorialLocks.EndFarming))
                changed++;
            if (tidy < 0)
            {
                entries.InsertArrayElementAtIndex(entries.arraySize);
                var e = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                e.FindPropertyRelative("phase").enumValueIndex = (int)DayPhase.CleanDisplay;
                e.FindPropertyRelative("label").stringValue = "장사 시작";
                e.FindPropertyRelative("keyHint").stringValue = "Enter";
                e.FindPropertyRelative("action").enumValueIndex = (int)PhaseActionHud.ActionKind.OpenSales;
                e.FindPropertyRelative("key").objectReferenceValue = endPhase;
                e.FindPropertyRelative("tutorialLock").stringValue = TutorialLocks.OpenSales;
                changed++;
            }
            else if (SetLock(entries.GetArrayElementAtIndex(tidy), TutorialLocks.OpenSales))
                changed++;
            so.ApplyModifiedPropertiesWithoutUndo();
            return changed > 0 ? $"단계 버튼 {changed}곳 고침" : "단계 버튼 유지";
        }

        private static bool SetLock(SerializedProperty entry, string lockName)
        {
            var p = entry.FindPropertyRelative("tutorialLock");
            if (p.stringValue == lockName)
                return false;
            p.stringValue = lockName;
            return true;
        }

        // 편지를 E로도 닫기 (기획 2절 편지 UI) + 긴 편지(①)가 종이를 넘지 않게 본문 글자 자동 크기
        private static string AddLetterCloseKey(OpeningLetterScreen letter, InputActionReference interact)
        {
            if (letter == null || interact == null)
                return "편지 E 닫기 건너뜀";
            var body = letter.transform.Find("Panel/Paper/Body")?.GetComponent<TMP_Text>();
            if (body != null && !body.enableAutoSizing)
            {
                body.enableAutoSizing = true;
                body.fontSizeMax = body.fontSize;
                body.fontSizeMin = 15f;
                EditorUtility.SetDirty(body);
            }
            var so = new SerializedObject(letter);
            var arr = so.FindProperty("closeActions");
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == interact)
                    return "편지 E 닫기 유지";
            arr.arraySize++;
            arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = interact;
            so.ApplyModifiedPropertiesWithoutUndo();
            return "편지 E 닫기 추가";
        }
    }
}
#endif
