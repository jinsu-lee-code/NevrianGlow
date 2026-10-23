using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Shop
{
    /// <summary>
    /// 가구 자리 (TASKS P-4, 레벨배치 설정안 2절): 진열 가구는 정해진 자리에만 놓인다(자유 배치 아님). 자리 종류 = 가구 종류.
    /// 기본 매장 8자리 = 과일 진열대 자리 4(가운데) + 일반 진열대 자리 4(벽 쪽, 2차). 위치 · 크기는 Glow Farm/Setup/Build Shop Layout이 정한다.
    /// 지금 놓인 가구 = 이 오브젝트의 자식 ShelfDisplay. 추가 구매 · 옮기기(E 길게)는 P1.
    /// </summary>
    public class FurnitureSpot : MonoBehaviour
    {
        [Header("조정값")]
        [Tooltip("이 자리에 놓을 수 있는 가구 종류")]
        [SerializeField] private FurnitureKind kind = FurnitureKind.FruitShelf;
        [Tooltip("자리 크기 x · z (m) — 쓰레기 · 얼룩 금지 구역과 기즈모")]
        [SerializeField] private Vector2 size = new Vector2(6.5f, 4.4f);
        [Tooltip("열리는 매장 확장 단계 (0 = 기본 매장, 확장은 2차)")]
        [SerializeField, Min(0)] private int unlockStage;

        [Header("기즈모")]
        [Tooltip("자리 테두리 표시 (가구 있음 노랑 · 빈 자리 초록)")]
        [SerializeField] private bool drawSpot = true;

        public FurnitureKind Kind => kind;
        public Vector2 Size => size;
        public int UnlockStage => unlockStage;

        /// <summary>지금 놓인 진열 가구 (없으면 null).</summary>
        public ShelfDisplay Furniture => GetComponentInChildren<ShelfDisplay>();

        private void OnDrawGizmos()
        {
            if (!drawSpot)
                return;
            bool filled = transform.childCount > 0 && Furniture != null;
            GizmoKit.FlatBox(transform.position + Vector3.up * 0.02f, new Vector3(size.x, 0f, size.y), filled ? GizmoKit.HarvestDisplay : GizmoKit.Spawn, 0.1f);
            GizmoKit.Label(transform.position + Vector3.up * 1.2f, $"{(kind == FurnitureKind.FruitShelf ? "과일" : "일반")} 자리{(unlockStage > 0 ? $" (확장 {unlockStage})" : string.Empty)}", filled ? GizmoKit.HarvestDisplay : GizmoKit.Spawn);
        }
    }
}
