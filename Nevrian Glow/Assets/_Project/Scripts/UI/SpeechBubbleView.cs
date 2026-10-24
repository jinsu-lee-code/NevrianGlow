using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 말풍선 (TASKS 5-7, 기획 튜토리얼 D1-15 · D2-10 손님 대사): 세상의 대상(손님 머리) 위에 화면 글자 상자를 띄우고 잠깐 뒤 사라진다.
    /// UICanvas에 하나(Core 씬, Setup 메뉴가 만든다) — 풍선 원본을 복제해 쓰고, 대상이 사라지거나 카메라 뒤면 숨긴다.
    /// 누가 말할지는 부르는 쪽(CustomerAI)이 정한다. 모양은 아트 패널(ui_panel_base).
    /// </summary>
    public class SpeechBubbleView : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("풍선 원본 (비활성 자식: Panel 이미지 + Label 글자) — 필요한 만큼 복제")]
        [SerializeField] private RectTransform bubbleTemplate;

        [Header("조정값")]
        [Tooltip("나타나고 사라지는 시간 (초)")]
        [SerializeField, Min(0f)] private float fadeTime = 0.2f;
        [Tooltip("대상 위로 띄우는 화면 간격 (px)")]
        [SerializeField] private float screenOffsetY = 24f;

        private sealed class Bubble
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public TMP_Text Label;
            public Transform Target;
            public float Height;
            public float Duration;
            public float Elapsed;
            public bool Active;
        }

        private static SpeechBubbleView instance;
        private readonly List<Bubble> bubbles = new List<Bubble>();
        private RectTransform canvasRect;
        private Canvas canvas;

        private void Awake()
        {
            if (bubbleTemplate == null)
            {
                Debug.LogError("[SpeechBubbleView] 참조가 비어 있음 - 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            instance = this;
            bubbleTemplate.gameObject.SetActive(false);
            canvas = GetComponentInParent<Canvas>();
            canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        /// <summary>대상의 머리(대상 위치 + height) 위에 글을 띄운다. 같은 대상이 말하고 있으면 글만 바꾼다. 말풍선이 없거나 글이 비면 아무 일도 없다.</summary>
        public static void Say(Transform target, float height, string text, float duration)
        {
            if (instance == null || !instance.enabled || target == null || string.IsNullOrWhiteSpace(text))
                return;
            instance.Show(target, height, text, duration);
        }

        private void Show(Transform target, float height, string text, float duration)
        {
            Bubble b = null;
            foreach (var x in bubbles)
                if (x.Active && x.Target == target) { b = x; break; }
            if (b == null)
                foreach (var x in bubbles)
                    if (!x.Active) { b = x; break; }
            if (b == null)
            {
                var rect = Instantiate(bubbleTemplate, bubbleTemplate.parent);
                rect.name = $"Bubble{bubbles.Count + 1}";
                var group = rect.GetComponent<CanvasGroup>();
                if (group == null)
                    group = rect.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                b = new Bubble { Rect = rect, Group = group, Label = rect.GetComponentInChildren<TMP_Text>(true) };
                bubbles.Add(b);
            }
            b.Target = target;
            b.Height = height;
            b.Duration = Mathf.Max(0.1f, duration);
            b.Elapsed = 0f;
            b.Active = true;
            if (b.Label != null)
                b.Label.text = text;
            b.Group.alpha = 0f;
            b.Rect.gameObject.SetActive(true);
            Place(b);
        }

        private void LateUpdate()
        {
            foreach (var b in bubbles)
            {
                if (!b.Active)
                    continue;
                b.Elapsed += Time.deltaTime;
                if (b.Target == null || b.Elapsed >= b.Duration)
                {
                    Hide(b);
                    continue;
                }
                float fade = Mathf.Max(0.0001f, fadeTime);
                b.Group.alpha = Mathf.Clamp01(Mathf.Min(b.Elapsed / fade, (b.Duration - b.Elapsed) / fade));
                Place(b);
            }
        }

        // 세상 위치 → 화면 → 캔버스 로컬. 카메라 뒤면 숨긴다 (사라지지는 않음 — 돌아보면 다시 보임)
        private void Place(Bubble b)
        {
            var cam = Camera.main;
            if (cam == null || canvasRect == null)
                return;
            Vector3 world = b.Target.position + Vector3.up * b.Height;
            Vector3 screen = cam.WorldToScreenPoint(world);
            bool visible = screen.z > 0f;
            b.Rect.gameObject.SetActive(visible);
            if (!visible)
                return;
            var worldCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, new Vector2(screen.x, screen.y + screenOffsetY), worldCam, out var local))
                b.Rect.anchoredPosition = local;
        }

        private static void Hide(Bubble b)
        {
            b.Active = false;
            b.Target = null;
            b.Group.alpha = 0f;
            b.Rect.gameObject.SetActive(false);
        }
    }
}
