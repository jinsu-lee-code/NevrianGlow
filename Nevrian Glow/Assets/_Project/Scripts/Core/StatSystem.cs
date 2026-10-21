using System.Collections.Generic;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Game;
using UnityEngine;

namespace NevrianGlowFarm.Core
{
    /// <summary>
    /// 능력치 = 기본값 + 변경(Modifier) (노션 GDD 12장 StatSystem, TASKS 4-3). 변경은 배운 스킬 단계에서 모은다.
    /// 값을 쓰는 쪽(판매가 · 손님 수 · 성장일 · 핫바 칸)이 기본값과 능력치 에셋을 넘겨 계산을 받는다 — 계산은 Logic StatCalculator.
    /// 변경이 바뀌면 GameEvents.StatsChanged. Core 씬에 하나. 없으면(테스트 씬) 기본값 그대로.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class StatSystem : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("스킬 목록 (Data/ContentDatabase)")]
        [SerializeField] private ContentDatabase database;

        [Header("디버그")]
        [Tooltip("지금 걸린 변경 (읽기 전용)")]
        [SerializeField] private List<string> modifierView = new List<string>();

        private static StatSystem instance;
        private readonly List<StatModifier> modifiers = new List<StatModifier>();

        public static StatSystem Instance => instance;
        public IReadOnlyList<StatModifier> Modifiers => modifiers;

        private void Awake()
        {
            if (database == null)
            {
                Debug.LogError("[StatSystem] 참조가 비어 있음 - 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void OnEnable()
        {
            GameEvents.SkillLevelChanged += OnSkillLevelChanged;
            GameEvents.DayChanged += OnDayChanged;
        }

        private void OnDisable()
        {
            GameEvents.SkillLevelChanged -= OnSkillLevelChanged;
            GameEvents.DayChanged -= OnDayChanged;
        }

        private void OnSkillLevelChanged(string skillId, int level) => Rebuild();

        // 새 게임(1일차 시작 알림) · 불러오기 후에도 한 번 맞춘다
        private void OnDayChanged(int day) => Rebuild();

        /// <summary>정수 능력치 (판매가 · 칸 수 · 성장일). 능력치 에셋이 비었거나 시스템이 없으면 기본값.</summary>
        public static int Apply(StatData stat, int baseValue) =>
            instance == null || stat == null ? baseValue : StatCalculator.ApplyRounded(stat.Id, baseValue, instance.modifiers);

        /// <summary>실수 능력치 (보너스 비율 등).</summary>
        public static double Apply(StatData stat, double baseValue) =>
            instance == null || stat == null ? baseValue : StatCalculator.Apply(stat.Id, baseValue, instance.modifiers);

        /// <summary>배운 스킬 단계에서 변경을 다시 모은다. 바뀌었으면 알린다.</summary>
        public void Rebuild()
        {
            var game = GameManager.Instance;
            var next = new List<StatModifier>();
            if (game != null && game.IsStarted)
                foreach (var skill in database.All<SkillData>())
                    skill.CollectModifiers(game.SkillLevel(skill.Id), next);

            if (Same(next))
                return;
            modifiers.Clear();
            modifiers.AddRange(next);
            modifierView.Clear();
            foreach (var m in modifiers)
                modifierView.Add($"{m.StatId} {(m.Op == ModifierOp.Add ? "+" : "×")}{m.Value:0.###}");
            GameEvents.RaiseStatsChanged();
        }

        private bool Same(List<StatModifier> next)
        {
            if (next.Count != modifiers.Count)
                return false;
            for (int i = 0; i < next.Count; i++)
                if (next[i].StatId != modifiers[i].StatId || next[i].Op != modifiers[i].Op || next[i].Value != modifiers[i].Value)
                    return false;
            return true;
        }
    }
}
