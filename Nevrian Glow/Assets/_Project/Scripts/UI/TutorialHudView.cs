using NevrianGlowFarm.Core;
using NevrianGlowFarm.Logic.Tutorial;
using NevrianGlowFarm.Systems.Tutorial;
using TMPro;
using UnityEngine;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 화면 위쪽 "오늘 할 일" 한 줄 (TASKS 5-7, 기획 튜토리얼 공통 규칙): 현재 단계 문구만 보인다.
    /// 단계가 바뀌면 글자가 사라졌다가(페이드) 다음 문구가 뜬다. 튜토리얼이 없거나 끝나면 숨긴다. 모양은 아트 패널(ui_panel_base).
    /// </summary>
    public class TutorialHudView : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("보이고 숨길 패널 (투명도는 CanvasGroup)")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("문구 글자")]
        [SerializeField] private TMP_Text label;

        [Header("조정값")]
        [Tooltip("문구가 사라지고 나타나는 시간 (초)")]
        [SerializeField, Min(0f)] private float fadeTime = 0.25f;
        [Tooltip("문구 앞에 붙는 머리말 (비우면 문구만, 게임 화면 - '—' 쓰지 않음)")]
        [SerializeField] private string header = "오늘 할 일";
        [Tooltip("머리말 색")]
        [SerializeField] private Color headerColor = new Color(0.56f, 0.92f, 1f, 1f);

        private string shown;
        private string target;
        private float alpha;
        private TutorialSystem tutorial;

        private void Awake()
        {
            if (group == null || label == null)
            {
                Debug.LogError("[TutorialHudView] 참조가 비어 있음 - 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            alpha = 0f;
            group.alpha = 0f;
            group.gameObject.SetActive(false);
        }

        // TutorialSystem은 Awake에서 준비된다 → 구독은 Start에서
        private void Start()
        {
            tutorial = TutorialSystem.Instance;
            if (tutorial == null)
                return;
            tutorial.StepChanged += OnStep;
            OnStep(tutorial.Current);
        }

        private void OnDestroy()
        {
            if (tutorial != null)
                tutorial.StepChanged -= OnStep;
        }

        private void OnStep(TutorialStep step) => target = step != null && !string.IsNullOrWhiteSpace(step.Text) ? step.Text : null;

        private void Update()
        {
            var game = GameManager.Instance;
            string wanted = game != null && game.IsStarted ? target : null;
            float speed = fadeTime > 0f ? Time.unscaledDeltaTime / fadeTime : 1f;

            if (shown != wanted)
            {
                // 먼저 사라지고 → 문구 바꾸고 → 나타난다
                alpha = Mathf.Max(0f, alpha - speed);
                if (alpha <= 0f)
                {
                    shown = wanted;
                    label.text = Compose(shown);
                }
            }
            else if (shown != null)
                alpha = Mathf.Min(1f, alpha + speed);
            else
                alpha = 0f;

            group.alpha = alpha;
            bool visible = alpha > 0f || shown != null;
            if (group.gameObject.activeSelf != visible)
                group.gameObject.SetActive(visible);
        }

        private string Compose(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            if (string.IsNullOrEmpty(header))
                return text;
            return $"<color=#{ColorUtility.ToHtmlStringRGB(headerColor)}><size=72%>{header}</size></color>  {text}";
        }
    }
}
