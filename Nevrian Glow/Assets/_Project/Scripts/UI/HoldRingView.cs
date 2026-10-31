using NevrianGlowFarm.Systems.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 화면 가운데 E 꾹 누르기 진행 고리 (기획서 v2.0: 수확 = E 꾹 0.5초 + 진행 고리). 조준점 둘레의 원형 채움 그림이 누르는 동안 차오른다.
    /// 누르지 않으면 숨긴다.
    /// </summary>
    public class HoldRingView : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("진행을 알려 주는 Raycaster")]
        [SerializeField] private InteractionRaycaster raycaster;
        [Tooltip("원형 채움 그림 (Filled · Radial 360)")]
        [SerializeField] private Image fill;
        [Tooltip("고리 바탕 (흐린 원, 비우면 없음)")]
        [SerializeField] private Image track;

        private void Awake()
        {
            if (raycaster == null || fill == null)
            {
                Debug.LogError("[HoldRingView] 참조가 비어 있음 - 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            Show(0f);
        }

        private void LateUpdate() => Show(raycaster.isActiveAndEnabled ? raycaster.HoldProgress : 0f);

        private void Show(float progress)
        {
            bool on = progress > 0f;
            if (fill.enabled != on)
                fill.enabled = on;
            if (track != null && track.enabled != on)
                track.enabled = on;
            if (on)
                fill.fillAmount = progress;
        }
    }
}
