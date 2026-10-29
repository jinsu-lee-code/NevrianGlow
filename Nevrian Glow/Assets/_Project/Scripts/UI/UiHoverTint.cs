using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 마우스를 올리면 밝게, 누르면 어둡게 — 여러 그림(바탕 + 테두리 프레임)을 한꺼번에 (5-4c). Button의 ColorTint는 그림 하나만 바꿔서 따로 둔다.
    /// 그림의 색(Image.color)은 건드리지 않고 CanvasRenderer 색을 곱한다 — 화면이 바꾸는 줄 색(SetSelected)과 겹쳐도 된다.
    /// </summary>
    public class UiHoverTint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("참조")]
        [Tooltip("함께 밝아질 그림들 (바탕 · 프레임)")]
        [SerializeField] private Graphic[] targets = new Graphic[0];

        [Header("조정값")]
        [Tooltip("마우스를 올렸을 때 밝기 배율 (배)")]
        [SerializeField, Min(0f)] private float highlight = 1.35f;
        [Tooltip("누르는 동안 밝기 배율 (배)")]
        [SerializeField, Min(0f)] private float pressed = 0.8f;
        [Tooltip("바뀌는 데 걸리는 시간 (초)")]
        [SerializeField, Min(0f)] private float fade = 0.08f;

        private bool hovered;
        private bool pressing;

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            Apply(fade);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            Apply(fade);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pressing = true;
            Apply(0f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressing = false;
            Apply(fade);
        }

        private void OnDisable()
        {
            hovered = false;
            pressing = false;
            Apply(0f);
        }

        private void Apply(float duration)
        {
            float m = pressing ? pressed : hovered ? highlight : 1f;
            var color = new Color(m, m, m, 1f);
            foreach (var g in targets)
                if (g != null)
                    g.CrossFadeColor(color, duration, true, true);
        }
    }
}
