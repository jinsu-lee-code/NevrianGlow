using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Systems.Interaction;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Zones
{
    /// <summary>
    /// 다른 지역으로 가는 문 (농장 문 → 가게, GDD 4-7). 정해진 단계에서만 E로 쓸 수 있고, 쓰면 페이드 사이에 다음 단계로 넘긴다
    /// (농장 문: 농사 → 청소·진열). 다른 단계에서는 언제 쓸 수 있는지만 알려 준다.
    /// </summary>
    public class ZoneDoor : MonoBehaviour, IHandInteractable
    {
        [Header("참조")]
        [Tooltip("가는 곳 (Data/Zones)")]
        [SerializeField] private ZoneProfileData destination;

        [Header("조정값")]
        [Tooltip("이 단계에서만 문을 쓸 수 있다")]
        [SerializeField] private DayPhase usablePhase = DayPhase.Farming;
        [Tooltip("문을 쓰면 다음 단계로 넘긴다 (농사 끝내기)")]
        [SerializeField] private bool advancesPhase = true;

        public string GetHandPrompt()
        {
            var game = GameManager.Instance;
            if (destination == null || game == null || !game.IsStarted)
                return null;
            if (game.Phase != usablePhase)
                return $"{destination.DisplayName} 문 — {DayPhaseNames.ToKorean(usablePhase)} 단계에 열림 (지금 {DayPhaseNames.ToKorean(game.Phase)})";
            return advancesPhase
                ? $"{DayPhaseNames.ToKorean(usablePhase)} 끝내고 {destination.DisplayName}(으)로 가기"
                : $"{destination.DisplayName}(으)로 가기";
        }

        public void InteractHand()
        {
            var game = GameManager.Instance;
            var transition = ZoneTransition.Instance;
            if (destination == null || game == null || transition == null || game.Phase != usablePhase)
                return;
            transition.Request(destination, advancesPhase ? game.AdvancePhase : (System.Action)null);
        }
    }
}
