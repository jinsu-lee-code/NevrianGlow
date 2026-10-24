using System.Collections.Generic;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Tutorial
{
    /// <summary>강조 대상 하나 — 세상 위치(머리 위 화살표)이거나 화면 사각형(테두리 펄스).</summary>
    public readonly struct HighlightHit
    {
        public readonly Vector3 World;
        public readonly RectTransform Rect;

        private HighlightHit(Vector3 world, RectTransform rect)
        {
            World = world;
            Rect = rect;
        }

        public bool IsUi => Rect != null;
        public static HighlightHit At(Vector3 world) => new HighlightHit(world, null);
        public static HighlightHit On(RectTransform rect) => new HighlightHit(default, rect);
    }

    /// <summary>
    /// 강조 대상을 이름으로 내주는 쪽 (기획 tutorial_steps.csv highlight 열 — "이름[:인자]", 개발 검토 2026-10-22).
    /// 밭 · 가게 정리 · 손님 · 핫바 · 상점 줄처럼 그때그때 생기는 대상은 각 시스템이, 문 · 창고 · 쓰레기통 · 간판처럼 고정된 것은 TutorialHighlightTarget이 맡는다.
    /// </summary>
    public interface ITutorialHighlightSource
    {
        /// <summary>이 이름(과 인자)의 대상을 지금 줄 수 있으면 true. 모르는 이름이면 false.</summary>
        bool TryGetHighlight(string name, string arg, out HighlightHit hit);
    }

    /// <summary>강조 대상 등록부 — 내주는 쪽은 OnEnable/OnDisable에서 등록 · 해제한다. 튜토리얼이 단계마다 이름을 묻는다.</summary>
    public static class TutorialHighlights
    {
        private static readonly List<ITutorialHighlightSource> sources = new List<ITutorialHighlightSource>();

        public static void Register(ITutorialHighlightSource source)
        {
            if (source != null && !sources.Contains(source))
                sources.Add(source);
        }

        public static void Unregister(ITutorialHighlightSource source) => sources.Remove(source);

        /// <summary>"이름[:인자]" 하나를 푼다. 아무도 모르면 false.</summary>
        public static bool TryResolve(string entry, out HighlightHit hit)
        {
            hit = default;
            if (string.IsNullOrWhiteSpace(entry))
                return false;
            string name = entry.Trim(), arg = null;
            int colon = name.IndexOf(':');
            if (colon >= 0)
            {
                arg = name.Substring(colon + 1).Trim();
                name = name.Substring(0, colon).Trim();
            }
            for (int i = sources.Count - 1; i >= 0; i--)
            {
                var s = sources[i];
                if (s is Object o && o == null)
                {
                    sources.RemoveAt(i);
                    continue;
                }
                if (s.TryGetHighlight(name, arg, out hit))
                    return true;
            }
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => sources.Clear();
    }

    /// <summary>
    /// 고정된 강조 대상 (문 · 창고 · 쓰레기통 · 진열대 · 간판) — Setup 메뉴가 Farm · Shop 씬의 그 오브젝트에 붙이고 이름을 적는다.
    /// 화면 요소(RectTransform)에 붙이면 테두리 펄스, 세상 오브젝트면 위치 + 높이 위에 화살표.
    /// </summary>
    public class TutorialHighlightTarget : MonoBehaviour, ITutorialHighlightSource
    {
        [Header("조정값")]
        [Tooltip("기획 highlight 이름 (예: door_shop · storage_farm · trash_bin · fruit_shelf_1 · signboard)")]
        [SerializeField] private string targetName;
        [Tooltip("세상 오브젝트일 때 화살표를 띄울 높이 (m)")]
        [SerializeField, Min(0f)] private float height = 1.2f;

        public string TargetName => targetName;

        private void OnEnable() => TutorialHighlights.Register(this);
        private void OnDisable() => TutorialHighlights.Unregister(this);

        public bool TryGetHighlight(string name, string arg, out HighlightHit hit)
        {
            hit = default;
            if (string.IsNullOrEmpty(targetName) || name != targetName)
                return false;
            hit = transform is RectTransform rect ? HighlightHit.On(rect) : HighlightHit.At(transform.position + Vector3.up * height);
            return true;
        }
    }
}
