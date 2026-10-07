namespace NevrianGlowFarm.Logic.Logging
{
    /// <summary>수집 대상 로그 유형. 엔진의 LogType과 분리해 Logic이 엔진에 의존하지 않게 한다. 끝에만 추가한다.</summary>
    public enum LogSeverity
    {
        Warning,
        Assert,
        Error,
        Exception,
    }
}
