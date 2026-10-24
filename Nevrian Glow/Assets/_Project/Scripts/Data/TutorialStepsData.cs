using System;
using System.Collections.Generic;
using NevrianGlowFarm.Logic.Tutorial;
using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>
    /// 튜토리얼 데이터 (TASKS 5-7): 단계 목록은 기획 `CSV/plan/data_csv/tutorial_steps.csv`를 `Glow Farm/Data/Import Plan CSV`가 채우고,
    /// 편지는 기획 튜토리얼_플로우 3절 문구를 Setup 메뉴가 채운다. 문구 · 완료 이벤트 · 잠금은 모두 여기 값 — 기획 대본(10/28)은 데이터만 바꾼다.
    /// 규칙은 Logic TutorialProgress, 게임 연결은 Systems/Tutorial/TutorialSystem.
    /// </summary>
    [CreateAssetMenu(menuName = "Glow Farm/Settings/Tutorial Steps", fileName = "TutorialSteps")]
    public class TutorialStepsData : ScriptableObject
    {
        [Serializable]
        public class Step
        {
            [Tooltip("단계 id (기획 step_id, 저장에 쓰임)")]
            [SerializeField] private string id;
            [Tooltip("일차")]
            [SerializeField, Min(1)] private int day = 1;
            [Tooltip("기획 단계 이름 (shop · farming · tidy · sales · settlement)")]
            [SerializeField] private string phase;
            [Tooltip("화면 위쪽 한 줄 문구 (게임 화면 - '—' 쓰지 않음)")]
            [SerializeField, TextArea(1, 3)] private string text;
            [Tooltip("강조 대상 (';' 구분) - 지금은 데이터만")]
            [SerializeField] private string highlight;
            [Tooltip("완료 이벤트 '종류[:대상][:수]' (예: owned:item_seed_moonroot:4, tilled:4, zone:zone_shop, auto)")]
            [SerializeField] private string completeEvent;
            [Tooltip("잠금 이름 (';' 구분 - end_farming · close_shop · open_sales · close_skill · other_skills · buy_other · move)")]
            [SerializeField] private string locks;

            public string Id => id;
            public int Day => day;
            public string Phase => phase;
            public string Text => text;
            public string Highlight => highlight;
            public string CompleteEvent => completeEvent;
            public string Locks => locks;

            public TutorialStep ToLogic() => new TutorialStep(id, day, phase, text, highlight, completeEvent, locks);
        }

        [Serializable]
        public class Letter
        {
            [Tooltip("편지 id (완료 이벤트 letter_closed:<id>와 같게)")]
            [SerializeField] private string id;
            [Tooltip("봉투 글귀 · 제목")]
            [SerializeField] private string title;
            [Tooltip("본문")]
            [SerializeField, TextArea(6, 16)] private string body;

            public string Id => id;
            public string Title => title;
            public string Body => body;

            public Letter() { }

            public Letter(string id, string title, string body)
            {
                this.id = id;
                this.title = title;
                this.body = body;
            }
        }

        [Header("단계 (기획 tutorial_steps.csv - Import Plan CSV가 채운다)")]
        [SerializeField] private List<Step> steps = new List<Step>();

        [Header("편지 (기획 튜토리얼_플로우 3절 - Create Tutorial HUD가 빈 것을 채운다)")]
        [SerializeField] private List<Letter> letters = new List<Letter>();

        public IReadOnlyList<Step> Steps => steps;
        public IReadOnlyList<Letter> Letters => letters;

        /// <summary>Logic 단계 목록 (순서 그대로, id가 빈 줄은 뺌).</summary>
        public List<TutorialStep> ToSteps()
        {
            var list = new List<TutorialStep>(steps.Count);
            foreach (var s in steps)
                if (s != null && !string.IsNullOrEmpty(s.Id))
                    list.Add(s.ToLogic());
            return list;
        }

        public bool TryGetLetter(string id, out Letter letter)
        {
            letter = null;
            if (string.IsNullOrEmpty(id))
                return false;
            foreach (var l in letters)
                if (l != null && l.Id == id)
                {
                    letter = l;
                    return true;
                }
            return false;
        }
    }
}
