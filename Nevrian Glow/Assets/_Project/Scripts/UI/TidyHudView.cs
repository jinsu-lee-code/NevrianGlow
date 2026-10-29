using NevrianGlowFarm.Core;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Systems.Cleaning;
using TMPro;
using UnityEngine;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 가게 HUD "오늘의 정리" (와이어프레임 · 회신2 3-5, TASKS P-3): 청소·진열 단계에만 남은 쓰레기 · 얼룩 · 흩어진 상품 · 흐트러진 칸과
    /// 예상 청결 · 정돈 등급을 보여 준다. 값은 짧은 간격으로 읽는다(바닥 아이템 · 얼룩은 여러 곳에서 바뀌어 알림 하나로 모으기 어렵다).
    /// 그레이박스: 단색 패널 + 글자 — UI 아트(5-4) 때 다듬는다.
    /// </summary>
    public class TidyHudView : MonoBehaviour, Systems.Tutorial.ITutorialHighlightSource
    {
        [Header("참조")]
        [Tooltip("보이고 숨길 패널")]
        [SerializeField] private GameObject panel;
        [Tooltip("내용 글자 (여러 줄)")]
        [SerializeField] private TMP_Text label;

        [Header("조정값")]
        [Tooltip("다시 읽는 간격 (초)")]
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;

        private float nextRefresh;

        private void Awake()
        {
            if (panel == null || label == null)
            {
                Debug.LogError("[TidyHudView] 참조가 비어 있음 - 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            panel.SetActive(false);
        }

        private void OnEnable() => Systems.Tutorial.TutorialHighlights.Register(this);
        private void OnDisable() => Systems.Tutorial.TutorialHighlights.Unregister(this);

        // 튜토리얼 강조 (5-7): hud_tidy_grade = 오늘의 정리 패널 (보일 때만)
        public bool TryGetHighlight(string name, string arg, out Systems.Tutorial.HighlightHit hit)
        {
            hit = default;
            if (name != "hud_tidy_grade" || panel == null || !panel.activeSelf)
                return false;
            hit = Systems.Tutorial.HighlightHit.On(panel.GetComponent<RectTransform>());
            return true;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh)
                return;
            nextRefresh = Time.unscaledTime + refreshInterval;

            var game = GameManager.Instance;
            var cleaning = CleaningSystem.Instance;
            bool show = game != null && game.IsStarted && game.Phase == DayPhase.CleanDisplay && cleaning != null && GameScreen.Current == null;
            if (panel.activeSelf != show)
                panel.SetActive(show);
            if (!show)
                return;

            float clean = cleaning.CurrentCleanliness();
            float tidy = cleaning.CurrentTidiness();
            label.text = "오늘의 정리\n"
                + $"쓰레기 {cleaning.FloorTrashCount()} · 얼룩 {cleaning.StainsLeft}\n"
                + $"흩어진 상품 {cleaning.FloorProductCount()} · 흐트러진 칸 {cleaning.DisorderedNow()}\n"
                + $"예상 청결 {cleaning.GradeOf(clean)} ({clean:P0}) · 정돈 {cleaning.GradeOf(tidy)} ({tidy:P0})";
        }
    }
}
