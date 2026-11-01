# ID_MAP.md — 기획 id ↔ 개발 id 대응표

> 결정 (2026-10-21, 사용자): **개발 에셋 id는 요청서에서 정한 그대로 유지**하고, 기획 CSV(`CSV/plan/`)의 id는 이 표로 연결한다.
> 이 표는 사람이 읽는 문서이면서 `Glow Farm/Data/Import Plan CSV`(기획 CSV 가져오기)가 그대로 읽는 설정이다 — 표를 고치면 다음 가져오기부터 반영된다.

## 읽는 법
- **표에 없는 기획 id는 개발 id가 같다** (예: `crop_moonroot`, `shop_seed_moonroot`, `skill_unlock_mushroom`).
- 상태
  - `연결` — 기획 값을 개발 에셋에 넣고 화면에 보인다.
  - `숨김` — 값은 넣지만 상점 · 스킬 화면에 보이지 않는다 (시스템이 아직 없거나 연결 전). 시스템을 붙이면 `연결`로 바꾼다.
  - `없음` — 개발 에셋이 없다. 가져오기가 건너뛰고 보고만 한다.
  - `개발만` — 기획에 없는 개발 에셋. 가져올 때 숨긴다.
- 기획 CSV의 `mvp = phase2` 행은 표와 관계없이 숨기거나 건너뛴다 (회신2 2-6 "2차는 상점 · 스킬트리에 표시하지 않음").
- 변환 `1+값 배율`: 기획의 보너스(add 0.05 = +5%)를 개발 배율(× 1.05)로 바꾼다.

## 작물 · 수확물 · 씨앗
개발은 작물 에셋(`CropData`) 하나가 수확물 아이템을 겸한다 → 기획의 수확물 id(`item_*`)는 작물 id로 연결한다.

| 종류 | 기획 id | 개발 id | 상태 | 변환 | 메모 |
|---|---|---|---|---|---|
| 수확물 | item_moonroot | crop_moonroot | 연결 | | 판매가 · 칸당 개수는 작물 에셋에 |
| 수확물 | item_mushroomflower | crop_mushroomflower | 연결 | | |
| 수확물 | item_lanternfruit | crop_lanternfruit | 연결 | | |
| 씨앗 | seed_moonroot | item_seed_moonroot | 연결 | | |
| 씨앗 | seed_mushroomflower | item_seed_mushroomflower | 연결 | | |
| 씨앗 | seed_lanternfruit | item_seed_lanternfruit | 연결 | | |

## 도구 · 아이템
| 종류 | 기획 id | 개발 id | 상태 | 변환 | 메모 |
|---|---|---|---|---|---|
| 도구 | tool_hoe_t1 | tool_hoe_1 | 연결 | | 낡은 괭이 (1회 작업량 1) |
| 도구 | tool_hoe_t2 | tool_hoe_2 | 연결 | | 철 괭이 (2) |
| 도구 | tool_hoe_t3 | tool_hoe_3 | 연결 | | 발광 괭이 (3) |
| 도구 | tool_watering_can_t1 | tool_watering_1 | 연결 | | 1칸 |
| 도구 | tool_watering_can_t2 | tool_watering_2 | 연결 | | 4칸 |
| 도구 | tool_watering_can_t3 | tool_watering_3 | 연결 | | 8칸 |
| 도구 | tool_watering_can_1 | tool_watering_1 | 연결 | | 10/22 묶음부터 기획 CSV가 개발 id 규칙(`_1`)을 쓰는데 물뿌리개만 이름이 다름 |
| 도구 | tool_watering_can_2 | tool_watering_2 | 연결 | | 〃 |
| 도구 | tool_watering_can_3 | tool_watering_3 | 연결 | | 〃 |
| 도구 | - | tool_watering_4 | 개발만 | | 4등급 폐기 (회신 #6) — 에셋 삭제함 |
| 도구 | tool_mop_t1 | tool_mop_1 | 연결 | | |
| 도구 | tool_mop_t2 | tool_mop_2 | 연결 | | |
| 도구 | tool_mop_t3 | tool_mop_3 | 연결 | | |
| 장치 | device_sprinkler | - | 없음 | | 스프링클러 (P1) — 설치 시스템 대기 |
| 가구 | furn_fruit_shelf | furn_fruit_shelf | 연결 | | 과일 진열대 (P0, P-4) — 1층 낮은 판매대 · 한 종류 · 작물 display_per_slot(30)개. `FurnitureData` |

## 상점 상품
| 종류 | 기획 id | 개발 id | 상태 | 변환 | 메모 |
|---|---|---|---|---|---|
| 상품 | shop_plot_expand | shop_plot_expand | 연결 | | 흩어진 밭 (2026-10-30): 단계마다 확장 밭 자리(FarmFieldSite) 하나 — 시작 밭과 같은 32칸. 자리 수만큼 단계 (10/30 shop.csv 5단계 → 길 끝마다 5곳, 2026-11-01) |
| 상품 | shop_fruit_shelf | - | 없음 | | 과일 진열대 추가 구매 (P1) — 빈 자리에 설치하는 구매 효과 대기 |
| 상품 | shop_device_sprinkler | - | 없음 | | 스프링클러 시스템 대기 |
| 상품 | - | shop_shelf_expand | 개발만 | | 진열 칸 확장 → 기획은 과일 진열대 추가 구매로 바뀜 |

## 상점 분류 (탭) — 회신3 2-4: 5개 탭
| 종류 | 기획 id | 개발 id | 상태 | 변환 | 메모 |
|---|---|---|---|---|---|
| 상점 분류 | seed | 씨앗 | 연결 | | |
| 상점 분류 | tool | 도구 | 연결 | | |
| 상점 분류 | expand | 확장 | 연결 | | |
| 상점 분류 | furniture | 가구 | 연결 | | |
| 상점 분류 | consumable | 소모품 · 장치 | 연결 | | 한 탭 |
| 상점 분류 | device | 소모품 · 장치 | 연결 | | 한 탭 |

## 스킬
P0 = 버섯꽃 · 등불 해금. 나머지 데모 스킬은 P1 — 효과가 연결된 것만 보이고, 연결 전은 숨김.

| 종류 | 기획 id | 개발 id | 상태 | 변환 | 메모 |
|---|---|---|---|---|---|
| 스킬 | skill_giant_dream | skill_giant_dream | 숨김 | | 거대 작물(4-4) 대기 |
| 스킬 | skill_seed_stock | skill_seed_stock | 숨김 | | 씨앗 입고 단계(seed_stock_level) 연결 전 |
| 스킬 | skill_unlock_sprinkler | skill_unlock_sprinkler | 숨김 | | 스프링클러 대기 |
| 스킬 | skill_clean_keep | skill_clean_keep | 연결 | | 흩어진 상품 · 흐트러짐 · 얼룩 생성 배율 (가게 정리 P-3에서 연결) |
| 스킬 | skill_signboard | skill_signboard | 연결 | | 광고 계수 p (입소문 손님 모델 P-2에서 연결) |
| 스킬 | skill_trash_compact | skill_trash_compact | 숨김 | | 아이템별 칸당 개수(stack_size) 연결 전 |
| 스킬 | skill_regular_basket | skill_regular_basket | 연결 | | 손님마다 1개 더 살 확률 (P-2에서 연결) |
| 스킬 | skill_light_steps | skill_light_steps | 숨김 | | move_speed 연결 전 |
| 스킬 | skill_fertile | skill_fertile | 숨김 | | 기획은 2차(재수확 −1) — 개발 임시 효과(성장일 −1)와 다름 |
| 스킬 | - | skill_quality | 개발만 | | 고품질 작물 (개발 임시) → 기획 '신선 작물 프리미엄'으로 대체 |

## 능력치 (skill_effects.csv stat_id)
| 종류 | 기획 id | 개발 id | 상태 | 변환 | 메모 |
|---|---|---|---|---|---|
| 능력치 | hotbar_slots | stat_hotbar_slots | 연결 | | set 5 → 핫바 5칸 |
| 능력치 | sell_price_raw | stat_sell_price | 연결 | 1+값 배율 | 신선 작물 프리미엄 |
| 능력치 | seed_stock_level | - | 없음 | | |
| 능력치 | giant_chance | - | 없음 | | |
| 능력치 | mess_rate | stat_mess_rate | 연결 | | 기본 1, 청결 유지가 곱함 (0.9 · 0.8 …) |
| 능력치 | customer_ad_rate | stat_customer_ad_rate | 연결 | | 기본값 = SalesSettings 광고 계수(0.02)에 더함 |
| 능력치 | extra_buy_chance | stat_extra_buy_chance | 연결 | | 회신3 2-3 (purchase_count에서 이름 바뀜), 기본 0 |
| 능력치 | stack_size | - | 없음 | | |
| 능력치 | move_speed | - | 없음 | | |
| 능력치 | - | stat_grow_days | 개발만 | | 쓰는 스킬 없음 (밭은 계속 읽음) |
| 능력치 | - | stat_customer_bonus | 개발만 | | 쓰는 곳 없음 (예전 손님 수 공식 — P-2에서 입소문 모델로 바뀜) |

## 설정 (game_settings.csv) — 어디로 들어가는지
가져오기 코드가 키마다 처리한다 (이 표는 설명용, 상태만 보고에 쓴다).

| 종류 | 기획 id | 개발 id | 상태 | 변환 | 메모 |
|---|---|---|---|---|---|
| 설정 | start_money | GameStartSettings.startingMoney | 연결 | | |
| 설정 | start_hotbar | GameStartSettings.startingHotbarItems | 연결 | | 위 도구 대응으로 |
| 설정 | start_bag | - | 연결 | | 비어 있어야 함 (개발은 시작 가방 칸이 없음) |
| 설정 | start_storage | GameStartSettings.startingStorageItems | 연결 | | |
| 설정 | start_beds | GameStartSettings.startPlotCount | 연결 | | 밭 1개 = 4칸, 구획 1개 = 16칸 → 8밭 = 2구획 |
| 설정 | level_xp_base | Progression.levelUpBase | 연결 | | 기본 = base − step |
| 설정 | level_xp_step | Progression.levelUpPerLevel | 연결 | | |
| 설정 | sp_per_level | Progression.skillPointsPerLevel | 연결 | | |
| 설정 | xp_per_revenue | Progression.expPerRevenue | 연결 | | |
| 설정 | buy_min | SalesSettings.purchaseMin | 연결 | | |
| 설정 | buy_max | SalesSettings.purchaseMax | 연결 | | |
| 설정 | sale_duration | SalesSettings.salesDuration | 연결 | | |
| 설정 | start_fruit_shelves | GameStartSettings.startFruitShelves | 연결 | | P-4: 가게 배치 메뉴 · 측정 봇이 읽음. store_width · store_depth · store_aisle_width · store_spots_* · trash_bin_location은 가게 배치(Build Shop Layout)의 기준값 — 메뉴가 레벨 배치도 14 좌표로 놓음 |
| 설정 | day1_customers | SalesSettings (손님 수 — 입소문 모델) | 연결 | | P-2: day1_* · customer_* · satisfaction_* · market_day_* · same_day_clean_min · buy_extra_max · store_spots_fruit + store_spots_shelf(가구 자리) 모두 |
| 설정 | trash_day1 | CleaningSettings (가게 정리) | 연결 | | P-3: trash_day1 · stain_day1 · trash_daily_* · stain_per_customer · scatter_* · disorder_ratio · grade_* (disorder_buy_mult는 SalesSettings). 매장 확장용 trash_expand_* · stain_expand는 2차 |
