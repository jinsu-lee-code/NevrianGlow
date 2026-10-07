namespace NevrianGlowFarm.Logic.Logging
{
    /// <summary>로그가 발생한 시점의 게임 상황 (일차 · 단계 · 구역 · 빌드 버전).</summary>
    public readonly struct LogContext
    {
        public int Day { get; }
        public string Phase { get; }
        public string Zone { get; }
        public string BuildVersion { get; }

        public LogContext(int day, string phase, string zone, string buildVersion)
        {
            Day = day;
            Phase = phase;
            Zone = zone;
            BuildVersion = buildVersion;
        }
    }
}
