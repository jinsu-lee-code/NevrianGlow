using System.Collections.Generic;
using NevrianGlowFarm.Systems.Tutorial;
using UnityEngine;
using UnityEngine.UI;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 튜토리얼 강조 표시 (TASKS 5-7, 기획 공통 규칙 "대상에 시안 외곽선 · 화살표"): 세상 대상 위에 아래를 가리키는 화살표(둥실거림),
    /// 화면 밖이면 가장자리에서 그쪽을 가리키는 화살표, 화면 요소는 그 위에 시안 테두리 펄스. UICanvas에 하나(Setup 메뉴가 만든다).
    /// 대상은 TutorialSystem이 단계마다 넣는다. 아트 외곽선 셰이더는 캐릭터 렌더 피처뿐이라 표식으로 대신한다.
    /// </summary>
    public class TutorialHighlightView : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("화살표 원본 (비활성 자식, 아래를 가리키는 그림) — 필요한 만큼 복제")]
        [SerializeField] private RectTransform arrowTemplate;
        [Tooltip("테두리 원본 (비활성 자식, 9-slice 테두리 그림) — 필요한 만큼 복제")]
        [SerializeField] private RectTransform frameTemplate;

        [Header("조정값")]
        [Tooltip("화살표 둥실거림 폭 (px)")]
        [SerializeField, Min(0f)] private float bobAmplitude = 10f;
        [Tooltip("둥실거림 · 테두리 펄스 주기 (초)")]
        [SerializeField, Min(0.1f)] private float period = 1.2f;
        [Tooltip("테두리 펄스 투명도 최소 ~ 최대")]
        [SerializeField, Range(0f, 1f)] private float frameMinAlpha = 0.45f;
        [Tooltip("테두리가 대상보다 바깥으로 나가는 여백 (px)")]
        [SerializeField, Min(0f)] private float framePadding = 10f;
        [Tooltip("화면 밖 대상의 가장자리 화살표 안쪽 여백 (px)")]
        [SerializeField, Min(0f)] private float edgePadding = 60f;

        private readonly List<HighlightHit> targets = new List<HighlightHit>();
        private readonly List<RectTransform> arrows = new List<RectTransform>();
        private readonly List<RectTransform> frames = new List<RectTransform>();
        private Canvas canvas;
        private RectTransform canvasRect;

        private void Awake()
        {
            if (arrowTemplate == null || frameTemplate == null)
            {
                Debug.LogError("[TutorialHighlightView] 참조가 비어 있음 - 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            arrowTemplate.gameObject.SetActive(false);
            frameTemplate.gameObject.SetActive(false);
            canvas = GetComponentInParent<Canvas>();
            canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
        }

        /// <summary>이번 단계의 대상들 (빈 목록 = 모두 숨김).</summary>
        public void SetTargets(IReadOnlyList<HighlightHit> hits)
        {
            targets.Clear();
            if (hits != null)
                targets.AddRange(hits);
        }

        private void LateUpdate()
        {
            if (canvasRect == null)
                return;
            var cam = Camera.main;
            int arrowCount = 0, frameCount = 0;
            float t = Time.unscaledTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t / period * Mathf.PI * 2f);
            foreach (var hit in targets)
            {
                if (hit.IsUi)
                {
                    if (hit.Rect == null || !hit.Rect.gameObject.activeInHierarchy)
                        continue;
                    var frame = Take(frames, frameTemplate, frameCount++);
                    PlaceFrame(frame, hit.Rect, pulse);
                }
                else if (cam != null)
                {
                    var arrow = Take(arrows, arrowTemplate, arrowCount++);
                    PlaceArrow(arrow, cam, hit.World, t);
                }
            }
            for (int i = arrowCount; i < arrows.Count; i++)
                arrows[i].gameObject.SetActive(false);
            for (int i = frameCount; i < frames.Count; i++)
                frames[i].gameObject.SetActive(false);
        }

        private static RectTransform Take(List<RectTransform> pool, RectTransform template, int index)
        {
            while (pool.Count <= index)
            {
                var rt = Instantiate(template, template.parent);
                rt.name = template.name.Replace("Template", (pool.Count + 1).ToString());
                pool.Add(rt);
            }
            var r = pool[index];
            r.gameObject.SetActive(true);
            return r;
        }

        // 화면 안: 대상 위에서 아래를 가리키며 둥실. 화면 밖(또는 카메라 뒤): 가장자리에서 그쪽으로 돌린 화살표
        private void PlaceArrow(RectTransform arrow, Camera cam, Vector3 world, float t)
        {
            Vector3 screen = cam.WorldToScreenPoint(world);
            bool behind = screen.z < 0f;
            if (behind)
                screen = new Vector3(Screen.width - screen.x, Screen.height - screen.y, 0f);
            bool inside = !behind && screen.x >= 0f && screen.x <= Screen.width && screen.y >= 0f && screen.y <= Screen.height;
            if (inside)
            {
                float bob = Mathf.Sin(t / period * Mathf.PI * 2f) * bobAmplitude;
                SetCanvasPosition(arrow, new Vector2(screen.x, screen.y + arrow.sizeDelta.y * 0.5f + bob));
                arrow.localRotation = Quaternion.identity;
                return;
            }
            // 화면 가운데에서 대상 쪽으로 뻗어 가장자리 안쪽에 두고, 화살표(아래 방향 그림)를 그 방향으로 돌린다
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dir = ((Vector2)screen - center);
            if (dir.sqrMagnitude < 1e-3f)
                dir = Vector2.up;
            dir.Normalize();
            float halfW = Screen.width * 0.5f - edgePadding, halfH = Screen.height * 0.5f - edgePadding;
            float scale = Mathf.Min(halfW / Mathf.Max(1e-3f, Mathf.Abs(dir.x)), halfH / Mathf.Max(1e-3f, Mathf.Abs(dir.y)));
            SetCanvasPosition(arrow, center + dir * scale);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f; // 그림은 아래(-y)를 가리킴 → 방향각 + 90
            arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void PlaceFrame(RectTransform frame, RectTransform target, float pulse)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, cam, out var local))
                    return;
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.anchoredPosition = (min + max) * 0.5f;
            frame.sizeDelta = max - min + Vector2.one * framePadding * 2f;
            var image = frame.GetComponent<Image>();
            if (image != null)
            {
                var c = image.color;
                c.a = Mathf.Lerp(frameMinAlpha, 1f, pulse);
                image.color = c;
            }
        }

        private void SetCanvasPosition(RectTransform rt, Vector2 screen)
        {
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, cam, out var local))
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = local;
            }
        }
    }
}
