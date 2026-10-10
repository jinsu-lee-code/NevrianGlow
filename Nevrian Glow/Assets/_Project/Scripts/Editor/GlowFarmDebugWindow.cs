#if UNITY_EDITOR
using System;
using System.IO;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Systems.Farm;
using NevrianGlowFarm.Systems.Player;
using UnityEditor;
using UnityEngine;

namespace NevrianGlowFarm.Editor
{
    /// <summary>"Glow Farm 디버그" 창. 디버그 기능은 게임 화면이 아니라 이 창에만 둔다.</summary>
    public class GlowFarmDebugWindow : EditorWindow
    {
        [MenuItem("Glow Farm/디버그 창")]
        private static void Open()
        {
            GetWindow<GlowFarmDebugWindow>("Glow Farm 디버그");
        }

        private Hotbar hotbar;
        private DayPhase forcedPhase;
        private int moneyAmount = 100;
        private int skillPointAmount = 3;
        private int expAmount = 80;
        private Data.ItemData addItem;
        private int addCount = 1;

        private void OnGUI()
        {
            DrawGameSection();
            EditorGUILayout.Space();
            DrawFarmSection();
            EditorGUILayout.Space();
            DrawCleaningSection();
            EditorGUILayout.Space();
            DrawSalesSection();
            EditorGUILayout.Space();
            DrawSkillSection();
            EditorGUILayout.Space();
            DrawHotbarSection();
            EditorGUILayout.Space();
            DrawLogSection();
        }

        private void DrawGameSection()
        {
            EditorGUILayout.LabelField("게임 진행 (GameManager)", EditorStyles.boldLabel);

            var game = GameManager.Instance;
            if (!Application.isPlaying || game == null)
            {
                EditorGUILayout.HelpBox("Core 씬을 플레이하면 일차 · 단계 · 달러 · SP를 바꿀 수 있다.", MessageType.None);
                return;
            }
            if (!game.IsStarted)
            {
                EditorGUILayout.HelpBox("구역 씬을 불러오는 중…", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField("현재", $"{game.Day}일차 · {DayPhaseNames.ToKorean(game.Phase)} · {game.Money}달러 · {game.SkillPoints} SP");
            EditorGUILayout.LabelField("레벨", $"Lv{game.Level} · 경험치 {game.Exp} / {game.ExpRequired}");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("다음 단계"))
                    game.AdvancePhase();
                if (GUILayout.Button("다음 날 (상점으로)"))
                    game.AdvanceToNextDay();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                forcedPhase = (DayPhase)EditorGUILayout.EnumPopup("단계 강제 전환", forcedPhase);
                if (GUILayout.Button("적용", GUILayout.Width(60)))
                    game.ForcePhase(forcedPhase);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                moneyAmount = EditorGUILayout.IntField("달러 (음수 = 빼기)", moneyAmount);
                if (GUILayout.Button("적용", GUILayout.Width(60)) && !game.TryChangeMoney(moneyAmount))
                    Debug.Log($"[디버그] 달러 {moneyAmount:+#;-#;0} 적용 안 됨 — 0이거나 보유량보다 많이 뺌");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                expAmount = EditorGUILayout.IntField("경험치 더하기", expAmount);
                if (GUILayout.Button("적용", GUILayout.Width(60)))
                {
                    var gain = game.AddExp(expAmount);
                    if (gain.LeveledUp)
                        Debug.Log($"[디버그] 경험치 +{gain.Exp} → Lv{game.Level} (레벨 {gain.LevelsGained} 오름 · SP +{gain.SkillPointsGained})");
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                skillPointAmount = EditorGUILayout.IntField("SP (음수 = 빼기)", skillPointAmount);
                if (GUILayout.Button("적용", GUILayout.Width(60)) && !game.TryChangeSkillPoints(skillPointAmount))
                    Debug.Log($"[디버그] SP {skillPointAmount:+#;-#;0} 적용 안 됨 — 0이거나 보유량보다 많이 뺌");
            }
        }

        private void DrawFarmSection()
        {
            EditorGUILayout.LabelField("밭 (FarmGrid)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                farm = null;
                EditorGUILayout.HelpBox("플레이 중일 때 칸 상태 요약과 물주기 버튼이 보인다. 밤 처리는 위의 '다음 단계'(스킬 → 다음 날) · '다음 날'로.", MessageType.None);
                return;
            }

            if (farm == null)
                farm = FindFirstObjectByType<FarmGrid>();
            if (farm == null)
            {
                EditorGUILayout.HelpBox("씬에 FarmGrid가 없다 (Farm 씬 로드 확인).", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField(farm.Summary, EditorStyles.wordWrappedLabel);
            if (GUILayout.Button("자라는 칸 모두 물주기"))
                Debug.Log($"[디버그] 물주기: {farm.DebugWaterAll()}칸");
        }

        private FarmGrid farm;

        private void DrawCleaningSection()
        {
            EditorGUILayout.LabelField("청소 (CleaningSystem)", EditorStyles.boldLabel);
            var cleaning = Systems.Cleaning.CleaningSystem.Instance;
            if (!Application.isPlaying || cleaning == null)
            {
                EditorGUILayout.HelpBox("플레이 중일 때 청결도와 치우기 버튼이 보인다. 쓰레기 · 얼룩은 청소·진열 단계에 들어설 때 놓인다.", MessageType.None);
                return;
            }

            string locked = cleaning.LockedCleanliness < 0f ? "아직" : cleaning.LockedCleanliness.ToString("P0");
            EditorGUILayout.LabelField("오늘 처음", $"쓰레기 {cleaning.InitialTrash} · 얼룩 {cleaning.InitialStains}");
            EditorGUILayout.LabelField("지금", $"바닥 쓰레기 {cleaning.FloorTrashCount()} · 얼룩 양 {cleaning.StainAmountSum():0.##} · 청결도 {cleaning.CurrentCleanliness():P0}");
            EditorGUILayout.LabelField("장사 시작 때 확정", locked);
            if (GUILayout.Button("바닥 쓰레기 · 얼룩 모두 치우기"))
                cleaning.DebugCleanAll();
        }

        // 스킬 단계 (SkillSystem 4-3 전 임시) — 상점 상품 해금 조건에 나오는 스킬 id를 모아 단계를 바로 바꾼다 (SP 소비 없음)
        private void DrawSkillSection()
        {
            EditorGUILayout.LabelField("스킬 단계 (4-3 전 임시)", EditorStyles.boldLabel);
            var game = GameManager.Instance;
            var shop = Systems.Shop.ShopSystem.Instance;
            if (!Application.isPlaying || game == null || !game.IsStarted || shop == null)
            {
                EditorGUILayout.HelpBox("플레이 중일 때 상점 해금 조건의 스킬 단계를 바꿀 수 있다 (SP를 쓰지 않음).", MessageType.None);
                return;
            }
            var ids = new System.Collections.Generic.SortedSet<string>();
            foreach (var p in shop.Products)
                foreach (var t in p.Tiers)
                    foreach (var r in t.Requirements)
                        if (r.Kind == Logic.Shop.UnlockKind.SkillLevel && !string.IsNullOrEmpty(r.TargetId))
                            ids.Add(r.TargetId);
            foreach (var id in ids)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    int level = game.SkillLevel(id);
                    EditorGUILayout.LabelField(id, $"{level}단계");
                    if (GUILayout.Button("-", GUILayout.Width(30)))
                        game.SetSkillLevel(id, level - 1);
                    if (GUILayout.Button("+", GUILayout.Width(30)))
                        game.SetSkillLevel(id, level + 1);
                }
            }
            EditorGUILayout.LabelField("오늘 상점 지출", $"{shop.SpentToday}달러");
        }

        // 장사 단계 진행 (SalesSystem 3-2) — 진행 요약 · 건너뛰기(게임의 Space와 같은 함수)
        private void DrawSalesSection()
        {
            EditorGUILayout.LabelField("장사 (SalesSystem)", EditorStyles.boldLabel);
            var game = GameManager.Instance;
            var sales = Systems.Sales.SalesSystem.Instance;
            if (!Application.isPlaying || game == null || !game.IsStarted)
            {
                EditorGUILayout.HelpBox("플레이 중일 때 장사 진행과 '건너뛰기' 버튼이 보인다. 장사는 장사 단계에 들어서면 자동으로 시작한다.", MessageType.None);
                return;
            }
            if (sales == null)
            {
                EditorGUILayout.HelpBox("씬에 SalesSystem이 없다 (Shop 씬 로드 확인).", MessageType.Warning);
                return;
            }

            var session = sales.Session;
            if (sales.IsRunning && session != null)
            {
                EditorGUILayout.LabelField("진행", $"{sales.Elapsed:0.0}초 · 손님 {session.Spawned}/{session.Total} (가게 안 {session.InStore})");
                EditorGUILayout.LabelField("판매", $"구매 {session.Buyers} · 빈손 {session.EmptyHanded} · {session.ItemsSold}개 · {session.Revenue}달러");
                if (GUILayout.Button("건너뛰기 (Space와 같음 — 남은 손님 바로 처리 → 정산)"))
                    sales.FinishNow(advancePhase: true);
            }
            else if (sales.LastResult.HasValue)
            {
                var r = sales.LastResult.Value;
                EditorGUILayout.LabelField("지난 장사", $"{r.Day}일차 · 손님 {r.Customers} (구매 {r.Buyers} · 빈손 {r.EmptyHanded}) · {r.ItemsSold}개 · {r.Revenue}달러{(r.Skipped ? " · 건너뜀" : "")}");
            }
            else
                EditorGUILayout.HelpBox("장사 단계가 아니다 (위 '다음 단계' · '단계 강제 전환'으로 장사 단계).", MessageType.None);
        }

        private void DrawHotbarSection()
        {
            EditorGUILayout.LabelField("핫바", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                hotbar = null;
                EditorGUILayout.HelpBox("플레이 중일 때 핫바 칸 수를 바꿀 수 있다 (스킬 핫바 확장 확인용).", MessageType.None);
                return;
            }

            if (hotbar == null)
                hotbar = FindFirstObjectByType<Hotbar>();
            if (hotbar == null)
            {
                EditorGUILayout.HelpBox("씬에 Hotbar가 없다.", MessageType.Warning);
                return;
            }

            var item = hotbar.SelectedItem;
            EditorGUILayout.LabelField("칸 수 · 선택", $"{hotbar.SlotCount}칸 · {hotbar.SelectedIndex + 1}번 ({(item != null ? item.DisplayName : "빈 칸")})");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("칸 −1"))
                    hotbar.SetSlotCount(hotbar.SlotCount - 1);
                if (GUILayout.Button("칸 +1"))
                    hotbar.SetSlotCount(hotbar.SlotCount + 1);
            }

            // 아이템 넣기 — 상점(3-3)이 없는 동안 등급 도구 · 씨앗 확인용. 핫바 빈 칸부터, 모자라면 가방
            using (new EditorGUILayout.HorizontalScope())
            {
                addItem = (Data.ItemData)EditorGUILayout.ObjectField("아이템 넣기", addItem, typeof(Data.ItemData), false);
                addCount = Mathf.Max(1, EditorGUILayout.IntField(addCount, GUILayout.Width(40)));
                using (new EditorGUI.DisabledScope(addItem == null))
                {
                    if (GUILayout.Button("넣기", GUILayout.Width(60)) && !hotbar.TryAdd(addItem, addCount))
                        Debug.Log($"[디버그] {addItem.DisplayName} ×{addCount} 넣기 실패 — 핫바 · 가방에 자리 없음 (도구는 칸마다 1개)");
                }
            }

            var inventory = hotbar.Inventory;
            if (inventory == null || inventory.Bag == null)
                return;
            int step = inventory.Settings.BagSlotsPerExpand;
            EditorGUILayout.LabelField("가방", $"{inventory.Bag.SlotCount}칸 (최대 {inventory.Settings.BagMaxSlots}) — 상점 가방 확장(3-3) 확인용");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button($"가방 −{step}"))
                    inventory.SetBagSlotCount(inventory.Bag.SlotCount - step);
                if (GUILayout.Button($"가방 +{step}"))
                    inventory.SetBagSlotCount(inventory.Bag.SlotCount + step);
            }
        }

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying)
                Repaint();
        }

        private void DrawLogSection()
        {
            EditorGUILayout.LabelField("로그 (LogCollector)", EditorStyles.boldLabel);

            string folder = LogCollector.GetLogFolder();
            EditorGUILayout.SelectableLabel(folder, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

            if (GUILayout.Button("로그 폴더 열기"))
            {
                Directory.CreateDirectory(folder);
                Application.OpenURL("file:///" + folder.Replace('\\', '/'));
            }

            var collector = LogCollector.Instance;
            if (!Application.isPlaying || collector == null)
            {
                EditorGUILayout.HelpBox("플레이 중(Core 씬 포함)일 때 이번 세션 파일과 테스트 버튼이 보인다.", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField("이번 세션 파일", Path.GetFileName(collector.SessionFilePath));

            if (GUILayout.Button("테스트 예외 발생 (같은 예외 1회 추가)"))
                Debug.LogException(new InvalidOperationException("[테스트] LogCollector 확인용 예외"));
        }
    }
}
#endif
