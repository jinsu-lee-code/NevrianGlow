using NevrianGlowFarm.Systems.Interaction;
using TMPro;
using UnityEngine;

namespace NevrianGlowFarm.UI
{
    /// <summary>화면 중앙 아래 상호작용 프롬프트. 프롬프트가 바뀔 때만 갱신하고, 비면 숨긴다.</summary>
    public class InteractionPromptView : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("프롬프트를 알려 주는 Raycaster")]
        [SerializeField] private InteractionRaycaster raycaster;
        [Tooltip("프롬프트를 띄울 패널 (비면 숨김)")]
        [SerializeField] private GameObject panel;
        [Tooltip("프롬프트 글자")]
        [SerializeField] private TMP_Text label;

        private void OnEnable()
        {
            if (raycaster == null || panel == null || label == null)
            {
                Debug.LogError("[InteractionPromptView] 참조가 비어 있음", this);
                return;
            }
            raycaster.PromptChanged += Apply;
            Apply(raycaster.Prompt);
        }

        private void OnDisable()
        {
            if (raycaster != null)
                raycaster.PromptChanged -= Apply;
        }

        private void Apply(string prompt)
        {
            bool show = !string.IsNullOrEmpty(prompt);
            panel.SetActive(show);
            if (show)
                label.text = prompt;
        }
    }
}
