# 상점 CSV 양식 (2026-10-13)

스킬 CSV(skill_csv)와 같은 규칙: UTF-8(BOM), 첫 줄 열 이름, 코드는 열 이름으로 읽음, 목록은 `;`, 해금은 `스킬id:단계`.
기획서 12.5의 ShopItemData 필드(id · 분류 · 단계별 가격 배열 · 최대 구매 횟수 · 일일 재고 · 해금 조건 · 효과)를 그대로 옮겼다.

## shop.csv — 상품 1개 = 1행

| 열 | 형식 | 설명 | 예 |
|---|---|---|---|
| shop_id | 문자 | 바뀌지 않는 id (스킬 해금 대상과 연결) | shop_plot_expand |
| category | seed · tool · expand · consumable · device · processor · furniture | 상점 탭 분류 | expand |
| name_ko / description | 문자 | 화면 표시 | 밭 확장권 |
| price_per_step | `;` 목록 | 구매할 때마다의 가격. 반복 구매 상품은 값 1개 | `1500;4000;10000;25000` |
| max_purchases | 정수 | 최대 구매 횟수. **0 = 무제한**(씨앗 · 비료 · 기구 · 장치) | 4 |
| grant_kind | 문자 | 구매 시 주는 것: item 아이템 / tool_tier 도구 등급 +1 / plot_cells 밭 칸 / bag_slots 가방 칸 / store_size 매장 가구 자리 | plot_cells |
| grant_target | 문자 | 대상 아이템 · 도구 id | farm |
| grant_value_per_step | `;` 목록 | 구매할 때마다 주는 양 (개수는 max_purchases와 같음, 무제한이면 1개) | `16;32;64;112` |
| stock_base / stock_max | 정수 | 씨앗 일일 입고량 기본 · 최대 (씨앗만) | 10 / 24 |
| unlock | `스킬id:단계;…` | 상점에 나타나는 조건. 비우면 처음부터 | skill_unlock_lantern:1 |
| scope | demo · phase2 | 데모 포함 여부 | demo |

## 코드가 읽는 방법
1. SkillCsvLoader와 같은 파서로 행을 읽고, `price_per_step` · `grant_value_per_step`을 배열로 바꾼다.
2. **표시 조건**: unlock의 스킬 단계를 모두 만족하면 상점에 표시.
3. **단계형 상한**: 물뿌리개 · 가방처럼 스킬이 단계별로 여는 상품은 skill_effects.csv의 `unlock_tier`(value = 살 수 있는 최대 단계)로 막는다. 구매 횟수 < 열린 단계일 때만 구매 버튼 활성.
4. **가격**: 구매 횟수 n(0부터)에 대해 `price_per_step[n]`. 무제한 상품은 항상 `price_per_step[0]`.
5. **씨앗 입고**: 입고량 = stock_base + round((stock_max − stock_base) × 풍성한 입고 단계 ÷ 4). 매일 아침 입고 초기화. **unlock 조건을 만족하기 전에는 입고 0**, 만족한 다음 날 아침부터 입고.
6. **검증**: 가격 · 지급값 개수 = max_purchases, unlock의 스킬 id 존재 여부. 현재 데이터 18종 검증 통과.

## 현재 데이터
상품 18종 (mvp 열 기준 P0 3 · P1 8 · 2차 7: 나선층꽃 · 진주핵꽃 씨앗, 가공 기구 3종, 진열대, 매장 확장). **mvp = phase2 행은 상점에 표시하지 않음.** 데모에서 뺀 상품: 비료 분사기 · 자동 수확기 · 씨앗 추출기 · 매장 확장 · 발광 조명 · 지역 이용권.
grant_kind 이름은 수치 id처럼 **임시**이며, 개발과 맞출 때 CSV와 코드의 종류 목록을 함께 바꾼다.
