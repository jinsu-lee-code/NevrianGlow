# BRANCH_SOUND.md — `Sound` 브랜치 사용법 (사운드 파트)

> 대상: 사운드 · QA(백경민). 공통 규칙은 [BRANCHES.md](BRANCHES.md).
> 이 브랜치는 **BGM · 효과음 · UI 소리를 올리는 곳**이다. 개발(이진수)이 `dev`로 가져와 소리 id에 연결한다 (AudioManager · SoundLibrary, TASKS 5-5).
> 콘셉트: "유리 종소리가 울리는 황혼의 숲" (GDD).

## 1. 내 폴더

| 폴더 (저장소 기준) | 넣는 것 |
|---|---|
| `Nevrian Glow/Assets/Audio/BGM/` | 배경음 4곡 |
| `Nevrian Glow/Assets/Audio/SFX/` | 효과음 (농사 · 청소 · 장사 · 이동 · 환경) |
| `Nevrian Glow/Assets/Audio/UI/` | UI 소리 (버튼 · 열기/닫기 · 레벨업 등) |
| `Nevrian Glow/Assets/Audio/_Source/` | 원본 프로젝트 파일 · 긴 원본 (선택, 50 MB 이하만) |

- 폴더가 아직 없으면 Unity Project 창에서 만든다 (`Assets` 우클릭 → Create → Folder)

## 2. 처음 한 번만

1. GitHub Desktop → File → Clone repository → `jinsu-lee-code/NevrianGlow`
2. 위쪽 **Current branch** → `Sound` 선택 (목록에 없으면 Fetch origin 후 다시)
3. Unity Hub → Add → 저장소 안의 **`Nevrian Glow`** 폴더 → 버전 **6000.3.25f1**로 연다

## 3. 작업 시작할 때마다 (최신 게임 받기)

1. Current branch가 **`Sound`** 인지 확인
2. **Fetch origin** → Pull origin이 보이면 누른다
3. 메뉴 **Branch → Merge into current branch…** → **`dev`** → **Create a merge commit** (충돌이 뜨면 멈추고 개발에게)
4. **Push origin**

## 4. 소리 id와 파일 이름 (게임은 id로 부른다)

게임 코드는 파일이 아니라 **소리 id**로 소리를 부른다. 파일 이름 = id 로 맞추면 개발이 바로 연결한다. 파일이 없으면 게임은 조용히 넘어간다(오류 아님).

### 4-1. BGM 4곡

| id (= 파일 이름) | 곡 | 쓰는 곳 | 상태 |
|---|---|---|---|
| `bgm_title` | 타이틀 | 타이틀 화면 (5-6) | id 제안 |
| `bgm_twilight_field` | 황혼의 밭 | 농장 | **게임에 이미 등록됨** |
| `bgm_spore_dust` | 포자 먼지 | (용도 기획 확인 — 밤 · 정산 등) | id 제안 |
| `bgm_lumian_market` | 루미안의 장날 | 가게 | **게임에 이미 등록됨** |

- 등록된 두 id는 그대로 써 주세요. 제안 id를 바꾸고 싶으면 납품 기록에 적으면 개발이 맞춘다
- 지역을 옮길 때 1.5초 교차로 바뀐다 → **루프 지점이 끊기지 않게** (곡 처음 · 끝이 자연스럽게 이어지게)

### 4-2. 효과음 (약 25종 + 후보)

- 이름: `sfx_분류_동작_번호` — 예 `sfx_farm_water_pour_01`, `sfx_farm_plant_01`, `sfx_farm_harvest_01`, `sfx_clean_mop_01`, `sfx_clean_trash_pick_01`, `sfx_shop_register_01`, `sfx_zone_door_01`
- **반복해서 나는 소리는 0.5초 이하 · 변형 3개** (`_01` `_02` `_03`) — 게임이 셋 중 무작위로 고른다
- 후보(GDD): 괭이 · 개간 완료 · 내려놓기 · 줍기 · 쓰레기통 비우기 · 비료
- 어떤 행동에 쓰는 소리인지 납품 기록 "어디에 쓰나"에 꼭 적는다 (개발이 그 행동에 연결)

### 4-3. UI 소리

- 이름: `ui_동작` — 예 `ui_click`, `ui_open`, `ui_close`, `ui_levelup`, `ui_coin`

## 5. 파일 형식 (권장)

| 항목 | BGM | 효과음 · UI |
|---|---|---|
| 형식 | WAV (또는 OGG) | WAV |
| 샘플레이트 | 48 kHz (44.1 kHz도 가능) | 48 kHz |
| 채널 | 스테레오 | 모노 권장 (공간 소리는 모노 필수) |
| 크기 | 곡당 50 MB 이하 | — |
| 레벨 | 곡끼리 체감 음량을 맞춘다 | 피크 −1 dBFS 이하, 앞뒤 무음 잘라내기 |

- Unity 임포트 설정(압축 · Load Type)은 개발이 맞춘다 — 바꾸지 않아도 된다

## 6. 게임에서 확인하기

- 5-5 AudioManager가 붙기 전까지는 게임에서 소리가 나지 않는다 (id만 호출, 파일 없음 = 무음)
- 들어 보려면 Unity Project 창에서 파일을 선택 → 인스펙터 아래 재생 버튼
- 게임 흐름을 보고 싶으면 `Nevrian Glow/Assets/_Project/Scenes/Core.unity` → Play. 게임 씬 · `_Project` 파일이 바뀌었으면 **커밋 전에 되돌린다**

## 7. 올리기 (커밋 · push)

1. **Changes** 탭: `Assets/Audio/…`와 `BRANCH_SOUND.md`만 있어야 한다. 다른 곳은 우클릭 → Discard. `.meta` 같이 체크
2. Summary `MM.DD/Sound/요약` (예 `10.18/Sound/SfxFirstBatch`) → **Commit to Sound** → **Push origin**
3. 아래 **납품 기록**에 한 줄 → 커밋(`MM.DD/Sound/DeliveryNote`) → Push
4. 개발(이진수)에게 메신저로 알림

## 8. 하지 말 것

- `dev` · `main`에 커밋
- `_Project` · `ProjectSettings` · `Packages` 수정
- 이미 게임에 연결된 id의 파일 이름을 말없이 바꾸기
- 탐색기에서 파일 옮기기 · 이름 바꾸기 (Unity Project 창에서)

## 9. 납품 예정 (TASKS 기준)

| 예정일 | 내용 |
|---|---|
| 10/18 | 효과음 1차 |
| 10/25 | BGM 4곡 · UI 효과음 |

## 10. 납품 기록

| 날짜 | id (파일) | 어디에 쓰나 (어떤 행동 · 화면) | 길이 · 루프 · 변형 수 | 개발에게 요청 | 개발 확인 |
|---|---|---|---|---|---|
| 10.18 | `sfx_seed_plant_01~03` | 씨앗 심기 (클릭 순간) | 0.38~0.40초 · 변형 3 | 무작위 재생 | |
| 10.18 | `sfx_water_pour_01~03` | 물주기 (물뿌리개 기울인 뒤) | 0.5초 · 변형 3 | 무작위 재생 | |
| 10.18 | `sfx_harvest_pull_01~03` | 수확 뽑기 | 0.4초 · 변형 3 | 무작위 재생 | |
| 10.18 | `sfx_trash_pickup_01~03` | 쓰레기 줍기 | 0.78~1.01초 · 3개 | 종류별 소리: 01 유리병 · 02 껍질/포자 · 03 종이/잎 — 쓰레기 종류에 맞춰 재생 부탁(종류 구분이 없으면 무작위) | |
| 10.18 | `sfx_trash_drop` | 쓰레기통에 버리기 | 0.35초 · 1개 |  | |
| 10.18 | `sfx_fertilizer_sparkle` | 비료 뿌리기 (P1) | 0.6초 · 1개 |  | |
| 10.18 | `sfx_crop_mature` | 작물 성숙 알림 (아침, 다 자란 작물이 있을 때 1회) | 1.6초 · 1개 |  | |
| 10.18 | `sfx_mop_loop` | 대걸레 닦기 (누르고 있는 동안) | 3.40초 · **루프** | AudioSource loop 재생, 이음새 크로스페이드 처리됨 | |
| 10.18 | `sfx_stain_clear` | 얼룩 완전 제거 | 1.12초 · 1개 |  | |
| 10.18 | `sfx_customer_enter` | 손님 입장 (문 + 풍경 소리) | 1.42초 · 1개 |  | |
| 10.18 | `sfx_customer_exit` | 손님 퇴장 (문 닫힘 + 종) | 1.32초 · 1개 |  | |
| 10.18 | `sfx_checkout_coin` | 계산 (매출 증가 순간) | 1.21초 · 1개 | 짤랑 종소리 징글(동전 소리 아님) | |
| 10.18 | `sfx_customer_happy_01~03` | 만족 반응 — 해피 아이들 (OnHappyGlow) | 1.18~1.37초 · 변형 3 | 무작위 재생 | |
| 10.19 | `sfx_ui_click` | 버튼 클릭 (누를 수 있는 UI 전부) | 0.1초 · 1개 |  | |
| 10.19 | `sfx_ui_buy_success` | 상점 구매 성공 | 0.5초 · 1개 |  | |
| 10.19 | `sfx_ui_buy_fail` | 구매 실패 · 놓을 수 없음 · 인벤토리 가득 참 | 0.4초 · 1개 |  | |
| 10.19 | `sfx_ui_skill_learn` | 스킬 습득 | 1.44초 · 1개 |  | |
| 10.19 | `sfx_ui_levelup` | 레벨업 팡파르 (LeveledUp) | 2.34초 · 1개 |  | |
| 10.19 | `sfx_ui_fade` | 농장↔가게 이동 페이드 (0.35초 전환) | 3.56초 · 1개 | 전환 뒤에도 꼬리가 남음 — 길면 알려 주세요 | |
| 10.19 | `sfx_basket_pick` | 장바구니 집기 (OnBasketAttach) | 0.40초 · 1개 | 다른 효과음보다 약간 작게 맞춤(-3 dBFS) | |
| 10.19 | `sfx_basket_put_01~03` | 장바구니에 상품 넣기 (OnItemPlace) | 0.3초 · 변형 3 | 무작위 재생 | |
| 10.19 | `sfx_customer_step_01~03` | 손님 발소리 (OnFootstep) | 약 6초 · 변형 3 | 파일 하나에 걸음 여러 번 — 한 걸음씩 잘라야 하면 알려 주세요 | |
| 10.20 | `sfx_ui_tool_switch` | 핫바 도구 바꾸기 (1~5) | 0.19초 · 1개 |  | |
| 10.20 | `bgm_farm_loop` | 농장 BGM (상점 · 농사) | 1분 52초 · 루프 | 실제 템포 약 86 BPM — 크로스페이드 1.5초 확인 부탁 | |
| 10.21 | `bgm_title_loop` | 타이틀 화면 | 2분 58초 · 곡 전체 반복 (도입부 포함) | 요청 1분보다 김 — 신비로운 도입부를 살리려고 곡 전체 사용, 길이 괜찮은지 확인 부탁 | |
| 10.21 | `bgm_market_loop` | 가게 장사 BGM | 1분 33초 · 루프 | 실제 템포 약 103 BPM | |
| 10.21 | (이름 변경) | UI 7종 `ui_*` → `sfx_ui_*`, BGM `bgm_twilight_field` → `bgm_farm_loop` · `bgm_title` → `bgm_title_loop` · `bgm_lumian_market` → `bgm_market_loop` | — | 파일 이름을 sound.md(요청서) 기준으로 통일 (이진수 님 안내). 게임에 먼저 등록된 id(`bgm_twilight_field` · `bgm_lumian_market`)도 sound.md 이름으로 연결 부탁 | |
| 10.21 | `bgm_clean_loop` | 청소 · 진열 BGM | 1분 36초 · 루프 (곡 도입부부터 시작, 끝 3초 페이드 후 처음으로) | 실제 템포 약 110 BPM | |
| 10.23 | `sfx_step_wood_01~03` | 플레이어 발소리 (가게 나무 바닥, zone_shop) | 0.42~0.66초 · 변형 3 | 3개 음량 맞춤 | |
| 10.23 | `sfx_step_soil_01~03` | 플레이어 발소리 (흙, zone_farm) | 0.27~0.42초 · 변형 3 | 3개 음량 맞춤 + 잡음 억제 · 흙 3 + 풀 3으로 나눔 (이진수 님과 협의) | |
| 10.23 | `sfx_step_grass_01~03` | 플레이어 발소리 (풀, zone_farm) | 0.31~0.58초 · 변형 3 | 3개 음량 맞춤 | |
| 10.23 | `amb_farm_loop` | 농장 환경음 (바람 · 벌레, zone_farm) | 45초 · 루프 · 스테레오 | 레이어 3개 겹침, -24 LUFS | |
| 10.23 | `amb_shop_loop` | 가게 실내 환경음 (zone_shop) | 30초 · 루프 · 스테레오 | 레이어 3개 겹침, -26 LUFS · 폴더 Assets/Audio/AMB/ 신규 | |
