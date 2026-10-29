using System;
using System.Collections.Generic;

namespace NevrianGlowFarm.Logic.Tutorial
{
    /// <summary>튜토리얼 상태. enum은 끝에만 추가.</summary>
    public enum TutorialStatus
    {
        /// <summary>안 켜짐 (튜토리얼 없던 저장 · 끔).</summary>
        Off,
        /// <summary>진행 중 — Current가 있다.</summary>
        Running,
        /// <summary>마지막 단계까지 끝남 (안내 꺼짐 · 잠금 없음).</summary>
        Done,
    }

    /// <summary>
    /// 튜토리얼 진행기 (TASKS 5-7, 기획 튜토리얼_플로우_1-2일차). 단계 목록(순서대로)과 현재 단계를 가진다. 엔진 의존 없음.
    /// 게임은 일어난 일을 이벤트 이름으로 알린다(Report · SetState). 양은 <b>하루 누적</b>(tilled 1 → 4처럼 같은 날 이어 세고, 날이 바뀌면 비움)이라
    /// 단계보다 먼저 온 이벤트(장사 끝 직전의 레벨업)도 다음 단계가 들어서면 바로 완료로 잡힌다. 'auto' 단계는 들어서자마자 넘어간다.
    /// 날이 바뀌면 지난 날의 단계는 건너뛴다(순서가 어긋나도 그날 단계에 맞춘다). 저장은 현재 단계 id 하나.
    /// </summary>
    public sealed class TutorialProgress
    {
        /// <summary>저장 값 — 다 끝남.</summary>
        public const string DoneKey = "done";

        private readonly List<TutorialStep> steps;
        private readonly Dictionary<string, float> counts = new Dictionary<string, float>();
        private int index = -1;
        private TutorialStatus status = TutorialStatus.Off;
        private int day;

        public TutorialProgress(IEnumerable<TutorialStep> steps)
        {
            this.steps = new List<TutorialStep>(steps ?? Array.Empty<TutorialStep>());
        }

        public IReadOnlyList<TutorialStep> Steps => steps;
        public TutorialStatus Status => status;
        public bool IsRunning => status == TutorialStatus.Running;
        /// <summary>현재 단계 (진행 중이 아니면 null).</summary>
        public TutorialStep Current => status == TutorialStatus.Running && index >= 0 && index < steps.Count ? steps[index] : null;
        /// <summary>현재 단계 번호 (0부터, 진행 중이 아니면 -1).</summary>
        public int Index => Current != null ? index : -1;
        public int Day => day;

        /// <summary>단계가 바뀌었을 때 (새 단계 — 끝났으면 null).</summary>
        public event Action<TutorialStep> StepChanged;
        /// <summary>마지막 단계를 끝냈을 때.</summary>
        public event Action Finished;

        public static string KeyOf(string kind, string target) => string.IsNullOrEmpty(target) ? kind : kind + ":" + target;

        // ── 시작 · 날 ──

        /// <summary>새 게임: 첫 단계부터 (그날보다 앞선 단계는 건너뜀).</summary>
        public void Start(int startDay)
        {
            counts.Clear();
            day = startDay;
            if (steps.Count == 0)
            {
                SetDone(notify: true);
                return;
            }
            status = TutorialStatus.Running;
            index = 0;
            SkipPastDays(notify: false);
            if (status == TutorialStatus.Running)
                Enter();
        }

        /// <summary>끈다 (튜토리얼 없는 저장 · 디버그).</summary>
        public void TurnOff()
        {
            bool was = status == TutorialStatus.Running;
            status = TutorialStatus.Off;
            index = -1;
            counts.Clear();
            if (was)
                StepChanged?.Invoke(null);
        }

        /// <summary>
        /// 날이 바뀜 = 'next_day' 이벤트 + 하루 누적 비움: 지난 날 누적은 모두 지우고 next_day만 넣어 현재 단계를 본 뒤(다음 날 단계가 지난 날 양으로 넘어가지 않게)
        /// 다시 비우고, 지난 날의 단계는 건너뛴다. 남은 단계가 없으면 끝. 게임은 next_day를 따로 Report하지 않는다.
        /// </summary>
        public void BeginDay(int newDay)
        {
            day = newDay;
            counts.Clear();
            if (status != TutorialStatus.Running)
                return;
            counts[TutorialEvents.NextDay] = 1f;
            Check();
            counts.Clear();
            if (status == TutorialStatus.Running)
                SkipPastDays(notify: true);
        }

        private void SkipPastDays(bool notify)
        {
            int from = index;
            while (index < steps.Count && steps[index].Day < day)
                index++;
            if (index >= steps.Count)
            {
                SetDone(notify: true);
                return;
            }
            if (index != from)
            {
                counts.Clear();
                if (notify)
                    Enter();
            }
        }

        // ── 이벤트 ──

        /// <summary>일어난 일을 더한다 (하루 누적). 단계가 넘어갔으면 true.</summary>
        public bool Report(string kind, string target = null, float amount = 1f)
        {
            if (status != TutorialStatus.Running || string.IsNullOrEmpty(kind))
                return false;
            Add(kind, amount);
            if (!string.IsNullOrEmpty(target))
                Add(KeyOf(kind, target), amount);
            return Check();
        }

        /// <summary>지금 값으로 둔다 (누적이 아닌 것 — 가진 개수). 단계가 넘어갔으면 true.</summary>
        public bool SetState(string kind, string target, float value)
        {
            if (status != TutorialStatus.Running || string.IsNullOrEmpty(kind))
                return false;
            counts[KeyOf(kind, target)] = value;
            return Check();
        }

        /// <summary>그 종류(와 대상)의 하루 누적 값.</summary>
        public float CountOf(string kind, string target = null) => counts.TryGetValue(KeyOf(kind, target), out float v) ? v : 0f;

        /// <summary>현재 단계의 진행 (0 ~ 필요량).</summary>
        public float CurrentProgress => Current != null ? CountOf(Current.Condition.Kind, Current.Condition.Target) : 0f;

        /// <summary>디버그: 현재 단계를 건너뛴다.</summary>
        public void Skip()
        {
            if (status == TutorialStatus.Running)
                Advance();
        }

        private void Add(string key, float amount)
        {
            counts.TryGetValue(key, out float v);
            counts[key] = v + amount;
        }

        // 현재 단계 조건을 본다. 넘어가면 새 단계도 바로 본다 (auto · 같은 날 이미 쌓인 양)
        private bool Check()
        {
            bool advanced = false;
            while (Current != null)
            {
                var c = Current.Condition;
                bool met = c.IsAuto || CountOf(c.Kind, c.Target) >= c.Threshold - 1e-4f;
                if (!met)
                    break;
                Advance();
                advanced = true;
            }
            return advanced;
        }

        private void Advance()
        {
            index++;
            if (index >= steps.Count)
                SetDone(notify: true);
            else
                Enter();
        }

        private void Enter() => StepChanged?.Invoke(Current);

        private void SetDone(bool notify)
        {
            bool was = status == TutorialStatus.Running;
            status = TutorialStatus.Done;
            index = steps.Count;
            if (!notify)
                return;
            if (was)
                StepChanged?.Invoke(null);
            Finished?.Invoke();
        }

        /// <summary>
        /// 그날 단계들이 그 종류로 요구하는 가장 큰 양 (진행 중일 때만, 없으면 0) — 게임이 그날 그만큼은 생기게 보장한다
        /// (기획 튜토리얼 고정값: 2일차 발자국 2 · 레벨업 보장). 특정 단계가 아니라 조건 종류로 본다.
        /// </summary>
        public float RequiredFor(string kind, int onDay)
        {
            if (status != TutorialStatus.Running || string.IsNullOrEmpty(kind))
                return 0f;
            float max = 0f;
            foreach (var s in steps)
                if (s.Day == onDay && s.Condition.Kind == kind)
                    max = Math.Max(max, s.Condition.Threshold);
            return max;
        }

        // ── 잠금 ──

        /// <summary>현재 단계가 그 이름을 잠갔는지.</summary>
        public bool IsLocked(string lockName) => Current != null && Current.HasLock(lockName);

        /// <summary>그 이름이 잠겨 있고, targetId가 현재 단계의 완료 대상이 아닌지 (buy_other · other_skills).</summary>
        public bool IsLockedFor(string lockName, string targetId) =>
            IsLocked(lockName) && !string.Equals(Current.Condition.Target, targetId, StringComparison.Ordinal);

        // ── 저장 ──

        /// <summary>저장 값: 진행 중이면 단계 id, 끝났으면 "done", 안 켜졌으면 null.</summary>
        public string SaveKey => status == TutorialStatus.Running && Current != null ? Current.Id : status == TutorialStatus.Done ? DoneKey : null;

        /// <summary>
        /// 저장 값으로 되돌린다 (불러오기). null = 끔, "done" = 끝, 단계 id = 그 단계부터(하루 누적은 비움).
        /// 모르는 id면 끝낸 것으로 보고 false (로그용).
        /// </summary>
        public bool Restore(string key, int savedDay)
        {
            counts.Clear();
            day = savedDay;
            if (string.IsNullOrEmpty(key))
            {
                TurnOff();
                return true;
            }
            if (key == DoneKey)
            {
                status = TutorialStatus.Done;
                index = steps.Count;
                StepChanged?.Invoke(null);
                return true;
            }
            int at = steps.FindIndex(s => s.Id == key);
            if (at < 0)
            {
                status = TutorialStatus.Done;
                index = steps.Count;
                StepChanged?.Invoke(null);
                return false;
            }
            status = TutorialStatus.Running;
            index = at;
            SkipPastDays(notify: false);
            if (status == TutorialStatus.Running)
                Enter();
            return true;
        }
    }
}
