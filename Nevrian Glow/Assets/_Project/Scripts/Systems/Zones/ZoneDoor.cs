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
        [Tooltip("문 모델의 Animator (아트 pf_env_farm_door — Bool 'IsOpen') — 비우면 열리는 연출 없음")]
        [SerializeField] private Animator doorAnimator;
        [Tooltip("문을 쓸 때 빨리 도는 포털 빛 (아트 pf_vfx_portal — 커지는 것은 아트 문 열림 애니메이션이 맡음) — 비우면 없음")]
        [SerializeField] private Transform portal;

        [Header("조정값")]
        [Tooltip("포털 연출 시간 — 페이드와 겹쳐 보인다 (초, 기획서 v2.0: 0.6)")]
        [SerializeField, Min(0f)] private float portalTime = 0.6f;
        [Tooltip("포털 연출 동안 입자 재생 속도 배율 (배)")]
        [SerializeField, Range(1f, 5f)] private float portalSpeed = 2.5f;
        [Tooltip("이 단계에서만 문을 쓸 수 있다")]
        [SerializeField] private DayPhase usablePhase = DayPhase.Farming;
        [Tooltip("문을 쓰면 다음 단계로 넘긴다 (농사 끝내기)")]
        [SerializeField] private bool advancesPhase = true;
        [Tooltip("문 Animator의 열림 Bool 이름 (아트 ac_env_farm_door)")]
        [SerializeField] private string openParameter = "IsOpen";


        // 켜져 있는 문 (HUD '농사 끝내기' 단축키가 문과 같은 길로 넘기게 — P-7). 문은 Farm 씬이라 씬 사이 참조 대신 목록.
        private static readonly System.Collections.Generic.List<ZoneDoor> active = new System.Collections.Generic.List<ZoneDoor>();

        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);

        /// <summary>
        /// 그 단계를 끝내는 문을 어디서든 바로 쓴다 (회신2 4절 #1: 농사 끝내기 = 버튼 + 문, 확인 없이). 쓸 문이 없거나 이동 중이면 false.
        /// </summary>
        public static bool TryUseFor(DayPhase phase)
        {
            var game = GameManager.Instance;
            var transition = ZoneTransition.Instance;
            if (game == null || !game.IsStarted || game.Phase != phase || transition == null || transition.IsTransitioning)
                return false;
            foreach (var door in active)
                if (door != null && door.advancesPhase && door.usablePhase == phase && door.destination != null)
                    return door.Go(game, transition);
            return false;
        }

        // 문을 열고(아트 연출 0.9초 — 페이드와 함께 보임) 이동한다. 화면이 어두워진 순간(플레이어가 떠난 뒤) 문을 다시 닫아 다음에 올 때 닫혀 있게.
        private bool Go(GameManager game, ZoneTransition transition)
        {
            // 튜토리얼(5-7): 개간 · 심기 전에는 농사를 끝낼 수 없다
            if (advancesPhase && Systems.Tutorial.TutorialSystem.IsLocked(Logic.Tutorial.TutorialLocks.EndFarming))
                return false;
            bool started = transition.Request(destination, () =>
            {
                if (advancesPhase)
                    game.AdvancePhase();
                SetOpen(false);
            });
            if (started)
            {
                SetOpen(true);
                if (portal != null && portalTime > 0f && isActiveAndEnabled)
                    StartCoroutine(PortalPulse());
            }
            return started;
        }

        // 포털: 0.6초 동안 입자가 빨리 돈다 (화면이 어두워지는 동안 보임). 크기는 아트 문 열림 애니메이션(Portal 크기)이 키운다 — 겹쳐 쓰지 않음
        private System.Collections.IEnumerator PortalPulse()
        {
            var systems = portal.GetComponentsInChildren<ParticleSystem>(true);
            var speeds = new float[systems.Length];
            for (int i = 0; i < systems.Length; i++)
            {
                var main = systems[i].main;
                speeds[i] = main.simulationSpeed;
                main.simulationSpeed = speeds[i] * portalSpeed;
            }
            yield return new WaitForSecondsRealtime(portalTime);
            for (int i = 0; i < systems.Length; i++)
                if (systems[i] != null)
                {
                    var main = systems[i].main;
                    main.simulationSpeed = speeds[i];
                }
        }

        private void SetOpen(bool open)
        {
            if (doorAnimator != null && !string.IsNullOrEmpty(openParameter))
                doorAnimator.SetBool(openParameter, open);
        }

        public string GetHandPrompt()
        {
            var game = GameManager.Instance;
            if (destination == null || game == null || !game.IsStarted)
                return null;
            if (game.Phase != usablePhase)
                return $"{destination.DisplayName} 문 - {DayPhaseNames.ToKorean(usablePhase)} 단계에 열림 (지금 {DayPhaseNames.ToKorean(game.Phase)})";
            if (advancesPhase && Systems.Tutorial.TutorialSystem.IsLocked(Logic.Tutorial.TutorialLocks.EndFarming))
                return $"{destination.DisplayName} 문 - 먼저: {Systems.Tutorial.TutorialSystem.LockedHint}";
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
            Go(game, transition);
        }
    }
}
