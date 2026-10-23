using UnityEngine;
using UnityEngine.UI;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 로고 별 반짝임 (2026-10-23, 사용자): 쉬다가 한 번 반짝 — 커지며 밝아졌다 사라지고 살짝 돈다. 쉬는 시간은 별마다 무작위라 서로 다른 때에 반짝인다.
    /// 실제 시간으로(일시정지와 무관). 투명도 · 크기 · 회전만 바꾼다.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class UiTwinkle : MonoBehaviour
    {
        [Header("조정값")]
        [Tooltip("반짝임 사이 쉬는 시간 범위 (초)")]
        [SerializeField] private Vector2 restRange = new Vector2(2.5f, 6f);
        [Tooltip("한 번 반짝이는 시간 (초)")]
        [SerializeField, Min(0.1f)] private float flashTime = 0.7f;
        [Tooltip("가장 밝을 때 투명도 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float peakAlpha = 0.95f;
        [Tooltip("반짝이는 동안 도는 각도 (°)")]
        [SerializeField, Range(0f, 90f)] private float spin = 25f;

        private Graphic graphic;
        private float nextFlash;
        private Vector3 baseScale;

        private void Awake()
        {
            graphic = GetComponent<Graphic>();
            baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            nextFlash = Time.unscaledTime + Random.Range(0.3f, restRange.y); // 첫 반짝임도 별마다 다르게
            SetAlpha(0f);
        }

        private void Update()
        {
            float t = (Time.unscaledTime - nextFlash) / flashTime;
            if (t < 0f)
                return;
            if (t >= 1f)
            {
                SetAlpha(0f);
                nextFlash = Time.unscaledTime + Random.Range(restRange.x, restRange.y);
                return;
            }
            float k = Mathf.Sin(t * Mathf.PI); // 0 → 1 → 0
            SetAlpha(peakAlpha * k);
            transform.localScale = baseScale * Mathf.Lerp(0.4f, 1.1f, k);
            transform.localRotation = Quaternion.Euler(0f, 0f, spin * (t - 0.5f));
        }

        private void SetAlpha(float a)
        {
            var c = graphic.color;
            c.a = a;
            graphic.color = c;
        }
    }
}
