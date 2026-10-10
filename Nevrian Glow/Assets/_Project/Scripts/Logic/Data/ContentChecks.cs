using System.Collections.Generic;

namespace NevrianGlowFarm.Logic.Data
{
    /// <summary>콘텐츠 에셋 검사 결과 한 줄.</summary>
    public readonly struct ContentProblem
    {
        public string Source { get; }
        public string Message { get; }

        public ContentProblem(string source, string message)
        {
            Source = source;
            Message = message;
        }

        public override string ToString() => $"{Source}: {Message}";
    }

    /// <summary>
    /// 콘텐츠 id 규칙 검사 (CLAUDE 핵심 규칙 5: 모든 콘텐츠는 바뀌지 않는 문자열 id).
    /// 빈 id, 중복 id, 소문자 · 숫자 · 밑줄 외 글자를 찾는다. 엔진 의존 없음.
    /// </summary>
    public static class ContentChecks
    {
        /// <summary>(id, 출처) 목록을 검사한다. 출처는 보통 에셋 경로.</summary>
        public static List<ContentProblem> CheckIds(IEnumerable<(string Id, string Source)> entries)
        {
            var problems = new List<ContentProblem>();
            var firstSource = new Dictionary<string, string>();

            foreach (var (id, source) in entries)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    problems.Add(new ContentProblem(source, "id가 비어 있음"));
                    continue;
                }
                if (!IsValidId(id))
                    problems.Add(new ContentProblem(source, $"id '{id}'에 소문자 · 숫자 · 밑줄(_) 외 글자가 있음"));
                if (firstSource.TryGetValue(id, out var first))
                    problems.Add(new ContentProblem(source, $"id '{id}'가 {first}와 겹침"));
                else
                    firstSource[id] = source;
            }
            return problems;
        }

        /// <summary>소문자 · 숫자 · 밑줄만 (에셋 이름 규칙 `분류_이름_변형`).</summary>
        public static bool IsValidId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            foreach (char c in id)
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_'))
                    return false;
            return true;
        }
    }
}
