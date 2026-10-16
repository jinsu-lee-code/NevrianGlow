# BRANCHES.md — 브랜치 운영 방식

> 결정 2026-10-16. 파트별 에셋은 각자 브랜치에 올리고, 개발(이진수)이 `dev`로 가져와 게임에 연결한다.
> 파트별 자세한 사용법: [BRANCH_ART.md](BRANCH_ART.md) · [BRANCH_ANIMATOR.md](BRANCH_ANIMATOR.md) · [BRANCH_SOUND.md](BRANCH_SOUND.md)

## 1. 브랜치 한눈에 보기

| 브랜치 | 누가 | 무엇을 | 누가 병합 |
|---|---|---|---|
| `main` | 개발 | 안정판. 금요일 빌드 · 마일스톤에서 `dev`를 병합 | 개발 |
| `dev` | 개발 | 통합. 게임 코드 · 씬 · 데이터 + 파트 에셋을 가져와 연결 | 개발 |
| `Art` | 아트 (김승희 · 배경) | 모델 · 텍스처 · 재질 · UI 아트 · 아이콘 · 배경 씬 · 셰이더 | 개발이 `dev`로 |
| `Animator` | 모션 (김기인) | 손님 리그 · 손님 애니메이션 · 손님 모델 | 개발이 `dev`로 |
| `Sound` | 사운드 · QA (백경민) | BGM · 효과음 · UI 소리 | 개발이 `dev`로 |
| `feature/이름-작업명` | 개발 | 위험한 작업(저장 등)만 따로 | 개발 |

```
Art ───────┐ (에셋 올림)
Animator ──┼──▶ dev ──(빌드 · 마일스톤)──▶ main
Sound ─────┘      │
   ▲              │
   └──── (작업 시작 전) dev를 파트 브랜치로 가져와 최신 게임 받기
```

## 2. 핵심 규칙 (모든 파트 공통)

1. **자기 폴더에만 올린다.** 폴더를 나눠 두면 파트끼리 · 개발과 충돌이 나지 않는다.

   | 파트 | 올리는 폴더 (이 안은 마음대로) |
   |---|---|
   | Art | `Nevrian Glow/Assets/Art/` (단, `Characters/` 제외) · `Nevrian Glow/Assets/Scenes/` (배경 씬) |
   | Animator | `Nevrian Glow/Assets/Art/Characters/` |
   | Sound | `Nevrian Glow/Assets/Audio/` |

2. **건드리지 않는 곳** — 바꿔야 하면 직접 고치지 말고 납품 기록의 "개발에게 요청"에 적는다.
   - `Nevrian Glow/Assets/_Project/` (게임 코드 · 게임 씬 Core/Farm/Shop · 데이터 · 게임용 애니메이터)
   - `Nevrian Glow/ProjectSettings/` · `Nevrian Glow/Packages/` (패키지 추가 포함)
   - `Nevrian Glow/Assets/Settings/` (URP 렌더 설정 — 아트는 예외 있음, BRANCH_ART.md 참고)
   - 다른 파트의 폴더, 저장소 루트의 문서(GDD · TASKS · BALANCE · CLAUDE). 단 **자기 파트의 BRANCH_파트.md 납품 기록**은 적는다
3. **`dev` · `main`에는 직접 커밋하지 않는다.** 자기 브랜치에만 커밋 · push.
4. **에셋은 Unity를 연 상태에서 넣는다.** Unity가 `.meta` 파일을 만들고, 커밋할 때 파일과 `.meta`를 **항상 같이** 올린다. 이름 바꾸기 · 이동도 Unity Project 창에서 한다(밖에서 옮기면 연결이 끊긴다).
5. **이미 게임에 들어간 에셋을 지우거나 이름을 바꿀 때**는 먼저 개발에게 말한다 (게임 쪽 연결이 끊어짐).
6. **파일 하나 50 MB 이하** (GitHub는 100 MB부터 거부). 더 크면 개발과 상의.
7. 에셋 이름은 소문자 `분류_이름_변형` (`crop_mushroom_small`, `sfx_water_pour_01`, `anim_customer_walk`).
8. 올린 뒤에는 자기 BRANCH_파트.md 맨 아래 **납품 기록**에 한 줄 적고, 개발에게 메신저로 알린다.

## 3. 커밋 메시지

| 누가 | 형식 | 예 |
|---|---|---|
| 파트 | `MM.DD/파트/요약` | `10.17/Art/CropModels1`, `10.18/Animator/CustomerWalkBrowse`, `10.18/Sound/SfxFirstBatch` |
| 개발 (가져오기) | `MM.DD/Merge/파트-요약` | `10.18/Merge/Art-CropModels1` |
| 개발 (항목) | `MM.DD/Phase-항목/요약` | `10.18/5-2/CropModelSwap` |

## 4. 개발 쪽 가져오기 순서 (이진수 · Claude)

1. Fetch → 파트 브랜치에 새 커밋이 있는지 확인
2. **폴더 검사**: `git diff --stat dev...origin/Art` — 자기 폴더 · 자기 BRANCH 문서 밖의 변경이 있으면 병합 전에 파트와 확인 (실수면 그 파일만 빼고 가져옴)
3. `dev`에서 병합: `git merge --no-ff origin/Art -m "MM.DD/Merge/Art-요약"`
4. Unity에서 열어 컴파일 오류 · 분홍 재질 · 콘솔 경고 확인 → 게임에 연결(프리팹 모델 교체 · 애니메이터 클립 교체 · SoundLibrary 등록) → 플레이 확인 → 커밋
5. 납품 기록의 "개발 확인" 칸에 날짜 · 결과를 적고, 파트에 알린다 → 파트는 다음 작업 전에 `dev`를 다시 받는다
6. 금요일 빌드 · 마일스톤: `dev`가 플레이 확인되면 `main`으로 병합

## 5. 자주 묻는 것

- **게임 안에서 내 에셋을 확인하고 싶다** → 작업 전에 `dev`를 자기 브랜치로 가져오면(BRANCH 문서 3단계) 최신 게임이 같이 온다. `Core` 씬을 열고 Play. 게임 씬(Core/Farm/Shop)에 확인용으로 놓아 본 것은 **커밋하지 말고 되돌린다**(Changes에서 Discard).
- **충돌(conflict)이 났다** → 혼자 해결하지 말고 개발에게 알린다. 자기 폴더만 쓰면 거의 생기지 않는다.
- **실수로 다른 폴더를 바꿨다** → GitHub Desktop Changes에서 그 파일 우클릭 → Discard changes.
