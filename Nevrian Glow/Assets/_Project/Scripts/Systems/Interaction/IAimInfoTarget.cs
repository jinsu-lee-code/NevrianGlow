namespace NevrianGlowFarm.Systems.Interaction
{
    /// <summary>
    /// 조준하면 행동 프롬프트 아래에 정보 한 줄을 보여 주는 대상 (키 없음) — 자라는 작물의 성장 정보 (수정사항 10/28 "성장 중인 식물에 호버링하면 성장 정보").
    /// </summary>
    public interface IAimInfoTarget
    {
        /// <summary>정보 한 줄. 없으면 null 또는 빈 문자열.</summary>
        string GetAimInfo();
    }
}
