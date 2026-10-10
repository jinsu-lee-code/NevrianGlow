# TASKS.md — Nevrian Glow Farm (이진수: 팀장 · 개발 전담 · 일정)

> 기준: 노션 GDD(2026-10-07, 10-08 규칙 갱신), GDD.md, BALANCE.md. 작성 2026-10-07, 10/08 기획 반영 2026-10-10.
> 한 번에 한 항목만 진행한다. 항목 하나 = 커밋 하나 (`MM.DD/Phase-항목/PascalCase요약`).
> 완료 = 테스트 통과 + 확인 방법 안내 + 플레이 확인. 구현만 끝나면 `— 플레이 확인 대기`.
> 진행 체크와 완료 기록은 Claude Code 작업 중에 이 파일에서 직접 관리한다 (2026-10-07).

## 상태 표시
- `[ ]` 할 일 · `[~]` 진행 중 · `[x]` 완료
- 꼬리표: `— 플레이 확인 대기` / `— 플레이 확인 완료` / `— 임시값 사용(교체 대기: 무엇)`

## 마일스톤
| 날짜 | 마일스톤 | 기준 |
|---|---|---|
| 10/10 (금) | 1주차 빌드 | Phase 0 완료 + 1인칭 이동 · 상호작용 그레이박스 |
| 10/11 (토) | 프로토타입 | 그레이박스로 개간 → 심기 → 물 → 다음 날 → 수확 한 바퀴 (10/08 개간 추가) |
| 10/17 (금) | 2주차 빌드 | 농장 ↔ 가게 이동, 청소 · 진열까지 |
| 10/23 (목) | 재미 검증 | 1~6일차 하루 루프 전체를 그레이박스로 플레이 (10/08 1~5 → 1~6) |
| 10/25 (토) | 알파 빌드 | 저장 제외 P0 전부 + 스킬 축소판 |
| 10/28 (화) | **기능 동결** | 이후 버그 수정 · 데이터 · 에셋 교체만 |
| 10/31 (금) | 4주차 빌드 | 폴리싱 반영 |
| 11/3 (화) | **최종 제출** | 빌드 + 플레이 영상 + 발표자료 |

## 다른 파트에서 받아야 하는 것 — 기다리지 않는다
결정 (2026-10-07): **시스템을 먼저 만들고 수치 · 배치 · 에셋은 나중에 교체한다.** 아래 입력이 없어도 모든 항목은 임시안으로 진행하며, 도착하면 데이터 · 위치 · 파일만 바꾼다 (코드 수정 없음이 목표).

| 받을 것 | 보내는 사람 | 예정 | 없을 때 임시안 (지금 바로 사용) | 교체 방법 |
|---|---|---|---|---|
| 레벨 배치도 | 김봉준 | 10/9 (미도착) | BALANCE 9-1 임시 배치 (시작 밭 2구획 32칸 임시, 16칸안 검토 중) | 씬에서 오브젝트 위치만 이동 |
| 작물 CSV | 김봉준 | 10/11 | BALANCE 2-2 초기값 (Setup 메뉴가 생성) | CSV 가져오기 메뉴 |
| UI 흐름도 | 김봉준 | 10/14 | GDD.md 화면 목록 + BALANCE 9-4 조작키 | 화면 프리팹 배치 · 전환만 수정 |
| 상점 CSV | 김봉준 | 10/17 | BALANCE 9-2 임시 상품 | CSV 가져오기 메뉴 |
| 스킬 CSV | 김봉준 | 10/21 | BALANCE 9-3 임시 스킬 세트 | CSV 가져오기 메뉴 |
| 콘텐츠 확정 목록 | 김봉준 | 10/14 제안 | 현재 GDD 범위 | 새 규칙이면 TASKS에 항목 추가 |
| 일차별 플레이 경험 목표 | 김봉준 | ~~10/21 제안~~ 1차 확정 (10/08, GDD 6장) | GDD 6장 프로토 목표 1~6일차 | 기획 보완 시 GDD 6장 갱신 |
| 신규 상품 · 스킬 가격 · SP (도구 등급 · 가방 7단계 · 핫바/가방 면허) | 김봉준 | 미정 | BALANCE 9-2 · 9-3 v1 임시 | 상품 · 스킬 데이터 값만 수정 |
| 기획 확인 대기 (GDD 열린 항목 19~23) | 김봉준 | 미정 | 비료 = 기본 상품, 시작 밭 2구획, 시작 소지품 BALANCE 2-3 | 데이터 값만 수정 |
| 손님 리그 · 애니메이션 5종 | 김기인 | 10/18 · 10/24 | 캡슐 + 공용 애니메이터(파라미터 이름 고정, BALANCE 9-5) | 애니메이터의 클립만 교체 |
| 작물 · 소품 · 환경 모델 | 김승희 | 순차 | 색만 다른 기본 도형 (팔레트 색) | 프리팹의 메시 · 재질 교체 |
| UI 아트 · 아이콘 | 김승희 | 10/23 | 단색 패널 + 글자 아이콘 | 스프라이트 교체 |
| 효과음 · BGM | 백경민 | 10/18 · 10/25 | 소리 id만 호출, 파일 없으면 무음 | SoundLibrary에 클립 연결 |
| 튜토리얼 대본 | 김봉준 | 10/28 | 임시 문구 | 대본 데이터 교체 |

---

## Phase 0. 세팅 (10/7 ~ 10/10)
목표: 엔진 · 구조 · 문서 · Git. 완료 기준: 빈 Core/Farm/Shop 씬이 함께 로드되고 문서 3종 + 테스트 어셈블리가 있다.

- [x] **0-1. Unity 프로젝트 생성 · 기본 설정** — 플레이 확인 완료 (2026-10-08)
  - URP 템플릿, 샘플 씬 · 샘플 에셋 제거, Input System 활성화(구 입력 끄기), Bloom 볼륨 1개
  - Player Settings: 회사명 · 제품명(저장 경로에 영향 → 여기서 확정), 버전 0.1.0
  - 완료 조건: 빈 씬 플레이 시 콘솔 경고 0
- [x] **0-2. 폴더 구조 · Git** — 플레이 확인 완료 (2026-10-08)
  - CLAUDE.md의 폴더 트리 생성, `.gitignore`(루트 기준 경로), `main`/`dev` 브랜치, 원격 저장소
  - 완료 조건: 다른 PC에서 clone → 열기 → 플레이 시 동일
- [x] **0-3. 씬 3개 + Additive 로드** — 플레이 확인 완료 (2026-10-08)
  - `Core`(GameManager 자리 · 카메라 · UI 캔버스 · AudioManager 자리 · ZoneTransition 자리), `Farm`, `Shop`
  - Core에서 Farm · Shop을 `LoadSceneAsync(Additive)`로 동시 로드 → 둘 다 끝나면 시작 가능 플래그
  - 완료 조건: Core만 열고 플레이하면 Farm · Shop이 같이 올라온다
- [x] **0-4. 문서 3종 + 테스트 어셈블리** — 플레이 확인 완료 (2026-10-08)
  - 저장소 루트에 CLAUDE.md · GDD.md · TASKS.md · BALANCE.md 커밋
  - `Tests/EditMode` 어셈블리 + 샘플 테스트 1개 (`Logic` 어셈블리 참조)
  - 완료 조건: Test Runner에서 1개 통과
- [x] **0-5. LogCollector** (노션 버그 트래킹 2번) — 10/10 금요일 빌드 전에 필수 — 플레이 확인 완료 (2026-10-08)
  - `Application.logMessageReceivedThreaded` → Error · Exception · Assert · Warning
  - 한 줄: 시각 · 유형 · 일차 · 단계 · 구역 · 빌드 버전 · 메시지 · 스택. 같은 메시지+스택은 묶고 횟수 증가
  - 저장: `persistentDataPath/Logs/session_날짜_시간.log`, 빌드에서도 동작, 디버그 창에 "로그 폴더 열기"
  - 테스트: 중복 묶기 · 형식 (순수 로직 부분)
  - 완료 조건: 일부러 Exception을 내면 세션 파일에 1줄 + 횟수 증가
- [x] **0-6. 임시 데이터 세트 Setup 메뉴** — 이후 모든 항목이 기획 입력 없이 돌아가게 하는 기반 — 플레이 확인 완료 (2026-10-08) — 임시값 사용(교체 대기: 작물 CSV 10/11)
  - 메뉴 `Glow Farm/Setup/Create Placeholder Data`: BALANCE 2장 · 9장의 값으로 Settings 에셋 · CropData 3종 · 임시 상품 · 임시 스킬 에셋을 만든다
  - **멱등**: 여러 번 실행해도 결과가 같고, 이미 있는 에셋은 덮어쓰지 않는다(옵션으로 "초기값으로 되돌리기")
  - 나중에 들어올 CSV 임포터(2-1)와 **같은 id · 같은 필드**를 쓴다 → CSV가 오면 같은 에셋이 값만 갱신된다
  - 완료 조건: 빈 프로젝트에서 메뉴 1번 → 데이터 폴더가 채워지고 콘솔 경고 0

결정 (2026-10-07): Unity 6000.x LTS — 최신 LTS, URP · Input System 안정
결정 (2026-10-07): UI는 uGUI + TextMeshPro — 자료 많고 일정 안전
결정 (2026-10-07): 손님 수 공식의 `일차 ÷ 2`는 내림(정수 나눗셈) — BALANCE 3-4

- [ ] **0-7. 임시 데이터 v1 (10/08 기획 반영)** — 0-6 Setup 메뉴의 값 갱신
  - 시작 자금 100 → 150, 버섯꽃 성장 4 → 2, Progression base 100 → 40 (perLevel 40 그대로 = 80 + 40 × (L − 1)) — BALANCE 2-3 · 2-2 · 2-7
  - 구조가 바뀌는 값(핫바 · 가방, 쓰레기 칸당 개수, 대걸레 등급)은 1-2b · 2-2 · 2-4에서 같은 메뉴에 추가
  - 완료 조건: "Reset to Defaults" 후 값이 BALANCE v1과 일치, 기존 테스트 통과

결정 (2026-10-10): 10/08 기획 갱신(노션 정리 2026-10-08) 반영 — 개간 · 통합 인벤토리 · 쓰레기 아이템화 · 도구 등급 · 데모 1~6일차. 기획 확인 전 임시: 비료 = 기본 상품, 시작 밭 2구획 32칸 (GDD 열린 항목 19 · 20) — 사용자 결정, 차후 데이터로 수정

## Phase 1. 핵심 조작 그레이박스 (10/8 ~ 10/11)
목표: 플레이어가 "하는 일" 하나 — 1인칭으로 밭을 개간하고 심고 물 주고 다음 날 수확. 색만 다른 기본 도형.

- [x] **1-1. PlayerController (1인칭 카메라 전용)** — 노션 GDD 12.3 — 플레이 확인 완료 (2026-10-08)
  - CharacterController 이동 · 시점 · 중력 · 질주(Shift) · 헤드밥. 수치는 PlayerSettings 에셋(BALANCE 2-1)
  - 완료 조건: 걷기 · 질주 · 시점 제한(83°) 정상, 경고 0
- [x] **1-2. InteractionRaycaster + IInteractable** — 노션 GDD 12.3 — 플레이 확인 완료 (2026-10-10)
  - 화면 중앙 Raycast(5m, Interactable 레이어), `GetPrompt(tool)` / `Interact(tool)`, 대상이 바뀔 때만 UI 갱신
  - 핫바 도구 선택 1~5 (그레이박스 UI)
  - 기즈모: 플레이 중 시안 직선 + 맞은 점
- [ ] **1-2b. 입력 분리 · ItemData · 핫바 4칸** — 10/08 조작 변경 (GDD 4-3, BALANCE 9-4)
  - 좌클릭 = 든 아이템 사용 / E = 손 동작(든 아이템과 무관) 분리, 휠 선택 추가
  - `ToolData` → 아이템 공통 `ItemData`(id · 이름 · 칸당 개수) + 도구 등급 데이터(괭이 1회 작업량 · 대걸레 반경/속도 · 물뿌리개 칸 수, BALANCE 2-11). 씨앗 주머니 · 손 도구 제거
  - 핫바 칸 수는 데이터(기본 4, 최대 8). 시작 소지품 BALANCE 2-3 임시안
  - 범위 밖: 수량 쌓기 · 가방 · Q · Tab은 2-2
  - 테스트: 휠 선택 순환, 칸 수 변경 시 선택 유지
- [ ] **1-3. GameManager 상태 머신 + Glow Farm 디버그 창**
  - 일차 · 단계(상점 → 농사 → 청소·진열 → 장사 → 정산 → 스킬), `SetPhase`, `GameEvents.PhaseChanged`
  - 디버그 창: 다음 날, 단계 강제 전환, 달러 · SP 추가
  - 테스트: 단계 순서 · 일차 증가 (순수 로직)
- [ ] **1-4. FarmGrid + 성장 로직 (그레이박스)** — 노션 GDD 12.3, 배치는 BALANCE 9-1 임시안
  - 칸 번호 → 위치 계산, 시작 구획 수는 데이터(임시 2구획 32칸), 칸 상태(미개간 · 개간 진행도 · 작물 · age · wet · regrow · fruit · 비료 효과 필드)
  - 개간(괭이 좌클릭, 작업량 기본 3) · 심기(개간된 칸만) · 물주기(1칸) · 수확(E), 작물이 사라진 칸 → 미개간
  - 밤 처리 파이프라인 `Logic/NightPipeline`: 단계 목록(장치 단계는 빈 자리) → 성장 → 거대 판정 → 작물 없는 칸 미개간 → 입고 초기화 (GDD 4-2)
  - 기즈모: 칸 상태 색, 다음 구획 빨강 점선 + 금지 구역 겹침 경고
  - 테스트: BALANCE 3-1(월광 1일 · 버섯꽃 2일 · 등불 8일 + 재수확 4일 × 3회 · 물 끊김) + 3-1b(개간 횟수 · 밤 미개간 복귀 · 재수확 칸 유지)
  - 완료 조건 = **프로토타입 10/11**: 개간 → 심기 → 물 → 다음 날 → 수확이 그레이박스로 된다
- [ ] **1-5. 1주차 빌드 (10/10 금)** — 백경민 QA 전달, 버그는 노션 버그 목록으로

## Phase 2. 시스템 연결 (10/12 ~ 10/18)
목표: 조작 결과가 데이터 · 인벤토리 · 다른 구역으로 이어지고 HUD에 숫자가 보인다.

- [ ] **2-1. CropData + ContentDatabase + CSV 임포터** — 노션 GDD 12.5, 데이터는 0-6 임시 세트로 먼저 (CSV는 도착 시 가져오기)
  - CropData(id · 가격 · 성장 · wetDays · 수확량 · 재수확 · 단계 프리팹 · 거대 설정), 폴더 자동 수집
  - 커스텀 인스펙터: 일일 수익(계산값) · 성장 타임라인 막대
  - 에디터 메뉴 "CSV 가져오기"(멱등)
- [ ] **2-2. 통합 Inventory · WorldItem · StorageSystem** — 노션 GDD 12.4, GDD 4-3 (갱신 2026-10-08)
  - 통합 인벤토리 = 핫바 4 + 가방 4 (칸 수 데이터), 도구 · 장치 칸당 1 / 그 외 10, 주운 아이템 순서: 같은 묶음 → 빈 핫바 → 빈 가방
  - 가방 화면(Tab): 집기 · 놓기 · 바꾸기 · 합치기
  - WorldItem: Q 1개 / Shift+Q 묶음 내려놓기(조준 바닥, 손 닿는 거리), E 줍기, 날짜가 지나도 유지(저장 대상)
  - 창고 하나(StorageAccessPoint 2곳), 넣기 · 전부 넣기 · 종류별 꺼내기, 쓰레기 불가, 가득 참 안내
  - 테스트: 쌓기 순서 · 핫바 → 가방 순서 · 가득 참 · 창고 공유 · 쓰레기 창고 거부
- [ ] **2-3. ZoneTransition (농장 ↔ 가게)** — 노션 GDD 11장, 배치는 BALANCE 9-1 임시안
  - 전환 7단계, ZoneProfile(앰비언트 · 안개 · 하늘 · BGM), FarmSpawn · ShopSpawn
  - 기즈모: 출발 지점 캡슐 + `CheckCapsule` 겹침 시 빨강
  - 완료 조건: 문(E) → 가게, 정산 '다음 날' → 농장. 벽에 끼지 않음
- [ ] **2-4. CleaningSystem** — 노션 GDD 12.4
  - 포아송 배치(금지 구역 · 바닥 아이템 제외) · 쓰레기 = 아이템(item_trash, E 줍기) · 쓰레기통 E 일괄 비우기 · 대걸레(Decal, 등급별 반경 · 속도) · 청결도 새 공식(GDD 4-4) · 전날 바닥 쓰레기 이월
  - 인스펙터 "배치 미리보기" 버튼
  - 테스트: 개수 공식 · 최소 간격 · 청결도 BALANCE 3-7 (시드 고정)
- [ ] **2-5. ShelfDisplay · PlacementSystem (홀로그램 진열)** — 노션 GDD 12.4
  - 칸 · stackPoints, 홀로그램 가능/불가, 클릭 놓기 · 우클릭 회수, 판매 시 마지막 것부터 제거
- [ ] **2-6. Progression** — 노션 GDD 12.4
  - 경험치 · 레벨업 반복 · SP. 커스텀 인스펙터에 Lv1~10 표
  - 테스트: BALANCE 3-2 레벨 표 그대로
- [ ] **2-7. 물뿌리개 단계 · 1인칭 도구 코드 애니메이션** (P1, 여유 시)
  - 칸 번호 연산(`i^1`, `(i&~3)+k`, `(i&~7)+k`), 기울이기 0.3초 후 물 효과, 괭이 내려치기 · 비료 뿌리기 동작 포함
  - 물뿌리개 단계는 스킬이 아니라 물뿌리개 등급 데이터(BALANCE 2-11)로 결정
- [ ] **2-8. HUD 최소판** — 화면 목록은 GDD.md 5번, 조작키는 BALANCE 9-4 임시안
  - 일차 · 단계 · 달러 · SP · 경험치 바 · 핫바 칸(개수 표시) · 상호작용 프롬프트(좌클릭 / E 구분)
- [ ] **2-9. 2주차 빌드 (10/17 금)**

## Phase 3. 하루 루프 완성 (10/19 ~ 10/22)
목표: 1일차 시작부터 6일차 정산까지 한 판이 그레이박스로 돈다.

- [ ] **3-1. SalesSimulator (순수 로직)** — 노션 GDD 12.4
  - 손님 수 공식, 생성 시각, 구매 개수 1~3, 진열 재고 가중 무작위, 건너뛰기 = 같은 함수 즉시 처리
  - 테스트: BALANCE 3-4 손님 수 표, 재고 0일 때 즉시 퇴장
- [ ] **3-2. CustomerAI + 계산대 줄** — 캡슐 + 공용 애니메이터(BALANCE 9-5)로 먼저, 김기인 납품 시 클립만 교체
  - 상태 머신(입장 → 장바구니 → 진열대 → 둘러보기 → 넣기 → 줄 → 계산 → 해피 → 퇴장), NavMeshAgent, `Speed` 연동
  - CustomerData(체형 · 속도 배율 · 등장 가중치 · unlockDay · 소켓 보정)
  - 기즈모: 동선 마젠타, 줄 칸 번호
- [ ] **3-3. ShopSystem(씨앗 · 비료 · 도구 등급 · 밭 구획 · 가방 확장 7단계 · 진열대) + 정산 화면** — 상품은 BALANCE 9-2 v1 임시 세트
  - 씨앗 일일 입고량, 해금 조건(UnlockCondition — 스킬 단계별 상품 단계 열림 포함), 도구 등급 구매 시 인벤토리의 도구 교체, 정산(손님 · 판매 · 매출 · 지출 · 순이익 · 경험치)
  - 확인: BALANCE 3-8 미채택 추천값(일차별 입고량 · 해금 시 씨앗 지급 · 첫 수확 해금 조건)을 데이터로 받을 수 있는 구조인지
- [ ] **3-4. 하루 루프 연결 + 6일차 종료 처리**
  - 전 단계 연결, 열린 항목 5번(데모 종료 후) 결정 반영
  - 완료 조건: 디버그 없이 1~6일차를 끝까지 플레이할 수 있다

## Phase 4. 재미 검증 · 측정 (10/22 ~ 10/25)
목표: 멈추고 플레이한다. 체감을 숫자로 바꾸고, 기획(김봉준)과 수치를 고친다. **이 단계 전에는 그래픽 작업을 하지 않는다.**

- [ ] **4-1. DaySimulator 측정 봇** (일차별 목표 1차 확정 10/08 → 착수 가능, 늦어지면 P1) (장르 적용: 시뮬레이션 · 생활 → 일별 수지 · 목표 도달 일수)
  - `Logic` 코드를 그대로 써서 1~6일차를 시드 20개로 반복, 봇 규칙은 BALANCE 4번, GDD 6장 일차별 목표 달성 판정
  - 리포트: 일별 매출 · 보유 달러 · 레벨 · SP · 밭 칸 · 청결도 가정
- [ ] **4-2. 직접 플레이 30분 + 진단** — 시스템 동작 · 손맛 위주 (수치 진단은 기획의 일차별 목표 도착 후)
  - 진단 형식(원인 번호) → 방향(A 수치 / B 규칙 / C 신규) → 김봉준과 결정 → BALANCE 실험 표
- [ ] **4-3. SkillSystem 축소판 + StatSystem · Modifier** — BALANCE 9-3 v1 임시 스킬 세트(해금 · 배율 · 선행 조건 경로를 모두 지나가는 최소 세트)
  - 작물 해금 · 물뿌리개 면허 · 가방 면허(EffectSO — 상점 상품 등장), 고품질 작물 · 핫바 확장 · 청결 유지(Modifier), 스킬 트리 화면(데이터로 자동 배치)
- [ ] **4-4. GiantCrop** (P1, 여유 시) — 디버그 "선택한 밭 거대화"
- [ ] **4-5. 알파 빌드 (10/25 토)** — 백경민 1차 플레이 테스트
- [ ] **4-6. 비료** (P1, 신규 2026-10-08) — GDD 4-9
  - item_fertilizer 들고 심은 칸 좌클릭 → 필요 성장일 −1(최소 1), 작물당 1회, 작물이 사라지면 효과 소멸
  - 획득: 기본 상품 (임시, GDD 열린 항목 19)
  - 테스트: BALANCE 3-1 비료 행, 중복 적용 거부, 최소 1일

## Phase 6(앞당김). 저장 (10/24 ~ 10/28)
가이드 순서상 확장 후보지만, 데모 포함 결정(10/7)으로 기능 동결 전에 끝낸다.

- [ ] **6-1. SaveSystem**
  - 저장 대상(GDD.md 4-8 — 개간 진행도 · 도구 등급 · 핫바/가방 칸 수 · 바닥 아이템 포함), id 기반 직렬화(`Logic`), 원자적 쓰기, 형식 버전, 없는 id는 건너뛰고 로그
  - 저장 시점: 정산 → 다음 날 전환 시 자동 + 일시정지 메뉴 수동, 타이틀 "이어하기"
  - 테스트: **저장 → 불러와 3일 진행 = 저장 없이 3일 진행** 왕복 테스트, 깨진 파일 처리
  - 주의: 에디터 플레이 테스트 · 촬영 전에 저장 폴더 백업, 끝나면 복원

## Phase 5. 비주얼 · 사운드 · UI · 메타 (10/23 ~ 10/28, 에셋 도착 순서대로)
- [ ] **5-1. 조명 · 후처리** — 황혼 앰비언트, Bloom, 가게 따뜻한 조명, 실시간 광원 4~6개
- [ ] **5-2. 모델 교체** — 그레이박스 → 리터칭 모델 (1유닛 정규화 → 실제 크기, URP 재질, 분홍 0)
- [ ] **5-3. 손님 애니메이션 최종 적용** — 5종 + 왼팔 바구니 레이어, 소켓 보정, 체형 3단계 검수
- [ ] **5-4. UI 아트 적용** — 패널 · 아이콘 15종 · 폰트(라이선스 확인)
- [ ] **5-5. AudioManager** — 소리는 id로만 호출, 클립이 없으면 무음 + 일반 Log 1회(Warning 아님, 버그 로그 오염 방지). BGM 단계 전환 · 1.5초 교차, 효과음 변형 무작위 · 쿨다운, 볼륨 버스
- [ ] **5-6. 메타** — 타이틀(이어하기 · 새 게임 · 설정 · 종료), 일시정지, 설정(볼륨 · 감도 · 해상도)
- [ ] **5-7. 튜토리얼** — 10/27까지 단계 시스템 + 임시 문구로 구현, 대본(10/28)은 데이터로 교체 (기능 동결 후에도 데이터 교체는 허용)
  - 1일차 개간 · 심기 · 물주기 · 청소 · 진열 / 2일차 첫 수확 (월광 1일 성장, 변경 2026-10-10: ~~3일차 수확~~) / 첫 레벨업 스킬 / 칸 부족 시 "창고에 넣고 오세요", 화면 위쪽 한 줄 · 행동 시 사라짐
- [ ] **5-8. VFX 연결** (P1) — 물방울 · 수확 · 포자 · 거품 · 레벨업 · 홀로그램 등

## 10/28 기능 동결 이후 — 폴리싱 · QA (10/29 ~ 11/1)
- [ ] **7-1. 버그 수정** — 노션 버그 목록 심각도 순, 수정 후 다음 빌드에서 로그 소멸 확인
- [ ] **7-2. 빌드 메뉴** — 메뉴 한 번으로 출력 + 결과 요약(성공 · 크기 · 시간 · 오류 · 경고)
- [ ] **7-3. 실행 스모크 테스트** — 새 게임 → 1일 진행 → 저장 → 종료 → 이어하기 → 6일차까지
- [ ] **7-4. 처음 하는 사람 테스트 반영** (백경민 10/28~10/30 결과)
- [ ] **7-5. 4주차 빌드 (10/31 금)**

## 마감 (11/2 ~ 11/3)
- [ ] **8-1. 촬영용 세이브 파일 준비** — 1 · 3 · 5 · 6일차 대표 상태 저장 (영상 · 발표 시연용)
- [ ] **8-2. 플레이 영상 촬영 · 편집** (백경민과) — 3~4분, 1920×1080, HUD 여부 결정
- [ ] **8-3. 발표자료** (김봉준과) — 기획 · 결정 이유 · 구현 구조 · 트러블슈팅 표 · 회고
- [ ] **8-4. 최종 빌드 · 제출 (11/3)** — 버전 표기 일치(메뉴 · 빌드 설정 · 문서), `git status` 정리

## 매주 반복 (팀장 업무)
- 금요일: 빌드 → 백경민 QA 전달 → 로그 파일 수집
- 월요일: 노션 버그 목록 분배, 작업 보드 갱신, 입력 대기 표 확인
- 노션 타임라인에 일일 작업 정리 (요약 · 한 일 · 다음 할 일 · 이슈 · 오류)

---

## 작업 일지
(항목을 진행하면서 아래 형식으로 이어 적는다)

```markdown
- [x] **N-M. 항목 이름** (YYYY-MM-DD) — 플레이 확인 대기
  - 결정 (YYYY-MM-DD): A안 — 이유
  - 구현: `클래스`(역할), 에셋 `경로`, 메뉴 `경로`
  - 수치: BALANCE.md N번
  - 테스트 K개(무엇) — 전체 T개 통과
  - 측정 (조건): 지표 전 → 후
  - 피드백: 현상 → 원인 → 수정
  - 참고: 알려진 한계 / 다음 할 일
```

- [x] **0-1. Unity 프로젝트 생성 · 기본 설정** (2026-10-07) — 플레이 확인 완료 (2026-10-08)
  - 결정 (2026-10-07): 회사명 `FarmingDiary`(임시, 차후 확정) · 제품명 `Nevrian Glow Farm` — 저장 경로 `LocalLow/FarmingDiary/Nevrian Glow Farm`. 회사명 변경 시 저장 경로가 바뀌므로 6-1 저장 구현 전에 확정
  - 결정 (2026-10-07): Mobile 품질 단계 · 렌더러 제거, PC 단일 품질 — 플랫폼 PC 전용, 설정 하나로 Bloom · 광원 확인 단순화
  - 결정 (2026-10-07): Bloom은 URP 프로젝트 기본 볼륨 프로필(`Assets/Settings/DefaultVolumeProfile.asset`)에 둔다 — 모든 씬(Core/Farm/Shop)에 별도 오브젝트 없이 적용. 구역별 차이는 2-3 ZoneProfile에서 덮어쓴다
  - 구현: Unity 6000.3.25f1, Player Settings(회사 · 제품 · 0.1.0 · `com.FarmingDiary.NevrianGlowFarm`), Input System 단독(`activeInputHandler: 1`), 기본 볼륨 Bloom(임계 1 · 강도 0.25 · 산란 0.5 · 고품질) + Tonemapping Neutral + Vignette 0.2 — 임시값 사용(교체 대기: 5-1 조명 · 후처리)
  - 제거: TutorialInfo, Readme, SampleScene, SampleSceneProfile, Mobile_RPAsset, Mobile_Renderer, 빌드 씬 목록 비움
  - 테스트: 임시 씬 `Assets/Scenes/_Empty01.unity`(카메라 · 조명 · 큐브) 플레이 → 콘솔 0건(오류 0 · 경고 0)
  - 참고: `_Empty01`은 확인용 임시 씬 → 0-3에서 Core/Farm/Shop 생성 시 삭제. 카메라 Post Processing 체크는 0-3 Core 카메라에서 켠다. Git은 GitHub Desktop으로 관리한다 (결정 2026-10-07, 사용자 요청) — 명령줄 작업은 GitHub Desktop 내장 git(`%LOCALAPPDATA%\GitHubDesktop\app-*\resources\app\git\cmd\git.exe`) 사용
- [x] **0-2. 폴더 구조 · Git** (2026-10-07) — 플레이 확인 완료 (2026-10-08)
  - 결정 (2026-10-07): 항목 커밋은 `dev`에 바로 쌓고, 금요일 빌드 · 마일스톤에서 `dev → main` 병합. feature 브랜치는 위험한 작업(저장 등)에만 — 개발 1인이라 브랜치 전환 비용을 줄임
  - 결정 (2026-10-07): 항목 구현이 끝나면 확인 없이 자동 커밋, push는 사용자가 GitHub Desktop에서 (CLAUDE.md Git 섹션에 반영)
  - 구현: `Assets/_Project/` 폴더 트리(CLAUDE.md 기준, `Systems/` 하위 폴더는 해당 항목에서 생성), 빈 폴더마다 `.gitkeep` — clone 시 폴더 .meta만 남아 Unity가 경고를 내는 문제 방지
  - `.gitignore`: 전부 루트 기준 경로(`/Nevrian Glow/Library/`, `/Build/` 등)로 재작성, `*.slnx` 추가 → 이미 추적 중이던 `Nevrian Glow.slnx` 추적 해제
  - 원격: `origin` = https://github.com/jinsu-lee-code/NevrianGlow.git (기존), `dev` 브랜치는 로컬 생성 → GitHub Desktop에서 Publish 필요
  - 테스트: 커밋 후 임시 폴더로 clone → 추적 파일에 Library · Temp · csproj 없음, 짝 없는 .meta 0개
  - 참고: 다른 PC에서 열어 플레이하는 실제 확인은 사용자 몫
- [x] **0-3. 씬 3개 + Additive 로드** (2026-10-07) — 플레이 확인 완료 (2026-10-08) — 임시값 사용(교체 대기: 레벨 배치도 10/9)
  - 결정 (2026-10-07): Shop 씬 전체를 `ShopRoot` (500, 0, 0) 아래에 둔다 — Farm · Shop이 동시에 로드되므로 공간이 겹치면 안 됨. BALANCE 9-1의 Shop 좌표는 `ShopRoot` 기준 로컬 좌표로 읽는다. Farm은 -X로 확장되므로 +X 쪽으로 분리. 오프셋은 루트 위치만 바꾸면 됨
  - 구현: `SceneBootstrapper`(Core, 구역 씬 이름 목록을 인스펙터로 받아 `LoadSceneAsync(Additive)`, 이미 열려 있으면 건너뜀, 모두 끝나면 `IsReady` + `Ready` 이벤트, 실패 시 Error 로그 후 Ready 안 함)
  - 씬 `Assets/_Project/Scenes/` Core · Farm · Shop, Build Settings 0 Core / 1 Farm / 2 Shop
    - Core: SceneBootstrapper, GameManager · AudioManager · ZoneTransition(빈 자리), Main Camera(Post Processing 켬, 임시로 FarmSpawn 위치), Directional Light(청보라 임시), UICanvas(1920×1080 기준, 0.5 매칭), EventSystem(`InputSystemUIInputModule`)
    - Farm: `FarmRoot` / Ground 40×20 m (`mat_greybox_ground_farm` #0E2640)
    - Shop: `ShopRoot` (500,0,0) / Floor 12×10 m (`mat_greybox_floor_shop` #2A2440)
  - 제거: 임시 씬 `_Empty01`, `Assets/Scenes` 폴더
  - 테스트: Core만 열고 Play → Core · Farm · Shop 모두 loaded, `IsReady = true`, 콘솔 0건 (오류 0 · 경고 0)
  - 참고: Farm이나 Shop만 열고 Play하면 Core가 없어 아무것도 시작되지 않는다 → 필요해지면 "항상 Core에서 플레이" 에디터 옵션을 별도 항목으로. 플레이어 · 카메라 이동은 1-1, 스폰 지점은 2-3
- [x] **0-4. 문서 3종 + 테스트 어셈블리** (2026-10-07) — 플레이 확인 완료 (2026-10-08)
  - 문서: CLAUDE.md · GDD.md · TASKS.md · BALANCE.md 저장소 루트에 커밋되어 있음 (0-0 · 0-2)
  - 구현: `NevrianGlowFarm.Logic` asmdef(`Scripts/Logic/`, `noEngineReferences: true` — 엔진 참조 자체를 막음), 식별용 `LogicAssembly`
  - 구현: `NevrianGlowFarm.Tests.EditMode` asmdef(`Tests/EditMode/`, Editor 전용, `UNITY_INCLUDE_TESTS`, Logic 참조)
  - 테스트 1개(`LogicAssembly_DoesNotReferenceUnityEngine` — Logic 어셈블리가 UnityEngine/UnityEditor를 참조하지 않음) — 전체 1개 통과
  - 참고: 런타임 스크립트(`Scripts/Core` 등)는 아직 Assembly-CSharp. 테스트 어셈블리는 Assembly-CSharp를 참조할 수 없으므로 MonoBehaviour 쪽 테스트가 필요해지면 Runtime asmdef를 추가한다. 규칙 테스트는 전부 Logic으로
  - 참고: 콘솔 경고 1건 "Account API did not become accessible" — `com.unity.ai.assistant` 패키지(에디터 전용, MCP 연결용)에서 발생, 프로젝트 코드와 무관
- [x] **0-5. LogCollector** (2026-10-07) — 플레이 확인 완료 (2026-10-08)
  - 결정 (2026-10-07): 새 로그는 받는 즉시 파일에 쓰고(직후 크래시 대비), 횟수만 늘면 1초 간격으로 모아 쓴다 — 매 프레임 오류가 매 프레임 파일 쓰기가 되지 않게. 파일은 임시 파일에 쓴 뒤 교체(원자적 쓰기), 첫 로그가 들어올 때 생성(빈 파일 없음)
  - 구현: Logic `LogAggregator`(메시지+스택 묶기 · 횟수 · 최대 개수 초과분 집계), `LogLineFormatter`(한 줄 형식 · 줄바꿈 → ` ↵ `), `LogSeverity` · `LogContext`
  - 구현: Core `LogCollector`(`logMessageReceivedThreaded`, 실행 순서 -1000, 잠금으로 스레드 안전, 자기 로그 재귀 방지) → Core 씬 첫 오브젝트로 배치
  - 구현: 에디터 메뉴 `Glow Farm/디버그 창` — 로그 폴더 경로 · "로그 폴더 열기" · 플레이 중 이번 세션 파일 · "테스트 예외 발생"
  - 형식: `시각 | 유형 | Day N | 단계 | 구역 | v버전 | xN (last 시각) | 메시지 | 스택`, 첫 줄은 `# 제품 세션 로그 · 시작 시각 · 버전 · Unity 버전 · 플랫폼`
  - 저장: `persistentDataPath/Logs/session_yyyyMMdd_HHmmss.log` = `%USERPROFILE%\AppData\LocalLow\FarmingDiary\Nevrian Glow Farm\Logs\`
  - 테스트 6개(묶기 · 스택 다르면 분리 · 최대 개수 · 줄 형식 · 빈 상황 `-` · 파일 형식) — 전체 7개 통과
  - 확인: 플레이 중 같은 예외 2회 → 세션 파일에 1줄 `x2`, 임시 파일 남지 않음
  - 결정 (2026-10-08): Unity AI Assistant 패키지(`com.unity.ai.assistant`, MCP 브리지) 경고는 걸러내지 않고 그대로 둔다. QA · "경고 0" 판정에서 `Account API did not become accessible` · `Failed to collect Windows signature for ...claude.exe` 두 문구는 무시 — 에디터 전용 패키지라 빌드 로그에는 나오지 않고, 걸러내면 패키지의 진짜 문제도 가려짐 (Claude 실행 파일의 ECDSA 서명을 패키지가 해석하지 못해 생기는 경고, 연결은 정상)
  - 참고: 일차 · 단계 · 구역은 아직 `Day 0 | - | -` — `SetDayAndPhase`는 1-3 GameManager, `SetZone`은 2-3 ZoneTransition에서 연결. 빌드(플레이어)에서의 동작 확인은 1-5 빌드 때
- [x] **0-6. 임시 데이터 세트 Setup 메뉴** (2026-10-08) — 플레이 확인 완료 (2026-10-08) — 임시값 사용(교체 대기: 작물 CSV 10/11)
  - 결정 (2026-10-08): 이번 범위는 Settings 6종 + 작물 3종. 상품 · 스킬은 효과 구조(EffectSO · Modifier)를 정하는 3-3 · 4-3에서 같은 Setup 메뉴에 추가 — 미리 만든 효과 구조를 다시 뜯는 일을 피함
  - 결정 (2026-10-08): 작물 재수확은 `regrowDays`(간격) + `maxHarvests`(한 포기 총 수확 횟수)로 표현 — BALANCE "최대 3회"와 3-1 검산(9 · 13 · 17일차)이 총 3회이므로 "추가 횟수"보다 헷갈리지 않음. 거대 X 작물은 `giantChance = 0`
  - 결정 (2026-10-08): 구획 가격 · 씨앗 일일 입고량 · 인벤토리/진열대 확장 가격은 Settings가 아니라 상점 상품(BALANCE 9-2, 3-3)에 둔다
  - 구현: `Scripts/Data/` — `ContentData`(id · 표시 이름 공통), `CropData`, `PlayerSettingsData`, `GameStartSettingsData`, `InventorySettingsData`, `CleaningSettingsData`(개수 규칙 `기본 + min(일차 ÷ n, 상한)`), `SalesSettingsData`, `ProgressionData`. 클래스 필드에는 수치 초기값 없음(범위 하한만)
  - 구현: 메뉴 `Glow Farm/Setup/Create Placeholder Data`(없는 에셋만 생성, 있으면 유지) · `Glow Farm/Setup/Reset Placeholder Data to Defaults`(확인 창 후 BALANCE 값으로 덮어씀) — `PlaceholderDataSetup`, 수치는 이 파일 한 곳에만
  - 에셋: `Data/Crops/crop_moonroot · crop_mushroomflower · crop_lanternfruit`, `Data/Stats/PlayerSettings · GameStartSettings · InventorySettings · CleaningSettings · SalesSettings · Progression`
  - 확인: 1회차 생성 9 → 값 BALANCE 2-1 ~ 2-7과 일치, 값 하나 바꾸고 2회차 → 유지 9 · 바꾼 값 그대로, 초기화 → 초기화 9 · 원래 값 복원. 콘솔 오류 · 경고 0 (AI Assistant 경고 제외)
  - 참고: 필드 이름이 2-1 CSV 임포터의 열 이름 기준이 된다. 김봉준 작물 CSV가 오면 열 이름을 이 필드에 맞추거나 임포터에서 대응표를 둔다
- [x] **1-1. PlayerController (1인칭 카메라 전용)** (2026-10-08) — 플레이 확인 완료 (2026-10-08) — 임시값 사용(교체 대기: 레벨 배치도 10/9 — 시작 위치)
  - 결정 (2026-10-08): 입력은 템플릿의 프로젝트 전체 입력 에셋(`Assets/InputSystem_Actions.inputactions`)의 `Player/Move · Look · Sprint`를 `InputActionReference`로 받는다 — 키 배치가 BALANCE 9-4와 같음. 핫바 · 상점 키 등 액션 정리와 에셋 위치 이동은 1-2에서 필요할 때
  - 결정 (2026-10-08): 플레이어는 Core 씬에 둔다(구역을 오가며 유지). 몸(`Player`)이 yaw, 자식 카메라가 pitch · 눈높이 · 헤드밥. 구역 씬 로드 완료(`SceneBootstrapper.IsReady`) 전에는 움직이지 않음 — 땅이 올라오기 전 낙하 방지
  - 구현: Logic `FirstPersonMath`(시점 yaw · pitch 제한, 헤드밥 위상 · 높이), `Systems/Player/PlayerController`(CharacterController 이동 · 중력 · 질주 · 헤드밥 · 커서 잠금, 수치는 `PlayerSettings` 에셋)
  - 씬: Core에 `Player`(4, 0, 0) · -X 방향, CharacterController 높이 1.8 · 반지름 0.35 · 중심 0.9, `Main Camera`를 자식으로 이동(눈높이 1.7)
  - 테스트 5개(시점 방향 · pitch 제한 · yaw 감기 · 헤드밥 위상 진행/정지 초기화 · 헤드밥 높이) — 전체 12개 통과
  - 확인(자동): 플레이 30프레임 뒤 땅 위(y = 0, grounded), 카메라 높이 1.7, 콘솔 오류 · 경고 0 (AI Assistant 경고 제외)
  - 미확인: 걷기 · 질주 · 시점 · 헤드밥의 실제 조작 — MCP에서 키 입력을 넣을 수 없어 사용자 플레이 확인으로 대신
  - 참고: 농장 바닥(40×20 m) 끝에는 벽이 없어 밖으로 걸어 나가면 떨어진다 — 그레이박스 경계는 레벨 배치도(10/9) 반영 때
- [x] **1-2. InteractionRaycaster + IInteractable** (2026-10-08) — 플레이 확인 완료 (2026-10-10) — 임시값 사용(교체 대기: UI 흐름도 10/14 · UI 아트 10/23)
  - 결정 (2026-10-08): 도구는 enum이 아니라 `ToolData` 에셋(`Data/Tools/tool_*`, 0-6 Setup 메뉴에 추가) — 콘텐츠는 에셋 참조로 구분 규칙. 핫바 순서는 Player의 `Hotbar` 인스펙터 목록, 칸 수 = 목록 길이(숫자 키 1~9)
  - 결정 (2026-10-08): 그레이박스 한글 폰트는 사용자가 넣은 NEXON Maplestory Bold(`Art/Fonts/NEXON_Maplestory`) → TMP 동적 폰트 에셋 `font_maplestory_bold_sdf`를 TMP 기본 폰트로 지정. 배포 라이선스 확인과 최종 폰트는 5-4
  - 구현: `IInteractable`(`GetPrompt(tool)` · `Interact(tool)`), `InteractionRaycaster`(카메라 중앙 Raycast, 사거리 = PlayerSettings 5 m, Interactable 레이어만, 콜라이더가 바뀔 때만 컴포넌트 탐색, 대상 · 도구 변경과 상호작용 직후에만 프롬프트 갱신 → `PromptChanged`, E · 좌클릭에 `Interact`, 기즈모 시안 직선 + 맞은 점), `Hotbar`(숫자 키, `SelectionChanged`), Logic `HotbarSelection`, 공용 `GizmoKit`(색 규칙 6종)
  - 구현: UI `InteractionPromptView`(화면 중앙 아래, 비면 숨김) · `HotbarView` + `HotbarSlotView`(도구 수만큼 칸 생성, 선택 칸 시안) · 조준점 — 단색 패널 + 글자
  - 설정: 레이어 `Interactable`(8), 입력 에셋 `Player/Hotbar`(키 1~9) 추가. 좌클릭은 템플릿의 `Player/Attack`(좌클릭 · Enter)을 그대로 사용
  - 확인용: Farm `FarmRoot/InteractionProbe`(1, 1, 0) 기둥 0.6×2×0.6 m — `InteractionProbe`가 도구 이름으로 프롬프트를 만들고 상호작용마다 색을 바꿈. **실제 대상(1-4 밭 칸)이 생기면 씬에서 지운다**
  - 테스트 4개(키 번호 → 칸, 같은 칸 무시, 없는 칸 무시, 칸 수 고정 안 됨) — 전체 16개 통과
  - 확인(자동): 정면 기둥에 맞음 → 프롬프트 `[E / 클릭] 손(으)로 확인 (0회)`, 변화 없을 때 갱신 0회 · 도구 변경 시 1회, 핫바 5칸(손 · 씨앗 주머니 · 물뿌리개 · 대걸레 · 비료) 생성 · 4번 선택 시 강조 이동, 한글 글리프 누락 0, 콘솔 오류 · 경고 0
  - 플레이 확인 (2026-10-10, 사용자): 실제 키(1~5 · E · 좌클릭) 입력 정상 작동
  - 참고 (2026-10-10): 10/08 조작 변경(좌클릭 / E 분리, 핫바 4칸, 씨앗 주머니 · 손 도구 폐지)은 1-2b에서 개편. Raycast · 프롬프트 · GizmoKit 구조는 그대로 사용
  - 참고: 상호작용 강조 효과(외곽선 등)는 그레이박스에서 생략 — 프롬프트만. Raycast는 Interactable 레이어만 보므로 벽 너머 대상도 잡힌다 → 벽이 생기는 2-3에서 가림 레이어 추가 검토
  - 참고: TMP 동적 폰트 에셋은 플레이 중 새 글자가 아틀라스에 추가되며 파일이 바뀔 수 있다(빌드 시 동적 데이터 정리 켬)

## 트러블슈팅
| 문제 | 원인 | 해결 |
|---|---|---|
| Unity MCP `RunCommand`에서 `AssetDatabase.DeleteAsset` · `NewScene` 호출 시 "User interactions are not supported" | 사용자 확인이 필요한 API로 분류됨 | 삭제는 `Unity_ManageAsset(Delete)`, 씬 생성은 `Unity_ManageScene(Create)` 사용 |
| MCP로 플레이 중 상태를 확인하면 프레임이 2에서 멈춤 | 에디터 창이 포커스를 잃으면 플레이 모드 프레임이 진행되지 않음 | `EditorApplication.isPaused = true` + `Step()`으로 프레임을 넘겨 확인, 끝나면 `isPaused = false`로 복구 |
| MCP에서 `InputSystem.QueueStateEvent`로 키를 넣어도 플레이어가 움직이지 않음 (`wKey=False`) | 에디터 밖(MCP 호출 문맥)에서 큐에 넣은 이벤트가 플레이 프레임의 입력 업데이트로 처리되지 않음. Game view 포커스 설정을 바꿔도 동일 | 조작 확인은 사용자 플레이로. 자동 검증은 Logic 단위 테스트 + 정지 상태(위치 · 접지) 확인까지 |
| (작업 중 겪을 때마다 추가) | | |
