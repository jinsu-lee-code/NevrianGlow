using UnityEngine;
using UnityEngine.UI;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 로고 빛 쓸기 (2026-10-23, 사용자): 몇 초마다 비스듬한 빛 띠가 왼쪽에서 오른쪽으로 한 번 지나간다.
    /// 셰이더 NevrianGlowFarm/UI/Shine 재질의 _ShinePos를 움직인다 — 재질 에셋은 그대로 두고 실행 중 사본만 바꾼다. 실제 시간으로(일시정지와 무관).
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class UiShineSweep : MonoBehaviour
    {
        [Header("조정값")]
        [Tooltip("한 번 지나간 뒤 다음까지 쉬는 시간 (초)")]
        [SerializeField, Min(0.5f)] private float interval = 6f;
        [Tooltip("빛 띠가 지나가는 시간 (초)")]
        [SerializeField, Min(0.1f)] private float duration = 1.4f;
        [Tooltip("처음 지나가기까지 기다리는 시간 — 타이틀이 열리고 로고가 보인 뒤 (초)")]
        [SerializeField, Min(0f)] private float firstDelay = 1.5f;

        private static readonly int ShinePosId = Shader.PropertyToID("_ShinePos");
        private Material material;
        private float start;

        private void OnEnable()
        {
            var graphic = GetComponent<Graphic>();
            if (material == null && graphic.material != null)
            {
                material = new Material(graphic.material);
                graphic.material = material;
            }
            start = Time.unscaledTime + firstDelay;
        }

        private void OnDestroy()
        {
            if (material != null)
                Destroy(material);
        }

        private void Update()
        {
            if (material == null)
                return;
            float t = Time.unscaledTime - start;
            float pos = -1f; // 쉬는 동안은 띠가 그림 밖
            if (t >= 0f)
            {
                float cycle = t % (duration + interval);
                if (cycle < duration)
                    pos = Mathf.Lerp(-0.3f, 1.3f, Mathf.SmoothStep(0f, 1f, cycle / duration)); // 기울기만큼 양끝 여유
            }
            material.SetFloat(ShinePosId, pos);
        }
    }
}
