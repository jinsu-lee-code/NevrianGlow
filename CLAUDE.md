# CLAUDE.md — Nevrian Glow Farm

## 프로젝트 개요
외계 행성의 발광 작물을 키워 직접 운영하는 가게에서 파는 **3D 1인칭 농사·경영 시뮬레이션**. Unity / PC, 데모 1~6일차(변경 2026-10-08), 마감 2026-11-03.
기획은 GDD.md, 작업 순서는 TASKS.md, 수치는 BALANCE.md. 원본 상세 기획은 노션 「게임 기획서 (GDD) — Nevrian Glow Farm」(이하 노션 GDD, 장 번호로 참조)이다.
수치는 BALANCE.md와 데이터 에셋(ScriptableObject) 기준이며 코드에 임의로 넣지 않는다.

## 참조 문서
| 문서 | 내용 |
|---|---|
| GDD.md | 컨셉, 하루 루프, 규칙, MVP, 범위 제외, 결정 사항, 열린 항목 |
| TASKS.md | Phase별 작업 순서와 작업 일지 (개발 전담: 이진수) |
| BALANCE.md | 수치 표, 검산 케이스, 데이터 위치, 측정 기준 |
| 노션 GDD 11장 | 장소 이동 (Core/Farm/Shop, ZoneTransition) |
| 노션 GDD 12장 | 인스펙터·기즈모 규칙, 시스템별 알고리즘과 파라미터, 확장 구조 |
| 노션 「버그 트래킹」 | LogCollector 설계, 버그 등록 형식, 심각도 기준 |

## 작업 원칙
- 한 번에 하나의 기능 단위만. TASKS.md의 현재 항목만 구현한다.
- 요청받지 않은 시스템은 미리 만들지 않는다 (GDD.md "범위 제외" 항목 금지).
- 구현 후 테스트 방법(어느 씬에서 무엇을 눌러 무엇이 보이면 정상)을 함께 설명한다.
- 에디터 수작업은 코드로 우회하지 말고 수동 작업 목록으로 안내한다. 반복 생성 작업은 여러 번 실행해도 결과가 같은 에디터 메뉴로 만든다.
- 불확실한 설계 결정은 선택지 + 추천안으로 질문한다. 답은 날짜와 이유를 붙여 TASKS.md에 남긴다.
- 완료 = 테스트 통과 + 확인 방법 안내 + 사용자 플레이 확인. 구현만 끝나면 "플레이 확인 대기".
- 실패 · 생략 · 미확인은 그대로 보고한다.
- **시스템 먼저, 수치 · 배치 · 에셋은 나중 (결정 2026-10-07).** 기획 · 아트 · 모션 · 사운드 입력을 기다리지 않는다. 없으면 BALANCE.md 9장 임시안으로 진행하고 TASKS 항목에 `— 임시값 사용(교체 대기: 무엇)`을 붙인다.
- 임시값이 바뀌어도 코드는 바뀌지 않아야 한다. 콘텐츠 개수(작물 3종, 손님 2종 등)나 특정 값을 가정하는 코드를 쓰지 않는다.

## 기술 스택 (변경 금지)
- 플랫폼: PC (Windows, 키보드·마우스)
- 엔진: Unity 6000.x LTS (결정 2026-10-07, 정확한 패치 버전은 셋업 후 기입)
- 렌더 파이프라인: URP + Bloom 후처리 (발광은 Emissive + Bloom, 실시간 광원 4~6개 이하)
- 입력: Input System (구 Input Manager 사용 안 함)
- UI: uGUI + TextMeshPro (결정 2026-10-07)
- 이동: CharacterController / 상호작용: 화면 중앙 Raycast / 손님 이동: NavMesh(가게 바닥만)
- 테스트: Unity Test Framework (EditMode 어셈블리를 첫 기능부터)

## 폴더 구조
```
Assets/_Project/
  Data/     Crops/ Shop/ Skills/ Customers/ Stats/        (Recipes/ Regions/ 는 2차)
  Scripts/  Core/      GameManager, DayCycle, ContentDatabase, StatSystem, GameEvents, LogCollector
            Systems/   Farm, Inventory, Shop, Skill, Sales, Cleaning, Placement, Save
            Logic/     엔진 의존 없는 순수 규칙 코드 (성장 · 손님 수 · 경험치 · 저장 직렬화)
            Data/      ...Data (ScriptableObject 클래스)
            Behaviors/ Effects/ Conditions/
            UI/
            Editor/    CSV 임포터, 콘텐츠 검사, Glow Farm 디버그 창, Setup 메뉴
  Tests/    EditMode/
  Prefabs/  Art/  Audio/  Scenes/ (Core, Farm, Shop)
```

## 코딩 규칙
- 네임스페이스 `NevrianGlowFarm.*`. 클래스·메서드 PascalCase, private 필드 camelCase.
- `public` 필드 금지. `[SerializeField] private` + 읽기 전용 프로퍼티로만 연다.
- 인스펙터 헤더 순서: 참조 → 조정값 → 기즈모 → 디버그. 툴팁은 한국어 한 줄 + 단위 (예: "닦는 반경 (m)"). 조정값에는 `[Range]` 또는 `[Min]`.
- 원본 수치(가격, 성장일)는 ScriptableObject에만 둔다. 컴포넌트가 데이터를 복사해 들고 있지 않는다.
- 규칙과 표현 분리: 성장 처리 · 손님 수 · 경험치 · 저장은 `Logic/`의 순수 코드로 쓰고, MonoBehaviour는 얇은 어댑터로 둔다 (게임 · 측정 봇 · 테스트가 같은 코드를 쓴다).
- 이벤트(`GameEvents`, C# event)로 결합도를 낮추고 "바뀐 것만" 알린다.
- 프레임마다 Find/GetComponent 금지(캐시). 밤 성장 처리는 일차 전환 시 1회.
- 난수는 인터페이스로 주입하고 용도별로 분리한다 (거대 작물 · 손님 구매 · 쓰레기 배치).
- enum은 끝에만 추가한다. 콘텐츠 종류는 enum이 아니라 에셋 참조로 구분한다.
- 기즈모는 공용 정적 클래스 `GizmoKit`으로만 그린다. 색 규칙: 시안=상호작용, 파랑=도구 범위, 초록=생성·출발, 마젠타=손님 경로, 노랑=수확·진열, 빨강=금지·겹침.
- 디버그 기능은 `#if UNITY_EDITOR` 에디터 창 "Glow Farm 디버그"에만 둔다. 게임 화면에 디버그 패널 금지.

## 핵심 설계 규칙 (변경 금지)
1. 씬 구성은 `Core`(항상 로드) + `Farm` + `Shop`을 Additive로 함께 로드한다. 장소 이동은 페이드 + 순간이동 (노션 GDD 11장, 결정 2026-10-06).
2. 하루 단계는 GameManager의 상태 머신 하나로 관리한다: 상점 → 농사 → 청소·진열 → 장사 → 정산 → 스킬 → 다음 날.
3. 장사는 완전 자동(B안)이다. 계산대 모드는 만들지 않는다 (결정 2026-10-07).
4. 플레이어는 1인칭 카메라만 있다. 캐릭터 모델 · 1인칭 팔 리그 없음. 도구 동작은 코드 애니메이션 (결정 2026-10-07).
5. 모든 콘텐츠 에셋은 바뀌지 않는 문자열 id를 갖는다 (`crop_moonroot` 등). 저장 · CSV는 id로 연결된다. 특정 id로 분기하는 코드 금지.
6. 콘텐츠 추가는 에셋 추가만으로 끝나야 한다 (ContentDatabase가 폴더에서 자동 수집).
7. 저장은 원자적 쓰기(임시 파일 후 교체) + 형식 버전 + "저장→불러와 진행 = 저장 없이 진행" 왕복 테스트를 지킨다.
8. CharacterController로 순간이동할 때는 끄고 → 위치 지정 → `Physics.SyncTransforms()` → 다시 켠다.

## 아트 · 그래픽 규칙
- 스타일: 핸드페인팅 로우폴리, 영원한 황혼, 어두운 청록·남색 바탕 + 시안·마젠타·보라 발광 (팔레트는 GDD.md 8번).
- 발광은 Emissive 재질 + Bloom. 점광원은 4~6개 이하.
- AI 생성 모델(Varco3D)은 1유닛 정규화 상태로 들어온다 → 임포트 후 프리팹에서 실제 크기(m)로 맞춘다. 원본은 별도 폴더에 보관.
- 가져온 모델은 URP 재질로 교체하고 분홍(깨진 재질)이 없는지 확인한다.
- 에셋 이름: 소문자 `분류_이름_변형` (`crop_mushroom_small`, `sfx_water_pour_01`, `anim_customer_walk`).
- 그래픽 작업은 재미 검증(TASKS Phase 4) 이전에 하지 않는다. 그레이박스는 색만 다른 기본 도형.

## Git
- 브랜치 `main`(안정) / `dev`(통합) / `feature/이름-작업명`. 한 항목 = 한 커밋.
- 커밋 메시지: `MM.DD/Phase-항목/PascalCase요약` (예: `10.08/0-3/SetupScenes`). 수정은 `…Fix`.
- 항목 구현이 끝나면 확인 없이 자동 커밋한다. Git은 GitHub Desktop 내장 git(`%LOCALAPPDATA%\GitHubDesktop\app-*\resources\app\git\cmd\git.exe`)을 쓰고, push는 사용자가 GitHub Desktop에서 한다 (결정 2026-10-07).
- `.gitignore`의 출력 폴더는 루트 기준 경로로만 (`/Build/`, `/Temp/`). `**/Build/` 같은 넓은 패턴 금지.
