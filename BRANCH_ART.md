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
| 10/16 | 배경 2차 업데이트 (가게 문 · 등불 열매 갱신 · 달/해 렌즈 플레어 · 구름 노멀맵 · 텍스처 메모리 줄이기 · 풀 바람은 플레이어 주변만) | `Assets/Scenes/BackGroundScene.unity`, `Assets/Art/Environment/` (새: `Shop/Shop_Door.fbx` · 문 재질 3 · `Shaders/ToonWicker.shader` · `Scripts/ToonLensFlareFade.cs` · `Sky/LF_ToonSky.asset` · `Sky/Editor/ToonLensFlareBuilder.cs` · `Sky/Shaders/CloudBillboard.hlsl` · `Sky/Textures/T_CloudCard_Normal.png`) | 문 `Shop_Door` (22, 0, −9) 농장 구역 밖, 콜라이더 없음. `LanternFruit`는 자리가 농장 구역 안 (1.2, 0, 0.6)이라 꺼 둠. 렌즈 플레어는 `Directional Light`의 `ToonLensFlareFade`가 만듦. 주인공 1인칭 바구니와 그 스크립트(`CarriedBasket`)는 이번에 뺌, 손님 오브젝트도 씬에서 뺌. `Main Camera` · `TallGrass_Test` 꺼 둔 상태 유지 | `DayNight`(DayNightBlend)에 손님 Cunsumer 재질 연결 42개가 있음 — Art 브랜치만으론 Missing으로 보이지만 Animator 브랜치에 같은 GUID로 있어 둘 다 가져오면 연결됨(코드는 빈 값을 건너뜀). 풀 제외 구역 · 시간대 질문은 아직 답 대기 | |
| 10/16 | 소품 9종 — Varco 생성 8개 + 바구니 | 원본 `Assets/Art/_Source/<이름>/` (손대지 않음), 프리팹 `Assets/Art/Props/<id>/pf_prop_<id>.prefab` (`alien_glowing_plant` · `glowing_plant_a` · `glowing_plant_b` · `dropper_bottle` · `leaf_pouch` · `rock_mound` · `gem_bucket` · `gem_shield`), `Assets/Art/Props/Basket/pf_prop_basket.prefab` | 루트 아래 `Model`, 피봇 바닥 가운데. 크기는 임시값(가장 긴 변 0.9 · 0.8 · 0.8 · 0.2 · 0.3 · 1.0 · 0.4 · 0.6 m) — 배치하며 바꿔도 됨. 바구니는 열매 87개 포함 · 지름 0.53 m, 손잡이 위치 자식 `GripSocket`, 드는 스크립트는 뺌. Varco 노멀맵이 2048이라 나중에 줄일 예정 | 쓰임새(진열대 장식 · 상품 등) 알려 주면 크기 맞춤 | |
| 10/19 | 수정(크리스탈) 셰이더 · 모델 · 재질 (씬 배치 없음) | `Assets/Art/Environment/Crystal/` (`Crystal_Shards.fbx` 조각 16개, 수정 재질 `M_Crystal_<색>` 12개, 빛 웅덩이 재질 `M_GlowPool_<색>` 7개, `T_MatCap_Crystal.png`), `Assets/Art/Environment/Shaders/ToonCrystal.shader` · `ToonCrystalForward.hlsl` · `GlowPool.shader` | 배경 소품용. `ToonCrystal` = 불투명 셀 셰이딩 + 은은한 발광 맥동(5 m 구역마다 박자 공유) + 밑동에서 퍼지는 빛 · 지면 안개 · 받는 그림자 끔(토글), 재질 안에 반투명 유리 모드도 있음. `GlowPool` = 수정 밑 땅에 눕힌 Quad에 쓰는 가산 빛(박자마다 빛 고리), 실제 라이트 아님. 그림자는 렌더러에서 끄는 걸 권장 | 쓰려면 Quad를 땅에 눕혀(X 90°) `M_GlowPool_*`. 재질 색은 밤 기준 | |
| 10/20 | 요청서(art) 파일명으로 맞춤 + 계산기 · 나무 텍스처 · `ToonSoft` 셰이더 (씬 배치 없음) | **대응표 `Assets/Art/mapping.md`**. 새: `Props/counter/` (`pf_prop_counter`) · `Props/_Shared/Textures/tex_prop_wood*` · `Props/Shaders/ToonSoft.shader`. 이름 바뀜: 바구니 → `Props/basket_customer/` (`pf_prop_basket_customer`), 등불 열매 → `Crops/lanternfruit/` (`crop_lanternfruit_mature.fbx`), 가게 문 → `Environment/farm_door/` (`env_farm_door.fbx`), 지면 텍스처 → `tex_env_ground_tile_*`, `leaf_pouch` → `Props/seedbag/pf_tool_seedbag`, `dropper_bottle` → `Props/fertilizer/pf_tool_fertilizer`, 그 밖의 배경 에셋도 `env_` · `mat_` · `tex_` · `pf_` 규칙으로 | 파일명 = 요청서 id(모델 `<id>.fbx`, 프리팹 `pf_<id>`, 재질 `mat_<id>_부위`, 텍스처 `tex_<id>_맵`). **GUID는 그대로라 씬 · 재질 연결은 안 바뀜**, 씬 오브젝트 이름 `Shop_Door` · `LanternFruit`도 유지. 요청서와 다른 점(계산대 탁자가 아니라 금전등록기, 바구니에 열매가 담김, 재질 슬롯 이름 등)은 mapping.md에 적음 | 1) `prop_counter`(금전등록기)와 계산대 탁자를 따로 둘지 2) 손님 바구니를 빈 0.35 m로 줄일지 3) `PlaceholderDataSetup.cs`의 하늘 재질 경로를 바꿔도 되면 알려 주세요 | |
| 10/20 | 소품 3종 — 쓰레기통 · 물뿌리개 · 비료병(Varco 원본 → 재모델 교체) (씬 배치 없음, 그레이박스는 안 건드림) | `Props/trashbin/pf_prop_trashbin`, `Props/wateringcan/pf_tool_wateringcan_iron`, `Props/fertilizer/pf_tool_fertilizer`(같은 GUID, `Model`만 바뀜), 공용 재질 `Props/_Shared/Materials/mat_prop_shared_*`, 새 셰이더 `Props/Shaders/ToonLiquid.shader`, `Environment/Shaders/ToonCrystal` 갱신(기능 추가 · 기본값 꺼짐이라 기존 수정 모습 그대로) | 루트 아래 `Model`. 쓰레기통 → 그레이박스 `trash_bin` 자리, 물뿌리개 · 비료병 → 도구 `heldPrefab` 자리. 크기는 요청서 기준(쓰레기통은 0.62 × 0.75 m 그대로) | 그레이박스 `Model` 교체 · `heldPrefab` 연결 부탁드립니다. 자세한 건 `Assets/Art/mapping.md` | 10/20 5-2 — 쓰레기통 `Model` 교체(콜라이더 0.62 × 0.75 × 0.63). 물뿌리개 · 비료병은 도구 단계에서 |
| 10/20 | 월광 뿌리 3단계 + 씨앗 둔덕 (Varco 소품을 요청서 이름으로) · 소품 2개 제거 | `Crops/moonroot/pf_crop_moonroot_sprout` · `_mature` · `_giant`, `Crops/common/pf_crop_common_seeded` | 옛 `glowing_plant_a` · `alien_glowing_plant` · `glowing_plant_b` · `rock_mound` (프리팹 GUID 그대로). 크기를 요청서 기준으로(새싹 0.25 m · 완성 0.5 m · 거대 높이 1.6 m · 둔덕 0.8 m). `gem_bucket` · `gem_shield`(와 `_Source` 원본)는 짝이 없어서 지움 | `CropData` 단계 프리팹 연결 부탁드립니다. 거대 작물을 밭 4칸 폭(3.5 m)으로 더 키울지 알려 주세요 | 10/20 5-2 연결 — 둔덕 · 새싹 · 완성을 `CropData` 단계에 연결(게임용 Variant `_Project/Prefabs/Farm/Crops/`). 거대는 4-4(거대 작물)에서 연결 · 크기 답은 그때 |
| 10/20 | 발광 조명 2종 (기둥형 · 벽걸이, 씬 배치 없음) | `Props/lamp_glow/pf_prop_lamp_glow_stand`, `pf_prop_lamp_glow_wall` (모델 · 재질 5 · 텍스처 4), 새 셰이더 `Props/Shaders/LanternCrystal.shader` | 요청서 `prop_lamp_glow` (2종 중 택1이라 둘 다 넣음). 기둥형 높이 1.8 m, 벽걸이 0.55 m. 등 유리는 셰이더로 빛나 보이기만 함 — 실제 라이트 없음 | 가게에 Point Light를 둘지(씬 전체 4~6개 제한) 알려 주세요 | 10/20 5-2 — 기둥형 4개 배치, Point Light는 계산대 옆 · 진열대 양 끝 3개만(입구는 발광만). 벽걸이는 벽이 생기면 |
| 10/20 | 계산대 종 (씬 배치 없음) | `Props/counter/pf_prop_counter_bell`, 공용 재질 `mat_prop_shared_gold` · `_mint` 추가 | 계산대(`Counter`) 위에 등록기(`pf_prop_counter`)와 같이 올리는 소품, 0.17 m | 등록기 · 종을 계산대 큐브 위(높이 1.0 m)에 놓아 주세요 | 10/20 5-2 — 새 `pf_prop_counter`(탁자 + 등록기 + 종)째로 계산대에 연결 |
| 10/20 | 물뿌리개 텍스처 (UV 새로 펴고 원본 색 굽기) | `Props/wateringcan/Models/tool_wateringcan_iron.fbx`(같은 GUID, UV만 바뀜), `Textures/tex_tool_wateringcan_iron_albedo.png`, `Materials/mat_tool_wateringcan_iron_paint.mat` | `pf_tool_wateringcan_iron` 몸체 재질이 단색 → 텍스처 재질로 | 없음 | |
| 10/20 | 물뿌리개 업그레이드 단계 금 · 다이아 | `Props/wateringcan/pf_tool_wateringcan_gold` · `pf_tool_wateringcan_diamond` (+ 텍스처 2 · 재질 2) | 요청서 D: 철 · 금 · 다이아 = 같은 메시 + 텍스처만 3장. 철 텍스처의 붓질을 살리고 색만 바꿈 | 게임 데이터 `tool_watering_1~4`는 4단계인데 요청서는 3단계 — 4단계를 어떻게 할지(예: 4단계도 다이아, 또는 색 하나 더) 알려 주세요 | |
| 10/20 | 가게 문 텍스처 (Varco 원본 색 굽기) | `Environment/farm_door/Textures/tex_env_farm_door_{frame,door,stairs}_albedo.png`, 재질 3개 갱신(같은 GUID) | 배경 씬 `Shop_Door`가 단색 → 원본 문 그림(창문 · 문양 · 나무결)으로. 재질 밝기는 예전과 비슷하게 맞춤 | 없음 | |
| 10/20 | 가게 문 마무리 — 문짝 · 채광창을 모델 모양대로 칠하고 유리 푸른 발광 | `Environment/farm_door/` 텍스처 · 발광 마스크 · 재질 3개, `Models/env_farm_door.fbx`(채광창 UV만 키움, 부품 위치 · 회전 · 문짝 경첩 기준점 그대로), `Environment/Shaders/ToonWicker.shader`(발광 마스크 기능 추가 — 기본값 꺼짐이라 바구니 등 다른 재질은 그대로) | 배경 씬 `Shop_Door` 모습만 바뀜 | 없음 | |
| 10/20 | 가게 소품 — 계산대 탁자 · 손님 바구니 받침대 · 대걸레 + 가게 바닥 (씬 배치 없음, 그레이박스는 안 건드림) | `Props/counter/pf_prop_counter`(**새로: 탁자 + 등록기 + 종**), 등록기는 `pf_prop_counter_register` · `Models/prop_counter_register.fbx`로 이름만 바뀜(GUID 그대로), `Props/basket_stand/pf_prop_basket_stand`, `Props/mop/pf_tool_mop`, `Environment/shop_floor/` (재질 1 · 텍스처 3), 공용 `mat_prop_shared_woodplanks` · `tex_prop_woodplanks_orange_albedo` | 계산대 · 받침대는 Varco 원본을 다시 모델링(원본 비율 유지라 요청서 크기와 다름 — mapping.md), 대걸레는 원본 모델 + 툰용 텍스처, 피벗 = 손잡이. 바닥은 `env_shop_floor` 타일 | 그레이박스 `Counter` · `BasketStand`의 `Model` 교체, 대걸레 `heldPrefab` 연결, 가게 바닥에 `mat_env_shop_floor` 부탁드립니다. 10/20 오전에 `pf_prop_counter`(등록기)를 이미 끼웠다면 그 자리는 등록기 그대로 보입니다 | 10/20 5-2 — 계산대 · 받침대 `Model` 교체(받침대는 열린 앞면이 손님 쪽 −X로 Y 270°), 바닥 재질 연결, NavMesh 다시 구움. 대걸레 `heldPrefab`은 도구 단계에서 |
| 10/20 (개발) | 10/16 ~ 10/20 납품 전부 dev 병합 (`87ce793`) — 폴더 밖 변경 0, 컴파일 오류 0, 테스트 통과 | — | 게임 연결(작물 단계 프리팹 · 가게 소품 · 물뿌리개 · 조명)은 다음 작업 5-2에서 asset_placement 순서대로. 위 요청 칸 질문은 그때 답함 | 셰이더 `[Header(...)]` 문구에 괄호 · 쉼표가 있어 Unity가 머리글을 못 그린다는 경고 6개(Relief · Emission · Cut Face Glow) — 문구에서 괄호 · 쉼표를 빼 주세요. 배경 `CharacterShadow`가 플레이 시 "no character renderers" 경고 1개 — 캐릭터가 없을 때 경고를 끄거나 Log로 바꿔 주세요 | 10/20 병합 |
| 10/20 | 물뿌리개 3단계 방향 수정 | `Props/wateringcan/pf_tool_wateringcan_iron` · `_gold` · `_diamond` (`Model` 회전 Y 90°만) | 피벗은 원래대로 손잡이 윗부분(쥐는 곳). 주둥이가 −X를 보던 것을 요청서대로 **+Z(화면 안쪽)**로 돌림. 주둥이 끝 `WateringCan_SpoutTip` = (0, −0.07, 0.29) | 이미 코드에서 회전을 보정했다면 그 보정은 빼 주세요 | |
| 10/20 | 비료병 액체가 안 보이던 문제 수정 | `Props/Scripts/LiquidWobble.cs` (새), `Props/fertilizer/pf_tool_fertilizer` (액체 `Model/tool_fertilizer_liquid`에 컴포넌트 추가) | `ToonLiquid` 셰이더는 액체 위치를 이 스크립트에서 받아 수면 위를 잘라냄 — 지난 납품 때 스크립트가 빠져서 액체가 안 보였음. 병을 움직이면 수면이 출렁임(화면 효과만, 게임 로직 없음) | 비료를 쓸 때 `LiquidWobble.Fill`(0~1, 기본 0.75)을 내려 주면 병 속 액체가 줄어듭니다(선택) | |
| 10/20 | 외곽선 두께를 모델 크기에 비례하게 + `ToonSoft` 점 조명 | `Props/Shaders/ToonSoft.shader`, `Environment/Shaders/ToonCrystal` · `ToonStem` · `ToonWicker.shader` | 외곽선 두께(`_OutlineWidth`)가 "모델 원래 크기 기준 m"가 됨 — 프리팹에서 0.18배로 줄인 비료병은 선도 0.18배. 지금까지 줄인 소품(비료병 · 물뿌리개 · 조명 등)은 선이 상대적으로 너무 두꺼웠음. `ToonSoft`는 Point · Spot Light도 받음(`Point / Spot Light Strength`, 기본 1) — 씬에 점 조명이 없으면 모습 그대로 | 없음 (크기를 바꿔 배치해도 선 두께가 같이 맞춰짐) | |
| 10/20 | 쓰레기통 폴리곤 줄임 (8,658 → 4,066 tris) | `Props/trashbin/Models/prop_trashbin.fbx` (같은 GUID, 프리팹 그대로) | 원본 모델은 2,100 tris — 내보낼 때 모서리 둥글림이 3단이라 4배로 늘어 있었음 → 1단으로. 크기 · 뚜껑 분리 · 재질 그대로 | 없음 (예산 2천보다는 아직 많음 — 더 줄여야 하면 알려 주세요) | |
| 10/20 | 밭(두둑 + 칸 흙) · 괭이 · 심기 흙 연출 · 등불 열매 아이템 · 물방울 텍스처 · 얼룩 데칼 (씬 배치 없음, 그레이박스는 안 건드림) | `Environment/plot/` (`pf_env_plot`, `pf_env_plot_soil`, 재질 2 · 텍스처 1), `Environment/Shaders/ToonSoil.shader`, `Environment/Scripts/SoilFX.cs`, `Crops/lanternfruit/pf_item_lanternfruit` (+ 모델 · 재질 2), `Environment/FX/Textures/vfx_water_drop.png`, `Environment/decals/` (`decal_stain_01.png`, `mat_decal_stain_01`) | 칸 흙은 `farm_cell`의 `Soil` 교체용 — 흙 색은 지금처럼 `FarmCellView`가 칠함. `SoilFX`: 괭이가 땅에 닿을 때(스윙 약 35%) `OnTillHit(진행도, 맞은 위치)` → 맞은 자리부터 파이고 흙덩이가 튐, 진행도 1이면 이랑 3줄. 심을 때 `OnPlant()` → 구덩이 → 덮인 둔덕. 젖음 · 물 파티클은 요청서대로 개발 몫이라 안 넣음. 자세한 건 mapping.md | 1) `farm_cell` `Soil` 교체 + 위 두 함수 호출(각 한 줄) 2) 흙 색 3개가 질감을 곱하면 너무 어두움 — 예: 안 갈림 (0.16, 0.22, 0.30) · 갈림 (0.30, 0.44, 0.60) · 젖음 (0.16, 0.30, 0.52)로 밝혀 주세요 3) 데칼이 보이려면 `PC_Renderer`에 Decal 기능 추가, Technique = Screen Space 4) `item_lanternfruit` 진열 · 바구니 연결 | |
| 10/20 (개발) | 5-2 작물 연결 — 밭에서 단계별 모델로 보임 | `_Project/Prefabs/Farm/Crops/` (Variant 4 + `crop_lanternfruit_mature` 1.8 m) | 단계 칸이 빈 곳은 그레이박스 구로 보임 — 에셋이 오면 단계 칸만 채우면 됨 | 1) 버섯꽃 단계 모델(새싹 · 다 자람) 2) 등불 열매 묘목 · 줄기 단계 3) 등불 열매 열매를 따로(`fruit_*`) 나눌 수 있는지 4) 월광 뿌리는 성장 1일이라 새싹이 게임에서 보이지 않음(둔덕 → 완성) — 참고만 | 10/20 |
| 10/20 (개발) | 5-2 가게 소품 연결 — 계산대 · 바구니 받침대 · 쓰레기통 · 조명 4 · 가게 바닥 | `_Project/Prefabs/Shop/counter` · `basket_stand` · `lamp_glow_stand`, `Prefabs/Cleaning/trash_bin`, Shop 씬 | 진열대 · 창고 상자 · 창고 선반은 모델이 없어 그레이박스 그대로 | 1) `prop_shelf_produce`(진열대 6칸 모듈 1.8 m), `prop_storage_chest`, `prop_storage_shelf` 2) 받침대 위 장식용 **빈 바구니**(0.35 m) — 지금 바구니는 열매가 담겨 있어 받침대에 못 올림 | 10/20 |
| 10/21 (개발) | 5-2 도구 · 농장 문 연결 — 손에 드는 모델(물뿌리개 3종 · 대걸레 · 비료병 · 씨앗 주머니), 농장 문 | `_Project/Prefabs/Held/held_*`(아트 프리팹을 감싸 1인칭 위치 · 크기만 맞춤), `_Project/Prefabs/Zones/zone_door`(Model = `env_farm_door.fbx` ×0.65) | 물뿌리개 4단계는 다이아 + 발광 강화 재질 베리언트(개발 쪽 `_Project`). 아트 파일은 손대지 않음 | 1) 물뿌리개 4단계 표현을 이대로 갈지(다이아 + 발광) 2) 대걸레 단계별 재질(요청서: 같은 메시 · 단계별 재질) 3) 괭이 모델(`tool_hoe`, 요청서에 없음 — 추가 요청) 4) 배경 씬의 `Shop_Door`(22, 0, −9)가 게임의 농장 문(7, 0, 0)과 같은 모델이라 두 개 보임 — 배경 쪽 문을 끌지 알려 주세요 5) `env_plot` · `env_farm_path` · 가게 벽/천장/입구 일정 | 10/21 |
| 10/21 | 괭이 3단계 · 대걸레 3단계 · 가게 벽 · 천장 · 입구 · 배경 문 끔 (그레이박스는 안 건드림) | `Props/hoe/pf_tool_hoe_worn` · `_iron` · `_glow` (+ 모델 · 텍스처 4 · 재질 3), `Props/mop/pf_tool_mop`(같은 GUID, 재질만 낡은 단계로) · `pf_tool_mop_good` · `pf_tool_mop_glow` (+ 텍스처 4 · 재질 3), `Environment/shop_wall/pf_env_shop_wall` (+ 재질 `mat_env_shop_wall` · `mat_env_shop_trim`), `Environment/shop_ceiling/pf_env_shop_ceiling` (+ 재질 1), `Environment/shop_entrance/pf_env_shop_entrance`, `Props/Shaders/ToonSoft.shader`(발광 마스크 슬롯 추가 — 기본 흰색이라 기존 재질 모습 그대로), `Scenes/BackGroundScene.unity`(`Shop_Door`만 끔) | 괭이: 피벗 = 손잡이 가운데, 날 위(+Y) · 날 끝 +Z(그레이박스 괭이 기준), 낡은 · 철 · 발광 = `tool_hoe_1~3`. 대걸레: 낡은(회갈색 천) · 좋은(크림 천 + 청록 끈) · 발광(술 끝 시안) = `tool_mop_1~3` — 기존 `pf_tool_mop`이 1단계 모습으로 바뀜. 가게: 벽 · 천장 피벗 = 가게 바닥 가운데라 `ShopRoot` (500, 0, 0)에 그대로, 입구는 (500, 0, −5.1). 문 구멍은 지금 손님 입구 `CustomerEntrance`(x 500)와 맞음. 배경 씬의 장식 문 `Shop_Door`(22, 0, −9)는 게임 농장 문과 같은 모델이라 껐음 | 1) 괭이 · 대걸레 2 · 3단계 `heldPrefab` 연결(1인칭 위치 · 크기는 `held_*` 감싸기로) 2) 가게 벽 · 천장 · 입구 배치 + **벽 콜라이더**(프리팹엔 없음 — 벽 4면 상자 콜라이더나 MeshCollider), NavMesh 다시 굽기 3) 창문은 뚫려 있어 밖의 하늘이 보임 — 유리가 필요하면 알려 주세요 4) 질문 답: 물뿌리개 4단계 = 지금처럼 다이아 + 발광 강화로 확정 | |
| 10/21 | 물뿌리개 4단계 "발광 다이아" (같은 날 수정) | `Props/wateringcan/pf_tool_wateringcan_bright` — 몸통은 다이아 칠 그대로 + `Models/tool_wateringcan_bright_gem.fbx`(보석 + 금테, 568 tris) · `Materials/mat_tool_wateringcan_bright_gem` | 처음 올린 짙은 파랑 + 발광 선 버전은 지웠음(재질 1 · 텍스처 2). 이제 3단계와 같은 흰 다이아 몸통에 양 옆면 가운데 시안 물방울 보석 + 금테로 구분. 피벗 · 방향은 다른 단계와 같음 | `held_wateringcan_diamond_bright`의 모델을 이 프리팹으로 바꾸고, `_Project/Art/Materials/mat_tool_wateringcan_diamond_bright` 변형 재질은 지워 주세요 | |
| 10/21 | 가게 입구 문짝 (농장 문 문짝 재사용) | `Environment/shop_entrance/pf_env_shop_entrance` (같은 GUID) — 자식 `Door_L_Hinge` · `Door_R_Hinge` 추가, 메시 · 재질은 `farm_door`의 문짝 그대로 | 문은 **항상 열린 상태**로 프리팹에 고정(바깥쪽으로 80°). 플레이어가 드나들 때도 열려 있는 판정으로 처리하고, 손님은 문을 신경 쓰지 않고 지나가면 됨. 문짝에 콜라이더 없음 | 문 열고 닫기 코드는 만들지 말아 주세요(열린 판정 고정). | |
| 10/21 | 가게 입구 밖 포탈 + 농장 문 포탈 (VFX) | `Environment/FX/portal/pf_vfx_portal` (+ 재질 1), `Environment/shop_entrance/pf_env_shop_entrance`(같은 GUID, 자식 `Portal` 추가: 어두운 판 `Portal_Void` + 포탈 VFX), `Environment/shop_entrance/Materials/mat_env_shop_portal_void`, `Scenes/BackGroundScene.unity`(`ShopGate_Portal` 1개 추가) | 가게: 열린 문 너머 빈 바깥이 안 보이게 어두운 판 + 빛 소용돌이. 배경 씬 포탈은 같은 날 농장 문 프리팹 안으로 옮김(아래 줄) | 없음 | |
| 10/21 | 농장 문 프리팹 + 열림 애니메이션 (아트 쪽 첫 문 프리팹) | `Environment/farm_door/pf_env_farm_door.prefab` (`Model` + `Portal` + Animator), `Environment/farm_door/Animations/` (클립 3 · `ac_env_farm_door`), `Scenes/BackGroundScene.unity`(장식 문 `Shop_Door` · 임시 `ShopGate_Portal` 지움) | Bool `IsOpen` 하나로 열고 닫음: 켜면 0.9초 동안 문짝 두 개가 80° 안쪽(계단 반대)으로 열리며 뒤의 포탈이 커지고, 끄면 반대. 기본은 닫힘. 피벗 · 크기는 FBX 그대로라 지금 `Model` 자리에 같은 Transform으로 바꿔 끼우면 됨 | 1) `_Project/Prefabs/Zones/zone_door`의 `Model`(env_farm_door.fbx ×0.65)을 `pf_env_farm_door`로 교체 — Transform 그대로 2) 문을 열 때(상호작용 · 근접 판정 등) 루트 Animator의 `IsOpen`을 켜고 끄기 한 줄 | |
| 10/21 | 버섯꽃 3종 (작은 갓 · 완성 · 거대) | `Crops/mushroomflower/pf_crop_mushroomflower_small` · `_mature` · `_giant` (+ 모델 3 · 텍스처 9 · 재질 3) | 요청서 A-4 · A-5 크기 그대로(0.3 m · 0.6 m, 3천 tris, 피벗 바닥 가운데). 월광 뿌리와 같은 ToonWicker 재질에 꽃잎 테두리 보라 발광. **성장 · 단계 전환 애니메이션 포함**: 둔덕 → 작은 갓은 땅에서 솟아오르고(0.9초, 빛 가루), 작은 갓 → 완성은 작은 갓 크기에서 이어서 커지며 보라 포자가 퍼짐(0.8초), 그 뒤 살랑임 — 공용 `Crops/common/Animations/`(`ac_crop_grow` · `ac_crop_grow_up`), 코드 필요 없음. 거대 버섯꽃(A-6, 3.5 × 2.0 × 3.5 m)은 나타날 때 커지며 큰 포자가 퍼짐 | 버섯꽃 작물 단계 칸 연결 부탁드립니다: `crop_mushroomflower` 0번 = 지금처럼 공용 `crop_common_seeded`(아래 줄에서 모습만 바뀜), 1번 = `pf_crop_mushroomflower_small`, 2번 = `pf_crop_mushroomflower_mature`, 거대 작물 = `pf_crop_mushroomflower_giant`(4칸 한가운데). 불러오기 직후 모든 칸이 한꺼번에 솟아오르는 게 싫으면 알려 주세요(첫 표시만 건너뛰게 바꿀 수 있음) | |
| 10/21 | 씨앗 심은 단계(공용) 교체 | `Crops/common/pf_crop_common_seeded` (같은 GUID — `_Project`의 `crop_common_seeded` 변형이 그대로 따라감), `Models/crop_common_seeded_bud.fbx`, `Materials/mat_crop_seeded_glow` | 돌 박힌 흙 무더기가 밭 흙의 심기 둔덕(SoilFX)과 겹쳐 어색해서, 흙에서 살짝 나온 빛나는 시안 씨앗 + 잎 두 장으로. 월광 뿌리 · 버섯꽃 · 등불 열매 1단계가 모두 바뀜 | 없음. 단 밭 흙에 `SoilFX.OnPlant()`를 연결하기 전에는 둔덕이 없어 씨앗이 흙 위에 0.05 m쯤 떠 보입니다 | |
| 10/21 | 물뿌리개 4단계 텍스처 교체 (팀 피드백 '도구 등급끼리 색 통일') | `Props/wateringcan/Textures/tex_tool_wateringcan_{iron,gold,diamond}_albedo.png`(같은 GUID), 새 `tex_tool_wateringcan_diamond_glow_mask`, 재질 `_gold_paint` · `_diamond_paint` · 새 `_bright_paint`, `pf_tool_wateringcan_bright`(보석 뺌). 보석 메시 · 재질은 지움 | 1단계 은회색 + 은은한 구리빛, 2단계 은회색 + 소용돌이 무늬(노란 금색 아님), 3단계 다이아 시안 선 발광, 4단계는 같은 다이아 무늬가 더 세게 빛남 + 시안 테두리. 프리팹 이름은 그대로(`_gold`도 유지) | 없음 (이전 줄의 '보석 + 금테'는 취소) | |
| 10/21 | 물뿌리개 텍스처 보정 | `Props/wateringcan/Textures/tex_tool_wateringcan_{iron,gold,diamond}_albedo.png` · `_diamond_glow_mask`(같은 GUID) | 옮겨 구울 때 빈 픽셀이 검은 띠로 보이던 것 채움, UV 조각 경계의 색 끊김 보정, 1단계 구릿빛 약 80 % | 없음 | |
| 10/21 | 농산물 진열대 2종 (팀원 제작) | `Props/shelf_produce/pf_prop_shelf_produce` (3단 선반 1.8 × 1.4 × 0.9 m) · `pf_prop_shelf_produce_2` (낮은 판매대 1.8 × 0.9 × 1.4 m, 수정 발광) (+ 모델 2 · 텍스처 3 · 재질 2) | 1.8 × 1.4 × 0.9 m 3단 선반, 앞면 +Z(낮은 단이 앞), 피벗 바닥 가운데. 칸 2열 나눔 · 가격표는 없음. 둘 다 AO를 텍스처에 구워 넣고 셰이딩(그림자 경계 · 림)을 다듬음 | Shop `Shelf`의 그레이박스(6.2 m 상자) 자리에 두 종류를 섞어 배치해 주세요(예: 선반 · 판매대 · 선반, Y 180°). 진열 칸 위치(ShelfDisplay)는 단 높이에 맞춰 조정 필요 | |
| 10/21 | 등불 열매 단계 3종 + 성장 애니메이션 | `Crops/lanternfruit/pf_crop_lanternfruit_sapling` · `_stem` · `_mature` (묘목은 Varco 묘목을 나무 색으로 다시 칠함, 줄기 · 완성은 완성 모델 하나로), `Crops/common/Animations/anim_crop_lanternfruit_unfurl` · `ac_crop_lanternfruit_unfurl` · `anim_crop_lanternfruit_ripen` · `ac_crop_lanternfruit_ripen` | 묘목 0.5 m(보라 줄기 · 파란 잎으로 나무와 같은 색, 솟아오름) → 줄기 1.2 m(고사리처럼 말린 줄기가 아래부터 풀리며 뻗고, 가지 · 잎 · 꽃봉오리가 뒤따라 펴짐, `anim_crop_lanternfruit_unfurl` · `ac_crop_lanternfruit_unfurl`) → 완성 1.8 m(줄기가 아래 마디부터 늘어나고 끝이 마저 펴진 뒤 봉오리 7개가 차례로 부풀어 열매). 줄기 · 잎에 손그림 질감(`tex_crop_lanternfruit_stem_paint` · `_leaf_paint`, ToonStem/ToonLeaf에 Paint Detail 슬롯 추가, 기본값 꺼짐이라 다른 머티리얼 영향 없음). 코드 필요 없음(단계 프리팹이 생성될 때 재생) | `crop_lanternfruit` 단계 칸: 0 공용 씨앗 · 1 `pf_crop_lanternfruit_sapling` · 2 `pf_crop_lanternfruit_stem` · 3 `pf_crop_lanternfruit_mature`(지금 `_Project` 변형 대신). 10/20 질문 답: 열매는 이미 `LanternFruit_Fruit_01~07`로 분리돼 있어 재수확 때 이것만 껐다 켜면 됩니다 | |
