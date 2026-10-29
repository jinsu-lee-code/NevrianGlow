using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NevrianGlowFarm.UI
{
    /// <summary>툴팁 한 장의 내용: 제목 · 작은 분류 · 본문 · 아래 줄(가격 · 비용 · 보유). 비어 있는 줄은 숨긴다.</summary>
    public readonly struct TooltipContent
    {
        public string Title { get; }
        public string Subtitle { get; }
        public string Body { get; }
        public string Footer { get; }

        public TooltipContent(string title, string subtitle = null, string body = null, string footer = null)
        {
            Title = title;
            Subtitle = subtitle;
            Body = body;
            Footer = footer;
        }

        public bool IsEmpty => string.IsNullOrEmpty(Title);
    }

    /// <summary>
    /// 마우스를 올린 칸 · 줄의 설명 창 (5-4c, 스타듀밸리식). UICanvas에 하나만 있고 TooltipTrigger가 보여 주고 숨긴다.
    /// 마우스를 따라다니며 화면 밖으로 나가면 반대쪽에 붙는다. 글자가 없는 줄은 숨겨 높이를 줄인다 (VerticalLayoutGroup + ContentSizeFitter).
    /// </summary>
    public class TooltipView : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("창 패널 (이 오브젝트의 자식) — 보이고 숨긴다")]
        [SerializeField] private RectTransform panel;
        [Tooltip("제목")]
        [SerializeField] private TMP_Text titleLabel;
        [Tooltip("작은 분류 글자 (씨앗 · 도구 · 농장 운영) — 비우면 숨김")]
        [SerializeField] private TMP_Text subtitleLabel;
        [Tooltip("본문 (설명 · 수치)")]
        [SerializeField] private TMP_Text bodyLabel;
        [Tooltip("아래 줄 (가격 · 비용 · 보유)")]
        [SerializeField] private TMP_Text footerLabel;

        [Header("조정값")]
        [Tooltip("마우스에서 창을 띄우는 거리 (px, 오른쪽 아래 기준)")]
        [SerializeField] private Vector2 offset = new Vector2(18f, -18f);

        private Canvas canvas;
        private RectTransform canvasRect;
        private object owner;

        public static TooltipView Instance { get; private set; }

        public bool IsShown => panel != null && panel.gameObject.activeSelf;

        private void Awake()
        {
            if (panel == null || titleLabel == null || bodyLabel == null)
            {
                Debug.LogError("[TooltipView] 참조가 비어 있음 - 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            Instance = this;
            canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                canvas = canvas.rootCanvas;
            canvasRect = canvas != null ? (RectTransform)canvas.transform : null;
            panel.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>내용을 채워 보인다. 제목이 비면 숨긴다. owner는 누가 띄웠는지 (그 주인만 숨길 수 있다).</summary>
        public void Show(TooltipContent content, object by)
        {
            if (content.IsEmpty)
            {
                Hide(by);
                return;
            }
            owner = by;
            titleLabel.text = content.Title;
            SetLine(subtitleLabel, content.Subtitle);
            SetLine(bodyLabel, content.Body);
            SetLine(footerLabel, content.Footer);
            panel.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            Follow();
        }

        /// <summary>주인이 같을 때만 숨긴다 (다른 줄로 옮겨 간 뒤 늦게 온 숨김이 새 툴팁을 지우지 않게). null이면 무조건.</summary>
        public void Hide(object by)
        {
            if (by != null && owner != null && !ReferenceEquals(owner, by))
                return;
            owner = null;
            if (panel != null)
                panel.gameObject.SetActive(false);
        }

        private static void SetLine(TMP_Text label, string text)
        {
            if (label == null)
                return;
            bool has = !string.IsNullOrEmpty(text);
            label.gameObject.SetActive(has);
            label.text = has ? text : string.Empty;
        }

        private void LateUpdate()
        {
            if (IsShown)
                Follow();
        }

        // 마우스 위치를 캔버스 좌표로 바꿔 창의 모서리를 둔다. 오른쪽 · 아래가 모자라면 반대쪽으로
        private void Follow()
        {
            if (canvasRect == null || Mouse.current == null)
                return;
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Mouse.current.position.ReadValue(), cam, out var local))
                return;
            var area = canvasRect.rect;
            var size = panel.rect.size;
            var pivot = new Vector2(0f, 1f);
            var shift = offset;
            if (local.x + offset.x + size.x > area.xMax)
            {
                pivot.x = 1f;
                shift.x = -offset.x;
            }
            if (local.y + offset.y - size.y < area.yMin)
            {
                pivot.y = 0f;
                shift.y = -offset.y;
            }
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = pivot;
            panel.anchoredPosition = local + shift;
        }
    }
}
