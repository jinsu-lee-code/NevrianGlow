using System.Collections;
using System.Collections.Generic;
using NevrianGlowFarm.Data;
using UnityEngine;
using UnityEngine.AI;

namespace NevrianGlowFarm.Systems.Sales
{
    /// <summary>손님 상태 (GDD 4-5 흐름 순서). enum은 끝에만 추가.</summary>
    public enum CustomerState
    {
        Entering,
        PickingBasket,
        ToShelf,
        Browsing,
        Putting,
        ToLine,
        InLine,
        Paying,
        Happy,
        Leaving,
    }

    /// <summary>
    /// 손님 한 명 (노션 GDD 12.4 CustomerAI): 입장 → 장바구니 집기 → 진열대 → 둘러보기 → 넣기 × 1~3 → 계산대 줄 → 계산 → 해피 → 퇴장.
    /// 아무것도 못 사면 계산을 건너뛰고 바로 퇴장. 이동은 NavMeshAgent, 애니메이터 Speed = 실제 속도.
    /// 무엇을 사고 얼마를 내는지는 SalesSystem(→ Logic SalesRules · SalesSession)이 정한다 — 여기는 움직임 · 표시만.
    /// 애니메이션 이벤트(OnItemTake · OnItemPlace · OnHappyGlow)가 오면 그때 넘어가고, 없으면(빈 클립) 대체 시간으로 넘어간다 (BALANCE 9-5).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class CustomerAI : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("길찾기 (가게 바닥 NavMesh)")]
        [SerializeField] private NavMeshAgent agent;
        [Tooltip("공용 애니메이터 ac_customer (파라미터 Speed · Browsing · PickBasket · PutItem · Happy · HasBasket)")]
        [SerializeField] private Animator animator;
        [Tooltip("겉모습 (그레이박스 캡슐 — 키 · 색을 손님 종류대로)")]
        [SerializeField] private Renderer model;
        [Tooltip("장바구니 (집기 전에는 숨김)")]
        [SerializeField] private Transform basket;
        [Tooltip("장바구니에 담긴 상품 하나의 겉모습 (Prefabs/Shop/shelf_item)")]
        [SerializeField] private Renderer basketItemPrefab;

        [Header("조정값")]
        [Tooltip("애니메이션 이벤트가 없을 때 장바구니 집기 시간 (초, 임시)")]
        [SerializeField, Min(0f)] private float pickBasketFallback = 0.5f;
        [Tooltip("애니메이션 이벤트가 없을 때 상품 하나 넣는 시간 (초, 임시)")]
        [SerializeField, Min(0f)] private float putItemFallback = 0.4f;
        [Tooltip("애니메이션 이벤트가 없을 때 해피 아이들 시간 (초, 임시)")]
        [SerializeField, Min(0f)] private float happyFallback = 1f;
        [Tooltip("도착으로 보는 남은 거리 여유 — 정지 거리에 더한다 (m)")]
        [SerializeField, Min(0f)] private float arriveSlack = 0.1f;
        [Tooltip("한 지점으로 걷다 이 시간이 지나면 도착한 것으로 본다 — 막혔을 때 멈추지 않게 (초)")]
        [SerializeField, Min(1f)] private float walkTimeout = 12f;
        [Tooltip("그레이박스 해피 동작 — 제자리 뛰기 높이 (m)")]
        [SerializeField, Min(0f)] private float happyHop = 0.15f;

        [Header("디버그")]
        [Tooltip("지금 상태 (읽기 전용)")]
        [SerializeField] private CustomerState state;
        [Tooltip("장바구니 내용 (읽기 전용)")]
        [SerializeField] private List<string> basketView = new List<string>();

        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int BrowsingId = Animator.StringToHash("Browsing");
        private static readonly int PickBasketId = Animator.StringToHash("PickBasket");
        private static readonly int PutItemId = Animator.StringToHash("PutItem");
        private static readonly int HappyId = Animator.StringToHash("Happy");
        private static readonly int HasBasketId = Animator.StringToHash("HasBasket");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private SalesSystem system;
        private CustomerData data;
        private List<CropData> items;
        private readonly List<Renderer> basketItems = new List<Renderer>();
        private bool paid;
        private int animEvents;
        private Vector3 modelBase;

        public int Number { get; private set; }
        public CustomerData Data => data;
        public CustomerState State => state;
        /// <summary>장바구니를 채웠는지 (진열대에서 이미 가져갔는지).</summary>
        public bool HasShopped => items != null;
        public bool HasPaid => paid;
        public IReadOnlyList<CropData> Items => items;

        private void Reset() => agent = GetComponent<NavMeshAgent>();

        /// <summary>SalesSystem이 입구에서 만든 직후 부른다.</summary>
        public void Begin(SalesSystem owner, CustomerData type, int number, SalesSettingsData settings, Vector3 entrance)
        {
            system = owner;
            data = type;
            Number = number;
            name = $"Customer{number:00}_{type.Id}";

            agent.Warp(entrance);
            agent.speed = settings.WalkSpeed * type.WalkSpeedMul;
            agent.stoppingDistance = settings.StopDistance;
            agent.height = type.Height;
            agent.avoidancePriority = 30 + number % 40; // 서로 비켜 가게 조금씩 다르게
            if (animator != null)
                animator.speed = type.WalkSpeedMul; // 이동 속도와 걷기 재생 속도를 같은 비율로

            // 그레이박스: 캡슐 키 = 체형, 색 = 손님 종류
            float h = type.Height;
            model.transform.localScale = new Vector3(h * 0.3f, h * 0.5f, h * 0.3f);
            model.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            modelBase = model.transform.localPosition;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, type.GreyboxColor);
            model.SetPropertyBlock(block);
            if (basket != null)
            {
                basket.localPosition = type.BasketSocketOffset;
                basket.gameObject.SetActive(false);
            }
            StartCoroutine(Run(settings));
        }

        private void Update()
        {
            if (animator != null && agent.enabled)
                animator.SetFloat(SpeedId, agent.velocity.magnitude);
        }

        private IEnumerator Run(SalesSettingsData settings)
        {
            state = CustomerState.Entering;
            yield return WalkTo(system.BasketStandPosition);

            state = CustomerState.PickingBasket;
            SetTrigger(PickBasketId);
            yield return WaitAnimation(pickBasketFallback);
            if (basket != null)
                basket.gameObject.SetActive(true);
            SetBool(HasBasketId, true);

            state = CustomerState.ToShelf;
            Vector3 browse = system.BrowsePoint(Number);
            yield return WalkTo(browse);
            Face(system.ShelfLookPoint(browse));

            state = CustomerState.Browsing;
            SetBool(BrowsingId, true);
            yield return new WaitForSeconds(settings.BrowseDuration);

            // 진열대에서 이 순간 가져간다 (재고 비례 무작위 — Logic SalesRules.FillBasket)
            items = system.FillBasket();
            UpdateBasketView();
            state = CustomerState.Putting;
            for (int i = 0; i < items.Count; i++)
            {
                SetTrigger(PutItemId);
                yield return WaitAnimation(putItemFallback);
                ShowBasketItem(items[i]);
            }
            SetBool(BrowsingId, false);

            if (items.Count == 0)
            {
                // 못 사면 계산을 건너뛰고 바로 퇴장
                PayOnce();
                yield return LeaveStore();
                yield break;
            }

            state = CustomerState.ToLine;
            int slot = system.JoinLine(Number);
            while (true)
            {
                state = CustomerState.ToLine;
                yield return WalkTo(system.QueueSlotPosition(slot));
                Face(system.CounterPosition);
                state = CustomerState.InLine;
                // 앞 손님이 빠질 때까지 제자리 — 칸이 당겨지면 다시 걷는다
                while (system.LinePosition(Number) == slot && !system.IsFront(Number))
                    yield return null;
                int now = system.LinePosition(Number);
                if (now == 0 && slot == 0)
                    break;
                slot = Mathf.Max(0, now);
            }

            state = CustomerState.Paying;
            yield return new WaitForSeconds(settings.CheckoutDuration);
            PayOnce();
            system.LeaveLine(Number);

            state = CustomerState.Happy;
            SetTrigger(HappyId);
            yield return HappyRoutine();

            yield return LeaveStore();
        }

        private IEnumerator LeaveStore()
        {
            state = CustomerState.Leaving;
            yield return WalkTo(system.EntrancePosition);
            system.Despawn(this);
        }

        /// <summary>장사가 바로 끝날 때(건너뛰기 · 시간 초과): 아직 안 샀으면 지금 같은 함수로 사고, 안 냈으면 지금 낸다.</summary>
        public void SettleNow()
        {
            StopAllCoroutines();
            if (items == null)
                items = system.FillBasket();
            PayOnce();
        }

        private void PayOnce()
        {
            if (paid)
                return;
            paid = true;
            system.Pay(items ?? new List<CropData>());
        }

        // ── 이동 · 동작 ─────────────────────────────────

        private IEnumerator WalkTo(Vector3 target)
        {
            if (!agent.isOnNavMesh)
            {
                // 길이 없으면 순간 이동 (NavMesh를 굽지 않았을 때도 흐름은 이어지게) — Glow Farm/Setup/Bake Shop NavMesh
                transform.position = target;
                yield break;
            }
            agent.SetDestination(target);
            float start = Time.time;
            while (Time.time - start < walkTimeout)
            {
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + arriveSlack)
                    break;
                yield return null;
            }
            agent.ResetPath();
        }

        private void Face(Vector3 point)
        {
            Vector3 dir = point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.LookRotation(dir);
        }

        private IEnumerator WaitAnimation(float fallback)
        {
            int before = animEvents;
            float start = Time.time;
            while (animEvents == before && Time.time - start < fallback)
                yield return null;
        }

        private IEnumerator HappyRoutine()
        {
            int before = animEvents;
            float start = Time.time;
            while (animEvents == before && Time.time - start < happyFallback)
            {
                // 그레이박스 해피: 제자리에서 두 번 통통 (클립이 오면 애니메이터가 대신)
                float t = (Time.time - start) / Mathf.Max(0.01f, happyFallback);
                model.transform.localPosition = modelBase + Vector3.up * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f)) * happyHop;
                yield return null;
            }
            model.transform.localPosition = modelBase;
        }

        private void ShowBasketItem(CropData crop)
        {
            if (basket == null || basketItemPrefab == null || crop == null)
                return;
            var r = Instantiate(basketItemPrefab, basket);
            int i = basketItems.Count;
            // 장바구니 루트는 크기 1 (모델 자식만 납작한 상자) — 상품은 바구니 위에 나란히
            r.transform.localPosition = new Vector3((i % 3 - 1) * 0.09f, 0.1f + i / 3 * 0.08f, 0f);
            r.transform.localScale = Vector3.one * 0.09f;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, crop.GreyboxColor);
            r.SetPropertyBlock(block);
            basketItems.Add(r);
        }

        private void UpdateBasketView()
        {
            basketView.Clear();
            foreach (var c in items)
                basketView.Add(c != null ? c.Id : "-");
        }

        private void SetTrigger(int id) { if (animator != null && animator.runtimeAnimatorController != null) animator.SetTrigger(id); }
        private void SetBool(int id, bool value) { if (animator != null && animator.runtimeAnimatorController != null) animator.SetBool(id, value); }

        // ── 애니메이션 이벤트 (김기인 납품 클립에 넣는 이름 — BALANCE 9-5) ──
        public void OnItemTake() => animEvents++;
        public void OnItemPlace() => animEvents++;
        public void OnHappyGlow() => animEvents++;
    }
}
