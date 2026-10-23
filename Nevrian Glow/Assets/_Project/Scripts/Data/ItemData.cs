using System.Collections.Generic;
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
        [Tooltip("창고에 넣을 수 있는지 — 쓰레기는 끈다 (GDD 4-3 · 4-4)")]
        [SerializeField] private bool canStore = true;

        [Header("모습")]
        [Tooltip("들었을 때 1인칭 화면 아래쪽에 보이는 모델 — 비우면 종류별 그레이박스 (모델 교체 5-2)")]
        [SerializeField] private GameObject heldPrefab;
        [Tooltip("바닥 · 진열대 · 손님 바구니에 놓인 모습 (피벗 바닥 가운데) — 그레이박스 도형 크기에 맞춰 줄인다. 여러 개면 놓일 때마다 하나 (쓰레기 5종), 비우면 그레이박스")]
        [SerializeField] private List<GameObject> worldModels = new List<GameObject>();

        public virtual int MaxStack => maxStack;
        public bool CanStore => canStore;
        public GameObject HeldPrefab => heldPrefab;
        public IReadOnlyList<GameObject> WorldModels => worldModels;

        /// <summary>놓인 모습 하나 (seed로 고름 — 같은 seed면 같은 모델). 없으면 null.</summary>
        public GameObject WorldModelFor(int seed)
        {
            int n = worldModels.Count;
            if (n == 0)
                return null;
            var model = worldModels[((seed % n) + n) % n];
            return model != null ? model : worldModels[0];
        }
    }
}
