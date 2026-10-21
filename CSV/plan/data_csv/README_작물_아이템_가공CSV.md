# 작물 · 아이템 · 가공 CSV 양식 (2026-10-13)

스킬 · 상점 CSV와 같은 규칙: UTF-8(BOM), 첫 줄 열 이름, 코드는 열 이름으로 읽음, 목록은 `;`, 해금은 `스킬id:단계`. 파서는 skill_csv/SkillCsvLoader.cs의 `ReadTable`을 그대로 쓴다.

## 값은 한 곳에만 (정규화)
| 값 | 들어 있는 파일 |
|---|---|
| 판매가 · 칸당 개수 · 진열 칸당 개수 | **items.csv** (작물 수확물과 가공품을 같은 표에서 관리) |
| 씨앗 가격 · 일일 입고량 | shop_csv/shop.csv |
| 해금 SP · 선행 | skill_csv/skills.csv |
| 성장 · 물 · 수확 · 먹이 · 거대 작물 | **crops.csv** |
| 가공 기구 · 레시피 | **processors.csv**, **recipes.csv** |
| 손님 모델 · 레벨 공식 · 시작 상태 · 정리 수치 · 등급 기준 | **game_settings.csv** (key · value · unit · desc · source) |

같은 숫자를 두 파일에 두지 않으므로 하나만 고치면 된다.

## 공통 열: mvp
crops · items · processors · recipes의 마지막 열 `mvp` = P0 · P1 · phase2 (MVP 우선순위, phase2는 데모에서 숨김).

## game_settings.csv — 설정 값 1개 = 1행
`key` 이름으로 읽는다. 손님 모델(customer_*), 레벨(level_*), 시작 상태(start_*), 쓰레기 · 얼룩 · 흩어짐(trash_* · stain_* · scatter_* · disorder_*), 구매 개수(buy_*), 등급 기준(grade_*), 장사 시간 등.

## items.csv — 아이템 1개 = 1행 (37종)
| 열 | 설명 |
|---|---|
| item_id | 바뀌지 않는 id |
| item_type | seed · crop · processed · tool · consumable · trash · device · processor · furniture |
| sell_price | 판매가. 0 = 팔 수 없음 |
| stack_size | 칸당 개수 (도구 · 기구 1, 나머지 10) |
| display_per_slot | 진열 칸당 개수: 작물 30(과일 진열대 한 대) · 큰 가공품(포대 · 호리한 병) 12 · 작은 가공품(둥근 유리병 · 말린 다발) 30(일반 진열대 한 층) |
| feed_ok | 진주핵꽃 먹이 가능 여부 (월광 뿌리 · 등불 열매 · 나선층 꽃잎) |
| container | 가공품 용기: glass_bottle · sack · bundle · slender_flask |
| tool_kind / tool_tier / tool_power / tool_radius / tool_speed | 도구만. 괭이 power = 1회 작업량(1 · 2 · 3), 물뿌리개 power = 칸 수(1 · 4 · 8), 대걸레 radius · speed |

## crops.csv — 작물 1종 = 1행 (5종)
| 열 | 설명 |
|---|---|
| crop_id / name_ko / name_en | 이름 |
| growth_form | normal · tree |
| is_mushroom | 버섯형 (먹이로 못 씀) |
| seed_item_id / product_item_id | items.csv와 연결 |
| grow_days | 첫 수확까지 일수 |
| needs_water / wet_days | 물 필요 여부 · 물 한 번에 유지 일수 |
| yield_base / harvest_count / regrow_days | 1회 수확량 · 총 수확 횟수 · 재수확 간격 |
| yield_growth_per_day / yield_max | 다 자란 뒤 물 준 날마다 수확량 증가 · 최대 (나선층꽃) |
| requires_feed / feed_rule / digest_days / water_after_mature | 먹이형 (진주핵꽃) |
| can_be_giant / giant_chance / giant_yield_mult | 거대 작물 |
| stage_thresholds | 성장 단계가 바뀌는 진행률 |
| glow_color | 대표 발광색 |

## processors.csv — 가공 기구 1종 = 1행 (3종)
| 열 | 설명 |
|---|---|
| processor_id / item_id | 기구 id와 설치용 아이템 id (같은 값) |
| mode | per_item(재료 1개 → 1개) · combine(재료 2개 → 1개) |
| input_slots / batches_per_day / process_days | 넣는 칸 · 하루 처리 수 · 처리 일수 (스킬 "가공 숙련"이 stat processor_slots로 늘림) |
| placement | farm_free = 밭 바깥 빈 땅 자유 설치 |

## recipes.csv — 레시피 1개 = 1행 (10종)
| 열 | 설명 |
|---|---|
| recipe_id / processor_id | 레시피와 기구 |
| input_1 · qty_1 / input_2 · qty_2 | 재료 (두 번째는 증류관만) |
| output_item_id / output_qty | 완성품 |
| unlock | 추가 해금 조건 (증류관 레시피 3종만, 나머지는 기구만 있으면 사용) |

레시피별 늘어난 가치(완성품 − 재료): 월광즙 +12 · 등불즙 +55 · 진주즙 +100 · 말린 버섯꽃 +15 · 말린 꽃잎 +15 · 포자 가루 +75 · 꽃잎 가루 +135 · 향유 +260 · 영양 주스 +495 · 진주핵 영양주 +1,110.

## 코드가 읽는 순서
1. **items.csv** → 아이템 사전 (다른 파일이 모두 아이템 id를 참조하므로 먼저)
2. **crops.csv** → 씨앗 · 수확물 아이템 연결
3. **processors.csv** → **recipes.csv** (재료 · 완성품 아이템, 기구 연결)
4. **skill_csv**, **shop_csv** (해금 대상: 상점 상품 · 레시피 id)
5. **상호 검증**: 모든 id 참조가 존재하는지, 해금 대상이 실제로 있는지 확인 → 현재 CSV 9개 전체 **오류 없음** (validate_plan.py로 검사)

## 미정 · 임시
- item_type · container · mode · feed_rule 같은 종류 이름은 임시 — 개발과 맞출 때 CSV와 코드 목록을 함께 바꾼다
- 아이템 · 작물 · 레시피 id는 저장 데이터와 연결되므로 확정 후 변경 금지
