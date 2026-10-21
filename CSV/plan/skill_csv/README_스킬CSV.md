# 스킬트리 CSV 양식 (2026-10-13)

기획서 12.5의 SkillData 필드(id · 이름 · 탭 · 단계별 비용 · 단계별 효과 · 선행 조건 · 화면 좌표)와 SkillTreeTab(id · 이름 · 아이콘 · 정렬 순서)을 그대로 CSV 열로 옮겼다. 노션에 CSV 열 양식 자체는 정의되어 있지 않아 이 문서가 기준 양식이다.

## 파일 구성

| 파일 | 한 행의 의미 | 용도 |
|---|---|---|
| skill_tabs.csv | 탭 1개 | 탭 목록 · 정렬 순서 |
| skills.csv | 스킬 1개 | 이름 · 단계 수 · 단계별 비용 · 선행 조건 · 화면 위치 |
| skill_effects.csv | (스킬, 단계, 효과) 1개 | 단계마다 무엇이 바뀌는지 |

모두 UTF-8(BOM 포함, 엑셀에서 한글이 깨지지 않음), 첫 줄은 열 이름. 코드는 **열 이름으로 읽으므로 열 순서를 바꿔도 된다.**

## skills.csv

| 열 | 형식 | 설명 | 예 |
|---|---|---|---|
| skill_id | 문자 | 바뀌지 않는 id (저장 데이터와 연결) | skill_unlock_lantern |
| tab_id | 문자 | skill_tabs.csv의 tab_id | tab_farm |
| name_ko / description | 문자 | 화면 표시 | 등불 열매 해금 |
| max_level | 정수 | 단계 수 | 1 |
| cost_per_level | `;` 목록 | 단계별 SP. 개수 = max_level | `2;3;5` |
| prerequisites | `id:단계;id:단계` | 선행 조건. 전부 만족해야 열림(AND). 비우면 처음부터 열림 | `skill_unlock_grinder:1;skill_unlock_mushroom:1` |
| node_col / node_row | 정수 | 스킬트리 화면 격자 위치 (열 = 해금 순서, 행 = 줄) | 3 / 1 |
| scope | demo / phase2 | 데모 포함 여부 | demo |

**트리 형태**: 선행 조건이 곧 트리의 선입니다. `B의 prerequisites = A:2`이면 화면에 A → B 선을 긋고, A를 2단계까지 올려야 B가 열립니다. 여러 개면 여러 선이 모입니다.

## skill_effects.csv

| 열 | 설명 |
|---|---|
| skill_id, level | 어느 스킬의 몇 단계 효과인지 |
| effect_kind | `unlock` 해금 · `unlock_tier` 단계형 해금 · `stat` 수치 변경 |
| stat_id | stat일 때 바꿀 수치 (아래 목록) |
| op | `add` 더하기 · `mul` 곱하기 · `set` 덮어쓰기 |
| value | **그 단계일 때의 값 (누적값)**. 2단계 행의 값은 1단계 + 2단계가 아니라 "2단계일 때 최종값" |
| target_id | unlock: 해금 대상 id / unlock_tier: 대상 상품 id / stat: 적용 대상(비우면 전체) |

value를 누적값으로 둔 이유: 단계를 올릴 때 "이전 단계 효과를 빼고 새 단계 효과를 넣기"만 하면 되어 계산 실수가 없다.

### effect_kind별 해석
| 종류 | 의미 | 예 |
|---|---|---|
| unlock | target_id를 해금 (상점 상품 · 레시피 · 손님 · 기능) | `shop_seed_lanternfruit`, `recipe_nutrient_juice`, `customer_wanderer`, `feature_remote_storage` |
| unlock_tier | 단계형 상품의 구매 가능 단계를 value까지 엶 | 물뿌리개 1 → 2 → 3, 가방 확장 2 → 4 → 7 |
| stat | stat_id 수치를 op · value로 바꿈 | `customer_ad_rate add 0.03` (간판 3단계) |

### stat_id 목록
| stat_id | 기본값 | 의미 | 쓰는 스킬 |
|---|---|---|---|
| seed_stock_level | 0 | 씨앗 입고 단계 (0~4). 작물별 입고량 = 기본 + (최대 − 기본) × 단계 ÷ 4, 반올림 | 풍성한 입고 |
| giant_chance | 0.02 | 거대 작물 확률 | 거대한 꿈 |
| seed_return_chance | 0 | 수확 시 씨앗 회수 확률 | 씨앗 되받기 |
| regrow_days / digest_days | 작물별 | 재수확 · 소화 일수 (최소 1) | 비옥한 땅 |
| processor_slots | 기구별 | 기구 처리 칸 · 증류관 하루 조합 수 (target_id = 기구) | 가공 숙련 |
| mess_rate | 1.0 | 흩어진 상품 · 흐트러진 진열 · 얼룩 생성 비율 | 청결 유지 |
| customer_ad_rate | 0.02 | 손님 모델 광고 계수 p | 간판 |
| purchase_count | 0 | 손님 구매 개수 보너스 | 단골의 장바구니 |
| sell_price_raw / sell_price_processed | 0 | 원재료 · 가공품 판매가 보너스 | 신선 작물 프리미엄 · 장인의 가공품 |
| perfect_tidy_sales | 0 | 청결 · 정돈 모두 S로 시작 시 매출 보너스 | 반짝이는 가게 |
| display_stack | 8 | 과일 진열대 층당 작물 개수 (target_id = furn_fruit_shelf) | 가득 찬 진열대 |
| stack_size | 아이템별 | 칸당 개수 (target_id = 아이템) | 쓰레기 압축 |
| hotbar_slots | 4 | 핫바 칸 수 | 분주한 손 |
| move_speed / interact_range | 0 | 이동 속도 보너스 · 상호작용 거리 추가(m) | 가벼운 발걸음 · 긴 팔 |

## 수치 id(stat_id)는 임시 — 변경 방법

위 stat_id는 **기획 단계의 임시 이름**이다. 개발의 수치 시스템(StatSystem)에서 실제 이름이 정해지면 바꿀 수 있도록 다음 규칙을 둔다.

- **id는 CSV와 수치 목록 두 곳에만 존재**한다. 코드에 stat_id 문자열을 직접 쓰지 않고, 개발 쪽 수치 목록 에셋(StatDefinition, 예: `Data/Stats/stat_*.asset`)의 id와 CSV의 stat_id가 같으면 연결된다.
- **이름을 바꿀 때**: ① 수치 목록 에셋의 id를 바꾸고 ② CSV에서 같은 문자열을 일괄 치환(skill_effects.csv의 stat_id 열) ③ "CSV 가져오기" 실행. 가져오기 때 **수치 목록에 없는 stat_id가 있으면 경고하고 그 행을 건너뛴다** → 바꾸다 빠뜨린 곳이 바로 드러난다.
- **옛 이름 호환(선택)**: 수치 목록 에셋에 `aliases`(옛 이름 목록)를 두면 CSV를 고치기 전에도 옛 이름으로 읽힌다. 바꾼 뒤 한 번 정리하면 별칭은 지운다.
- **스킬 · 상품 id(skill_*, shop_*, recipe_*)는 저장 데이터와 연결**되므로 stat_id와 달리 확정 후에는 바꾸지 않는다.

## 코드가 읽는 방법 (SkillCsvLoader.cs)

1. **읽기**: 첫 줄로 열 이름 → 위치 표를 만들고, 각 행을 `열 이름 → 값`으로 읽는다.
2. **skills.csv**: `cost_per_level`을 `;`로 나눠 정수 배열, `prerequisites`를 `;` → `:`로 나눠 (선행 id, 필요 단계) 목록으로 만든다.
3. **skill_effects.csv**: 행마다 해당 스킬의 효과 목록에 추가한다 (같은 단계에 여러 행 가능).
4. **검증**: 비용 개수 = 단계 수, 없는 선행 id, 선행 단계 초과, 단계별 효과 누락, **선행 순환**(깊이 우선 탐색)을 검사하고, 문제가 있으면 줄을 알려 주고 중단한다.
5. **에셋 반영**: 에디터 임포터가 skill_id로 기존 SkillData를 찾아 값만 갱신하고, 없으면 새로 만든다 (멱등).
6. **런타임 판정**:
   - 잠김: 선행 중 하나라도 `현재 단계 < 필요 단계`
   - 습득 가능: 선행 모두 충족 + SP ≥ `cost_per_level[현재 단계]`
   - 습득: SP 차감 → 이전 단계 효과 제거 → 새 단계 효과 적용
7. **화면**: `node_col`, `node_row`로 노드를 배치하고, 선행 관계로 선을 그린다(`Edges()`).

## 새 스킬 추가 절차 (코드 수정 없음)
1. skills.csv에 한 줄 추가 (id · 탭 · 단계 · 비용 · 선행 · 위치)
2. skill_effects.csv에 단계 수만큼 효과 행 추가
3. 유니티 메뉴 "CSV 가져오기" 실행 → 검증 통과 시 에셋 생성
4. 새로운 stat_id나 effect_kind가 필요할 때만 개발에 요청

> 현재 데이터: 탭 3개, 스킬 31종, 효과 74행, 전체 342 SP. 검증 통과(순환 · 누락 없음).
> 2026-10-13 변경: 풍성한 입고 추가, 물뿌리개 라이선스 2단계(4칸 → 8칸), 비료 분사기 · 자동 수확기 해금 스킬 제거.
> 기존 임시 id(skill_unlock_mushroom, skill_hotbar 등)는 저장 데이터 연결을 위해 그대로 유지. 화면 이름만 바뀜(예: skill_hotbar → "분주한 손").
