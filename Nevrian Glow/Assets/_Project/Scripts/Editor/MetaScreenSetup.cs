#if UNITY_EDITOR
using System.Linq;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Systems.Save;
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
    /// 타이틀 · 일시정지 화면 그레이박스 (TASKS 5-6): Core 씬 UICanvas 아래에 TitleScreen · PauseScreen(각각 설정 패널 · 확인 창 포함)을 만들고 참조를 잇는다.
    /// 여러 번 실행해도 결과가 같다 — 이미 있으면 건드리지 않는다. 모양은 UI 아트(5-4) 때 다듬는다.
    /// </summary>
    public static class MetaScreenSetup
    {
        private const string InputPath = "Assets/InputSystem_Actions.inputactions";
        private const string FontPath = "Assets/_Project/Art/Fonts/font_maplestory_bold_sdf.asset";

        private static readonly Color PanelColor = new Color(0.02f, 0.09f, 0.12f, 0.92f);
        private static readonly Color ButtonColor = new Color(0.1f, 0.3f, 0.35f, 0.95f);
        private static readonly Color TitleGlow = new Color(0.55f, 0.95f, 1f, 1f);

        private static TMP_FontAsset font;

        [MenuItem("Glow Farm/Setup/Create Meta Screens")]
        private static void CreateMenu()
        {
            var canvas = GameObject.Find("UICanvas");
            if (canvas == null)
            {
                Debug.LogError("[Setup] UICanvas가 없음 - Core 씬을 연다");
                return;
            }
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
                font = Object.FindFirstObjectByType<TextMeshProUGUI>(FindObjectsInactive.Include)?.font;
            var cancel = AssetDatabase.LoadAllAssetsAtPath(InputPath).OfType<InputActionReference>().FirstOrDefault(r => r.action != null && r.action.actionMap.name == "UI" && r.action.name == "Cancel");
            var save = Object.FindFirstObjectByType<SaveSystem>();
            var boot = Object.FindFirstObjectByType<SceneBootstrapper>();
            if (cancel == null || save == null || boot == null)
            {
                Debug.LogError($"[Setup] 참조를 찾지 못함 - UI/Cancel {cancel != null} · SaveSystem {save != null} · SceneBootstrapper {boot != null}");
                return;
            }

            string result = string.Empty;
            var fader = canvas.transform.Find("ScreenFader");
            if (canvas.transform.Find("TitleScreen") == null)
            {
                BuildTitle(canvas.transform, boot, save, cancel);
                result += "TitleScreen 생성 · ";
            }
            else
                result += "TitleScreen 유지 · ";
            if (FillHideWhileOpen(canvas.transform))
                result += "타이틀 중 숨길 HUD 연결 · ";
            if (canvas.transform.Find("PauseScreen") == null)
            {
                BuildPause(canvas.transform, save, cancel);
                result += "PauseScreen 생성";
            }
            else
                result += "PauseScreen 유지";
            // 장소 이동 페이드는 늘 맨 위
            if (fader != null)
                fader.SetAsLastSibling();

            EditorSceneManager.MarkSceneDirty(canvas.scene);
            Debug.Log("[Setup] 메타 화면: " + result);
        }

        // ── 가게 HUD '오늘의 정리' (P-3) ──

        [MenuItem("Glow Farm/Setup/Create Tidy HUD")]
        private static void CreateTidyHudMenu()
        {
            var canvas = GameObject.Find("UICanvas");
            if (canvas == null)
            {
                Debug.LogError("[Setup] UICanvas가 없음 - Core 씬을 연다");
                return;
            }
            if (canvas.transform.Find("TidyHud") != null)
            {
                Debug.Log("[Setup] 오늘의 정리 HUD: 유지");
                return;
            }
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var root = new GameObject("TidyHud", typeof(RectTransform));
            var rt = root.GetComponent<RectTransform>();
            rt.SetParent(canvas.transform, false);
            Place(rt, new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(460f, 150f), new Vector2(1f, 1f));
            var panel = NewImage("Panel", rt, PanelColor);
            Stretch(panel, rt);
            var label = MakeText("Label", panel, "오늘의 정리", 22, Color.white, TextAlignmentOptions.TopLeft);
            Stretch(label.rectTransform, panel);
            label.rectTransform.offsetMin = new Vector2(18f, 10f);
            label.rectTransform.offsetMax = new Vector2(-12f, -10f);
            var view = root.AddComponent<TidyHudView>();
            var so = new SerializedObject(view);
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("label").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
            var fader = canvas.transform.Find("ScreenFader");
            if (fader != null)
                fader.SetAsLastSibling();
            Undo.RegisterCreatedObjectUndo(root, "Create TidyHud");
            EditorSceneManager.MarkSceneDirty(canvas.scene);
            Debug.Log("[Setup] 오늘의 정리 HUD: 생성");
        }

        // ── 타이틀 배경 카메라 투어 (2026-10-23) ──

        [MenuItem("Glow Farm/Setup/Create Title Camera Tour")]
        private static void CreateTitleTourMenu()
        {
            const string corePath = "Assets/_Project/Scenes/Core.unity";
            var scene = EditorSceneManager.GetSceneByPath(corePath);
            bool opened = false;
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(corePath, OpenSceneMode.Additive);
                opened = true;
            }
            TitleScreen title = null;
            foreach (var root in scene.GetRootGameObjects())
                title ??= root.GetComponentInChildren<TitleScreen>(true);
            if (title == null)
            {
                Debug.LogError("[Setup] Core 씬에 TitleScreen이 없음 - Create Meta Screens 먼저");
                return;
            }
            // 장면 값은 코드 기본값으로 다시 깐다 (있으면 지우고 새로 — 장면 형식이 바뀌어도 같은 결과)
            bool created = !title.TryGetComponent<TitleCameraTour>(out var tour);
            if (!created)
                Object.DestroyImmediate(tour, true);
            tour = title.gameObject.AddComponent<TitleCameraTour>();
            var so = new SerializedObject(tour);
            so.FindProperty("title").objectReferenceValue = title;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
            Debug.Log("[Setup] 타이틀 카메라 투어: " + (created ? "생성" : "기본 장면으로 다시 만듦"));
        }

        // ── P-7 화면 · 조작: 상점 탭 · 단계 넘기기 HUD 버튼 · 오프닝 편지 ──

        [MenuItem("Glow Farm/Setup/Create P-7 UI")]
        private static void CreateP7Menu()
        {
            var canvas = GameObject.Find("UICanvas");
            if (canvas == null)
            {
                Debug.LogError("[Setup] UICanvas가 없음 - Core 씬을 연다");
                return;
            }
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var refs = AssetDatabase.LoadAllAssetsAtPath(InputPath).OfType<InputActionReference>().ToList();
            InputActionReference Ref(string map, string action) => refs.FirstOrDefault(r => r.action != null && r.action.actionMap.name == map && r.action.name == action);
            var endPhase = Ref("Player", "EndPhase");
            var cancel = Ref("UI", "Cancel");
            if (endPhase == null || cancel == null)
            {
                Debug.LogError($"[Setup] 입력을 찾지 못함 - Player/EndPhase {endPhase != null} · UI/Cancel {cancel != null}");
                return;
            }

            var notes = new System.Collections.Generic.List<string>();
            notes.Add(BuildShopTabs(canvas.transform));
            notes.Add(BuildPhaseActionHud(canvas.transform, endPhase));
            notes.Add(BuildOpeningLetter(canvas.transform, endPhase, cancel));

            var fader = canvas.transform.Find("ScreenFader");
            if (fader != null)
                fader.SetAsLastSibling();
            EditorSceneManager.MarkSceneDirty(canvas.scene);
            Debug.Log("[Setup] P-7 UI: " + string.Join(" · ", notes));
        }

        // 상점 탭 줄: 제목 아래에 탭 버튼(상품 줄 원본을 복제한 모양), 상품 목록은 그 아래로 내린다
        private static string BuildShopTabs(Transform canvas)
        {
            var screen = canvas.Find("ShopScreen")?.GetComponent<ShopScreen>();
            var panel = canvas.Find("ShopScreen/Panel") as RectTransform;
            var list = canvas.Find("ShopScreen/Panel/EntryList") as RectTransform;
            var entry = canvas.Find("ShopScreen/Panel/EntryList/EntryTemplate");
            if (screen == null || panel == null || list == null || entry == null)
                return "상점 화면 구조를 찾지 못함 (ShopScreen/Panel/EntryList/EntryTemplate)";
            // 제목이 두 줄로 넘쳐 탭 줄과 겹치지 않게 패널 폭만큼 (P-7 확인 2026-10-22)
            if (panel.Find("Title") is RectTransform titleRt)
                titleRt.sizeDelta = new Vector2(1184f, titleRt.sizeDelta.y);
            if (panel.Find("TabBar") != null)
                return "상점 탭 유지";

            var bar = new GameObject("TabBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var barRt = bar.GetComponent<RectTransform>();
            barRt.SetParent(panel, false);
            Place(barRt, new Vector2(0f, 1f), new Vector2(28f, -66f), new Vector2(1184f, 48f), new Vector2(0f, 1f));
            var layout = bar.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var tab = Object.Instantiate(entry.gameObject, barRt);
            tab.name = "TabTemplate";
            var le = tab.GetComponent<LayoutElement>();
            if (le == null)
                le = tab.AddComponent<LayoutElement>();
            le.preferredWidth = 224f;
            le.preferredHeight = 48f;
            le.flexibleWidth = 0f;
            tab.SetActive(false);

            // 상품 목록을 탭 줄 아래로
            list.anchoredPosition = new Vector2(list.anchoredPosition.x, -126f);
            list.sizeDelta = new Vector2(list.sizeDelta.x, 366f);

            var empty = MakeText("EmptyTab", panel, "데모버전에서는 해금되지 않은 컨텐츠입니다.", 26, new Color(0.75f, 0.85f, 0.9f), TextAlignmentOptions.Center);
            Place(empty.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(1000f, 60f));
            empty.gameObject.SetActive(false);

            var so = new SerializedObject(screen);
            so.FindProperty("tabTemplate").objectReferenceValue = tab.GetComponent<HotbarSlotView>();
            so.FindProperty("tabParent").objectReferenceValue = barRt;
            so.FindProperty("emptyLabel").objectReferenceValue = empty;
            so.ApplyModifiedPropertiesWithoutUndo();
            Undo.RegisterCreatedObjectUndo(bar, "Create Shop Tabs");
            return "상점 탭 생성";
        }

        // 화면 오른쪽 아래 단계 버튼: 농사 "농사 끝내기 [Enter]"(문과 같은 길) · 장사 "장사 건너뛰기 [Space]"(키는 SalesSystem)
        private static string BuildPhaseActionHud(Transform canvas, InputActionReference endPhase)
        {
            if (canvas.Find("PhaseActionHud") != null)
                return "단계 버튼 유지";
            var root = new GameObject("PhaseActionHud", typeof(RectTransform));
            var rt = root.GetComponent<RectTransform>();
            rt.SetParent(canvas, false);
            Place(rt, new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(380f, 60f), new Vector2(1f, 0f));
            var button = MakeButton("Button", rt, "농사 끝내기 [Enter]");
            Stretch(button.GetComponent<RectTransform>(), rt);

            var hud = root.AddComponent<PhaseActionHud>();
            var so = new SerializedObject(hud);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("label").objectReferenceValue = button.GetComponentInChildren<TMP_Text>();
            var entries = so.FindProperty("entries");
            entries.arraySize = 2;
            void Entry(int i, Logic.Game.DayPhase phase, string label, string key, PhaseActionHud.ActionKind action, InputActionReference input)
            {
                var e = entries.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("phase").enumValueIndex = (int)phase;
                e.FindPropertyRelative("label").stringValue = label;
                e.FindPropertyRelative("keyHint").stringValue = key;
                e.FindPropertyRelative("action").enumValueIndex = (int)action;
                e.FindPropertyRelative("key").objectReferenceValue = input;
            }
            Entry(0, Logic.Game.DayPhase.Farming, "농사 끝내기", "Enter", PhaseActionHud.ActionKind.UseDoor, endPhase);
            Entry(1, Logic.Game.DayPhase.Sales, "장사 건너뛰기", "Space", PhaseActionHud.ActionKind.SkipSales, null);
            so.ApplyModifiedPropertiesWithoutUndo();
            FillTitleHide(canvas, root);
            Undo.RegisterCreatedObjectUndo(root, "Create PhaseActionHud");
            return "단계 버튼 생성";
        }

        // 타이틀이 떠 있는 동안 숨길 목록에 더한다 (이미 있으면 그대로)
        private static void FillTitleHide(Transform canvas, GameObject target)
        {
            var title = canvas.Find("TitleScreen")?.GetComponent<TitleScreen>();
            if (title == null)
                return;
            var so = new SerializedObject(title);
            var arr = so.FindProperty("hideWhileOpen");
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == target)
                    return;
            arr.arraySize++;
            arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = target;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string BuildOpeningLetter(Transform canvas, InputActionReference endPhase, InputActionReference cancel)
        {
            var existing = canvas.Find("OpeningLetterScreen")?.GetComponent<OpeningLetterScreen>();
            string note = "오프닝 편지 유지";
            if (existing == null)
            {
                var root = Stretch(new GameObject("OpeningLetterScreen", typeof(RectTransform)), canvas);
                var panel = Stretch(NewImage("Panel", root, new Color(0.01f, 0.03f, 0.05f, 0.75f)), root);
                var paper = NewImage("Paper", panel, new Color(0.06f, 0.12f, 0.16f, 0.96f));
                Place(paper, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 700f));
                var title = MakeText("Title", paper, string.Empty, 38, TitleGlow, TextAlignmentOptions.Center);
                Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(860f, 56f), new Vector2(0.5f, 1f));
                var body = MakeText("Body", paper, string.Empty, 25, new Color(0.9f, 0.93f, 0.95f), TextAlignmentOptions.TopLeft);
                Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -116f), new Vector2(840f, 470f), new Vector2(0.5f, 1f));
                body.lineSpacing = 10f;
                var start = MakeButton("Start", paper, "농장 시작하기");
                Place(start.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(380f, 60f), new Vector2(0.5f, 0f));

                existing = root.gameObject.AddComponent<OpeningLetterScreen>();
                var so = new SerializedObject(existing);
                so.FindProperty("panel").objectReferenceValue = panel.gameObject;
                so.FindProperty("titleLabel").objectReferenceValue = title;
                so.FindProperty("bodyLabel").objectReferenceValue = body;
                so.FindProperty("startButton").objectReferenceValue = start;
                var keys = so.FindProperty("closeActions");
                keys.arraySize = 2;
                keys.GetArrayElementAtIndex(0).objectReferenceValue = endPhase;
                keys.GetArrayElementAtIndex(1).objectReferenceValue = cancel;
                so.ApplyModifiedPropertiesWithoutUndo();
                Undo.RegisterCreatedObjectUndo(root.gameObject, "Create OpeningLetterScreen");
                note = "오프닝 편지 생성";
            }
            var titleScreen = canvas.Find("TitleScreen")?.GetComponent<TitleScreen>();
            if (titleScreen != null)
            {
                var tso = new SerializedObject(titleScreen);
                var prop = tso.FindProperty("openingLetter");
                if (prop.objectReferenceValue == null)
                {
                    prop.objectReferenceValue = existing;
                    tso.ApplyModifiedPropertiesWithoutUndo();
                    note += " · 타이틀 연결";
                }
            }
            return note;
        }

        // ── 타이틀 ──

        private static void BuildTitle(Transform canvas, SceneBootstrapper boot, SaveSystem save, InputActionReference cancel)
        {
            var root = Stretch(new GameObject("TitleScreen", typeof(RectTransform)), canvas);
            var panel = Stretch(NewImage("Panel", root, new Color(0.01f, 0.04f, 0.07f, 0.55f)), root);

            var title = MakeText("GameTitle", panel, "Nevrian Glow Farm", 84, TitleGlow, TextAlignmentOptions.Center);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(1200f, 110f));
            var sub = MakeText("Subtitle", panel, "외계 행성의 발광 작물 농장", 28, new Color(0.85f, 0.8f, 1f), TextAlignmentOptions.Center);
            Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -290f), new Vector2(1200f, 40f));

            var menu = Menu(panel, new Vector2(0f, -60f));
            var cont = MakeButton("Continue", menu, "이어하기");
            var newGame = MakeButton("NewGame", menu, "새 게임");
            var settingsButton = MakeButton("Settings", menu, "설정");
            var quit = MakeButton("Quit", menu, "종료");
            var info = MakeText("SaveInfo", menu, string.Empty, 22, new Color(0.8f, 0.9f, 0.95f), TextAlignmentOptions.Center);
            info.GetComponent<RectTransform>().sizeDelta = new Vector2(440f, 34f);
            info.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;

            var settings = BuildSettings(panel);
            var confirm = BuildConfirm(panel);

            var screen = root.gameObject.AddComponent<TitleScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("bootstrapper").objectReferenceValue = boot;
            so.FindProperty("saveSystem").objectReferenceValue = save;
            so.FindProperty("menu").objectReferenceValue = menu.gameObject;
            so.FindProperty("continueButton").objectReferenceValue = cont;
            so.FindProperty("newGameButton").objectReferenceValue = newGame;
            so.FindProperty("settingsButton").objectReferenceValue = settingsButton;
            so.FindProperty("quitButton").objectReferenceValue = quit;
            so.FindProperty("saveInfoLabel").objectReferenceValue = info;
            so.FindProperty("settings").objectReferenceValue = settings;
            so.FindProperty("confirm").objectReferenceValue = confirm;
            so.FindProperty("cancelAction").objectReferenceValue = cancel;
            so.ApplyModifiedPropertiesWithoutUndo();
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Create TitleScreen");
        }

        // 타이틀이 떠 있는 동안 숨길 HUD · 핫바 · 조준점 (비어 있을 때만 채운다)
        private static bool FillHideWhileOpen(Transform canvas)
        {
            var title = canvas.Find("TitleScreen")?.GetComponent<TitleScreen>();
            if (title == null)
                return false;
            var so = new SerializedObject(title);
            var arr = so.FindProperty("hideWhileOpen");
            if (arr.arraySize > 0)
                return false;
            var targets = new[] { "HUD", "Hotbar", "Crosshair" }.Select(n => canvas.Find(n)).Where(t => t != null).ToArray();
            arr.arraySize = targets.Length;
            for (int i = 0; i < targets.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = targets[i].gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // ── 일시정지 ──

        private static void BuildPause(Transform canvas, SaveSystem save, InputActionReference cancel)
        {
            var root = Stretch(new GameObject("PauseScreen", typeof(RectTransform)), canvas);
            var panel = Stretch(NewImage("Panel", root, new Color(0.01f, 0.03f, 0.05f, 0.7f)), root);

            var title = MakeText("Title", panel, "일시정지", 48, Color.white, TextAlignmentOptions.Center);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(800f, 70f));

            var menu = Menu(panel, new Vector2(0f, -40f));
            var resume = MakeButton("Resume", menu, "계속");
            var settingsButton = MakeButton("Settings", menu, "설정");
            var toTitle = MakeButton("ToTitle", menu, "타이틀로");
            var quit = MakeButton("Quit", menu, "게임 종료");
            var info = MakeText("SaveInfo", menu, string.Empty, 20, new Color(0.8f, 0.9f, 0.95f), TextAlignmentOptions.Center);
            info.GetComponent<RectTransform>().sizeDelta = new Vector2(600f, 60f);
            info.gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;

            var settings = BuildSettings(panel);
            var confirm = BuildConfirm(panel);

            var screen = root.gameObject.AddComponent<PauseScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("saveSystem").objectReferenceValue = save;
            so.FindProperty("menu").objectReferenceValue = menu.gameObject;
            so.FindProperty("resumeButton").objectReferenceValue = resume;
            so.FindProperty("settingsButton").objectReferenceValue = settingsButton;
            so.FindProperty("titleButton").objectReferenceValue = toTitle;
            so.FindProperty("quitButton").objectReferenceValue = quit;
            so.FindProperty("saveInfoLabel").objectReferenceValue = info;
            so.FindProperty("settings").objectReferenceValue = settings;
            so.FindProperty("confirm").objectReferenceValue = confirm;
            so.FindProperty("cancelAction").objectReferenceValue = cancel;
            so.ApplyModifiedPropertiesWithoutUndo();
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Create PauseScreen");
        }

        // ── 설정 패널 ──

        private static SettingsView BuildSettings(RectTransform parent)
        {
            var panel = NewImage("Settings", parent, PanelColor);
            Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 600f));
            var title = MakeText("Title", panel, "설정", 32, Color.white, TextAlignmentOptions.Left);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -28f), new Vector2(400f, 44f), new Vector2(0f, 1f));

            string[] names = { "전체 볼륨", "음악", "효과음", "마우스 감도" };
            var sliders = new Slider[4];
            var values = new TMP_Text[4];
            for (int i = 0; i < 4; i++)
            {
                float y = -100f - i * 70f;
                var label = MakeText("Label" + i, panel, names[i], 24, Color.white, TextAlignmentOptions.Left);
                Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(40f, y), new Vector2(200f, 40f), new Vector2(0f, 1f));
                sliders[i] = MakeSlider("Slider" + i, panel, new Vector2(250f, y - 10f));
                values[i] = MakeText("Value" + i, panel, "100%", 24, new Color(0.8f, 0.95f, 1f), TextAlignmentOptions.Right);
                Place(values[i].rectTransform, new Vector2(0f, 1f), new Vector2(660f, y), new Vector2(120f, 40f), new Vector2(0f, 1f));
            }

            float ry = -100f - 4 * 70f;
            var resLabel = MakeText("ResolutionLabel", panel, "해상도", 24, Color.white, TextAlignmentOptions.Left);
            Place(resLabel.rectTransform, new Vector2(0f, 1f), new Vector2(40f, ry), new Vector2(200f, 40f), new Vector2(0f, 1f));
            var prev = MakeButton("ResolutionPrev", panel, "<");
            Place(prev.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(250f, ry), new Vector2(60f, 44f), new Vector2(0f, 1f));
            var res = MakeText("Resolution", panel, "1920 × 1080", 24, Color.white, TextAlignmentOptions.Center);
            Place(res.rectTransform, new Vector2(0f, 1f), new Vector2(320f, ry), new Vector2(330f, 44f), new Vector2(0f, 1f));
            var next = MakeButton("ResolutionNext", panel, ">");
            Place(next.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(660f, ry), new Vector2(60f, 44f), new Vector2(0f, 1f));

            var full = MakeButton("Fullscreen", panel, "전체 화면: 켜짐");
            Place(full.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(250f, ry - 64f), new Vector2(470f, 48f), new Vector2(0f, 1f));

            var back = MakeButton("Back", panel, "뒤로");
            Place(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(300f, 54f), new Vector2(0.5f, 0f));

            var view = panel.gameObject.AddComponent<SettingsView>();
            var so = new SerializedObject(view);
            so.FindProperty("masterSlider").objectReferenceValue = sliders[0];
            so.FindProperty("musicSlider").objectReferenceValue = sliders[1];
            so.FindProperty("sfxSlider").objectReferenceValue = sliders[2];
            so.FindProperty("sensitivitySlider").objectReferenceValue = sliders[3];
            var arr = so.FindProperty("valueLabels");
            arr.arraySize = 4;
            for (int i = 0; i < 4; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.FindProperty("resolutionLabel").objectReferenceValue = res;
            so.FindProperty("resolutionPrev").objectReferenceValue = prev;
            so.FindProperty("resolutionNext").objectReferenceValue = next;
            so.FindProperty("fullscreenButton").objectReferenceValue = full;
            so.FindProperty("fullscreenLabel").objectReferenceValue = full.GetComponentInChildren<TMP_Text>();
            so.FindProperty("backButton").objectReferenceValue = back;
            so.ApplyModifiedPropertiesWithoutUndo();
            // 그래픽 품질 줄 (최적화 2026-10-30) — 품질 데이터가 있으면 해상도 줄 아래에
            var quality = AssetDatabase.LoadAssetAtPath<NevrianGlowFarm.Data.GraphicsQualityData>(RenderBudgetSetup.QualityPath);
            if (quality != null)
                RenderBudgetSetup.AddQualityRow(view, quality);
            panel.gameObject.SetActive(false);
            return view;
        }

        // ── 확인 창 ──

        private static ConfirmDialog BuildConfirm(RectTransform parent)
        {
            var panel = NewImage("Confirm", parent, PanelColor);
            Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 300f));
            var message = MakeText("Message", panel, string.Empty, 26, Color.white, TextAlignmentOptions.Center);
            Place(message.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(700f, 150f), new Vector2(0.5f, 1f));
            var yes = MakeButton("Yes", panel, "예");
            Place(yes.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(-170f, 34f), new Vector2(300f, 56f), new Vector2(0.5f, 0f));
            var no = MakeButton("No", panel, "아니오");
            Place(no.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(170f, 34f), new Vector2(300f, 56f), new Vector2(0.5f, 0f));
            no.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.26f, 0.95f);

            var dialog = panel.gameObject.AddComponent<ConfirmDialog>();
            var so = new SerializedObject(dialog);
            so.FindProperty("messageLabel").objectReferenceValue = message;
            so.FindProperty("yesButton").objectReferenceValue = yes;
            so.FindProperty("yesLabel").objectReferenceValue = yes.GetComponentInChildren<TMP_Text>();
            so.FindProperty("noButton").objectReferenceValue = no;
            so.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
            return dialog;
        }

        // ── 부품 ──

        private static RectTransform Menu(RectTransform parent, Vector2 position)
        {
            var go = new GameObject("Menu", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Place(rt, new Vector2(0.5f, 0.5f), position, new Vector2(600f, 0f));
            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            go.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rt;
        }

        private static Button MakeButton(string name, Transform parent, string label)
        {
            var img = NewImage(name, parent, ButtonColor);
            img.sizeDelta = new Vector2(440f, 60f);
            var button = img.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.75f, 1f, 1f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.5f);
            button.colors = colors;
            var text = MakeText("Label", img, label, 26, Color.white, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, img);
            return button;
        }

        private static Slider MakeSlider(string name, Transform parent, Vector2 position)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var rt = root.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Place(rt, new Vector2(0f, 1f), position, new Vector2(390f, 22f), new Vector2(0f, 1f));
            var bg = Stretch(NewImage("Background", rt, new Color(0.08f, 0.15f, 0.2f, 1f)), rt);
            var fillArea = Stretch(new GameObject("Fill Area", typeof(RectTransform)), rt);
            var fill = Stretch(NewImage("Fill", fillArea, new Color(0.3f, 0.85f, 0.95f, 1f)), fillArea);
            var handleArea = Stretch(new GameObject("Handle Slide Area", typeof(RectTransform)), rt);
            handleArea.offsetMin = new Vector2(10f, 0f);
            handleArea.offsetMax = new Vector2(-10f, 0f);
            var handle = NewImage("Handle", handleArea, new Color(0.9f, 0.95f, 1f, 1f));
            handle.sizeDelta = new Vector2(20f, 0f);
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            handle.offsetMin = new Vector2(-10f, -6f);
            handle.offsetMax = new Vector2(10f, 6f);
            var slider = root.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            _ = bg;
            return slider;
        }

        private static TextMeshProUGUI MakeText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
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
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        private static RectTransform NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go.GetComponent<RectTransform>();
        }

        private static RectTransform Stretch(GameObject go, Transform parent) => Stretch(go.GetComponent<RectTransform>(), parent);

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

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size) => Place(rt, anchor, position, size, new Vector2(0.5f, 0.5f));

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }
    }
}
#endif
