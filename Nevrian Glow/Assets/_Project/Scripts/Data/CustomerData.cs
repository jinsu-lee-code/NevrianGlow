using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>손님 체형 (공용 리그의 체형 단계, BALANCE 9-5). enum은 끝에만 추가.</summary>
    public enum CustomerBodySize
    {
        Small,
        Medium,
        Large,
    }

    /// <summary>
    /// 손님 종류 하나 = 에셋 하나 (노션 GDD 12.4 CustomerData). 9종까지 코드 수정 없이 에셋만 추가 (ContentDatabase가 자동 수집).
    /// </summary>
    [CreateAssetMenu(menuName = "Glow Farm/Data/Customer", fileName = "customer_new")]
    public class CustomerData : ContentData
    {
        [Header("모습")]
        [Tooltip("손님 프리팹 — 공용 애니메이터(ac_customer) 연결 (Prefabs/Shop/customer)")]
        [SerializeField] private GameObject prefab;
        [Tooltip("체형 단계 — 키: 작은 1.2 · 보통 1.6 · 큰 2.0 m, 모델은 이 키에 맞춰 크기를 바꾼다 (BALANCE 9-5)")]
        [SerializeField] private CustomerBodySize bodySize = CustomerBodySize.Medium;
        [Tooltip("그레이박스 캡슐 색 (손님 구분용, 아트 모델에는 안 씀)")]
        [SerializeField] private Color greyboxColor = Color.white;
        [Tooltip("손 소켓 보정 — 장바구니 (m, 왼손 소켓 Socket_Basket 로컬)")]
        [SerializeField] private Vector3 basketSocketOffset;
        [Tooltip("손 소켓 보정 — 집은 상품 (m, 오른손 소켓 Socket_Item 로컬)")]
        [SerializeField] private Vector3 itemSocketOffset;

        [Header("등장")]
        [Tooltip("이동 속도와 걷기 재생 속도 배율 (0.7~1.3)")]
        [SerializeField, Range(0.7f, 1.3f)] private float walkSpeedMul = 1f;
        [Tooltip("등장 확률 가중치 (0~5)")]
        [SerializeField, Range(0f, 5f)] private float spawnWeight = 1f;
        [Tooltip("이 일차부터 등장 (일)")]
        [SerializeField, Min(1)] private int unlockDay = 1;

        public GameObject Prefab => prefab;
        public CustomerBodySize BodySize => bodySize;
        public Color GreyboxColor => greyboxColor;
        public Vector3 BasketSocketOffset => basketSocketOffset;
        public Vector3 ItemSocketOffset => itemSocketOffset;
        public float WalkSpeedMul => walkSpeedMul;
        public float SpawnWeight => spawnWeight;
        public int UnlockDay => unlockDay;

        /// <summary>체형 키 (m).</summary>
        public float Height => bodySize == CustomerBodySize.Small ? 1.2f : bodySize == CustomerBodySize.Large ? 2.0f : 1.6f;
    }
}
