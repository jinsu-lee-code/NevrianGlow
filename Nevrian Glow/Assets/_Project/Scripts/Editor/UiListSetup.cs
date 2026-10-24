#if UNITY_EDITOR
using System.Collections.Generic;
using NevrianGlowFarm.Systems.Player;
using NevrianGlowFarm.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NevrianGlowFarm.Editor
{
    /// <summary>
    /// 5-4c 목록 · 툴팁 (2026-10-24, 스타듀밸리식 가독성): 5-4a 스킨 · 5-4b 레이아웃 뒤에 실행한다. Core 씬 UICanvas. 여러 번 실행해도 결과가 같다.
    /// ① 툴팁 창(Tooltip) 하나 — ScreenFader 바로 아래 ② 상점 · 스킬 · 창고 목록 줄 원본을 [아이콘] 이름 / 둘째 줄 … 값 [아이콘] 모양으로 (올림 · 누름 그림)
    /// ③ 상점 · 스킬 탭 줄 참조 연결 ④ 목록을 스크롤(EntryScroll ▸ EntryList)로 ⑤ 칸 원본의 아이콘 참조 연결(비어 있어 아이콘이 안 보이던 것) + 가방 · 창고 칸 툴팁
    /// </summary>
    public static class UiListSetup
    {
        private const string CorePath = "Assets/_Project/Scenes/Core.unity";
        private const string Panels = "Assets/Art/UI/Panels";
        private const string Icons = "Assets/Art/UI/Icons";
        private const float EdgeX = 44f;      // 5-4b 창 안쪽 여백 (px)
        private const float RowGap = 4f;      // 목록 줄 간격 (px)
        private const float TabHeight = 44f;
        private static readonly Color DetailColor = new Color(0.72f, 0.84f, 0.9f, 1f);
        private static readonly Color ValueColor = new Color(1f, 0.9f, 0.6f, 1f);
        private static readonly Color BadgeColor = new Color(0.55f, 0.95f, 1f, 1f);
        private static readonly Color SubtitleColor = new Color(0.55f, 0.9f, 1f, 1f);
        private static readonly Color BodyColor = new Color(0.88f, 0.93f, 0.96f, 1f);
        private static readonly Color Unavailable = new Color(0.6f, 0.6f, 0.66f, 1f);

        private struct Art
        {
            public Sprite Panel, Normal, Hover, Pressed, Disabled, Dollar, SkillPoint;
            public TMP_FontAsset Font;
        }

        [MenuItem("Glow Farm/Setup/Apply UI Lists (5-4c)")]
        private static void ApplyMenu()
        {
            var art = new Art
            {
                Panel = Load(Panels, "ui_panel_base"),
                Normal = Load(Panels, "ui_button_normal"),
                Hover = Load(Panels, "ui_button_hover"),
                Pressed = Load(Panels, "ui_button_pressed"),
                Disabled = Load(Panels, "ui_button_disabled"),
                Dollar = Load(Icons, "icon_dollar"),
                SkillPoint = Load(Icons, "icon_skillpoint"),
            };
            if (art.Panel == null || art.Normal == null)
            {
                Debug.LogError($"[Setup] UI 목록: 아트 그림이 없음 ({Panels}) - 5-4a 스킨을 먼저");
                return;
            }

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
                Debug.LogError("[Setup] UI 목록: Core 씬에 UICanvas가 없음");
                return;
            }
            var reference = canvas.Find("ShopScreen/Panel/Title");
            art.Font = reference != null && reference.TryGetComponent<TMP_Text>(out var refText) ? refText.font : null;

            PlayerInventory inventory = null;
            foreach (var root in scene.GetRootGameObjects())
                if (inventory == null)
                    inventory = root.GetComponentInChildren<PlayerInventory>(true);

            var notes = new List<string>
            {
                Tooltip(canvas, art),
                SlotIcons(canvas),
                Shop(canvas, art, inventory),
                Skill(canvas, art),
                Storage(canvas, art),
            };

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
            Debug.Log("[Setup] UI 목록 5-4c: " + string.Join(" · ", notes));
        }

        // ── ① 툴팁 ──

        private static string Tooltip(Transform canvas, Art art)
        {
            var root = canvas.Find("Tooltip") as RectTransform;
            if (root == null)
            {
                root = new GameObject("Tooltip", typeof(RectTransform), typeof(TooltipView)).GetComponent<RectTransform>();
                root.SetParent(canvas, false);
            }
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            // 맨 위에 그리되 페이드 판 아래 (ScreenFader 바로 앞 순서)
            var fader = canvas.Find("ScreenFader");
            root.SetSiblingIndex(fader != null ? fader.GetSiblingIndex() : canvas.childCount - 1);

            var panel = Child(root, "Panel", typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0f, 1f);
            panel.sizeDelta = new Vector2(380f, 120f);
            var image = panel.GetComponent<Image>();
            image.sprite = art.Panel;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 2f;
            image.color = Color.white;
            image.raycastTarget = false;
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 18, 18);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = panel.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var title = Text(panel, "Title", art.Font, 22f, Color.white, TextAlignmentOptions.TopLeft);
            var subtitle = Text(panel, "Subtitle", art.Font, 15f, SubtitleColor, TextAlignmentOptions.TopLeft);
            var body = Text(panel, "Body", art.Font, 17f, BodyColor, TextAlignmentOptions.TopLeft);
            var footer = Text(panel, "Footer", art.Font, 16f, ValueColor, TextAlignmentOptions.TopLeft);

            var so = new SerializedObject(root.GetComponent<TooltipView>());
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("subtitleLabel").objectReferenceValue = subtitle;
            so.FindProperty("bodyLabel").objectReferenceValue = body;
            so.FindProperty("footerLabel").objectReferenceValue = footer;
            so.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
            return "툴팁";
        }

        // ── ⑤ 칸 원본: 아이콘 참조 · 툴팁 ──

        private static string SlotIcons(Transform canvas)
        {
            int wired = 0, tips = 0;
            var seen = new HashSet<HotbarSlotView>();
            foreach (var v in canvas.GetComponentsInChildren<HotbarView>(true))
                if (Template(v, "slotTemplate") is HotbarSlotView s && seen.Add(s))
                    wired += WireSlot(s, ref tips);
            foreach (var v in canvas.GetComponentsInChildren<SlotGridView>(true))
                if (Template(v, "slotTemplate") is HotbarSlotView s && seen.Add(s))
                    wired += WireSlot(s, ref tips);
            return $"칸 원본 아이콘 참조 {wired} · 칸 툴팁 {tips}";
        }

        private static int WireSlot(HotbarSlotView view, ref int tips)
        {
            var so = new SerializedObject(view);
            int wired = 0;
            var iconRt = view.transform.Find("Icon");
            if (iconRt != null && iconRt.TryGetComponent<Image>(out var image))
            {
                image.raycastTarget = false;
                image.preserveAspect = true;
                image.enabled = false; // 아이템이 들어오면 HotbarSlotView.SetIcon이 켠다
                so.FindProperty("icon").objectReferenceValue = image;
                EditorUtility.SetDirty(image);
                wired = 1;
            }
            // 버튼 칸(가방 · 창고 격자)만 툴팁 — HUD 핫바는 커서가 잠겨 있어 올릴 수 없다
            if (view.GetComponent<Button>() != null)
            {
                so.FindProperty("tooltip").objectReferenceValue = Trigger(view);
                tips++;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return wired;
        }

        // ── 상점 ──

        private static string Shop(Transform canvas, Art art, PlayerInventory inventory)
        {
            var screen = canvas.GetComponentInChildren<ShopScreen>(true);
            var panel = screen != null ? screen.transform.Find("Panel") as RectTransform : null;
            if (screen == null || panel == null || !(Template(screen, "entryTemplate") is HotbarSlotView entry))
                return "상점 화면 없음";

            const float rowHeight = 64f;
            const int visibleRows = 6;
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, 680f); // 590 → 680: 탭 + 6줄
            float width = panel.sizeDelta.x - EdgeX * 2f;
            var tab = TabBar(panel, screen, art, new Vector2(EdgeX, -82f), width);
            ScrollList(panel, screen, new Vector2(EdgeX, -82f - TabHeight - 8f), new Vector2(width, visibleRows * rowHeight + (visibleRows - 1) * RowGap));
            BuildRow(entry, art, rowHeight, badge: false, valueWidth: 150f, valueIcon: true);

            var so = new SerializedObject(screen);
            if (so.FindProperty("emptyLabel").objectReferenceValue == null && panel.Find("EmptyTab") is Transform empty && empty.TryGetComponent<TMP_Text>(out var emptyText))
                so.FindProperty("emptyLabel").objectReferenceValue = emptyText;
            so.FindProperty("inventory").objectReferenceValue = inventory;
            so.FindProperty("priceIcon").objectReferenceValue = art.Dollar;
            so.FindProperty("tabNormalColor").colorValue = Unavailable;
            so.FindProperty("tabSelectedColor").colorValue = Color.white;
            so.ApplyModifiedPropertiesWithoutUndo();
            Hint(panel, "줄을 누르면 산다 · 마우스를 올리면 설명 · B / Esc 닫기");
            return $"상점 줄 {rowHeight} px · 탭 {(tab != null ? "연결" : "없음")} · 달러 아이콘 {(art.Dollar != null ? 1 : 0)}";
        }

        // ── 스킬 ──

        private static string Skill(Transform canvas, Art art)
        {
            var screen = canvas.GetComponentInChildren<SkillScreen>(true);
            var panel = screen != null ? screen.transform.Find("Panel") as RectTransform : null;
            if (screen == null || panel == null || !(Template(screen, "entryTemplate") is HotbarSlotView entry))
                return "스킬 화면 없음";

            const float rowHeight = 56f;
            const int visibleRows = 5;
            float width = panel.sizeDelta.x - EdgeX * 2f;
            var tab = TabBar(panel, screen, art, new Vector2(EdgeX, -82f), width);
            ScrollList(panel, screen, new Vector2(EdgeX, -82f - TabHeight - 8f), new Vector2(width, visibleRows * rowHeight + (visibleRows - 1) * RowGap));
            BuildRow(entry, art, rowHeight, badge: true, valueWidth: 120f, valueIcon: true);

            var so = new SerializedObject(screen);
            so.FindProperty("costIcon").objectReferenceValue = art.SkillPoint;
            so.FindProperty("tabNormalColor").colorValue = Unavailable;
            so.FindProperty("tabSelectedColor").colorValue = Color.white;
            so.ApplyModifiedPropertiesWithoutUndo();
            Hint(panel, "줄을 누르면 배운다 · 마우스를 올리면 단계별 효과 · K / Esc 닫기");
            return $"스킬 줄 {rowHeight} px · 탭 {(tab != null ? "연결" : "없음")} · SP 아이콘 {(art.SkillPoint != null ? 1 : 0)}";
        }

        // ── 창고 ──

        private static string Storage(Transform canvas, Art art)
        {
            var screen = canvas.GetComponentInChildren<StorageScreen>(true);
            var panel = screen != null ? screen.transform.Find("Panel") as RectTransform : null;
            if (screen == null || panel == null || !(Template(screen, "entryTemplate") is HotbarSlotView entry))
                return "창고 화면 없음";

            const float rowHeight = 56f;
            const int visibleRows = 6;
            ScrollList(panel, screen, new Vector2(EdgeX, -86f), new Vector2(360f, visibleRows * rowHeight + (visibleRows - 1) * RowGap));
            BuildRow(entry, art, rowHeight, badge: false, valueWidth: 70f, valueIcon: false);
            Hint(panel, "창고 목록을 누르면 한 묶음 꺼내기 · 마우스를 올리면 설명 | Tab · Esc 닫기");
            return $"창고 줄 {rowHeight} px";
        }

        // ── ② 목록 줄 원본: [아이콘(또는 표시 글자)] 이름 / 둘째 줄 … 값 [값 아이콘] ──

        private static void BuildRow(HotbarSlotView view, Art art, float height, bool badge, float valueWidth, bool valueIcon)
        {
            var rt = (RectTransform)view.transform;
            var le = rt.GetComponent<LayoutElement>();
            if (le == null)
                le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            le.flexibleWidth = 1f;

            var so = new SerializedObject(view);
            float iconSize = height - 16f;
            float left = 12f + iconSize + 12f;
            float right = 14f;

            var icon = Child(rt, "Icon", typeof(Image));
            icon.SetSiblingIndex(0);
            icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
            icon.pivot = new Vector2(0f, 0.5f);
            icon.anchoredPosition = new Vector2(12f, 0f);
            icon.sizeDelta = new Vector2(iconSize, iconSize);
            var iconImage = icon.GetComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;
            iconImage.enabled = false;
            so.FindProperty("icon").objectReferenceValue = iconImage;

            // 아이콘 자리의 표시 글자 (스킬 "현재/최대") — 아이콘이 없는 줄에만
            if (badge)
            {
                var key = Text(rt, "Badge", art.Font, 20f, BadgeColor, TextAlignmentOptions.Center);
                key.rectTransform.anchorMin = key.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                key.rectTransform.pivot = new Vector2(0f, 0.5f);
                key.rectTransform.anchoredPosition = new Vector2(12f, 0f);
                key.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
                so.FindProperty("keyLabel").objectReferenceValue = key;
            }

            // 값 아이콘 · 값 (오른쪽부터)
            float valueX = right;
            if (valueIcon)
            {
                var vIcon = Child(rt, "ValueIcon", typeof(Image));
                vIcon.anchorMin = vIcon.anchorMax = new Vector2(1f, 0.5f);
                vIcon.pivot = new Vector2(1f, 0.5f);
                vIcon.anchoredPosition = new Vector2(-right, 0f);
                vIcon.sizeDelta = new Vector2(28f, 28f);
                var vImage = vIcon.GetComponent<Image>();
                vImage.raycastTarget = false;
                vImage.preserveAspect = true;
                vImage.enabled = false;
                so.FindProperty("valueIcon").objectReferenceValue = vImage;
                valueX += 28f + 8f;
            }
            else
                so.FindProperty("valueIcon").objectReferenceValue = null;
            var value = Text(rt, "Value", art.Font, 22f, ValueColor, TextAlignmentOptions.MidlineRight);
            value.rectTransform.anchorMin = value.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            value.rectTransform.pivot = new Vector2(1f, 0.5f);
            value.rectTransform.anchoredPosition = new Vector2(-valueX, 0f);
            value.rectTransform.sizeDelta = new Vector2(valueWidth, height);
            so.FindProperty("valueLabel").objectReferenceValue = value;
            float nameRight = valueX + valueWidth + 8f;

            // 이름: 아이콘 뒤부터 값 앞까지, 줄 높이 전체 (둘째 줄이 있으면 위쪽 정렬 — HotbarSlotView.SetDetail)
            var name = rt.Find("Name") as RectTransform;
            if (name != null && name.TryGetComponent<TMP_Text>(out var nameText))
            {
                name.anchorMin = Vector2.zero;
                name.anchorMax = Vector2.one;
                name.offsetMin = new Vector2(left, 6f);
                name.offsetMax = new Vector2(-nameRight, -7f);
                nameText.enableAutoSizing = false;
                nameText.fontSize = 22f;
                nameText.alignment = TextAlignmentOptions.MidlineLeft;
                nameText.textWrappingMode = TextWrappingModes.NoWrap;
                nameText.overflowMode = TextOverflowModes.Ellipsis;
                nameText.raycastTarget = false;
                EditorUtility.SetDirty(nameText);
            }

            // 둘째 줄 (아래 띠)
            var detail = Text(rt, "Detail", art.Font, 15f, DetailColor, TextAlignmentOptions.BottomLeft);
            detail.rectTransform.anchorMin = new Vector2(0f, 0f);
            detail.rectTransform.anchorMax = new Vector2(1f, 0f);
            detail.rectTransform.pivot = new Vector2(0.5f, 0f);
            detail.rectTransform.offsetMin = new Vector2(left, 7f);
            detail.rectTransform.offsetMax = new Vector2(-nameRight, 7f + 20f);
            detail.textWrappingMode = TextWrappingModes.NoWrap;
            detail.overflowMode = TextOverflowModes.Ellipsis;
            so.FindProperty("detailLabel").objectReferenceValue = detail;

            so.FindProperty("tooltip").objectReferenceValue = Trigger(view);
            so.ApplyModifiedPropertiesWithoutUndo();

            // 올림 · 누름 그림 (5-4a는 줄을 보통 그림으로만)
            var button = view.GetComponent<Button>();
            if (button != null && art.Hover != null)
            {
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState { highlightedSprite = art.Hover, selectedSprite = art.Hover, pressedSprite = art.Pressed, disabledSprite = art.Disabled };
                EditorUtility.SetDirty(button);
            }
            EditorUtility.SetDirty(rt);
        }

        // ── ③ 탭 줄: 제목 아래 가로 버튼 (이미 있으면 크기 · 참조만) ──

        private static HotbarSlotView TabBar(RectTransform panel, Object screen, Art art, Vector2 pos, float width)
        {
            var bar = panel.Find("TabBar") as RectTransform;
            if (bar == null)
            {
                bar = new GameObject("TabBar", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
                bar.SetParent(panel, false);
            }
            bar.anchorMin = bar.anchorMax = new Vector2(0f, 1f);
            bar.pivot = new Vector2(0f, 1f);
            bar.anchoredPosition = pos;
            bar.sizeDelta = new Vector2(width, TabHeight);
            var layout = bar.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var tab = bar.Find("TabTemplate") as RectTransform;
            if (tab == null)
            {
                tab = new GameObject("TabTemplate", typeof(RectTransform), typeof(Image), typeof(Button), typeof(HotbarSlotView), typeof(LayoutElement)).GetComponent<RectTransform>();
                tab.SetParent(bar, false);
            }
            var le = tab.GetComponent<LayoutElement>();
            if (le == null)
                le = tab.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 200f;
            le.preferredHeight = TabHeight;
            le.flexibleWidth = 0f;
            var image = tab.GetComponent<Image>();
            image.sprite = art.Normal;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 2f;
            image.color = Color.white;
            image.raycastTarget = true;
            var button = tab.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { highlightedSprite = art.Hover, selectedSprite = art.Hover, pressedSprite = art.Pressed, disabledSprite = art.Disabled };
            var name = Text(tab, "Name", art.Font, 20f, Color.white, TextAlignmentOptions.Center);
            name.rectTransform.anchorMin = Vector2.zero;
            name.rectTransform.anchorMax = Vector2.one;
            name.rectTransform.offsetMin = new Vector2(6f, 4f);
            name.rectTransform.offsetMax = new Vector2(-6f, -4f);
            name.enableAutoSizing = false;
            // 옛 탭 원본(상품 줄 복제)에 남은 아이콘 자리 · 둘째 줄은 치운다
            foreach (var extra in new[] { "Icon", "Detail", "Value", "ValueIcon", "Badge" })
                if (tab.Find(extra) is Transform t)
                    Object.DestroyImmediate(t.gameObject);

            var view = tab.GetComponent<HotbarSlotView>();
            var so = new SerializedObject(view);
            so.FindProperty("background").objectReferenceValue = image;
            so.FindProperty("nameLabel").objectReferenceValue = name;
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("icon").objectReferenceValue = null;
            so.FindProperty("detailLabel").objectReferenceValue = null;
            so.FindProperty("valueLabel").objectReferenceValue = null;
            so.FindProperty("valueIcon").objectReferenceValue = null;
            so.FindProperty("tooltip").objectReferenceValue = null;
            so.FindProperty("normalSprite").objectReferenceValue = art.Normal;
            so.FindProperty("selectedSprite").objectReferenceValue = art.Hover;
            so.ApplyModifiedPropertiesWithoutUndo();
            tab.gameObject.SetActive(false);

            var screenSo = new SerializedObject(screen);
            screenSo.FindProperty("tabTemplate").objectReferenceValue = view;
            screenSo.FindProperty("tabParent").objectReferenceValue = bar;
            screenSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bar);
            return view;
        }

        // ── ④ 목록 스크롤: EntryList(VerticalLayoutGroup)를 EntryScroll(ScrollRect + RectMask2D) 안으로 (이미 되어 있으면 자리 · 크기만) ──

        private static RectTransform ScrollList(RectTransform panel, Object screen, Vector2 pos, Vector2 size)
        {
            var scroll = panel.Find("EntryScroll") as RectTransform;
            var list = scroll != null ? scroll.Find("EntryList") as RectTransform : panel.Find("EntryList") as RectTransform;
            if (list == null)
                return null;
            if (scroll == null)
            {
                scroll = new GameObject("EntryScroll", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect)).GetComponent<RectTransform>();
                scroll.SetParent(panel, false);
                scroll.SetSiblingIndex(list.GetSiblingIndex());
                list.SetParent(scroll, false);
            }
            scroll.anchorMin = scroll.anchorMax = new Vector2(0f, 1f);
            scroll.pivot = new Vector2(0f, 1f);
            scroll.anchoredPosition = pos;
            scroll.sizeDelta = size;

            list.anchorMin = new Vector2(0f, 1f);
            list.anchorMax = new Vector2(1f, 1f);
            list.pivot = new Vector2(0.5f, 1f);
            list.anchoredPosition = Vector2.zero;
            list.sizeDelta = new Vector2(0f, size.y);
            var layout = list.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
                layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = RowGap;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = list.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = list.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sr = scroll.GetComponent<ScrollRect>();
            sr.content = list;
            sr.viewport = scroll;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 40f;

            var so = new SerializedObject(screen);
            so.FindProperty("entryParent").objectReferenceValue = list;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(scroll);
            EditorUtility.SetDirty(list);
            return list;
        }

        // ── 도우미 ──

        private static Sprite Load(string folder, string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/{name}.png");

        private static Object Template(Object owner, string property) => new SerializedObject(owner).FindProperty(property)?.objectReferenceValue;

        private static RectTransform Child(Transform parent, string name, params System.Type[] components)
        {
            var rt = parent.Find(name) as RectTransform;
            if (rt == null)
            {
                var types = new List<System.Type> { typeof(RectTransform) };
                types.AddRange(components);
                rt = new GameObject(name, types.ToArray()).GetComponent<RectTransform>();
                rt.SetParent(parent, false);
            }
            foreach (var type in components)
                if (rt.GetComponent(type) == null)
                    rt.gameObject.AddComponent(type);
            return rt;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
        {
            var rt = Child(parent, name, typeof(TextMeshProUGUI));
            var text = rt.GetComponent<TextMeshProUGUI>();
            if (font != null)
                text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            if (string.IsNullOrEmpty(text.text))
                text.text = string.Empty;
            EditorUtility.SetDirty(text);
            return text;
        }

        private static TooltipTrigger Trigger(Component owner)
        {
            var trigger = owner.GetComponent<TooltipTrigger>();
            if (trigger == null)
                trigger = owner.gameObject.AddComponent<TooltipTrigger>();
            return trigger;
        }

        private static void Hint(RectTransform panel, string text)
        {
            if (panel.Find("Hint") is Transform hint && hint.TryGetComponent<TMP_Text>(out var label))
            {
                label.text = text;
                EditorUtility.SetDirty(label);
            }
        }
    }
}
#endif
