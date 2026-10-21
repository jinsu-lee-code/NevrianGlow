using UnityEngine;

namespace NevrianGlowFarm.Systems.Sales
{
    /// <summary>
    /// 리그 모델의 애니메이션 이벤트를 CustomerAI로 넘긴다. 애니메이션 이벤트는 Animator가 붙은 오브젝트(모델)로만 오기 때문.
    /// 이벤트 이름은 animation.md 4번 표 (OnFootstep · OnBasketAttach · OnItemTake · OnItemPlace · OnHappyGlow).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class CustomerAnimationEvents : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("이벤트를 받을 손님 (손님 루트)")]
        [SerializeField] private CustomerAI customer;

        private void Reset() => customer = GetComponentInParent<CustomerAI>();

        public void OnFootstep() { if (customer != null) customer.OnFootstep(); }
        public void OnBasketAttach() { if (customer != null) customer.OnBasketAttach(); }
        public void OnItemTake() { if (customer != null) customer.OnItemTake(); }
        public void OnItemPlace() { if (customer != null) customer.OnItemPlace(); }
        public void OnHappyGlow() { if (customer != null) customer.OnHappyGlow(); }
    }
}
