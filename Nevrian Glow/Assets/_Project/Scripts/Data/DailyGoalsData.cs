using System;
using System.Collections.Generic;
using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>
    /// 3 ~ 6일차 아침 편지 · 오늘의 목표 · 상점 '추천' (튜토리얼 v3 5 · 6절, 10/30). daily_goals.csv · letters.csv를 아직 못 받아
    /// v3 문서 표로 만든 데이터 (메뉴 Apply Tutorial v3). 조건 문법은 Logic GoalConditions. 편지 문구는 튜토리얼 데이터(TutorialSteps)의 편지 id.
    /// </summary>
    [CreateAssetMenu(menuName = "Glow Farm/Data/Daily Goals", fileName = "DailyGoals")]
    public class DailyGoalsData : ScriptableObject
    {
        [Serializable]
        public class Goal
        {
            [Tooltip("목표 id (못 한 핵심 목표를 다음 날 다시 띄울 때 같은 것인지)")]
            [SerializeField] private string id;
            [Tooltip("체크리스트 글자 (게임 화면 — '—' 쓰지 않음)")]
            [SerializeField] private string text;
            [Tooltip("완료 조건 — Logic GoalConditions 문법 (예: bought:shop_hoe_upgrade:1)")]
            [SerializeField] private string condition;
            [Tooltip("핵심 목표 — 그날 못 하면 다음 날 목표 맨 위에 다시")]
            [SerializeField] private bool core;

            public Goal(string id, string text, string condition, bool core)
            {
                this.id = id; this.text = text; this.condition = condition; this.core = core;
            }

            public string Id => id;
            public string Text => text;
            public string Condition => condition;
            public bool Core => core;
        }

        [Serializable]
        public class Day
        {
            [Tooltip("일차")]
            [SerializeField, Min(1)] private int day = 3;
            [Tooltip("아침에 여는 편지 id (튜토리얼 데이터의 편지, 비우면 없음)")]
            [SerializeField] private string letterId;
            [Tooltip("오늘의 목표 (위에서부터)")]
            [SerializeField] private List<Goal> goals = new List<Goal>();
            [Tooltip("상점 '추천' 꼬리표를 붙일 상품 id")]
            [SerializeField] private List<string> recommended = new List<string>();

            public Day(int day, string letterId, List<Goal> goals, List<string> recommended)
            {
                this.day = day; this.letterId = letterId; this.goals = goals; this.recommended = recommended;
            }

            public int Number => day;
            public string LetterId => letterId;
            public IReadOnlyList<Goal> Goals => goals;
            public IReadOnlyList<string> Recommended => recommended;
        }

        [SerializeField] private List<Day> days = new List<Day>();

        public IReadOnlyList<Day> Days => days;

        /// <summary>그 일차의 묶음 (없으면 null).</summary>
        public Day Get(int day)
        {
            foreach (var d in days)
                if (d != null && d.Number == day)
                    return d;
            return null;
        }
    }
}
