using System;
using System.Collections.Generic;

namespace NevrianGlowFarm.Logic.Logging
{
    /// <summary>같은 메시지+스택을 하나로 묶은 로그 한 건. 상황(context)은 처음 발생한 시점 기준.</summary>
    public sealed class LogEntry
    {
        public LogSeverity Severity { get; }
        public string Message { get; }
        public string StackTrace { get; }
        public LogContext Context { get; }
        public DateTime FirstTime { get; }
        public DateTime LastTime { get; private set; }
        public int Count { get; private set; }

        internal LogEntry(LogSeverity severity, string message, string stackTrace, DateTime time, LogContext context)
        {
            Severity = severity;
            Message = message;
            StackTrace = stackTrace;
            Context = context;
            FirstTime = time;
            LastTime = time;
            Count = 1;
        }

        internal void AddOccurrence(DateTime time)
        {
            Count++;
            LastTime = time;
        }
    }

    public enum LogRecordResult
    {
        /// <summary>처음 보는 로그 — 새 줄이 생겼다.</summary>
        New,
        /// <summary>이미 있는 로그 — 횟수만 늘었다.</summary>
        Repeated,
        /// <summary>최대 개수를 넘어 버렸다.</summary>
        Dropped,
    }

    /// <summary>
    /// 세션 동안 받은 로그를 메시지+스택 기준으로 묶는다. 스레드 안전하지 않으므로 호출하는 쪽이 잠근다.
    /// </summary>
    public sealed class LogAggregator
    {
        private readonly int maxEntries;
        private readonly List<LogEntry> entries = new List<LogEntry>();
        private readonly Dictionary<string, LogEntry> entriesByKey = new Dictionary<string, LogEntry>();

        public LogAggregator(int maxEntries)
        {
            if (maxEntries < 1)
                throw new ArgumentOutOfRangeException(nameof(maxEntries), "1 이상이어야 한다.");
            this.maxEntries = maxEntries;
        }

        /// <summary>처음 발생한 순서대로 정렬된 로그.</summary>
        public IReadOnlyList<LogEntry> Entries => entries;

        /// <summary>최대 개수를 넘어 기록하지 못한 발생 횟수.</summary>
        public int DroppedCount { get; private set; }

        public LogRecordResult Record(LogSeverity severity, string message, string stackTrace, DateTime time, LogContext context)
        {
            message = message ?? string.Empty;
            stackTrace = stackTrace ?? string.Empty;
            string key = message + "\0" + stackTrace;

            if (entriesByKey.TryGetValue(key, out var entry))
            {
                entry.AddOccurrence(time);
                return LogRecordResult.Repeated;
            }

            if (entries.Count >= maxEntries)
            {
                DroppedCount++;
                return LogRecordResult.Dropped;
            }

            entry = new LogEntry(severity, message, stackTrace, time, context);
            entries.Add(entry);
            entriesByKey.Add(key, entry);
            return LogRecordResult.New;
        }
    }
}
