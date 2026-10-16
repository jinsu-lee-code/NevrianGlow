# BRANCH_ANIMATOR.md — `Animator` 브랜치 사용법 (모션 파트)

> 대상: 모션(김기인). 공통 규칙은 [BRANCHES.md](BRANCHES.md).
> 이 브랜치는 **손님 리그 · 손님 애니메이션 · 손님 모델을 올리는 곳**이다. 개발(이진수)이 `dev`로 가져와 게임 애니메이터에 끼운다.
> 1인칭 도구 동작(물뿌리개 기울이기 등)은 개발이 코드로 하므로 모션 범위가 아니다 (결정 2026-10-06).

## 1. 내 폴더

| 폴더 (저장소 기준) | 넣는 것 |
|---|---|
| `Nevrian Glow/Assets/Art/Characters/` | 손님 모델 · 공용 휴머노이드 리그 · 애니메이션 클립(FBX) · 미리보기용 애니메이터 · 미리보기 씬 |

- 지금 들어 있는 것 (10/14 배경 납품 때 같이 들어옴): `XBot/`(X Bot 모델 · `anim_customer_idle` · `anim_customer_walk` · `anim_customer_walkstart` · `AC_XBot_Locomotion`), `Person/Male/`(`mdl_customer_male`)
- 안쪽 정리 권장: `Characters/Customer/Models/`, `Characters/Customer/Animations/`, `Characters/Customer/Preview/`
- `Nevrian Glow/Assets/_Project/Art/Animations/Customer/`(게임용 `ac_customer` 컨트롤러 + 빈 클립)는 **개발 폴더** — 고치지 않는다. 납품 클립을 여기에 끼우는 건 개발이 한다.

## 2. 처음 한 번만

1. GitHub Desktop → File → Clone repository → `jinsu-lee-code/NevrianGlow`
2. 위쪽 **Current branch** → `Animator` 선택 (목록에 없으면 Fetch origin 후 다시)
3. Unity Hub → Add → 저장소 안의 **`Nevrian Glow`** 폴더 → 버전 **6000.3.25f1**로 연다

## 3. 작업 시작할 때마다 (최신 게임 받기)

1. Current branch가 **`Animator`** 인지 확인
2. **Fetch origin** → Pull origin이 보이면 누른다
3. 메뉴 **Branch → Merge into current branch…** → **`dev`** → **Create a merge commit** (충돌이 뜨면 멈추고 개발에게)
4. **Push origin**

## 4. 게임이 기대하는 것 (이름 고정 — 바꾸면 게임이 못 찾는다)

### 4-1. 클립 5종 (BALANCE 9-5, GDD)

| 클립 이름 (파일 · 클립 둘 다) | 동작 | 반복 | 애니메이션 이벤트 |
|---|---|---|---|
| `anim_customer_walk` | 걷기 | Loop | — |
| `anim_customer_browse` | 진열대 앞 둘러보기 | Loop | — |
| `anim_customer_pick_basket` | 입구에서 장바구니 집기 | 1회 | 바구니를 손에 쥐는 순간 `OnItemTake` |
| `anim_customer_put_item` | 상품을 집어 장바구니에 넣기 | 1회 | 상품을 집는 순간 `OnItemTake`, 바구니에 넣는 순간 `OnItemPlace` |
| `anim_customer_happy` | 계산 후 해피 아이들 | 1회 | 빛나는 순간 `OnHappyGlow` |

- 이벤트 이름은 대소문자까지 그대로. 이벤트가 없어도 게임은 정해진 시간(둘러보기 1.2초 · 계산 1.0초)으로 넘어가지만, 넣으면 손 동작과 상품 움직임이 맞는다
- `anim_customer_idle`도 있으면 좋다 (줄 서서 기다릴 때 — 연결은 개발이 판단)

### 4-2. 애니메이터 파라미터 (게임 쪽 `ac_customer`, 이름 고정)

| 파라미터 | 종류 | 언제 |
|---|---|---|
| `Speed` | float | 실제 걷는 속도 (m/s) — 0이면 서 있음 |
| `Browsing` | bool | 진열대 앞에서 둘러보는 동안 true |
| `PickBasket` | trigger | 바구니 집기 시작 |
| `PutItem` | trigger | 상품 넣기 시작 |
| `Happy` | trigger | 계산 끝 |
| `HasBasket` | bool | 바구니를 든 동안 true → **왼팔 레이어 `Basket`** 이 바구니 드는 자세로 |

- 미리보기용 애니메이터는 내 폴더에 마음대로 만들어도 된다. 같은 파라미터 이름을 쓰면 개발 쪽 교체가 쉽다
- **왼팔 바구니 자세**: 왼팔만 움직이는 짧은 클립 또는 포즈 (예 `anim_customer_hold_basket`) + 왼팔만 덮는 Avatar Mask(`mask_customer_left_arm`)를 같이 주면 레이어에 그대로 끼운다

### 4-3. 리그 · 임포트 설정

- **Animation Type = Humanoid** (공용 리그 하나로 손님 종류가 늘어도 같은 클립을 쓴다)
- **Root Motion 끔** — 이동은 게임(NavMesh)이 한다. 걷기는 제자리 걸음으로
- 걷기 기준 속도 **2.1 m/s** (손님마다 0.7 ~ 1.3배) — 발이 미끄러지지 않게 이 속도에 맞춘다
- 체형 3단계: 키 **작은 1.2 m · 보통 1.6 m · 큰 2.0 m** (피피 = 작은, 오르 = 큰). 같은 클립이 세 체형에서 어색하지 않은지 확인
- 모델은 실제 크기(m)로. 재질은 URP용(분홍 없음)
- **손 소켓**: 오른손 · 왼손 뼈 이름(예 `mixamorig:RightHand`)과 바구니 · 상품을 쥐는 위치를 납품 기록에 적어 주면 개발이 CustomerData의 소켓 보정값에 넣는다

### 4-4. 이름 규칙

- 클립 `anim_customer_동작`, 모델 `mdl_customer_종류`, 마스크 `mask_customer_부위`, 미리보기 컨트롤러 `ac_preview_이름`
- 소문자 · 밑줄

## 5. 게임에서 확인하기

1. `Nevrian Glow/Assets/_Project/Scenes/Core.unity` → **Play** → 장사 단계에서 손님이 가게에 들어온다 (디버그 창 `Glow Farm/디버그 창`에서 단계 강제 전환 가능)
2. 지금 게임 손님은 캡슐 + 빈 클립이다. 내 클립을 게임 손님에 끼워 보는 건 개발 몫 — 미리 보고 싶으면 내 폴더에 미리보기 씬(`Characters/Customer/Preview/`)을 만들어 확인
3. 게임 씬 · `_Project` 파일을 바꿨다면 **커밋 전에 되돌린다** (Changes → 우클릭 → Discard changes)

## 6. 올리기 (커밋 · push)

1. **Changes** 탭: `Assets/Art/Characters/…`와 `BRANCH_ANIMATOR.md`만 있어야 한다. 다른 곳은 Discard. `.meta` 같이 체크
2. Summary `MM.DD/Animator/요약` (예 `10.18/Animator/CustomerWalkBrowse`) → **Commit to Animator** → **Push origin**
3. 아래 **납품 기록**에 한 줄 → 커밋(`MM.DD/Animator/DeliveryNote`) → Push
4. 개발(이진수)에게 메신저로 알림

## 7. 하지 말 것

- `dev` · `main`에 커밋
- `_Project/Art/Animations/Customer/ac_customer.controller` 등 `_Project` 안 수정
- 클립 · 파라미터 · 이벤트 이름을 말없이 바꾸기
- 탐색기에서 파일 옮기기 · 이름 바꾸기 (Unity Project 창에서)

## 8. 납품 예정 (TASKS 기준)

| 예정일 | 내용 |
|---|---|
| 10/18 | 공용 손님 리그 + 걷기 · 둘러보기 · 장바구니 집기 |
| 10/24 | 장바구니에 넣기 · 해피 아이들 + 피피 · 오르 적용 |

## 9. 납품 기록

| 날짜 | 무엇을 | 폴더 · 파일 | 클립별 메모 (이벤트 시점 · 반복 · 손 소켓 뼈) | 개발에게 요청 | 개발 확인 |
|---|---|---|---|---|---|
| 10/14 | X Bot 모델 · idle · walk · walkstart, 손님 남성 모델 (배경 납품에 포함되어 들어옴) | `Assets/Art/Characters/` | 게임 연결 전 | — | 10/14 dev에 들어옴, 아직 게임 손님에 연결 안 함 |
| 10/16 | 손님 외계인 Cunsumer 모델 · 장바구니 집기 · 왼팔 바구니 자세 · 미리보기 컨트롤러 (1차 일부) | `Assets/Art/Characters/Customer/` — `Cunsumer/mdl_customer_cunsumer.fbx`(+재질 · 텍스처 · 셰이더 `DSEWA/ToonCharacter` · `ToonAlienEye`), `Animations/anim_customer_pick_basket.fbx` · `anim_customer_hold_basket.anim` · `mask_customer_left_arm.mask` (+선택: `anim_customer_arm_out_r` · `mask_customer_right_arm`), `Preview/ac_preview_cunsumer.controller` | pick_basket: 1회, 5초, 왼손으로 집음, `OnItemTake` 2.25초(손잡이를 쥐는 순간), 루트 이동·회전 Bake Into Pose. 손 소켓 뼈 `LeftHand` / `RightHand`. 걷기는 기존 `XBot/anim_customer_walk` 그대로. 미끄러지지 않게 재생 속도 = 실제 이동 속도 ÷ (1.62 × `animator.humanScale`) — 클립 원래 속도 1.62 m/s는 휴먼 스케일 1 기준, Cunsumer는 humanScale 0.968 → 1.57 m/s. 미리보기 컨트롤러는 Walk 상태 speed 1/1.57 × 파라미터 `Speed`(Idle↔Walk는 Speed 0.05 기준)로 맞춰 둠: 2.1 m/s에서 1.34배, 손님 배율 0.7~1.3배(1.47~2.73 m/s)에서도 발 미끄러짐이 루트 모션 기준과 같음. Cunsumer 키 1.84 m(안테나 포함). browse · put_item · happy는 아직 | ① `ac_customer`의 Basket 레이어(가중치 1)에서 빈 상태 `NoBasket`이 바구니가 없을 때도 왼팔을 기본 자세로 덮어씀(Humanoid, Write Defaults 꺼도 동일) → 레이어 가중치를 코드로 `HasBasket`에 맞춰 0↔1 하는 방식 제안. 미리보기 컨트롤러는 Basket 레이어 가중치 0으로 둠 ② 셰이더가 `Assets/Art/Environment/Shaders/CloudShadow.hlsl` · `ToonShadow.hlsl`을 include (이미 저장소에 있음) | |
