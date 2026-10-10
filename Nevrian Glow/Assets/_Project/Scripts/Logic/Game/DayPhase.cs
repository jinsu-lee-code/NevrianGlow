namespace NevrianGlowFarm.Logic.Game
{
    /// <summary>
    /// 하루 단계 (GDD 4-1). 진행 순서는 DayCycle.Order가 정한다 — enum 값 순서에 기대지 않는다.
    /// enum은 끝에만 추가한다 (저장 · 로그 호환).
    /// </summary>
    public enum DayPhase
    {
        Shop,
        Farming,
        CleanDisplay,
        Sales,
        Settlement,
        Skill,
    }

    public static class DayPhaseNames
    {
        /// <summary>화면 · 로그용 한국어 이름.</summary>
        public static string ToKorean(DayPhase phase)
        {
            switch (phase)
            {
                case DayPhase.Shop: return "상점";
                case DayPhase.Farming: return "농사";
                case DayPhase.CleanDisplay: return "청소·진열";
                case DayPhase.Sales: return "장사";
                case DayPhase.Settlement: return "정산";
                case DayPhase.Skill: return "스킬";
                default: return phase.ToString();
            }
        }
    }
}
