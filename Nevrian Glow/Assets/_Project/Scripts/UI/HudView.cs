using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Systems.Zones;
using TMPro;
using UnityEngine;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// HUD 최소판 (GDD 5 화면 목록, TASKS 2-8): 일차 · 단계 · 지역 / 달러 · SP / 레벨 · 경험치 바.
    /// 핫바(HotbarView) · 상호작용 프롬프트(InteractionPromptView)는 따로 있다. 값은 GameEvents가 알릴 때만 바꾼다(매 프레임 갱신 없음).
    /// 그레이박스: 단색 패널 + 글자 — UI 아트(5-4) · UI 흐름도 도착 후 한꺼번에 다듬는다.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("일차 · 단계 · 지역 한 줄")]
        [SerializeField] private TMP_Text dayLabel;
        [Tooltip("달러 · SP 한 줄 (SP 글자가 따로 있으면 달러만)")]
        [SerializeField] private TMP_Text walletLabel;
        [Tooltip("SP 글자 — 아이콘과 함께 따로 보일 때 (비우면 달러 줄에 같이, 5-4b)")]
        [SerializeField] private TMP_Text skillPointLabel;
        [Tooltip("레벨 · 경험치 숫자")]
        [SerializeField] private TMP_Text levelLabel;
        [Tooltip("경험치 바 채움 — 가로 앵커(anchorMax.x)를 비율로 바꾼다")]
        [SerializeField] private RectTransform expFill;

        private int day;
        private DayPhase phase;
        private string zoneName = string.Empty;
        private int money;
        private int skillPoints;
        private int exp;
        private int expRequired = 1;
        private bool subscribed;

        private void OnEnable()
        {
            if (dayLabel == null || walletLabel == null || levelLabel == null || expFill == null)
            {
                Debug.LogError("[HudView] 참조가 비어 있음 — 인스펙터의 참조 항목 확인", this);
                return;
            }
            GameEvents.PhaseChanged += OnPhase;
            GameEvents.MoneyChanged += OnMoney;
            GameEvents.SkillPointsChanged += OnSkillPoints;
            GameEvents.ExpChanged += OnExp;
            subscribed = true;
            ReadCurrent();
        }

        private void Start()
        {
            // ZoneTransition은 Awake에서 준비된다 → 구독은 Start에서
            if (ZoneTransition.Instance != null)
            {
                ZoneTransition.Instance.ZoneChanged += OnZone;
                if (ZoneTransition.Instance.Current != null)
                    OnZone(ZoneTransition.Instance.Current);
            }
        }

        private void OnDisable()
        {
            if (!subscribed)
                return;
            GameEvents.PhaseChanged -= OnPhase;
            GameEvents.MoneyChanged -= OnMoney;
            GameEvents.SkillPointsChanged -= OnSkillPoints;
            GameEvents.ExpChanged -= OnExp;
            subscribed = false;
        }

        private void OnDestroy()
        {
            if (ZoneTransition.Instance != null)
                ZoneTransition.Instance.ZoneChanged -= OnZone;
        }

        // 이미 시작한 게임이면(HUD가 늦게 켜짐) 지금 값을 바로 읽는다.
        private void ReadCurrent()
        {
            var game = GameManager.Instance;
            if (game == null || !game.IsStarted)
            {
                dayLabel.text = string.Empty;
                walletLabel.text = string.Empty;
                if (skillPointLabel != null)
                    skillPointLabel.text = string.Empty;
                levelLabel.text = string.Empty;
                SetFill(0f);
                return;
            }
            day = game.Day;
            phase = game.Phase;
            money = game.Money;
            skillPoints = game.SkillPoints;
            exp = game.Exp;
            expRequired = game.ExpRequired;
            RefreshDay();
            RefreshWallet();
            RefreshLevel();
        }

        private void OnPhase(PhaseChange change)
        {
            day = change.Day;
            phase = change.Phase;
            RefreshDay();
        }

        private void OnZone(ZoneProfileData zone)
        {
            zoneName = zone != null ? zone.DisplayName : string.Empty;
            RefreshDay();
        }

        private void OnMoney(int value)
        {
            money = value;
            RefreshWallet();
        }

        private void OnSkillPoints(int value)
        {
            skillPoints = value;
            RefreshWallet();
        }

        private void OnExp(int value, int required)
        {
            exp = value;
            expRequired = Mathf.Max(1, required);
            RefreshLevel();
        }

        private void RefreshDay()
        {
            if (day <= 0)
                return;
            string zone = string.IsNullOrEmpty(zoneName) ? string.Empty : $" · {zoneName}";
            dayLabel.text = $"{day}일차 · {DayPhaseNames.ToKorean(phase)}{zone}";
        }

        private void RefreshWallet()
        {
            if (skillPointLabel == null)
            {
                walletLabel.text = $"{money:N0} 달러 · SP {skillPoints}";
                return;
            }
            walletLabel.text = $"{money:N0} 달러";
            skillPointLabel.text = $"SP {skillPoints}";
        }

        private void RefreshLevel()
        {
            var game = GameManager.Instance;
            int level = game != null ? game.Level : Progression.FirstLevel;
            levelLabel.text = $"Lv{level}  {exp} / {expRequired}";
            SetFill((float)exp / expRequired);
        }

        private void SetFill(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            // 채움 그림(Image Type = Filled, 아트 ui_bar_xp_fill)이면 그림을 자르고, 아니면 가로 앵커로 줄인다 (그레이박스)
            if (expFill.TryGetComponent<UnityEngine.UI.Image>(out var image) && image.type == UnityEngine.UI.Image.Type.Filled)
            {
                image.fillAmount = ratio;
                ratio = 1f;
            }
            expFill.anchorMin = new Vector2(0f, 0f);
            expFill.anchorMax = new Vector2(ratio, 1f);
            expFill.offsetMin = Vector2.zero;
            expFill.offsetMax = Vector2.zero;
        }
    }
}
