using UnityEngine;

namespace NevrianGlowFarm.Systems.Tutorial
{
    /// <summary>
    /// 고정된 강조 대상 (문 · 창고 · 쓰레기통 · 진열대 · 간판) — Setup 메뉴가 Farm · Shop 씬의 그 오브젝트에 붙이고 이름을 적는다.
    /// 화면 요소(RectTransform)에 붙이면 테두리 펄스, 세상 오브젝트면 위치 + 높이 위에 화살표.
    /// </summary>
    public class TutorialHighlightTarget : MonoBehaviour, ITutorialHighlightSource
    {
        [Header("조정값")]
        [Tooltip("기획 highlight 이름 (예: door_shop · storage_farm · trash_bin · fruit_shelf_1 · signboard)")]
        [SerializeField] private string targetName;
        [Tooltip("세상 오브젝트일 때 화살표를 띄울 높이 (m)")]
        [SerializeField, Min(0f)] private float height = 1.2f;

        public string TargetName => targetName;

        private void OnEnable() => TutorialHighlights.Register(this);
        private void OnDisable() => TutorialHighlights.Unregister(this);

        public bool TryGetHighlight(string name, string arg, out HighlightHit hit)
        {
            hit = default;
            if (string.IsNullOrEmpty(targetName) || name != targetName)
                return false;
            hit = transform is RectTransform rect ? HighlightHit.On(rect) : HighlightHit.At(transform.position + Vector3.up * height);
            return true;
        }
    }
}
