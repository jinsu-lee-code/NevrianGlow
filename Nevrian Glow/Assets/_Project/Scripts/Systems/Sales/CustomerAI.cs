using System.Collections;
using System.Collections.Generic;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

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
        [Tooltip("공용 애니메이터 ac_customer (파라미터 Speed · Browsing · PickBasket · PutItem · Happy · HasBasket · WalkPlayback) — 리그 모델이면 모델 쪽 Animator")]
        [SerializeField] private Animator animator;
        [Tooltip("겉모습 루트 — 체형 키에 맞춰 크기를 바꾼다")]
        [SerializeField] private Transform model;
        [Tooltip("그레이박스 캡슐 (있으면 손님 색 · 캡슐 비율을 입힌다, 아트 모델이면 비움)")]
        [FormerlySerializedAs("model")]
        [SerializeField] private Renderer greyboxRenderer;
        [Tooltip("장바구니 (집기 전에는 숨김) — 리그 모델이면 왼손 소켓 Socket_Basket 아래")]
        [SerializeField] private Transform basket;
        [Tooltip("장바구니에 담긴 상품을 놓는 자리 (비우면 장바구니)")]
        [SerializeField] private Transform basketContents;
        [Tooltip("장바구니에 담긴 상품 하나의 겉모습 (Prefabs/Shop/shelf_item)")]
        [SerializeField] private Renderer basketItemPrefab;
        [Tooltip("살 게 없어 빈손으로 나갈 때 머리 위에 켜는 실망 아이콘 (회신 #7 — 신규 모션 대신, 비우면 없음)")]
        [SerializeField] private GameObject disappointedIcon;

        [Header("조정값")]
        [Tooltip("모델 원래 키 — 체형 키 ÷ 이 값으로 크기를 바꾼다 (m)")]
        [SerializeField, Min(0.1f)] private float modelHeight = 1.84f;
        [Tooltip("왼팔 바구니 레이어를 켜고 끄는 시간 (초)")]
        [SerializeField, Min(0f)] private float basketLayerBlend = 0.25f;
        [Tooltip("애니메이션 이벤트가 없을 때 장바구니 집기 시간 (초, 임시)")]
        [SerializeField, Min(0f)] private float pickBasketFallback = 0.5f;
        [Tooltip("상품 하나 넣기를 기다리는 최대 시간 — OnItemPlace(바구니에 넣는 순간)가 오면 그때 넘어간다 (초, put_item 클립 1.53초 · 이벤트 1.03초)")]
        [SerializeField, Min(0f)] private float putItemFallback = 1.2f;
        [Tooltip("애니메이션 이벤트가 없을 때 해피 아이들 시간 (초, 임시)")]
        [SerializeField, Min(0f)] private float happyFallback = 1f;
        [Tooltip("도착으로 보는 남은 거리 여유 — 정지 거리에 더한다 (m)")]
        [SerializeField, Min(0f)] private float arriveSlack = 0.1f;
        [Tooltip("한 지점으로 걷다 이 시간이 지나면 도착한 것으로 본다 — 막혔을 때 멈추지 않게 (초)")]
        [SerializeField, Min(1f)] private float walkTimeout = 12f;
        [Tooltip("그레이박스 해피 동작 — 제자리 뛰기 높이 (m)")]
        [SerializeField, Min(0f)] private float happyHop = 0.15f;
        [Tooltip("발소리 간격 — 걸은 거리 (m). 걷기 클립에 OnFootstep 이벤트가 있으면 이벤트로 바뀐다 (animation.md B-1)")]
        [SerializeField, Min(0.1f)] private float stepDistance = 1.05f;
        [Tooltip("말풍선(튜토리얼 대사)이 보이는 시간 (초)")]
        [SerializeField, Min(0.5f)] private float speechDuration = 3.5f;
        [Tooltip("말풍선 높이 — 체형 키 위로 (m)")]
        [SerializeField, Min(0f)] private float speechHeight = 0.35f;

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
        private static readonly int WalkPlaybackId = Animator.StringToHash("WalkPlayback");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private const string BasketLayer = "Basket";

        private SalesSystem system;
        private CustomerData data;
        private List<CropData> items;
        private readonly List<Renderer> basketItems = new List<Renderer>();
        private bool paid;
        private int animEvents;
        private int placeEvents;
        private CustomerFace face;
        private Vector3 modelBase;
        private float stepTravel;
        private bool footstepEvents;
        private bool hasBasket;
        private int basketLayer = -1;
        private float walkClipSpeed = 1f;

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
            walkClipSpeed = settings.WalkClipSpeed;
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                basketLayer = animator.GetLayerIndex(BasketLayer);
                if (basketLayer >= 0)
                    animator.SetLayerWeight(basketLayer, 0f);
            }

            float h = type.Height;
            if (model == null && greyboxRenderer != null)
                model = greyboxRenderer.transform;
            if (greyboxRenderer != null)
            {
                // 그레이박스: 캡슐 키 = 체형, 색 = 손님 종류
                model.localScale = new Vector3(h * 0.3f, h * 0.5f, h * 0.3f);
                model.localPosition = new Vector3(0f, h * 0.5f, 0f);
                var block = new MaterialPropertyBlock();
                block.SetColor(BaseColorId, type.GreyboxColor);
                greyboxRenderer.SetPropertyBlock(block);
            }
            else if (model != null)
                model.localScale = Vector3.one * (h / modelHeight); // 아트 모델: 체형 키에 맞춰 같은 비율로
            modelBase = model != null ? model.localPosition : Vector3.zero;
            // 표정(피피 · 오르 아트 모델, BRANCH_ART 10/22) — 없으면 그레이박스 · Cunsumer
            face = model != null ? model.GetComponentInChildren<CustomerFace>(true) : null;
            if (basket != null)
            {
                basket.localPosition = type.BasketSocketOffset;
                basket.gameObject.SetActive(false);
            }
            if (disappointedIcon != null)
                disappointedIcon.SetActive(false);
            StartCoroutine(Run(settings));
        }

        // 실망 아이콘은 늘 카메라를 본다 (켜져 있을 때만)
        private void LateUpdate()
        {
            if (disappointedIcon == null || !disappointedIcon.activeSelf)
                return;
            var cam = Camera.main;
            if (cam != null)
                disappointedIcon.transform.rotation = Quaternion.LookRotation(disappointedIcon.transform.position - cam.transform.position, Vector3.up);
        }

        private void Update()
        {
            if (!agent.enabled)
                return;
            float speed = agent.velocity.magnitude;
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetFloat(SpeedId, speed);
                // 발이 미끄러지지 않게: 재생 배속 = 실제 속도 ÷ (클립 기준 속도 × 휴먼 스케일 × 모델 크기)
                float scale = model != null ? model.lossyScale.y : 1f;
                animator.SetFloat(WalkPlaybackId, speed / Mathf.Max(0.01f, walkClipSpeed * animator.humanScale * scale));
                // 왼팔 바구니 레이어: 바구니를 든 동안만 (빈 상태가 팔을 기본 자세로 덮지 않게 — BRANCH_ANIMATOR 10/16 ①)
                if (basketLayer >= 0)
                {
                    float w = Mathf.MoveTowards(animator.GetLayerWeight(basketLayer), hasBasket ? 1f : 0f,
                        basketLayerBlend > 0f ? Time.deltaTime / basketLayerBlend : 1f);
                    animator.SetLayerWeight(basketLayer, w);
                }
            }

            // 발소리: 걷기 클립에 OnFootstep 이벤트가 없으면 걸은 거리로
            if (footstepEvents)
                return;
            stepTravel += speed * Time.deltaTime;
            if (stepTravel >= stepDistance)
            {
                stepTravel -= stepDistance;
                AudioManager.Play(SoundCues.CustomerStep, transform.position);
            }
        }

        // 튜토리얼이 진행 중인 동안만 손님 데이터의 대사를 머리 위 말풍선으로 (기획 튜토리얼 4 · 5절 — 피피 등장 · 구매 · 퇴장)
        private void Say(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || !Systems.Tutorial.TutorialSystem.IsActive)
                return;
            UI.SpeechBubbleView.Say(transform, (data != null ? data.Height : 1.6f) + speechHeight, line, speechDuration);
        }

        private IEnumerator Run(SalesSettingsData settings)
        {
            state = CustomerState.Entering;
            Say(data.EnterLineFor(GameManager.Instance != null ? GameManager.Instance.Day : 1));
            yield return WalkTo(system.BasketStandPosition);

            state = CustomerState.PickingBasket;
            SetTrigger(PickBasketId);
            yield return WaitAnimation(pickBasketFallback);
            if (basket != null)
                basket.gameObject.SetActive(true);
            AudioManager.Play(SoundCues.BasketPick, transform.position);
            hasBasket = true;
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
            if (items.Count > 0)
                Say(data.BuyLine);
            for (int i = 0; i < items.Count; i++)
            {
                SetTrigger(PutItemId);
                // 집는 순간(OnItemTake)이 아니라 바구니에 넣는 순간(OnItemPlace)까지 — 넣는 동작이 잘리지 않게 (BRANCH_ANIMATOR 10/22 ①)
                yield return WaitPlace(putItemFallback);
                ShowBasketItem(items[i]);
                AudioManager.Play(SoundCues.BasketPut, transform.position);
            }
            SetBool(BrowsingId, false);

            if (items.Count == 0)
            {
                // 못 사면 계산을 건너뛰고 바로 퇴장 — 머리 위 실망 아이콘 (구매 실패는 만족도로 다음 날 손님에 반영)
                if (disappointedIcon != null)
                    disappointedIcon.SetActive(true);
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
            AudioManager.Play(SoundCues.CustomerHappy, transform.position);
            if (face != null)
                face.SetHappy(true);
            yield return HappyRoutine();
            if (face != null)
                face.SetHappy(false);

            yield return LeaveStore();
        }

        private IEnumerator LeaveStore()
        {
            state = CustomerState.Leaving;
            if (paid && items != null && items.Count > 0)
                Say(data.LeaveLine);
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

        private IEnumerator WaitPlace(float fallback)
        {
            int before = placeEvents;
            float start = Time.time;
            while (placeEvents == before && Time.time - start < fallback)
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
                if (model != null)
                    model.localPosition = modelBase + Vector3.up * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f)) * happyHop;
                yield return null;
            }
            if (model != null)
                model.localPosition = modelBase;
        }

        private void ShowBasketItem(CropData crop)
        {
            if (basket == null || basketItemPrefab == null || crop == null)
                return;
            var r = Instantiate(basketItemPrefab, basketContents != null ? basketContents : basket);
            int i = basketItems.Count;
            // 장바구니 루트는 크기 1 (모델 자식만 납작한 상자) — 상품은 바구니 위에 나란히
            r.transform.localPosition = new Vector3((i % 3 - 1) * 0.09f, 0.1f + i / 3 * 0.08f, 0f);
            r.transform.localScale = Vector3.one * 0.09f;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, crop.GreyboxColor);
            r.SetPropertyBlock(block);
            Systems.Items.ItemModelView.Apply(r, crop, i);
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
        // 리그 모델은 Animator가 모델 쪽에 있어 CustomerAnimationEvents가 여기로 넘긴다.
        public void OnBasketAttach() => animEvents++;
        public void OnItemTake() => animEvents++;
        public void OnItemPlace() { animEvents++; placeEvents++; }
        public void OnHappyGlow() => animEvents++;

        public void OnFootstep()
        {
            footstepEvents = true; // 이제부터 걸은 거리 발소리는 끈다
            AudioManager.Play(SoundCues.CustomerStep, transform.position);
        }
    }
}
