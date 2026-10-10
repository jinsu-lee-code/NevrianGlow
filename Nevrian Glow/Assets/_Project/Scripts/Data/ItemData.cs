using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>
    /// 인벤토리 칸을 차지하는 아이템 공통 (GDD 4-3). 도구 · 씨앗 · 작물 · 비료 · 장치 · 쓰레기 모두 이 형태로 칸에 들어간다.
    /// 든 아이템이 좌클릭 동작을 정한다 — 대상은 아이템 에셋(또는 그 종류)을 보고 할 일을 고른다. 특정 id로 분기하지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Glow Farm/Data/Item", fileName = "item_new")]
    public class ItemData : ContentData
    {
        [Header("칸")]
        [Tooltip("한 칸에 쌓을 수 있는 개수 — 도구는 이 값과 상관없이 1 (개)")]
        [SerializeField, Min(1)] private int maxStack = 1;

        public virtual int MaxStack => maxStack;
    }
}
