# BRANCH_ART.md — `Art` 브랜치 사용법 (아트 파트)

> 대상: 아트(김승희 · 배경 담당). 공통 규칙은 [BRANCHES.md](BRANCHES.md).
> 이 브랜치는 **아트 에셋을 올리는 곳**이다. 올린 에셋은 개발(이진수)이 `dev`로 가져와 게임에 연결한다.

## 1. 내 폴더

| 폴더 (저장소 기준) | 넣는 것 |
|---|---|
| `Nevrian Glow/Assets/Art/Environment/` | 배경 · 지형 · 하늘 · 풀 · 환경 재질 · 환경 셰이더 · 환경 스크립트 (지금 있는 것) |
| `Nevrian Glow/Assets/Art/Crops/` | 작물 단계별 모델 (월광 뿌리 · 버섯꽃 · 등불 열매 …) |
| `Nevrian Glow/Assets/Art/Props/` | 소품 (밭 · 진열대 · 창고 · 쓰레기통 · 문 · 팻말 · 도구 …) |
| `Nevrian Glow/Assets/Art/UI/` | UI 패널 · 아이콘 (`Icons/`, `Panels/`) |
| `Nevrian Glow/Assets/Art/VFX/` | 이펙트 텍스처 · 파티클 · 셰이더 |
| `Nevrian Glow/Assets/Art/_Source/` | AI 생성(Varco3D) **원본** 모델 보관 — 손대지 않은 원본 |
| `Nevrian Glow/Assets/Scenes/` | 배경 씬 (`BackGroundScene.unity`) |
| `Nevrian Glow/Assets/Art/SDCat/` | 외곽선 셰이더 (지금 있는 것) |

- `Nevrian Glow/Assets/Art/Characters/`는 **모션 파트(Animator 브랜치) 폴더**다 — 손님 모델 · 리그는 거기서.
- 폴더 이름은 위를 기준으로 하되, 안쪽 정리는 자유.

## 2. 처음 한 번만

1. GitHub Desktop → File → Clone repository → `jinsu-lee-code/NevrianGlow`
2. 위쪽 **Current branch** → `Art` 선택 (목록에 없으면 Fetch origin 후 다시)
3. Unity Hub → Add → 저장소 안의 **`Nevrian Glow`** 폴더 → 버전 **6000.3.25f1**로 연다 (처음엔 몇 분 걸림)

## 3. 작업 시작할 때마다 (최신 게임 받기)

1. GitHub Desktop에서 Current branch가 **`Art`** 인지 확인
2. **Fetch origin** → 버튼이 Pull origin으로 바뀌면 누른다
3. 메뉴 **Branch → Merge into current branch…** → **`dev`** 선택 → **Create a merge commit**
   - "This branch is up to date" 면 할 일 없음
   - 충돌(conflict)이 뜨면 멈추고 개발에게 알린다
4. **Push origin** (가져온 것도 올려 둔다)
5. Unity로 돌아오면 자동으로 다시 불러온다. 콘솔에 빨간 오류가 없으면 시작

## 4. 에셋 넣기

1. **Unity를 켠 상태에서** 파일을 내 폴더에 끌어다 놓는다 → `.meta`가 자동으로 생긴다
2. 이름: 소문자 `분류_이름_변형`
   - 모델 `mdl_crop_moonroot_stage1`, `mdl_prop_shelf`
   - 재질 `mat_crop_moonroot`, 텍스처 `tex_crop_moonroot_albedo`
   - 아이콘 `ico_item_seed_moonroot`, UI `ui_panel_dark`
   - 프리팹 `pf_crop_moonroot_stage1`
3. **AI 생성 모델(Varco3D)** 은 1유닛 정규화 상태로 들어온다
   - 원본은 `_Source/`에 그대로 두고
   - 게임용은 **프리팹에서 실제 크기(m)** 로 맞춘다 (모델 파일 자체 Scale Factor를 바꿔도 됨)
   - 크기 기준: 밭 한 칸 간격 2 m, 플레이어 눈높이 1.7 m, 손님 키 1.2 / 1.6 / 2.0 m, 가게 바닥 12 × 10 m
4. **재질은 URP용**으로 (URP/Lit, URP/Unlit 또는 Shader Graph). 씬에서 **분홍색(깨진 재질)이 하나도 없어야** 한다
5. **발광**은 Emissive(HDR 색) + 후처리 Bloom으로. 점광원(Point Light)은 씬 전체 4~6개 이하
6. 팔레트: 심연 `#06161F` · 심청 `#0E2640` · 시안 발광 `#62F2E1` · 마젠타 발광 `#FF74D8` · 보라 발광 `#A08BFF` · 금빛 `#FFD27A`
7. **작물 · 소품 프리팹 구조** (모델만 바꿔 끼울 수 있게):
   ```
   pf_crop_moonroot_stage1        ← 루트 (위치 · 크기 0,0,0 / 1,1,1)
     └ Model                      ← 실제 모델 (여기 크기 · 회전 조정)
   ```
   작물은 단계마다 프리팹 1개 (예: 월광 뿌리 = 새싹 · 다 자람). 게임 쪽 연결은 개발이 한다.
8. **UI 아이콘 · 패널**: PNG, 투명 배경, 아이콘 512 × 512 권장. Unity에서 Texture Type = **Sprite (2D and UI)**

## 5. 배경 씬 (`Assets/Scenes/BackGroundScene.unity`)

게임은 Core · Farm · Shop 씬에 **배경 씬을 함께 불러와** 쓴다 (2026-10-14부터). 그래서 다음을 지킨다.

- 배경 씬의 **해(Directional Light) · 하늘(Skybox) · 후처리 Volume · DayNight · GroundFog가 게임 조명이다.** 바꾸면 게임 화면이 바로 바뀐다.
- **켜진 카메라 · AudioListener를 두지 않는다.** 룩데브용 `Main Camera`는 꺼 둔 채로 커밋 (켜서 보는 건 괜찮고, 커밋 전에 다시 끈다)
- 테스트용 오브젝트(`TallGrass_Test` · `LanternFruit` 등)는 꺼 둔 채로 커밋
- **농장 구역(x −40 ~ 10, z −12 ~ 12, 바닥 높이 0)** 은 밭 · 길 · 문이 있는 자리 — 콜라이더(충돌체)를 두지 말고, 지형 높이를 0으로 유지
- 가게는 (500, 0, 0) 근처 실내 — 지형 밖이다
- 씬에서 오브젝트를 지우는 대신 끄는 걸 권장 (지우면 개발 쪽 확인이 어렵다)

**지금 정해야 할 것 (개발에게 답 주세요)**
- 풀(`Terrain_LookDev`의 `LayerGrassSpawner`, 중심 (0,0,0) 반경 35 m)이 밭 · 길 위에도 자란다 → 밭 · 길 · 문 주변을 비울 방법
- 시간대: `DayNight.hour = 24`(밤) — 기획의 "영원한 황혼"과 맞는지

## 6. URP 설정 · 패키지 (예외 규칙)

- `Nevrian Glow/Assets/Settings/`(PC_Renderer · PC_RPAsset · 볼륨 프로필)는 **아트가 바꿀 수 있다** — 외곽선 같은 렌더 기능이 필요할 때. 대신 납품 기록 "개발에게 요청"에 **무엇을 왜 바꿨는지** 적는다
- **패키지 추가는 하지 말고 요청한다.** (예: 10/14 배경 스크립트가 Visual Effect Graph를 필요로 해서 프로젝트 전체 컴파일이 멈췄다 — 지금은 설치됨)
- 스크립트를 넣을 때 `UnityEditor`를 쓰면 `#if UNITY_EDITOR`로 감싼다 (안 그러면 빌드가 안 됨)

## 7. 게임에서 확인하기

1. `Nevrian Glow/Assets/_Project/Scenes/Core.unity`를 열고 **Play** → 농장에서 시작
2. 내 프리팹 · 모델을 보고 싶으면 Farm/Shop 씬에 잠깐 끌어다 놓고 확인 → **커밋 전에 되돌린다** (GitHub Desktop Changes에서 `Core.unity` · `Farm.unity` · `Shop.unity`가 보이면 우클릭 → Discard changes)

## 8. 올리기 (커밋 · push)

1. GitHub Desktop **Changes** 탭에서 목록 확인
   - 내 폴더(`Assets/Art/…`, `Assets/Scenes/…`)와 `BRANCH_ART.md`만 있어야 한다
   - 다른 곳(`_Project`, `ProjectSettings`, `Packages`, 다른 파트 폴더)이 보이면 → 우클릭 → **Discard changes** (또는 체크 해제)
   - `.meta` 파일도 같이 체크돼 있는지 확인
2. 아래 **Summary**에 `MM.DD/Art/요약` (예: `10.17/Art/CropModels1`) → **Commit to Art**
3. **Push origin**
4. 이 문서 맨 아래 **납품 기록**에 한 줄 추가 → 커밋(`MM.DD/Art/DeliveryNote`) → Push
5. 개발(이진수)에게 메신저로 "Art 브랜치에 ○○ 올렸어요"

## 9. 하지 말 것

- `dev` · `main`에 커밋 · push
- `Nevrian Glow/Assets/_Project/` 안의 파일 수정 · 커밋 (게임 씬 · 프리팹 · 재질 포함 — 교체는 개발이 한다)
- 탐색기에서 파일 이름 바꾸기 · 옮기기 (Unity Project 창에서)
- 이미 게임에 들어간 에셋을 말없이 지우기 · 이름 바꾸기
- 50 MB 넘는 파일 (지형 데이터 `TD_LookDev_DeepBlue.asset`이 지금 약 22 MB — 더 커지면 상의)

## 10. 납품 기록

맨 아래에 한 줄씩 추가한다. "개발 확인"은 개발이 가져와서 적는다.

| 날짜 | 무엇을 | 폴더 · 파일 | 어디에 쓰나 / 메모 | 개발에게 요청 | 개발 확인 |
|---|---|---|---|---|---|
| 10/14 | 배경 씬 1차 (지형 · 하늘 · 구름 · 풀 · FX · 낮/밤) | `Assets/Scenes/BackGroundScene.unity`, `Assets/Art/Environment/`, `Assets/Art/SDCat/` | 농장 배경 | URP 볼륨 프로필 · 외곽선 렌더 기능 · GPU Resident Drawer 켬 | 10/14 dev 반영 (`b1e688e`) — VFX Graph 패키지 추가, 룩데브 카메라 · 테스트 오브젝트 끔. 풀 제외 구역 · 시간대 답 대기 |
