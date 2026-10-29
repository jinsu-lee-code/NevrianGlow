using UnityEngine;
using UnityEngine.UI;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// UI 빛 그림이 숨쉬듯 은은하게 밝아졌다 어두워진다 (타이틀 로고 달의 형광 — 2026-10-23, 사용자). 투명도만 바꾼다. 일시정지와 상관없이 실제 시간으로.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class UiGlowPulse : MonoBehaviour
    {
        [Header("조정값")]
        [Tooltip("가장 어두울 때 투명도 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float minAlpha = 0.25f;
        [Tooltip("가장 밝을 때 투명도 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.6f;
        [Tooltip("한 번 숨쉬는 시간 (초)")]
        [SerializeField, Min(0.1f)] private float period = 3.5f;
        [Tooltip("시작 위상 — 여러 빛이 같이 깜빡이지 않게 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float phase;

        private Graphic graphic;

        private void Awake() => graphic = GetComponent<Graphic>();

        private void Update()
        {
            float k = 0.5f - 0.5f * Mathf.Cos((Time.unscaledTime / period + phase) * Mathf.PI * 2f);
            var c = graphic.color;
            c.a = Mathf.Lerp(minAlpha, maxAlpha, k);
            graphic.color = c;
        }
    }
}
