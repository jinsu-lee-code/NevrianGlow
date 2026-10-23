# HANDOFF.md — 다른 컴퓨터에서 이어서 작업하기 (2026-10-23 기준)

> 마지막 커밋 `a8b091e 10.23/5-4b/HudIcons` (dev = origin/dev). 상세 기록은 TASKS.md 작업 일지, 수치는 BALANCE.md.
> 이 파일은 넘겨받는 메모 — 다음 작업 세션이 시작되면 내용을 TASKS에 반영하고 갱신한다.

## 1. 새 컴퓨터 준비
1. GitHub Desktop으로 저장소 clone → 브랜치 **`dev`** 체크아웃. Git은 GitHub Desktop 내장 git(`%LOCALAPPDATA%\GitHubDesktop\app-*\resources\app\git\cmd\git.exe`)
2. Unity **6000.3.25f1**로 `Nevrian Glow` 폴더 열기 (첫 열기 때 Library 생성으로 시간이 걸림)
3. 열린 뒤 확인: 콘솔 컴파일 오류 0 → Test Runner EditMode **236개 통과**
4. Core 씬을 열고 Play — 타이틀(로고 · 카메라 투어) → 이어하기/새 게임
5. 저장 파일은 컴퓨터마다 따로: `%USERPROFILE%\AppData\LocalLow\FarmingDiary\Nevrian Glow Farm\save_01.ngf` — 자동 플레이 확인 전 백업 → 끝나면 복원
6. 플레이 기록 `Reports/playtest_*.md`는 커밋하지 않는 파일이라 이 컴퓨터에만 있음 (옮겨지지 않음)
7. 알아 둘 것: **Unity 창이 비활성이면 플레이가 1프레임에서 멈춤**(Run In Background 꺼짐 — 프로젝트 설정은 그대로 둠). 자동 확인 중에는 실행 중 `Application.runInBackground = true`로만 켬

## 2. 커밋 전 규칙 (그대로 유지)
- 플레이 후 생기는 변경 되돌리기: `git checkout -- "Nevrian Glow/Assets/Art" "Nevrian Glow/Assets/_Project/Art/Fonts/font_maplestory_bold_sdf.asset"`
- 파트 폴더(`Assets/Art` · `Assets/Audio` · `Assets/Scenes`)는 병합 외에 수정 금지 — 게임 연결은 `_Project` 쪽에서
- 커밋 메시지 `MM.DD/Phase-항목/PascalCase`, 끝나면 자동 push (force 금지)

## 3. 오늘(10/23) 한 일 요약
| 커밋 | 내용 |
|---|---|
| `bcce479` · `44012c9` | Art · Animator 병합 (손님 피피 · 오르, VFX 7, 쓰레기 5, 수확물 2, 창고 보관함, 길, 얼룩 2, 아이콘 16, UI 패널 · 버튼, 둘러보기 · 넣기 클립) |
| `7de8ef8` | 기획 묶음 최종 10/22 반영 (CSV가 개발 id 사용 — 물뿌리개만 ID_MAP 3줄 추가) |
| `421ee3a` | 손님 2종 · 놓인 아이템 모델 · 쓰레기 5종 · 얼룩 데칼(URP Decal 기능 추가) · 칸 흙(SoilFX) · 밭 테두리 · 창고 보관함 · 길 조각 |
| `0a9bc83` | VFX 7종(VfxManager · VfxLibrary) · 아이템 아이콘 · 조준점 · 앱 아이콘 |
| `8005ebe` | 미개간 흙 색 밝게 · 밭 · 길 배경 풀 지우기(실행 중 지형 사본) · 길 판 없앰 · 타이틀 카메라 투어(장면 10초) |
| `c036006` · `42f6dc2` · `84f8bed` | 5-4a 패널 · 버튼 스킨, 타이틀 로고(개발 폴더 사본) · 달 형광 · 별 반짝임 · 빛 쓸기 |
| `80a774f` · `a8b091e` | 5-4b 레이아웃(정사각 칸 · 핫바 이름 줄 · 창 여백 · 목록 줄 · 슬라이더) · HUD 아이콘 |

Setup 메뉴 (모두 여러 번 실행해도 같음): `Connect Art (1) ~ (10)`, `Build Farm Layout`, `Create Title Camera Tour`, `Apply UI Skin (5-4a)` → `Apply UI Layout (5-4b)` 순서

## 4. 다음에 할 일 (우선순위 순)
1. **사용자 직접 플레이 확인** — 오늘 연결한 것 전부 '플레이 확인 대기'. 특히 볼 것:
   - 손님 피피 · 오르 동작(둘러보기 · 넣기 · 웃는 얼굴), 진열 작물 크기(0.2 m)
   - 괭이질 흙 연출 시점(지금 클릭 순간 — 아트 권장은 휘두르기 35%), 물줄기 · 거품 VFX
   - UI: 마우스 올림 · 누름 버튼 그림, 칸 집기 · 놓기, 해상도별 모습
   - 타이틀 투어 · 로고 연출 세기(값 조정만 하면 됨)
2. **7-2 빌드 메뉴 → 4-5 알파 빌드 (마일스톤 10/25 토)** — 백경민 1차 플레이 테스트용. 빌드 설정에 씬 4개(Core · Farm · Shop · BackGroundScene), 앱 아이콘 들어감
3. **5-7 튜토리얼 (10/27까지)** — 기획 `CSV/plan/data_csv/tutorial_steps.csv`(D1-01 ~ D2-14, 완료 이벤트 · 잠금 이름 = 개발 제안 그대로) + `game_settings`의 `tutorial_*` 5개(지금 '시스템 대기 설정'). 개발 검토 문서 `Docs/기획확정/튜토리얼_플로우_1-2일차_개발검토.md`
4. 남은 아이콘 3개 위치 정하기 — `icon_customer`(정산 '손님 n명'?) · `icon_tool_hand`(빈손 · E 안내?) · `icon_tool_seedbag`(씨앗 탭?) — 사용자 결정 대기
5. 10/28 기능 동결 전 P1: 거대 작물 4-4(`vfx_merge_burst` · 거대 Variant 준비됨), 스프링클러 등 — TASKS 기획 확정 반영 P1 줄

## 5. 다른 파트에 전할 것
- **아트**: 타이틀 로고는 채팅으로 받아 `_Project/Art/UI/ui_logo.png`에 둠 → `Assets/Art/UI/ui_logo.png`로 브랜치 납품 요청(같은 이름이면 메뉴가 자동으로 그쪽 사용). 손님 폴리곤 예산 초과(피피 15.4천 · 오르 12.9천) — 성능 측정 후 판단. 밭 · 길 자리 풀은 게임에서 지우는 중(지형 원본은 그대로)
- **사운드**: 게임이 부르는데 파일이 없는 소리 6개 — `sfx_day_start` · `sfx_giant_merge` · `sfx_shelf_place` · `sfx_shelf_take` · `sfx_storage_open` · `sfx_storage_put`
- **기획**: id_map csv의 물뿌리개 줄 = 개발 `tool_watering_1~3`(기획 `tool_watering_can_*`), 1일차 쓰레기 30 중 25만 놓임(통로 밖만) — 줄일지, 측정 봇은 괭이 150에도 6일 안에 못 삼

## 6. 알려진 한계
- 가방 4칸일 때 가방 창에 빈 줄(확장 대비 2줄 자리)
- 배경 지형 풀 지우기는 시작 구획 기준 — 구획 확장(P1) 때 자동으로 다시 지움
- 타이틀 투어 장면 좌표 · 로고 별 위치는 지금 배경 · 로고 그림 기준 값(바뀌면 메뉴 기본값 수정)
