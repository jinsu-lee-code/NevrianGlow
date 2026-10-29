using NevrianGlowFarm.Core;
using TMPro;
using UnityEngine;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 레벨업 배너 (TASKS 5-7, 기획 튜토리얼 D2-11 levelup_banner): GameEvents.LeveledUp이 오면 화면 위쪽에 "레벨 업! Lv2 · SP 3"을 잠깐 띄운다.
    /// 소리 · 발밑 금빛 고리는 AudioManager · VfxManager가 같은 이벤트로 낸다. 실제 시간으로 움직여 정산 화면 위에서도 보인다.
    /// </summary>
    public class LevelUpBanner : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("보이고 숨길 패널 (투명도는 CanvasGroup)")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("글자")]
        [SerializeField] private TMP_Text label;

        [Header("조정값")]
        [Tooltip("보이는 시간 (초, 나타나고 사라지는 시간 포함)")]
        [SerializeField, Min(0.5f)] private float showTime = 3f;
        [Tooltip("나타나고 사라지는 시간 (초)")]
        [SerializeField, Min(0f)] private float fadeTime = 0.3f;
        [Tooltip("글자 — {0} 새 레벨, {1} 보유 SP (게임 화면 - '—' 쓰지 않음)")]
        [SerializeField] private string format = "레벨 업!  Lv{0} · SP {1}";

        private float elapsed = -1f;

        private void Awake()
        {
            if (group == null || label == null)
            {
                Debug.LogError("[LevelUpBanner] 참조가 비어 있음 - 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            group.alpha = 0f;
            group.gameObject.SetActive(false);
        }

        private void OnEnable() => GameEvents.LeveledUp += OnLeveledUp;
        private void OnDisable() => GameEvents.LeveledUp -= OnLeveledUp;

        private void OnLeveledUp(int level, int levelsGained)
        {
            var game = GameManager.Instance;
            label.text = string.Format(format, level, game != null ? game.SkillPoints : 0);
            elapsed = 0f;
            group.alpha = 0f;
            group.gameObject.SetActive(true);
        }

        /// <summary>디버그 · 확인용: 지금 값으로 한 번 띄운다.</summary>
        public void ShowNow() => OnLeveledUp(GameManager.Instance != null ? GameManager.Instance.Level : 1, 1);

        private void Update()
        {
            if (elapsed < 0f)
                return;
            elapsed += Time.unscaledDeltaTime;
            float fade = Mathf.Max(0.0001f, fadeTime);
            group.alpha = Mathf.Clamp01(Mathf.Min(elapsed / fade, (showTime - elapsed) / fade));
            if (elapsed >= showTime)
            {
                elapsed = -1f;
                group.alpha = 0f;
                group.gameObject.SetActive(false);
            }
        }
    }
}
