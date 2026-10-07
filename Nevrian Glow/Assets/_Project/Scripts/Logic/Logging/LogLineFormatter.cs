using System.Globalization;
using System.Text;

namespace NevrianGlowFarm.Logic.Logging
{
    /// <summary>
    /// 세션 로그 파일 형식. 로그 한 건 = 한 줄:
    /// 시각 | 유형 | 일차 | 단계 | 구역 | 빌드 버전 | 횟수 | 메시지 | 스택
    /// </summary>
    public static class LogLineFormatter
    {
        public const string Separator = " | ";
        /// <summary>메시지 · 스택 안의 줄바꿈을 대신하는 표시 (한 건을 한 줄로 유지).</summary>
        public const string NewlineMark = " ↵ ";
        public const string Empty = "-";

        private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";
        private const string ShortTimeFormat = "HH:mm:ss";

        public static string FormatLine(LogEntry entry)
        {
            var ctx = entry.Context;
            string count = entry.Count > 1
                ? $"x{entry.Count} (last {entry.LastTime.ToString(ShortTimeFormat, CultureInfo.InvariantCulture)})"
                : "x1";

            return string.Join(Separator,
                entry.FirstTime.ToString(TimeFormat, CultureInfo.InvariantCulture),
                entry.Severity.ToString(),
                "Day " + ctx.Day.ToString(CultureInfo.InvariantCulture),
                OrEmpty(ctx.Phase),
                OrEmpty(ctx.Zone),
                "v" + OrEmpty(ctx.BuildVersion),
                count,
                Flatten(entry.Message),
                Flatten(entry.StackTrace));
        }

        /// <summary>머리 줄 + 로그 줄(처음 발생 순) + 버린 건수 줄.</summary>
        public static string FormatFile(string header, LogAggregator aggregator)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(header))
                sb.Append("# ").Append(Flatten(header)).Append('\n');

            foreach (var entry in aggregator.Entries)
                sb.Append(FormatLine(entry)).Append('\n');

            if (aggregator.DroppedCount > 0)
                sb.Append("# 최대 개수 초과로 기록하지 못한 로그: ")
                  .Append(aggregator.DroppedCount.ToString(CultureInfo.InvariantCulture))
                  .Append("회\n");

            return sb.ToString();
        }

        public static string Flatten(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Empty;
            return text.TrimEnd('\r', '\n')
                       .Replace("\r\n", "\n")
                       .Replace("\r", "\n")
                       .Replace("\n", NewlineMark);
        }

        private static string OrEmpty(string text) => string.IsNullOrEmpty(text) ? Empty : text;
    }
}
