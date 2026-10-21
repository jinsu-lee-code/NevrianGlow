using NevrianGlowFarm.Logic.Cleaning;
using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>
    /// 청소 · 가게 정리 (청소 v2 설정안, TASKS P-3). 값은 기획 game_settings.csv에서 가져온다 (Import Plan CSV).
    /// 대걸레 반경 · 속도는 대걸레 등급 데이터(MopData)에 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "Glow Farm/Settings/Cleaning", fileName = "CleaningSettings")]
    public class CleaningSettingsData : ScriptableObject
    {
        [Header("쓰레기 · 얼룩")]
        [Tooltip("1일차 쓰레기 — 버려진 가게 대청소 (개, trash_day1)")]
        [SerializeField, Min(0)] private int trashDay1 = 30;
        [Tooltip("1일차 얼룩 (개, stain_day1)")]
        [SerializeField, Min(0)] private int stainDay1 = 12;
        [Tooltip("평소 하루 쓰레기 최소 (개, trash_daily_min)")]
        [SerializeField, Min(0)] private int trashDailyMin = 1;
        [Tooltip("평소 하루 쓰레기 최대 (개, trash_daily_max)")]
        [SerializeField, Min(0)] private int trashDailyMax = 3;
        [Tooltip("얼룩 · 발자국 = 어제 손님 × 이 값 (개, stain_per_customer)")]
        [SerializeField, Min(0f)] private float stainPerCustomer = 0.3f;

        [Header("흩어진 상품 · 흐트러진 진열")]
        [Tooltip("흩어진 상품 = 안 팔린 진열 × 이 값 + … (scatter_unsold_ratio)")]
        [SerializeField, Range(0f, 1f)] private float scatterUnsoldRatio = 0.15f;
        [Tooltip("… + 어제 손님 ÷ 이 값 (명, scatter_per_customers)")]
        [SerializeField, Min(0f)] private float scatterPerCustomers = 3f;
        [Tooltip("상품이 있는 진열 칸 중 흐트러지는 비율 (disorder_ratio)")]
        [SerializeField, Range(0f, 1f)] private float disorderRatio = 0.2f;

        [Header("등급 (청결 · 정돈 같은 기준)")]
        [Tooltip("S 하한 (grade_s_min)")]
        [SerializeField, Range(0f, 1f)] private float gradeS = 0.95f;
        [Tooltip("A 하한 (grade_a_min)")]
        [SerializeField, Range(0f, 1f)] private float gradeA = 0.8f;
        [Tooltip("B 하한 — 미만은 C (grade_b_min)")]
        [SerializeField, Range(0f, 1f)] private float gradeB = 0.6f;

        [Header("배치")]
        [Tooltip("쓰레기 · 얼룩 · 흩어진 상품 사이 최소 간격 (m)")]
        [SerializeField, Min(0f)] private float minSpacing;
        [Tooltip("한 개를 놓을 자리를 찾는 최대 시도 (회)")]
        [SerializeField, Min(1)] private int placementAttempts = 1;

        [Header("쓰레기")]
        [Tooltip("쓰레기 아이템 (Data/Items/item_trash) — 바닥에 놓이고, 주우면 인벤토리 칸을 차지하며, 창고에 넣을 수 없다")]
        [SerializeField] private ItemData trashItem;

        public float MinSpacing => minSpacing;
        public int PlacementAttempts => placementAttempts;
        public ItemData TrashItem => trashItem;

        /// <summary>Logic 가게 정리 값 (TidyRules).</summary>
        public MessSpec ToMessSpec() => new MessSpec(trashDay1, stainDay1, trashDailyMin, trashDailyMax, stainPerCustomer,
            scatterUnsoldRatio, scatterPerCustomers, disorderRatio, gradeS, gradeA, gradeB);
    }
}
