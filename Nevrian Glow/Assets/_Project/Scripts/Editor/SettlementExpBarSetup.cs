#if UNITY_EDITOR
using NevrianGlowFarm.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NevrianGlowFarm.Editor
{
    /// <summary>
    /// 정산 화면 경험치 막대 (기획 10/30 "정산에 경험치 바") — Core UICanvas/SettlementScreen/Panel에 막대 · 글자를 만들고 연결한다.
    /// 본문 아래 '다음 날' 버튼 위 자리, 본문 높이를 그만큼 줄인다. 여러 번 실행해도 결과가 같다. 막대 그림은 로딩 화면과 같은 아트 XP 막대.
    /// </summary>
    public static class SettlementExpBarSetup
    {
        private const string FontPath = "Assets/_Project/Art/Fonts/font_maplestory_bold_sdf.asset";
        private const string BarBackPath = "Assets/Art/UI/Panels/ui_bar_xp_bg.png";
        private const string BarFillPath = "Assets/Art/UI/Panels/ui_bar_xp_fill.png";
        private const float Left = 44f, Width = 672f;
        private const float LabelY = 152f, LabelHeight = 24f;   // 패널 아래에서 (px)
        private const float BarY = 134f, BarHeight = 14f;
        private const float BodyTop = 90f, BodyHeight = 280f;    // 본문 위 여백 · 높이 (막대 자리만큼 320 → 280)

        [MenuItem("Glow Farm/Setup/Add Settlement Exp Bar")]
        private static void AddMenu()
        {
            var screen = Object.FindFirstObjectByType<SettlementScreen>(FindObjectsInactive.Include);
            var panel = screen != null ? screen.transform.Find("Panel") as RectTransform : null;
            if (panel == null)
            {
                Debug.LogError("[Setup] Core 씬의 SettlementScreen/Panel을 찾지 못함 - Core 씬을 연다");
                return;
            }
            if (panel.Find("Body") is RectTransform body)
            {
                body.sizeDelta = new Vector2(body.sizeDelta.x, BodyHeight);
                body.anchoredPosition = new Vector2(body.anchoredPosition.x, -BodyTop);
                EditorUtility.SetDirty(body);
            }

            var label = Child(panel, "ExpLabel");
            label.anchorMin = label.anchorMax = Vector2.zero;
            label.pivot = Vector2.zero;
            label.anchoredPosition = new Vector2(Left, LabelY);
            label.sizeDelta = new Vector2(Width, LabelHeight);
            if (!label.TryGetComponent<TextMeshProUGUI>(out var text))
                text = label.gameObject.AddComponent<TextMeshProUGUI>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null)
                text.font = font;
            text.fontSize = 18f;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.color = new Color(0.85f, 0.95f, 1f, 1f);
            text.text = "Lv 1   경험치 0 / 200";
            text.raycastTarget = false;

            var bar = Child(panel, "ExpBar");
            bar.anchorMin = bar.anchorMax = Vector2.zero;
            bar.pivot = Vector2.zero;
            bar.anchoredPosition = new Vector2(Left, BarY);
            bar.sizeDelta = new Vector2(Width, BarHeight);
            if (!bar.TryGetComponent<Image>(out var back))
                back = bar.gameObject.AddComponent<Image>();
            back.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BarBackPath);
            back.type = back.sprite != null && back.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            back.color = back.sprite != null ? Color.white : new Color(0.05f, 0.12f, 0.2f, 0.9f);
            back.raycastTarget = false;

            var fillRt = Child(bar, "Fill");
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(3f, 3f);
            fillRt.offsetMax = new Vector2(-3f, -3f);
            if (!fillRt.TryGetComponent<Image>(out var fill))
                fill = fillRt.gameObject.AddComponent<Image>();
            fill.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BarFillPath);
            fill.color = fill.sprite != null ? Color.white : new Color(0.38f, 0.95f, 0.88f, 1f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0.4f;
            fill.raycastTarget = false;

            var so = new SerializedObject(screen);
            so.FindProperty("expFill").objectReferenceValue = fill;
            so.FindProperty("expLabel").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(screen.gameObject.scene);
            Debug.Log("[Setup] 정산 경험치 막대: 본문 높이 " + BodyHeight + " · 막대 · 글자 연결");
        }

        private static RectTransform Child(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t == null)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.layer = parent.gameObject.layer;
                go.transform.SetParent(parent, false);
                t = go.transform;
            }
            return (RectTransform)t;
        }
    }
}
#endif
