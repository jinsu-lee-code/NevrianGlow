#if UNITY_EDITOR
using System.Collections.Generic;
using NevrianGlowFarm.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NevrianGlowFarm.Editor
{
    /// <summary>
    /// 5-4b UI 레이아웃 다듬기 (2026-10-23): 5-4a 스킨(아트 9-slice 그림) 위에서 크기 · 여백만 맞춘다. Core 씬 UICanvas. 여러 번 실행해도 결과가 같다.
    /// ① 아이템 칸 정사각(아이콘 가운데 · 이름/개수 아래 작게), 핫바는 칸에 개수만 + 고른 아이템 이름을 핫바 위 한 줄
    /// ② 창 안 글자 · 목록을 패널 모서리 장식(테두리 32 px) 안쪽으로 — 위 · 아래 · 왼쪽 여백
    /// ③ 가방 · 창고 칸 격자를 새 칸 크기로 다시 쌓기 ④ 설정 슬라이더에 막대 그림
    /// </summary>
    public static class UiLayoutSetup
    {
        private const string CorePath = "Assets/_Project/Scenes/Core.unity";
        private const float Slot = 84f, Gap = 6f;           // 아이템 칸 (px)
        private const float EdgeX = 44f, EdgeTop = 34f, EdgeBottom = 30f; // 패널 안쪽 여백 (px, 테두리 32 + 숨 쉴 틈)

        private static readonly string[] WindowPanels =
        {
            "InventoryScreen/Panel", "StorageScreen/Panel", "ShopScreen/Panel", "SkillScreen/Panel",
            "SettlementScreen/Panel", "DemoEndScreen/Panel",
        };

        [MenuItem("Glow Farm/Setup/Apply UI Layout (5-4b)")]
        private static void ApplyMenu()
        {
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
                Debug.LogError("[Setup] UI 레이아웃: Core 씬에 UICanvas가 없음");
                return;
            }

            // 순서: 여백(모자란 만큼만 옮김) → 격자 쌓기(정해진 자리) — 다시 실행해도 같은 결과
            var notes = new List<string>
            {
                Slots(canvas),
                $"창 여백 {Margins(canvas)}",
                Grids(canvas),
                Lists(canvas),
                TitleGroup(canvas),
                Sliders(canvas),
            };

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
            Debug.Log("[Setup] UI 레이아웃 5-4b: " + string.Join(" · ", notes));
        }

        // ── ① 아이템 칸 ──

        private static string Slots(Transform canvas)
        {
            int n = 0;
            foreach (var v in canvas.GetComponentsInChildren<HotbarView>(true))
            {
                if (Template(v, "slotTemplate") is HotbarSlotView s)
                {
                    SquareSlot(s);
                    n++;
                }
                // 고른 아이템 이름: 핫바 자식(레이아웃 제외) — 타이틀 중엔 핫바와 함께 숨음
                var hotbar = (RectTransform)v.transform;
                var label = hotbar.Find("SelectedName") as RectTransform;
                if (label == null)
                {
                    label = new GameObject("SelectedName", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement)).GetComponent<RectTransform>();
                    label.SetParent(hotbar, false);
                }
                label.GetComponent<LayoutElement>().ignoreLayout = true;
                label.anchorMin = label.anchorMax = new Vector2(0.5f, 1f);
                label.pivot = new Vector2(0.5f, 0f);
                label.anchoredPosition = new Vector2(0f, 6f);
                label.sizeDelta = new Vector2(600f, 30f);
                var text = label.GetComponent<TextMeshProUGUI>();
                var reference = s0(v);
                if (reference != null)
                    text.font = reference.font;
                text.fontSize = 20f;
                text.alignment = TextAlignmentOptions.Bottom;
                text.color = new Color(0.9f, 0.97f, 1f, 1f);
                text.raycastTarget = false;
                text.text = string.Empty;
                var so = new SerializedObject(v);
                so.FindProperty("selectedNameLabel").objectReferenceValue = text;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var v in canvas.GetComponentsInChildren<SlotGridView>(true))
                if (Template(v, "slotTemplate") is HotbarSlotView s)
                {
                    SquareSlot(s);
                    n++;
                }
            return $"아이템 칸 원본 {n}개 {Slot} × {Slot}";
        }

        private static TMP_Text s0(HotbarView v) =>
            Template(v, "slotTemplate") is HotbarSlotView s ? s.GetComponentInChildren<TMP_Text>(true) : null;

        // 정사각 칸: 아이콘 가운데 위, 이름/개수 아래 띠, 숫자 키 왼쪽 위
        private static void SquareSlot(HotbarSlotView view)
        {
            var rt = (RectTransform)view.transform;
            rt.sizeDelta = new Vector2(Slot, Slot);
            if (view.transform.Find("Icon") is RectTransform icon)
            {
                icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
                icon.pivot = new Vector2(0.5f, 0.5f);
                icon.anchoredPosition = new Vector2(0f, 7f);
                icon.sizeDelta = new Vector2(54f, 54f);
            }
            if (view.transform.Find("Name") is RectTransform name && name.TryGetComponent<TMP_Text>(out var label))
            {
                name.anchorMin = new Vector2(0f, 0f);
                name.anchorMax = new Vector2(1f, 0f);
                name.pivot = new Vector2(0.5f, 0f);
                name.offsetMin = new Vector2(5f, 4f);
                name.offsetMax = new Vector2(-5f, 26f);
                label.alignment = TextAlignmentOptions.Bottom;
                label.enableAutoSizing = true;
                label.fontSizeMin = 9f;
                label.fontSizeMax = 14f;
                label.enableWordWrapping = true;
                EditorUtility.SetDirty(label);
            }
            if (view.transform.Find("Key") is RectTransform key && key.TryGetComponent<TMP_Text>(out var keyLabel))
            {
                key.anchorMin = Vector2.zero;
                key.anchorMax = Vector2.one;
                key.offsetMin = new Vector2(7f, 4f);
                key.offsetMax = new Vector2(-4f, -5f);
                keyLabel.fontSize = 14f;
                keyLabel.alignment = TextAlignmentOptions.TopLeft;
                EditorUtility.SetDirty(keyLabel);
            }
            EditorUtility.SetDirty(rt);
        }

        // ── ③ 가방 · 창고 칸 격자 ──

        private static string Grids(Transform canvas)
        {
            int n = 0;
            foreach (var grid in canvas.GetComponentsInChildren<GridLayoutGroup>(true))
            {
                grid.cellSize = new Vector2(Slot, Slot);
                grid.spacing = new Vector2(Gap, Gap);
                EditorUtility.SetDirty(grid);
                n++;
            }
            // 가방 창: 제목 → 가방(2줄) → 핫바 제목 → 핫바(1줄), 위에서부터 (가방 칸 16 = 8 × 2)
            Stack(canvas.Find("InventoryScreen/Panel"), EdgeX, -EdgeTop - 2f);
            Stack(canvas.Find("StorageScreen/Panel"), 420f, -EdgeTop - 40f);
            return $"칸 격자 {n}개";
        }

        // 제목 · 가방 격자 · 핫바 제목 · 핫바 격자를 위에서부터 쌓는다 (왼쪽 x, 시작 y)
        private static void Stack(Transform panel, float x, float top)
        {
            if (panel == null)
                return;
            float y = top;
            if (panel.Find("Title") is RectTransform title && x <= EdgeX + 1f)
                y = Row(title, x, y, 40f, 8f, keepWidth: true);
            else if (panel.Find("BagTitle") is RectTransform bagTitle)
                y = Row(bagTitle, x, y, 30f, 6f, keepWidth: true);
            if (panel.Find("BagGrid") is RectTransform bag)
                y = Row(bag, x, y, Slot * 2f + Gap, 16f, keepWidth: false);
            if (panel.Find("HotbarTitle") is RectTransform hotbarTitle)
                y = Row(hotbarTitle, x, y, 30f, 6f, keepWidth: true);
            if (panel.Find("HotbarGrid") is RectTransform hotbarGrid)
                Row(hotbarGrid, x, y, Slot, 0f, keepWidth: false);
        }

        private static float Row(RectTransform rt, float x, float top, float height, float gapAfter, bool keepWidth)
        {
            float width = keepWidth ? rt.rect.width : 8f * Slot + 7f * Gap;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(x, top - height);
            rt.offsetMax = new Vector2(x + width, top);
            EditorUtility.SetDirty(rt);
            return top - height - gapAfter;
        }

        // ── ② 창 여백 ──

        // 패널 바로 아래 자식 중 왼쪽 · 위 · 아래 가장자리에 붙은 것을 테두리 안쪽으로. 모자란 만큼만 옮겨 다시 실행해도 같음
        private static int Margins(Transform canvas)
        {
            int moved = 0;
            foreach (var path in WindowPanels)
            {
                if (!(canvas.Find(path) is RectTransform panel))
                    continue;
                float width = panel.rect.width;
                var top = new List<RectTransform>();
                var bottom = new List<RectTransform>();
                foreach (Transform c in panel)
                {
                    var rt = (RectTransform)c;
                    if (rt.anchorMin != rt.anchorMax)
                        continue; // 늘어나는 것(빈 탭 글자 등)은 그대로
                    if (Mathf.Approximately(rt.anchorMin.x, 0f))
                    {
                        // 왼쪽 여백
                        if (rt.offsetMin.x < EdgeX)
                        {
                            float d = EdgeX - rt.offsetMin.x;
                            rt.offsetMin += new Vector2(d, 0f);
                            rt.offsetMax += new Vector2(d, 0f);
                            moved++;
                        }
                        // 오른쪽이 테두리를 넘으면 줄인다
                        if (rt.offsetMax.x > width - EdgeX)
                        {
                            rt.offsetMax = new Vector2(width - EdgeX, rt.offsetMax.y);
                            moved++;
                        }
                    }
                    if (Mathf.Approximately(rt.anchorMin.y, 1f))
                        top.Add(rt);
                    else if (Mathf.Approximately(rt.anchorMin.y, 0f))
                        bottom.Add(rt);
                    EditorUtility.SetDirty(rt);
                }
                // 위 · 아래 묶음은 함께 옮겨 서로 겹치지 않게 — 옮긴 만큼 패널을 키워 가운데 공간은 그대로
                float topNeed = 0f, bottomNeed = 0f;
                foreach (var rt in top)
                    topNeed = Mathf.Max(topNeed, EdgeTop + rt.offsetMax.y);
                foreach (var rt in bottom)
                    bottomNeed = Mathf.Max(bottomNeed, EdgeBottom - rt.offsetMin.y);
                foreach (var rt in top)
                {
                    rt.offsetMin -= new Vector2(0f, topNeed);
                    rt.offsetMax -= new Vector2(0f, topNeed);
                }
                foreach (var rt in bottom)
                {
                    rt.offsetMin += new Vector2(0f, bottomNeed);
                    rt.offsetMax += new Vector2(0f, bottomNeed);
                }
                if (topNeed + bottomNeed > 0f)
                {
                    panel.sizeDelta += new Vector2(0f, topNeed + bottomNeed);
                    moved++;
                }
                // 목록(EntryList)이 아래 글자 · 버튼과 겹치면 목록 아래 끝을 올린다
                float height = panel.sizeDelta.y;
                float bottomTop = 0f;
                foreach (var rt in bottom)
                    bottomTop = Mathf.Max(bottomTop, rt.offsetMax.y);
                foreach (var rt in top)
                    if (rt.name == "EntryList" && rt.anchorMin.x == 0f && rt.offsetMin.x < 200f)
                    {
                        float limit = -(height - bottomTop - 10f);
                        if (rt.offsetMin.y < limit)
                            rt.offsetMin = new Vector2(rt.offsetMin.x, limit);
                    }
                EditorUtility.SetDirty(panel);
            }
            return moved;
        }

        // ── 목록 줄 높이 (상점 · 창고 · 스킬) — 스킬 10줄이 창 안에 들어가게 ──

        private const float RowHeight = 34f, RowGap = 4f;

        private static string Lists(Transform canvas)
        {
            int n = 0;
            foreach (var list in canvas.GetComponentsInChildren<VerticalLayoutGroup>(true))
            {
                if (list.name != "EntryList")
                    continue;
                list.spacing = RowGap;
                foreach (Transform row in list.transform)
                {
                    if (!row.TryGetComponent<LayoutElement>(out var le))
                        le = row.gameObject.AddComponent<LayoutElement>();
                    le.minHeight = RowHeight;
                    le.preferredHeight = RowHeight;
                    EditorUtility.SetDirty(le);
                }
                EditorUtility.SetDirty(list);
                n++;
            }
            return $"목록 {n}개 줄 {RowHeight} px";
        }

        // ── 타이틀: 로고 · 부제를 메뉴와 함께 숨기기 (설정 · 확인 창이 로고와 겹치지 않게) ──

        private static readonly string[] TitleShowWithMenu = { "Logo", "MoonHalo", "MoonShine", "LogoShine", "LogoStars", "Subtitle" };

        private static string TitleGroup(Transform canvas)
        {
            var title = canvas.GetComponentInChildren<TitleScreen>(true);
            var panel = title != null ? title.transform.Find("Panel") : null;
            if (panel == null)
                return "타이틀 없음";
            var list = new List<GameObject>();
            foreach (var name in TitleShowWithMenu)
                if (panel.Find(name) is Transform t)
                    list.Add(t.gameObject);
            var so = new SerializedObject(title);
            var arr = so.FindProperty("showWithMenu");
            arr.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return $"타이틀 로고 묶음 {list.Count}";
        }

        // ── ④ 설정 슬라이더 ──

        private static string Sliders(Transform canvas)
        {
            var bg = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Panels/ui_bar_xp_bg.png");
            var fill = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Panels/ui_bar_xp_fill.png");
            var knob = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/ui_star_glint.png");
            if (bg == null || fill == null)
                return "슬라이더 그림 없음";
            int n = 0;
            foreach (var slider in canvas.GetComponentsInChildren<Slider>(true))
            {
                if (slider.transform.Find("Background") is RectTransform b && b.TryGetComponent<Image>(out var bImage))
                {
                    bImage.sprite = bg;
                    bImage.type = Image.Type.Sliced;
                    bImage.pixelsPerUnitMultiplier = 1.8f;
                    bImage.color = Color.white;
                    EditorUtility.SetDirty(bImage);
                }
                if (slider.fillRect != null && slider.fillRect.TryGetComponent<Image>(out var fImage))
                {
                    fImage.sprite = fill;
                    fImage.type = Image.Type.Simple;
                    fImage.color = Color.white;
                    EditorUtility.SetDirty(fImage);
                }
                if (knob != null && slider.handleRect != null && slider.handleRect.TryGetComponent<Image>(out var hImage))
                {
                    hImage.sprite = knob;
                    hImage.type = Image.Type.Simple;
                    hImage.preserveAspect = true;
                    hImage.color = new Color(0.75f, 0.97f, 1f, 1f);
                    slider.handleRect.sizeDelta = new Vector2(36f, 0f); // 높이는 슬라이드 영역에 맞춤(+12)
                    EditorUtility.SetDirty(hImage);
                }
                n++;
            }
            return $"슬라이더 {n}";
        }

        private static Object Template(Object owner, string property) => new SerializedObject(owner).FindProperty(property)?.objectReferenceValue;
    }
}
#endif
