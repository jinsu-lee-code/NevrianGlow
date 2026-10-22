using NevrianGlowFarm.Core;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Systems.Cleaning;
using NevrianGlowFarm.Systems.Interaction;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Sales
{
    /// <summary>
    /// 가게 영업 팻말 (GDD 4-1 청소·진열 '완료', 결정 2026-10-11: 팻말 E). 청소·진열 단계에서 E → 장사 단계.
    /// 조준하면 지금 청결도 · 진열 개수를 보여 준다. 다른 단계에서는 언제 쓰는지만 알려 준다.
    /// </summary>
    public class OpenSign : MonoBehaviour, IHandInteractable
    {
        [Header("조정값")]
        [Tooltip("이 단계에서만 쓸 수 있다 — 쓰면 다음 단계(장사)로")]
        [SerializeField] private DayPhase usablePhase = DayPhase.CleanDisplay;

        public string GetHandPrompt()
        {
            var game = GameManager.Instance;
            if (game == null || !game.IsStarted)
                return null;
            if (game.Phase != usablePhase)
                return $"영업 팻말 - {DayPhaseNames.ToKorean(usablePhase)} 단계에 사용 (지금 {DayPhaseNames.ToKorean(game.Phase)})";
            var cleaning = CleaningSystem.Instance;
            float clean = cleaning != null ? cleaning.CurrentCleanliness() : 1f;
            float tidy = cleaning != null ? cleaning.CurrentTidiness() : 1f;
            int shown = SalesSystem.Instance != null ? SalesSystem.Instance.DisplayedNow() : 0;
            string empty = shown == 0 ? " - 팔 작물 없음" : string.Empty;
            return $"가게 열고 장사 시작 (청결도 {clean:P0} · 정돈도 {tidy:P0} · 진열 {shown}개{empty})";
        }

        public void InteractHand()
        {
            var game = GameManager.Instance;
            if (game != null && game.IsStarted && game.Phase == usablePhase)
                game.AdvancePhase();
        }
    }
}
