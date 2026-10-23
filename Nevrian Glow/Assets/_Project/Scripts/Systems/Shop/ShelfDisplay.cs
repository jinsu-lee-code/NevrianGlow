using System.Collections.Generic;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Shop;
using NevrianGlowFarm.Systems.Interaction;
using NevrianGlowFarm.Systems.Player;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Shop
{
    /// <summary>
    /// 진열대 (노션 GDD 12.4, GDD 4-3). 작물을 들고 보면 조준한 칸의 다음 자리에 홀로그램(가능 시안 / 불가 빨강),
    /// 좌클릭 1개 놓기(놓기 동작 0.15초), 우클릭 1개 회수. 칸마다 한 종류 · 칸당 4개 · 기본 6칸 (InventorySettings).
    /// 규칙은 Logic ShelfState, 장사(3-1)는 TryTakeForSale로 마지막에 놓은 것부터 가져간다.
    /// 칸은 로컬 x로 같은 간격 · 가운데 정렬, 각 칸의 쌓는 자리(stack point)는 2열 격자. 로컬 +Z = 손님 쪽.
    /// </summary>
    public class ShelfDisplay : MonoBehaviour, IItemUseTarget, ISecondaryUseTarget, IAimPointTarget
    {
        [Header("참조")]
        [Tooltip("달라붙는 거리 · 놓기 시간 (Data/Stats/InventorySettings) — 가구 데이터가 없으면 칸 수 · 칸당 개수도")]
        [SerializeField] private InventorySettingsData settings;
        [Tooltip("진열 가구 종류 (Data/Items/furn_fruit_shelf) — 층 수 · 층당 개수를 정한다 (P-4)")]
        [SerializeField] private FurnitureData furniture;
        [Tooltip("작물 id → 작물 데이터 (Data/ContentDatabase)")]
        [SerializeField] private ContentDatabase database;
        [Tooltip("진열대 몸체 — 칸 수에 맞춰 가로 길이를 바꾼다")]
        [SerializeField] private Transform body;
        [Tooltip("조준 콜라이더 — 몸체와 같이 늘린다")]
        [SerializeField] private BoxCollider aimCollider;
        [Tooltip("놓을 자리 홀로그램 (Holograms 패키지 셰이더 재질)")]
        [SerializeField] private Renderer hologram;
        [Tooltip("진열된 작물 하나의 겉모습 프리팹 (Prefabs/Shop/shelf_item, 콜라이더 없음)")]
        [SerializeField] private Renderer itemPrefab;

        [Header("조정값")]
        [Tooltip("칸 사이 간격 (m)")]
        [SerializeField, Min(0.2f)] private float slotPitch = 1f;
        [Tooltip("진열대 윗면 높이 (m)")]
        [SerializeField, Min(0f)] private float topHeight = 1f;
        [Tooltip("쌓는 자리 사이 간격 (m)")]
        [SerializeField, Min(0.05f)] private float stackSpacing = 0.32f;
        [Tooltip("쌓는 자리 열 수 — 칸당 개수를 이 열로 나눠 앞뒤 줄로 놓는다")]
        [SerializeField, Min(1)] private int stackColumns = 2;
        [Tooltip("진열된 작물 크기 (m)")]
        [SerializeField, Min(0.05f)] private float itemSize = 0.24f;
        [Tooltip("줄마다 (놓는 면 높이, 앞뒤 위치 z) — 0번 = 앞줄(손님 쪽). 층 · 경사 진열대(아트 2026-10-22)용, 비우면 topHeight 한 면에 stackSpacing 간격 (m)")]
        [SerializeField] private List<Vector2> rowLayout = new List<Vector2>();
        [Tooltip("놓을 수 있을 때 홀로그램 색 — 알파 = 투명도, 밝기는 재질의 Emission Scale (시안 #62F2E1)")]
        [SerializeField] private Color okColor = new Color(0.38f, 0.95f, 0.88f, 0.35f);
        [Tooltip("놓을 수 없을 때 홀로그램 색 (빨강)")]
        [SerializeField] private Color blockedColor = new Color(1f, 0.25f, 0.25f, 0.35f);
        [Tooltip("흐트러진 칸의 작물 기울기 (도) — 가게 정리 P-3")]
        [SerializeField, Range(0f, 60f)] private float disorderTilt = 28f;
        [Tooltip("흐트러진 칸의 작물이 제자리에서 벗어나는 정도 (m)")]
        [SerializeField, Range(0f, 0.3f)] private float disorderOffset = 0.08f;

        [Header("기즈모")]
        [Tooltip("칸 자리(노랑) 표시")]
        [SerializeField] private bool drawSlots = true;

        [Header("디버그")]
        [Tooltip("조준한 칸 (−1 = 없음, 읽기 전용)")]
        [SerializeField] private int aimedSlot = -1;
        [Tooltip("진열 내용 (읽기 전용)")]
        [SerializeField] private List<string> contentsView = new List<string>();

        private ShelfState state;
        private readonly List<List<Renderer>> views = new List<List<Renderer>>();
        private readonly List<(Transform Item, Vector3 To, float Time)> placing = new List<(Transform, Vector3, float)>();
        private CropData aimedCrop;
        private PlaceCheck aimedCheck = PlaceCheck.NoSlot;
        private MaterialPropertyBlock block;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int HologramColorId = Shader.PropertyToID("_Hologram_Color");

        public ShelfState State => state;
        public int AimedSlot => aimedSlot;
        public FurnitureData Furniture => furniture;

        // 가구 데이터가 있으면 그 값 (과일 진열대 1층 × 30), 없으면 예전 진열대 설정
        private int BaseSlots => furniture != null ? furniture.Layers : settings.ShelfBaseSlots;
        private int ItemsPerSlot => furniture != null ? furniture.ItemsPerLayer : settings.ShelfItemsPerSlot;
        // 칸이 넓으면(과일 진열대 한 대 = 한 칸) 칸 어디를 조준해도 그 칸
        private float SnapDistance => Mathf.Max(settings.ShelfSnapDistance, slotPitch * 0.5f);

        private void Awake()
        {
            if (settings == null || database == null || body == null || aimCollider == null || hologram == null || itemPrefab == null)
            {
                Debug.LogError("[ShelfDisplay] 참조가 비어 있음 — 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            block = new MaterialPropertyBlock();
            state = new ShelfState(BaseSlots, ItemsPerSlot);
            hologram.gameObject.SetActive(false);
            FitBody();
            RefreshAll();
        }

        private void Update()
        {
            // 놓기 동작: 홀로그램 자리 위에서 내려앉으며 커진다 (0.15초).
            for (int i = placing.Count - 1; i >= 0; i--)
            {
                var (item, to, start) = placing[i];
                float t = settings.PlaceDuration > 0f ? Mathf.Clamp01((Time.time - start) / settings.PlaceDuration) : 1f;
                if (item != null)
                {
                    item.localPosition = Vector3.Lerp(to + Vector3.up * itemSize, to, t);
                    item.localScale = Vector3.one * (itemSize * Mathf.Lerp(0.4f, 1f, t));
                }
                if (t >= 1f)
                    placing.RemoveAt(i);
            }
        }

        // ── 조준 ──────────────────────────────────────────

        public bool UpdateAim(Vector3 point, ItemData held)
        {
            int slot = ShelfLayout.NearestSlot(transform.InverseTransformPoint(point).x, state.SlotCount, slotPitch, SnapDistance);
            var crop = held as CropData;
            var check = crop != null ? state.CanPlace(slot, crop.Id) : PlaceCheck.NoSlot;
            bool changed = slot != aimedSlot || crop != aimedCrop || check != aimedCheck;
            aimedSlot = slot;
            aimedCrop = crop;
            aimedCheck = check;
            if (changed)
                ShowHologram();
            return changed;
        }

        public void ClearAim()
        {
            aimedSlot = -1;
            aimedCrop = null;
            aimedCheck = PlaceCheck.NoSlot;
            if (hologram != null)
                hologram.gameObject.SetActive(false);
        }

        private void ShowHologram()
        {
            bool show = aimedCrop != null && aimedSlot >= 0;
            hologram.gameObject.SetActive(show);
            if (!show)
                return;
            int count = state[aimedSlot].Count;
            // 놓을 수 있으면 다음 자리, 가득 차 있으면 마지막 자리 위에 빨강
            int index = aimedCheck == PlaceCheck.Full ? count - 1 : Mathf.Min(count, state.ItemsPerSlot - 1);
            hologram.transform.localPosition = StackPoint(aimedSlot, index);
            hologram.transform.localScale = Vector3.one * itemSize;
            block.Clear();
            block.SetColor(HologramColorId, aimedCheck == PlaceCheck.Ok ? okColor : blockedColor);
            hologram.SetPropertyBlock(block);
        }

        // ── 좌클릭: 놓기 ─────────────────────────────────

        public string GetUsePrompt(ItemData held)
        {
            if (!(held is CropData crop) || aimedSlot < 0)
                return null;
            bool messy = state.IsDisordered(aimedSlot);
            switch (state.CanPlace(aimedSlot, crop.Id))
            {
                case PlaceCheck.Ok: return messy
                    ? $"{crop.DisplayName} 다시 진열 - 흐트러진 칸 바로잡기 ({state[aimedSlot].Count}/{state.ItemsPerSlot})"
                    : $"{crop.DisplayName} 진열 ({state[aimedSlot].Count}/{state.ItemsPerSlot})";
                case PlaceCheck.OtherItem: return $"진열 불가 - 다른 작물 칸 ({NameOf(state[aimedSlot].ItemId)})";
                case PlaceCheck.Full: return messy ? "흐트러진 칸 - 우클릭으로 1개 회수한 뒤 다시 진열" : "진열 불가 - 칸이 가득 참";
                default: return null;
            }
        }

        public void Use(ItemData held)
        {
            if (!(held is CropData crop) || aimedSlot < 0 || state.CanPlace(aimedSlot, crop.Id) != PlaceCheck.Ok)
                return;
            var hotbar = PlayerHotbar();
            if (hotbar == null || !hotbar.TryConsumeSelected(1))
                return;
            state.TryPlace(aimedSlot, crop.Id);
            state.Straighten(aimedSlot); // 다시 진열하면 그 칸은 반듯해진다 (청소 v2 — 홀로그램 스냅)
            RefreshSlot(aimedSlot, animateLast: true);
            AudioManager.Play(SoundCues.ShelfPlace);
            VfxManager.Play(VfxCues.Sparkle, transform.TransformPoint(StackPoint(aimedSlot, state[aimedSlot].Count - 1)));
            // 홀로그램은 다음 자리로 (가득 차면 빨강). 마지막 1개를 놓아 손이 비면 다음 프레임 UpdateAim이 숨긴다.
            aimedCheck = state.CanPlace(aimedSlot, crop.Id);
            ShowHologram();
        }

        // ── 우클릭: 회수 ─────────────────────────────────

        public string GetSecondaryPrompt(ItemData held)
        {
            if (aimedSlot < 0 || state[aimedSlot].IsEmpty)
                return null;
            var item = database.Get<ItemData>(state[aimedSlot].ItemId);
            var inventory = PlayerInventory.Instance;
            string messy = state.IsDisordered(aimedSlot) ? " (흐트러짐 - 회수 뒤 다시 진열하면 바로잡힘)" : string.Empty;
            return inventory != null && inventory.CanAdd(item, 1)
                ? $"{NameOf(state[aimedSlot].ItemId)} 1개 회수{messy}"
                : $"{NameOf(state[aimedSlot].ItemId)} 회수 불가 - 넣을 칸 없음";
        }

        public void UseSecondary(ItemData held)
        {
            var inventory = PlayerInventory.Instance;
            if (aimedSlot < 0 || state[aimedSlot].IsEmpty || inventory == null)
                return;
            var item = database.Get<ItemData>(state[aimedSlot].ItemId);
            if (!inventory.CanAdd(item, 1))
                return;
            state.TakeOne(aimedSlot);
            inventory.TryAdd(item, 1);
            RefreshSlot(aimedSlot, animateLast: false);
            AudioManager.Play(SoundCues.ShelfTake);
            aimedCheck = aimedCrop != null ? state.CanPlace(aimedSlot, aimedCrop.Id) : PlaceCheck.NoSlot;
            ShowHologram();
        }

        // ── 장사 (3-1) · 확장 (3-3) ─────────────────────

        /// <summary>손님이 한 칸에서 1개 가져간다 (마지막에 놓은 것부터). 가져간 작물, 비었으면 null.</summary>
        public CropData TryTakeForSale(int slot)
        {
            string id = state.TakeOne(slot);
            if (id == null)
                return null;
            RefreshSlot(slot, animateLast: false);
            return database.Get<CropData>(id);
        }

        // ── 가게 정리 (P-3) ─────────────────────────────

        /// <summary>칸을 흐트러뜨린다 (상품이 있는 칸만). 흐트러졌으면 true.</summary>
        public bool SetDisordered(int slot)
        {
            state.SetDisordered(slot);
            if (!state.IsDisordered(slot))
                return false;
            RefreshSlot(slot, animateLast: false);
            return true;
        }

        /// <summary>흐트러짐을 모두 지운다 (새 날 배치 전).</summary>
        public void ClearDisorder()
        {
            if (state.DisorderedCount == 0)
                return;
            state.ClearDisorder();
            RefreshAll();
        }

        /// <summary>작물 1개를 놓을 수 있는 칸에 되돌린다 (같은 작물 칸 → 빈 칸 순). 놓았으면 true.</summary>
        public bool TryReturn(CropData crop)
        {
            if (crop == null)
                return false;
            for (int pass = 0; pass < 2; pass++)
                for (int slot = 0; slot < state.SlotCount; slot++)
                {
                    bool same = state[slot].ItemId == crop.Id;
                    if ((pass == 0) != same || state.CanPlace(slot, crop.Id) != PlaceCheck.Ok)
                        continue;
                    state.TryPlace(slot, crop.Id);
                    RefreshSlot(slot, animateLast: false);
                    return true;
                }
            return false;
        }

        /// <summary>진열 칸을 늘린다 (진열대 확장 상품). 몸체도 길어진다.</summary>
        public void SetSlotCount(int count)
        {
            state.SetSlotCount(count);
            FitBody();
            RefreshAll();
        }

        /// <summary>저장 내용(칸 수 · 칸마다 작물)으로 바꾼다 (6-1). 칸이 줄면 남는 칸의 겉모습을 지운다.</summary>
        public void LoadSlots(IReadOnlyList<Logic.Inventory.ItemStack> stacks)
        {
            // 가구는 층 수가 정해져 있다 — 그보다 많은 칸(예전 6칸 진열대 저장)은 앞 칸만
            if (furniture != null && stacks != null && stacks.Count > BaseSlots)
            {
                var trimmed = new List<Logic.Inventory.ItemStack>();
                for (int i = 0; i < BaseSlots; i++)
                    trimmed.Add(stacks[i]);
                stacks = trimmed;
            }
            state.Load(stacks);
            if (state.SlotCount < BaseSlots)
                state.SetSlotCount(BaseSlots);
            for (int slot = state.SlotCount; slot < views.Count; slot++)
            {
                foreach (var r in views[slot])
                    if (r != null)
                        Destroy(r.gameObject);
                views[slot].Clear();
            }
            placing.Clear();
            FitBody();
            RefreshAll();
        }

        // ── 표시 ─────────────────────────────────────────

        private void FitBody()
        {
            float width = state.SlotCount * slotPitch + 0.2f;
            var s = body.localScale;
            body.localScale = new Vector3(width, s.y, s.z);
            var size = aimCollider.size;
            aimCollider.size = new Vector3(width, size.y, size.z);
        }

        private void RefreshAll()
        {
            for (int slot = 0; slot < state.SlotCount; slot++)
                RefreshSlot(slot, animateLast: false);
        }

        private void RefreshSlot(int slot, bool animateLast)
        {
            while (views.Count <= slot)
                views.Add(new List<Renderer>());
            var list = views[slot];
            var stack = state[slot];
            var crop = stack.IsEmpty ? null : database.Get<CropData>(stack.ItemId);

            while (list.Count < stack.Count)
            {
                var r = Instantiate(itemPrefab, transform);
                r.name = $"Slot{slot + 1}_Item{list.Count + 1}";
                Items.ItemModelView.Apply(r, crop, slot * 31 + list.Count); // 작물 모델 (한 칸 = 한 종류라 칸이 빌 때까지 그대로)
                list.Add(r);
            }
            while (list.Count > stack.Count)
            {
                var last = list[list.Count - 1];
                list.RemoveAt(list.Count - 1);
                if (last != null)
                    Destroy(last.gameObject);
            }

            bool messy = state.IsDisordered(slot);
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                r.transform.localPosition = StackPoint(slot, i) + (messy ? MessOffset(slot, i) : Vector3.zero);
                // 흐트러진 칸은 작물이 기울고 자리에서 벗어난다 (칸 · 순서로 정해 같은 칸이면 같은 모습)
                r.transform.localRotation = messy
                    ? Quaternion.Euler(((slot * 3 + i) % 2 == 0 ? 1f : -1f) * disorderTilt, (slot * 53 + i * 71) % 360, ((i % 3) - 1) * disorderTilt * 0.6f)
                    : Quaternion.identity;
                r.transform.localScale = Vector3.one * itemSize;
                block.Clear();
                block.SetColor(BaseColorId, crop != null ? crop.GreyboxColor : Color.white);
                r.SetPropertyBlock(block);
            }
            if (animateLast && list.Count > 0)
                placing.Add((list[list.Count - 1].transform, StackPoint(slot, list.Count - 1), Time.time));

            UpdateDebugView();
        }

        /// <summary>칸 slot의 index번째 쌓는 자리 (로컬). 앞줄(손님 쪽 +Z)부터 채운다.</summary>
        private Vector3 StackPoint(int slot, int index)
        {
            int columns = Mathf.Max(1, stackColumns);
            int rows = Mathf.Max(1, Mathf.CeilToInt((float)state.ItemsPerSlot / columns));
            int col = index % columns;
            int row = index / columns;
            float x = (float)ShelfLayout.SlotCenterX(slot, state.SlotCount, slotPitch) + (col - (columns - 1) * 0.5f) * stackSpacing;
            if (rowLayout.Count > 0)
            {
                var r = rowLayout[Mathf.Min(row, rowLayout.Count - 1)];
                return new Vector3(x, r.x + itemSize * 0.5f, r.y);
            }
            float z = ((rows - 1) * 0.5f - row) * stackSpacing;
            return new Vector3(x, topHeight + itemSize * 0.5f, z);
        }

        private Vector3 MessOffset(int slot, int index)
        {
            float a = (slot * 2.399f + index * 1.7f) % (2f * Mathf.PI);
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * disorderOffset;
        }

        private string NameOf(string itemId)
        {
            var item = database.Get<ItemData>(itemId);
            return item != null ? item.DisplayName : itemId;
        }

        private static Hotbar cachedHotbar;
        // 핫바는 Core 씬에 있어 씬 사이 참조를 걸 수 없다 → 처음 쓸 때 한 번 찾는다.
        private static Hotbar PlayerHotbar()
        {
            if (cachedHotbar == null && PlayerInventory.Instance != null)
                cachedHotbar = PlayerInventory.Instance.GetComponent<Hotbar>();
            return cachedHotbar;
        }

        private void UpdateDebugView()
        {
            contentsView.Clear();
            for (int i = 0; i < state.SlotCount; i++)
                contentsView.Add(state[i].IsEmpty ? "-" : $"{state[i].ItemId} ×{state[i].Count}");
        }

        private void OnDrawGizmos()
        {
            if (!drawSlots || settings == null)
                return;
            int slots = state != null ? state.SlotCount : BaseSlots;
            Gizmos.matrix = transform.localToWorldMatrix;
            for (int s = 0; s < slots; s++)
            {
                float x = (float)ShelfLayout.SlotCenterX(s, slots, slotPitch);
                GizmoKit.WireBox(new Vector3(x, topHeight + 0.01f, 0f), new Vector3(slotPitch * 0.9f, 0.02f, stackSpacing * 2f), GizmoKit.HarvestDisplay);
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
