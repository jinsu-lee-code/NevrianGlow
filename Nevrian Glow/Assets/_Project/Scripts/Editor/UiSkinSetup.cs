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
    /// 5-4a UI 스킨 1차 (2026-10-23): 아트 패널 · 버튼 · 칸 · 편지지 · 경험치 막대 그림(요청서 G, `Assets/Art/UI/Panels`)을
    /// Core 씬 UICanvas의 그레이박스 Image에 입힌다. 그림은 색이 칠해진 9-slice라 Image 색은 흰색(상태 구분만 회색 틴트).
    /// 배치 · 크기 · 글자는 그대로(경험치 막대 높이만 그림에 맞춤). 여러 번 실행해도 결과가 같다.
    /// </summary>
    public static class UiSkinSetup
    {
        private const string CorePath = "Assets/_Project/Scenes/Core.unity";
        private const string Panels = "Assets/Art/UI/Panels";

        // 그레이박스 창 패널 (UICanvas 아래 경로) — 화면 전체를 어둡게 덮는 판(TitleScreen/Panel 등)은 빼고
        private static readonly string[] PanelPaths =
        {
            "HUD", "InteractionPrompt/Panel", "TidyHud/Panel",
            "InventoryScreen/Panel", "StorageScreen/Panel", "ShopScreen/Panel", "SkillScreen/Panel",
            "SettlementScreen/Panel", "DemoEndScreen/Panel",
            "TitleScreen/Panel/Settings", "TitleScreen/Panel/Confirm", "PauseScreen/Panel/Settings", "PauseScreen/Panel/Confirm",
        };
        // 작은 패널은 모서리 장식을 반으로 (테두리 32 px이 높이를 넘지 않게)
        private static readonly HashSet<string> SmallPanels = new HashSet<string> { "HUD", "InteractionPrompt/Panel", "TidyHud/Panel" };

        private const float RowBorder = 2f; // 목록 줄은 높이가 낮아(줄 높이 < 그림 테두리 40 px) 테두리를 반으로
        private static readonly Color Unavailable = new Color(0.6f, 0.6f, 0.66f, 1f);
        private static readonly Color Maxed = new Color(0.8f, 0.72f, 1f, 1f);
        private static readonly Color LetterInk = new Color(0.07f, 0.14f, 0.2f, 1f);

        [MenuItem("Glow Farm/Setup/Apply UI Skin (5-4a)")]
        private static void ApplyMenu()
        {
            var panel = Load("ui_panel_base");
            var normal = Load("ui_button_normal");
            var hover = Load("ui_button_hover");
            var pressed = Load("ui_button_pressed");
            var disabled = Load("ui_button_disabled");
            var slot = Load("ui_slot_inventory");
            var slotSelected = Load("ui_slot_inventory_selected");
            var letter = Load("ui_letter_bg");
            var barBg = Load("ui_bar_xp_bg");
            var barFill = Load("ui_bar_xp_fill");
            if (panel == null || normal == null || slot == null)
            {
                Debug.LogError($"[Setup] UI 스킨: 아트 그림이 없음 ({Panels})");
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
                Debug.LogError("[Setup] UI 스킨: Core 씬에 UICanvas가 없음");
                return;
            }

            int panels = 0, buttons = 0, slots = 0, rows = 0;
            // 1) 창 패널
            foreach (var path in PanelPaths)
            {
                var t = canvas.Find(path);
                if (t == null || !t.TryGetComponent<Image>(out var image))
                    continue;
                Skin(image, panel, Color.white, SmallPanels.Contains(path) ? 2f : 1f);
                panels++;
            }

            // 2) 아이템 칸 원본 (핫바 · 가방 · 핫바 격자) — 선택되면 밝은 테두리 그림
            var slotViews = new HashSet<HotbarSlotView>();
            foreach (var v in canvas.GetComponentsInChildren<HotbarView>(true))
            {
                SetColor(v, "normalColor", Color.white);
                SetColor(v, "selectedColor", Color.white);
                if (Template(v, "slotTemplate") is HotbarSlotView s && slotViews.Add(s))
                    slots += SkinSlot(s, slot, slotSelected);
            }
            foreach (var v in canvas.GetComponentsInChildren<SlotGridView>(true))
            {
                SetColor(v, "slotColor", Color.white);
                if (Template(v, "slotTemplate") is HotbarSlotView s && slotViews.Add(s))
                    slots += SkinSlot(s, slot, slotSelected);
            }

            // 3) 목록 줄 (상점 · 창고 · 스킬) · 상점 탭 — 버튼 그림, 살 수 없음 등은 회색 틴트
            foreach (var v in canvas.GetComponentsInChildren<ShopScreen>(true))
            {
                SetColor(v, "availableColor", Color.white);
                SetColor(v, "unavailableColor", Unavailable);
                SetColor(v, "tabNormalColor", Unavailable);
                SetColor(v, "tabSelectedColor", Color.white);
                if (Template(v, "entryTemplate") is HotbarSlotView e && slotViews.Add(e))
                    rows += SkinSlot(e, normal, normal, RowBorder);
                if (Template(v, "tabTemplate") is HotbarSlotView tab && slotViews.Add(tab))
                    rows += SkinSlot(tab, normal, hover, RowBorder);
            }
            foreach (var v in canvas.GetComponentsInChildren<SkillScreen>(true))
            {
                SetColor(v, "availableColor", Color.white);
                SetColor(v, "unavailableColor", Unavailable);
                SetColor(v, "maxedColor", Maxed);
                if (Template(v, "entryTemplate") is HotbarSlotView e && slotViews.Add(e))
                    rows += SkinSlot(e, normal, normal, RowBorder);
            }
            foreach (var v in canvas.GetComponentsInChildren<StorageScreen>(true))
            {
                SetColor(v, "entryColor", Color.white);
                if (Template(v, "entryTemplate") is HotbarSlotView e && slotViews.Add(e))
                    rows += SkinSlot(e, normal, normal, RowBorder);
            }

            // 4) 버튼 (칸 · 목록 줄 제외) — 그림 바꾸기(보통 · 올림 · 누름 · 못 누름)
            foreach (var button in canvas.GetComponentsInChildren<Button>(true))
            {
                if (button.GetComponent<HotbarSlotView>() != null || !(button.targetGraphic is Image image))
                    continue;
                // 아니오 · 취소 같은 보조 버튼은 원래 회색이었다 → 그림은 같고 조금 어둡게
                // (다시 실행해도 같게 — 이미 회색 틴트면 보조 버튼)
                bool secondary = (image.color.r < 0.25f && image.color.g < 0.25f && Mathf.Abs(image.color.r - image.color.g) < 0.05f)
                    || ((Vector4)(image.color - Unavailable)).sqrMagnitude < 1e-4f;
                Skin(image, normal, secondary ? Unavailable : Color.white, 1f);
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState { highlightedSprite = hover, selectedSprite = hover, pressedSprite = pressed, disabledSprite = disabled };
                EditorUtility.SetDirty(button);
                buttons++;
            }

            // 5) 오프닝 편지지 — 밝은 종이라 글자를 어두운 잉크색으로 (버튼 글자는 그대로)
            string letterNote = "편지지 없음";
            var paper = canvas.Find("OpeningLetterScreen/Panel/Paper");
            if (paper != null && letter != null && paper.TryGetComponent<Image>(out var paperImage))
            {
                Skin(paperImage, letter, Color.white, 1.4f);
                foreach (var text in paper.GetComponentsInChildren<TMP_Text>(true))
                    if (text.GetComponentInParent<Button>() == null)
                    {
                        text.color = LetterInk;
                        EditorUtility.SetDirty(text);
                    }
                // 그림 위쪽 투명 여백 · 모서리 장식을 피해 글자를 안쪽으로 (종이 960 × 700 기준, 위에서부터)
                Place(paper.Find("Title") as RectTransform, -84f, -136f, 340f);
                Place(paper.Find("Body") as RectTransform, -150f, -600f, 330f);
                letterNote = "편지지";
            }

            // 6) HUD 경험치 막대 — 바탕(가로 9-slice) + 채움(Filled 가로, HudView가 fillAmount)
            string barNote = "경험치 막대 없음";
            var bar = canvas.Find("HUD/ExpBar") as RectTransform;
            var fill = bar != null ? bar.Find("Fill") as RectTransform : null;
            if (bar != null && fill != null && barBg != null && barFill != null)
            {
                // 막대가 패널 아래 테두리(16 px)에 묻히지 않게 HUD를 조금 키운다
                var hud = bar.parent as RectTransform;
                if (hud != null && hud.sizeDelta.y < 142f)
                    hud.sizeDelta = new Vector2(hud.sizeDelta.x, 142f);
                bar.sizeDelta = new Vector2(bar.sizeDelta.x, 16f);
                Skin(bar.GetComponent<Image>(), barBg, Color.white, 2.4f); // 39 px 그림 → 16 px, 끝 장식 30 px도 같은 비율로
                var fillImage = fill.GetComponent<Image>();
                fillImage.sprite = barFill;
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                fillImage.color = Color.white;
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = Vector2.one;
                fill.offsetMin = Vector2.zero;
                fill.offsetMax = Vector2.zero;
                EditorUtility.SetDirty(fillImage);
                barNote = "경험치 막대";
            }

            // 7) 타이틀 로고 (사용자 전달 10/23 — 아트 브랜치 납품 전이라 개발 폴더 사본) — 글자 제목은 숨기고 부제는 로고 아래로
            string logoNote = "로고 없음";
            var titlePanel = canvas.Find("TitleScreen/Panel");
            var logoSprite = LoadLogo();
            if (titlePanel != null && logoSprite != null)
            {
                var logo = titlePanel.Find("Logo") as RectTransform;
                if (logo == null)
                {
                    logo = new GameObject("Logo", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                    logo.SetParent(titlePanel, false);
                }
                logo.anchorMin = logo.anchorMax = new Vector2(0.5f, 1f);
                logo.pivot = new Vector2(0.5f, 1f);
                logo.anchoredPosition = new Vector2(0f, -20f);
                logo.sizeDelta = new Vector2(900f, 450f); // 그림 1024 × 512 비율 — 위아래 투명 여백 포함
                var image = logo.GetComponent<Image>();
                image.sprite = logoSprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.color = Color.white;
                var gameTitle = titlePanel.Find("GameTitle");
                if (gameTitle != null)
                    gameTitle.gameObject.SetActive(false);
                if (titlePanel.Find("Subtitle") is RectTransform sub)
                    sub.anchoredPosition = new Vector2(0f, -400f);
                EditorUtility.SetDirty(image);

                // 왼쪽 달 형광 (사용자 10/23): 로고 뒤 큰 후광 + 앞의 옅은 빛, 숨쉬듯 — 달 중심 = 그림 (195, 245) / 1024 × 512
                var glow = LoadSprite(GlowDev);
                if (glow != null)
                {
                    Vector2 moon = new Vector2((195f / 1024f - 0.5f) * 900f, -(245f / 512f) * 450f); // 로고 위 가운데 기준
                    MoonGlow(titlePanel, logo, "MoonHalo", glow, moon, 340f, new Color(0.55f, 0.85f, 1f), 0.25f, 0.6f, behind: true);
                    MoonGlow(titlePanel, logo, "MoonShine", glow, moon, 190f, new Color(0.75f, 0.95f, 1f), 0.04f, 0.18f, behind: false);
                }
                logoNote = "타이틀 로고 · 달 형광" + LogoShine(titlePanel, logo, logoSprite) + LogoStars(titlePanel, logo);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
            Debug.Log($"[Setup] UI 스킨 5-4a: 패널 {panels} · 버튼 {buttons} · 아이템 칸 원본 {slots} · 목록 줄/탭 원본 {rows} · {letterNote} · {barNote} · {logoNote}");
        }

        private static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Panels}/{name}.png");

        // 로고: 아트 폴더에 납품되면 그것을, 없으면 개발 폴더 사본. 가져오기 설정을 UI 그림으로 (Sprite · 밉맵 끔 · 압축 없음)
        private const string LogoArt = "Assets/Art/UI/ui_logo.png";
        private const string LogoDev = "Assets/_Project/Art/UI/ui_logo.png";

        private const string GlowDev = "Assets/_Project/Art/UI/ui_glow_soft.png"; // 개발이 만든 부드러운 원형 빛 (흰색 · 알파)

        // 로고와 같은 부모에 빛 그림 — behind면 로고 바로 뒤, 아니면 바로 앞. 위치는 로고 위 가운데 기준
        private static void MoonGlow(Transform parent, RectTransform logo, string name, Sprite sprite, Vector2 center, float size, Color color, float min, float max, bool behind)
        {
            var rt = parent.Find(name) as RectTransform;
            if (rt == null)
            {
                rt = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(UiGlowPulse)).GetComponent<RectTransform>();
                rt.SetParent(parent, false);
            }
            rt.SetSiblingIndex(behind ? logo.GetSiblingIndex() : logo.GetSiblingIndex() + 1);
            if (behind && rt.GetSiblingIndex() > logo.GetSiblingIndex())
                logo.SetSiblingIndex(rt.GetSiblingIndex());
            rt.anchorMin = rt.anchorMax = logo.anchorMin;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = logo.anchoredPosition + center;
            rt.sizeDelta = new Vector2(size, size);
            var image = rt.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.color = new Color(color.r, color.g, color.b, max);
            var so = new SerializedObject(rt.GetComponent<UiGlowPulse>());
            so.FindProperty("minAlpha").floatValue = min;
            so.FindProperty("maxAlpha").floatValue = max;
            so.FindProperty("period").floatValue = 3.5f;
            so.FindProperty("phase").floatValue = behind ? 0f : 0.15f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(image);
        }

        // ── 로고 빛 쓸기 · 별 반짝임 (사용자 10/23 — 제안 3 · 4번) ──

        private const string ShineShader = "NevrianGlowFarm/UI/Shine";
        private const string ShineMaterialPath = "Assets/_Project/Art/UI/mat_ui_logo_shine.mat";
        private const string StarGlintDev = "Assets/_Project/Art/UI/ui_star_glint.png";

        // 로고 그림 속 네 꼭지 별 (그림 1024 × 512 기준 중심 · 크기 px, 색)
        private static readonly (Vector2 Pos, float Size, Color Color)[] LogoStarSpots =
        {
            (new Vector2(78f, 228f), 70f, new Color(0.85f, 0.65f, 1f)),   // 왼쪽 큰 보라
            (new Vector2(227f, 220f), 56f, new Color(0.65f, 0.95f, 1f)),  // 달 옆 청록
            (new Vector2(605f, 197f), 64f, new Color(0.85f, 0.65f, 1f)),  // 위 가운데 보라
            (new Vector2(940f, 268f), 76f, new Color(0.85f, 0.65f, 1f)),  // 오른쪽 큰 보라
            (new Vector2(300f, 255f), 40f, new Color(1f, 0.75f, 1f)),     // 분홍 작은 별
            (new Vector2(382f, 277f), 40f, new Color(0.65f, 0.95f, 1f)),
            (new Vector2(733f, 312f), 44f, new Color(0.65f, 0.95f, 1f)),
            (new Vector2(540f, 341f), 44f, new Color(0.65f, 0.95f, 1f)),
            (new Vector2(313f, 327f), 40f, new Color(1f, 0.75f, 1f)),
            (new Vector2(835f, 330f), 40f, new Color(0.85f, 0.65f, 1f)),
        };

        // 로고와 같은 자리 · 크기의 층 (위 가운데 앵커)
        private static RectTransform LogoLayer(Transform parent, RectTransform logo, string name, int siblingAfterLogo, params System.Type[] components)
        {
            var rt = parent.Find(name) as RectTransform;
            if (rt == null)
            {
                var types = new System.Collections.Generic.List<System.Type> { typeof(RectTransform) };
                types.AddRange(components);
                rt = new GameObject(name, types.ToArray()).GetComponent<RectTransform>();
                rt.SetParent(parent, false);
            }
            rt.SetSiblingIndex(logo.GetSiblingIndex() + siblingAfterLogo);
            rt.anchorMin = logo.anchorMin;
            rt.anchorMax = logo.anchorMax;
            rt.pivot = logo.pivot;
            rt.anchoredPosition = logo.anchoredPosition;
            rt.sizeDelta = logo.sizeDelta;
            return rt;
        }

        private static string LogoShine(Transform parent, RectTransform logo, Sprite logoSprite)
        {
            var shader = Shader.Find(ShineShader);
            if (shader == null)
                return " · 빛 쓸기 셰이더 없음";
            var material = AssetDatabase.LoadAssetAtPath<Material>(ShineMaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, ShineMaterialPath);
            }
            material.shader = shader;
            // 글자가 이미 밝아 약하면 안 보임 — 캡처로 맞춘 값 (10/23)
            material.SetColor("_ShineColor", new Color(0.85f, 0.97f, 1f, 1f));
            material.SetFloat("_ShineWidth", 0.09f);
            material.SetFloat("_ShineSlant", 0.35f);
            material.SetFloat("_Intensity", 1.8f);
            EditorUtility.SetDirty(material);
            var layer = LogoLayer(parent, logo, "LogoShine", 2, typeof(Image), typeof(UiShineSweep)); // 로고 · 앞 달빛 다음
            var image = layer.GetComponent<Image>();
            image.sprite = logoSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            image.material = material;
            EditorUtility.SetDirty(image);
            return " · 빛 쓸기";
        }

        private static string LogoStars(Transform parent, RectTransform logo)
        {
            var glint = LoadSprite(StarGlintDev);
            if (glint == null)
                return " · 별 그림 없음";
            var layer = LogoLayer(parent, logo, "LogoStars", 3);
            float scale = logo.sizeDelta.x / 1024f;
            for (int i = 0; i < LogoStarSpots.Length; i++)
            {
                var (pos, size, color) = LogoStarSpots[i];
                string name = $"Star{i + 1:00}";
                var rt = layer.Find(name) as RectTransform;
                if (rt == null)
                {
                    rt = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(UiTwinkle)).GetComponent<RectTransform>();
                    rt.SetParent(layer, false);
                }
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2((pos.x - 512f) * scale, -pos.y * scale);
                rt.sizeDelta = Vector2.one * size * scale * 1.6f; // 빛줄기가 별 꼭지보다 조금 길게
                rt.localScale = Vector3.one;
                var image = rt.GetComponent<Image>();
                image.sprite = glint;
                image.raycastTarget = false;
                image.color = new Color(color.r, color.g, color.b, 0f);
                EditorUtility.SetDirty(image);
            }
            return $" · 별 반짝임 {LogoStarSpots.Length}";
        }

        private static Sprite LoadLogo()
        {
            string path = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoArt) != null ? LogoArt : LogoDev;
            return LoadSprite(path);
        }

        private static Sprite LoadSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && path.StartsWith("Assets/_Project")
                && (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void Skin(Image image, Sprite sprite, Color color, float borderScale)
        {
            if (image == null || sprite == null)
                return;
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = borderScale;
            image.color = color;
            EditorUtility.SetDirty(image);
        }

        // 위 가운데 앵커 글자 상자: 위 · 아래 끝(y, 위에서 −) · 가운데에서 좌우 반폭
        private static void Place(RectTransform rt, float top, float bottom, float halfWidth)
        {
            if (rt == null)
                return;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(-halfWidth, bottom);
            rt.offsetMax = new Vector2(halfWidth, top);
            EditorUtility.SetDirty(rt);
        }

        private static int SkinSlot(HotbarSlotView view, Sprite normal, Sprite selected, float borderScale = 1f)
        {
            var so = new SerializedObject(view);
            so.FindProperty("normalSprite").objectReferenceValue = normal;
            so.FindProperty("selectedSprite").objectReferenceValue = selected;
            var background = so.FindProperty("background").objectReferenceValue as Image;
            so.ApplyModifiedPropertiesWithoutUndo();
            Skin(background, normal, Color.white, borderScale);
            return 1;
        }

        private static Object Template(Object owner, string property) => new SerializedObject(owner).FindProperty(property)?.objectReferenceValue;

        private static void SetColor(Object owner, string property, Color color)
        {
            var so = new SerializedObject(owner);
            var p = so.FindProperty(property);
            if (p == null)
                return;
            p.colorValue = color;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
