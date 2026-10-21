# 아트 에셋 ↔ 요청서(art) 대응표

요청서: 노션 "아트 에셋 요청서 (art)" (이진수, 2026-10-08). 파일명은 요청서 id 그대로 — 모델 = `<id>.fbx`, 프리팹 = `pf_<id>`, 재질 = `mat_<id>_부위`, 텍스처 = `tex_<id>_맵`.
프리팹은 모두 루트(0,0,0 / 1,1,1) 아래 `Model` 자식 구조. 크기는 프리팹에서 실제 m로 맞춤.

## 요청서 항목

| 요청서 id | 프리팹 · 모델 (Assets/Art/…) | 실제 크기 | 요청서와 다른 점 |
|---|---|---|---|
| `crop_lanternfruit_mature` | `Crops/lanternfruit/Models/crop_lanternfruit_mature.fbx` (씬 `BackGroundScene`의 `LanternFruit`) | 높이 약 1.8 m | 프리팹 없음(씬 배치만, 꺼 둠). 열매 `fruit_*` 분리 · 재질 슬롯 이름(`M_Base` 등) 미적용 |
| `item_lanternfruit` | `Crops/lanternfruit/pf_item_lanternfruit.prefab`, `Models/item_lanternfruit.fbx` | 0.125 × 0.15 × 0.125 m · 824 tris | 덩굴 열매와 같은 셰이더 `ToonLanternShell`. 진열대 칸(0.5 × 0.4)에 2 × 2. 피벗 바닥 가운데(아래를 평평하게 눌러 섬). 셰이더 값이 모델 좌표 기준이라 크기는 프리팹 `Model`에서 줄임(0.076배) — 크기를 바꿀 땐 루트에서 |
| `crop_common_seeded` | `Crops/common/pf_crop_common_seeded.prefab` (원본 `_Source/Teal_Rock_Mound_1240`) | 0.80 × 0.17 × 0.80 m | 씨앗 심은 흙 둔덕(공용). 옛 이름 `rock_mound` |
| `crop_moonroot_sprout` | `Crops/moonroot/pf_crop_moonroot_sprout.prefab` (원본 `_Source/Glowing_Fantasy_Plant_2029`) | 높이 0.25 m | 옛 이름 `glowing_plant_a` |
| `crop_moonroot_mature` | `Crops/moonroot/pf_crop_moonroot_mature.prefab` (원본 `_Source/Alien_Glowing_Plant_2163`) | 높이 0.50 m | 옛 이름 `alien_glowing_plant`. 뿌리 전체가 보임(요청서는 반쯤 묻힘 → 밭 칸에서 조금 내려 박아도 됨) |
| `crop_moonroot_giant` | `Crops/moonroot/pf_crop_moonroot_giant.prefab` (원본 `_Source/Glowing_Fantasy_Plant_4596`) | 1.37 × 1.60 × 1.44 m | 옛 이름 `glowing_plant_b`. 높이 기준으로 키움 — 요청서 폭 3.5 m(밭 4칸)까지 키우려면 크기만 더 올리면 됨 |
| `tool_wateringcan_gold` · `tool_wateringcan_diamond` | `Props/wateringcan/pf_tool_wateringcan_gold.prefab` · `pf_tool_wateringcan_diamond.prefab` | 철과 같음 | **같은 메시 + 텍스처만 바꾼 업그레이드 단계**(요청서대로). `tex_tool_wateringcan_{gold,diamond}_albedo` = 철 텍스처의 붓질 명암에 색만 입힘. 재질 `mat_tool_wateringcan_{gold,diamond}_paint` — 금은 광택 강하게, 다이아는 광택 + 하늘색 테두리 빛 + 밝은 부분 약한 발광 |
| `prop_lamp_glow` (기둥형) | `Props/lamp_glow/pf_prop_lamp_glow_stand.prefab`, `Models/prop_lamp_glow_stand.fbx` | 0.73 × 1.80 × 0.88 m | 등은 `Model/Lantern_Crystal`(셰이더 `Props/Shaders/LanternCrystal`, 발광). 원래 4 m → 요청서 1.8 m로 줄임. 실제 빛(Point Light)은 없음 |
| `prop_lamp_glow` (벽걸이) | `Props/lamp_glow/pf_prop_lamp_glow_wall.prefab`, `Models/prop_lamp_glow_wall.fbx` | 0.55 × 0.55 × 0.38 m | 등은 `Model/Lantern_Scale`. 피벗 바닥 가운데 — 벽에 붙일 때 벽판 뒷면이 +X 쪽 |
| `prop_counter` | `Props/counter/pf_prop_counter.prefab`, `Models/prop_counter.fbx` | 1.60 × 0.61 × 1.14 m (ㄱ자) | **계산대 탁자** (10/20 저녁 교체). Varco 원본을 다시 모델링 — 판자 상판 · 청록 틀, 안쪽은 서랍 없이 뚫린 선반. 위에 자식으로 등록기 `Register`(= `pf_prop_counter_register`)와 종 `Bell`(= `pf_prop_counter_bell`)이 얹혀 있음. 요청서 크기 1.4 × 1.0 × 0.7 m와 다름(원본 비율 유지). 장식 보석은 나중에 수정(크리스탈)으로 추가 예정 |
| (계산대 소품 · 돈통) | `Props/counter/pf_prop_counter_register.prefab`, `Models/prop_counter_register.fbx` | 약 0.41 × 0.40 × 0.39 m | 금전등록기(서랍 = 돈통, `Model/CashRegister_Drawer`). 10/20 오전까지 `pf_prop_counter`였던 것 — **이름만 바뀜, GUID 그대로**라 이미 끼운 곳은 그대로 등록기가 보임. 재질 5개 `mat_prop_counter_*` |
| `prop_basket_customer` | `Props/basket_customer/pf_prop_basket_customer.prefab`, `Models/prop_basket_customer.fbx` | 지름 0.53 m | **열매 87개가 담긴 상태**(요청서는 빈 바구니 0.35 m). 손잡이 위치 = 자식 `GripSocket`. 크기 · 빈 바구니 여부 알려 주시면 맞춤 |
| `env_farm_door` | `Environment/farm_door/Models/env_farm_door.fbx` (씬 `Shop_Door`, (22, 0, −9)) | — | 프리팹 없음(씬 배치). 재질 3개 `mat_env_farm_door_*` — 사용자가 편 UV에 Varco 원본(상점 입구1) 색을 구운 텍스처 `Textures/tex_env_farm_door_{frame,door,stairs}_albedo` 연결 |
| `env_ground_tile` | `Environment/Ground/Textures/tex_env_ground_tile_*`, `Environment/DeepBlue/Textures/tex_env_ground_tile_deepblue_*` | 1024 타일 | 여러 벌(보라 A/B/C · 길 · 딥블루 A/B/C) — 지형 레이어 `TL_*`가 사용 중 |
| `env_shop_floor` | `Environment/shop_floor/Materials/mat_env_shop_floor.mat`, `Textures/tex_env_shop_floor_{albedo,normal,ao}.png` | 1024 타일 | 가게 바닥 나무 판자. 바닥 12 × 10 m 평면에 쓸 때 타일링 5 × 4.17(재질에 들어 있음) |
| `env_plot` | `Environment/plot/pf_env_plot.prefab`, `Models/env_plot.fbx` | 4.0 × 0.2 × 4.0 m · 192 tris | 두둑 테두리(2×2칸 = 4 × 4 m). 판자 + 청록 기둥 + 칸 사이 낮은 칸막이 + 바닥 흙. 칸 흙은 따로(아래). 두둑 가운데가 피벗 — 칸 중심은 (±1, 0, ±1) |
| (env_plot 칸 흙) | `Environment/plot/pf_env_plot_soil.prefab`, `Models/env_plot_soil.fbx` | 1.66 × 0.11 × 1.66 m · 1,560 tris | `farm_cell`의 `Soil` 자식 교체용. 흰 바탕 + 회색 흙덩이 질감이라 `FarmCellView`가 칠하는 `_BaseColor`가 그대로 보임. 셰이더 `Environment/Shaders/ToonSoil` + 스크립트 `Environment/Scripts/SoilFX.cs`(`Model`에 붙어 있음): 괭이가 땅에 닿을 때 `OnTillHit(진행도 0~1, 맞은 위치)`, 심을 때 `OnPlant()`, 불러오기 후 `SetTill()`, 수확 후 `ResetSoil()`. 젖음은 기존처럼 색으로 |
| `tool_seedbag` | `Props/seedbag/pf_tool_seedbag.prefab` (원본 `_Source/Teal_Pouch_Glowing_Leaves_1296`) | 임시 0.3 m | Varco 원본 그대로, 피벗 바닥 가운데(요청서는 손잡이 쥐는 곳) |
| `tool_fertilizer` | `Props/fertilizer/pf_tool_fertilizer.prefab`, `Models/tool_fertilizer.fbx` | 높이 0.18 m | **Varco 원본 → 재모델로 교체** (프리팹 GUID 그대로). 병 · 액체 분리, 재질 `mat_tool_fertilizer_*` (셰이더 `ToonCrystal` · `Props/Shaders/ToonLiquid`). 액체 `Model/tool_fertilizer_liquid`에 `Props/Scripts/LiquidWobble.cs` — 병이 움직이면 수면이 출렁이고, **`Fill`(0~1)을 내리면 액체가 줄어듦**(비료 사용량 표시용). 이 스크립트가 없으면 액체가 안 보임. 원본은 `_Source/`에 그대로. 피벗 바닥 가운데 |
| (요청서 없음 · 계산대 소품) | `Props/counter/pf_prop_counter_bell.prefab`, `Models/prop_counter_bell.fbx` | 0.17 × 0.13 × 0.17 m | 계산대 위 종. 돔 `Model/Bell_Dome`, 누름쇠 `Model/Bell_Dome/Bell_Plunger` |
| `prop_trashbin` | `Props/trashbin/pf_prop_trashbin.prefab`, `Models/prop_trashbin.fbx` | 0.62 × 0.75 × 0.63 m | 뚜껑 `Model/TrashCan_Lid` 분리(열기 가능). 8.6천 tris (예산 2천) |
| `prop_basket_stand` | `Props/basket_stand/pf_prop_basket_stand.prefab`, `Models/prop_basket_stand.fbx` | 1.03 × 0.91 × 0.72 m | Varco 원본(손님바구니진열대)을 다시 모델링. 앞면 +Z가 열려 있고 판자 바닥에 바구니를 쌓는 형태. 요청서 0.6 × 0.9 × 0.6 m보다 넓음(원본 비율). 장식 보석은 자식 `Model/BasketStand_Gems`(재질 `mat_prop_shared_gem`) — 지워도 됨. 재질 `mat_prop_shared_woodplanks` · `_deepteal` |
| `tool_mop` | `Props/mop/pf_tool_mop.prefab`, `Models/tool_mop.fbx` | 길이 1.3 m | Varco 원본 모델 그대로, 텍스처만 손봄 — 원본에 그려진 명암을 눌러 툰 셰이딩에 맞춘 `tex_tool_mop_albedo`, 재질 `mat_tool_mop_cloth`(ToonSoft). **피벗 = 손잡이 쥐는 곳**(손잡이 위쪽 80 % 지점), 머리는 아래로 약 1.04 m. **단계 3종 (10/21)**: `pf_tool_mop`(같은 GUID) = 낡은 대걸레(`tool_mop_1`, 바랜 회갈색 천), `pf_tool_mop_good` = 좋은 대걸레(`_2`, 크림색 천 + 청록 끈), `pf_tool_mop_glow` = 발광 대걸레(`_3`, 술 끝 시안 발광 — `tex_tool_mop_glow_mask`). 같은 메시 + 텍스처 3장 · 재질 `mat_tool_mop_worn|good|glow`. 원래 파란 천 재질 `mat_tool_mop_cloth`는 안 쓰지만 남겨 둠 |
| `tool_wateringcan_iron` | `Props/wateringcan/pf_tool_wateringcan_iron.prefab`, `Models/tool_wateringcan_iron.fbx` | 길이 0.45 m | 피벗 = 손잡이, 주둥이 끝 `Model/WateringCan_SpoutTip`. UV를 새로 펴고 Varco 원본 색을 구운 텍스처 `tex_tool_wateringcan_iron_albedo`(재질 `mat_tool_wateringcan_iron_paint`). 금 · 다이아 색 변형은 아직 없음 |
| `tool_hoe` (요청서 없음 · 개발 추가 요청 10/21) | `Props/hoe/pf_tool_hoe_worn` · `_iron` · `_glow` (`Models/tool_hoe.fbx`) | 길이 0.83 m · 2,000 tris | Varco 원본 정리(메시 그대로). 3단계 = 같은 메시 + 텍스처 3장: 낡은(`tool_hoe_1`) · 철(`_2`) · 발광(`_3`). **피벗 = 손잡이 가운데**(그레이박스 괭이와 같은 기준), 날이 위(+Y), 날 끝 +Z. 발광 괭이는 `tex_tool_hoe_glow_mask`의 흰 줄무늬만 빛남(`ToonSoft` 새 슬롯 `Texture Glow Mask`) |
| `env_shop_wall` | `Environment/shop_wall/pf_env_shop_wall.prefab`, `Models/env_shop_wall.fbx` | 12.4 × 3.6 × 10.4 m · 1,580 tris | 가게 벽 4면 한 덩어리. **피벗 = 가게 바닥 가운데** → `ShopRoot`(500, 0, 0)에 그대로 놓으면 바닥 12 × 10 m 바깥에 0.2 m 두께로 둘러짐. 앞(−Z) 벽에 문 구멍 1.6 × 2.4 m(가운데 x 0), 창 1.9 × 1.4 m(창턱 0.9 m) 좌 · 우 1개씩 + 뒤 2개(x ±3). 창틀 · 걸레받이 · 윗몰딩 포함. 재질 `mat_env_shop_wall`(공용 오렌지 판자 텍스처를 가로로) · `mat_env_shop_trim`. 콜라이더 없음 |
| `env_shop_ceiling` | `Environment/shop_ceiling/pf_env_shop_ceiling.prefab`, `Models/env_shop_ceiling.fbx` | 12.4 × 2.6 × 10.4 m · 4,920 tris | 둥근 천장(벽 위 3.6 m에서 6.2 m까지) + 갈빗대. 피벗 = 가게 바닥 가운데(벽과 같은 자리에 놓음). 안쪽만 보이게 면이 아래를 봄 — 밖에서 보면 어둡게 보임. 재질 `mat_env_shop_ceiling`(가게 바닥 판자) · `mat_env_shop_trim` |
| `env_shop_entrance` | `Environment/shop_entrance/pf_env_shop_entrance.prefab`, `Models/env_shop_entrance.fbx` | 2.3 × 3.1 × 0.6 m · 360 tris | 손님 출입문 틀(기둥 · 상인방 · 문턱 + 바깥쪽 둥근 간판에 발광 수정 2개). 통로 폭 1.6 m. **피벗 = 문 통로 바닥 가운데** → 벽과 함께 쓰면 (500, 0, −5.1). 재질 `mat_env_shop_trim` · `mat_prop_shared_gem` |
| `tool_wateringcan` 4단계 (요청서 없음 · 개발 결정 10/21 "다이아 + 발광 강화") | `Props/wateringcan/pf_tool_wateringcan_bright.prefab` (같은 메시) | 길이 0.45 m | 몸통은 3단계와 같은 다이아 칠(`mat_tool_wateringcan_diamond_paint`) + 양 옆면 가운데에 금테 두른 시안 물방울 보석(`Models/tool_wateringcan_bright_gem.fbx` → `Model/WateringCan_GemSet`, 보석 `mat_tool_wateringcan_bright_gem`(ToonCrystal) · 금테 `mat_prop_shared_gold`, 568 tris). 3단계와는 보석으로 구분 |

## 요청서에 없는 것 (배경 · 추가분)

| 파일 | 내용 |
|---|---|
| `Environment/` 하늘(`Sky/`), 구름, 달 · 행성 · 별, 오로라 텍스처 | `env_sky`는 그림 한 장 대신 셰이더 하늘(`DeepBlue/M_Sky_DeepBlue.mat`)로 구현 |
| `Environment/Crystal/` | 수정 무리 + 빛 웅덩이(배경 장식) |
| `Environment/Grass/`, `DeepBlue/` 풀 프리팹 | 지형 위 풀 |
| `Environment/FX/` `tex_vfx_*` | 배경 먼지 · 빛줄기 (요청서 H의 `vfx_*`와는 별개) |
| `Props/_Shared/Textures/tex_prop_wood*` · `Props/_Shared/Materials/mat_prop_shared_*` | 나무판 · 나무결 공용 텍스처, 소품 공용 재질 (쓰레기통 · 물뿌리개 등) |

## 이름을 안 바꾼 것

- 스크립트 · 셰이더 (클래스 · 셰이더 이름이 코드에 걸려 있음)
- `_Source/` (AI 생성 원본 보관 규칙)
- `Environment/DeepBlue/M_Sky_DeepBlue.mat` — `_Project/Scripts/Editor/PlaceholderDataSetup.cs`가 경로로 씀
- `Environment/Sky/Meshes/SkyGalaxyStars.asset` — 씬에 경로 저장
- 지형 레이어 · 볼륨 프로필 · 렌즈 플레어 (요청서 · 규칙에 없는 종류)

## 지운 것 (2026-10-20)

- `Props/gem_bucket`, `Props/gem_shield`와 그 원본 `_Source/Wooden_Gem_Bucket_1650`, `_Source/Wooden_Gem_Shield_807` — 요청서에 짝이 없고 쓰는 곳도 없어서 제거 (git 기록에 남아 있음)


## 텍스처 · 데칼 (요청서 F · H)

| 요청서 id | 파일 | 메모 |
|---|---|---|
| `vfx_water_drop` | `Environment/FX/Textures/vfx_water_drop.png` | 128 × 128 흰 물방울(위가 뾰족) + 하이라이트, 알파. 색은 파티클에서 곱하기 |
| `decal_stain_01` | `Environment/decals/Textures/decal_stain_01.png`, 재질 `Materials/mat_decal_stain_01.mat` | 청록 포자 얼룩. URP Decal Projector에 재질만 넣으면 됨. **렌더러에 Decal 기능(Technique = Screen Space)이 있어야 보임** — 우리 툰 셰이더는 DBuffer를 안 읽음 |