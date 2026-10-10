using System.Collections.Generic;

namespace NevrianGlowFarm.Logic.Game
{
    /// <summary>지역 하나의 이동 규칙 (ZoneProfile 데이터에서 옮겨 온다).</summary>
    public readonly struct ZoneRule
    {
        public string ZoneId { get; }
        /// <summary>이 단계일 때는 플레이어가 이 지역에 있어야 한다.</summary>
        public IReadOnlyList<DayPhase> Phases { get; }
        /// <summary>날이 바뀌면 이 지역에서 시작한다 (농장).</summary>
        public bool IsDayStart { get; }

        public ZoneRule(string zoneId, IReadOnlyList<DayPhase> phases, bool isDayStart)
        {
            ZoneId = zoneId;
            Phases = phases ?? new DayPhase[0];
            IsDayStart = isDayStart;
        }
    }

    /// <summary>
    /// 단계가 바뀔 때 플레이어가 있어야 할 지역 (GDD 4-7: 농장 → 가게는 농사를 끝낼 때, 가게 → 농장은 정산의 다음 날).
    /// 지역 종류는 데이터(ZoneRule 목록)로 받는다 — 지역 추가는 데이터만으로. 엔진 의존 없음.
    /// </summary>
    public static class ZoneRules
    {
        /// <summary>
        /// 날이 바뀌었으면 하루 시작 지역, 아니면 새 단계를 맡은 지역, 맡은 지역이 없는 단계(상점 · 스킬 화면)면 지금 지역 그대로.
        /// </summary>
        public static string TargetZone(PhaseChange change, string currentZoneId, IReadOnlyList<ZoneRule> zones)
        {
            if (change.DayChanged)
            {
                foreach (var z in zones)
                    if (z.IsDayStart)
                        return z.ZoneId;
                return currentZoneId;
            }
            return ZoneForPhase(change.Phase, zones) ?? currentZoneId;
        }

        /// <summary>이 단계를 맡은 지역. 없으면 null.</summary>
        public static string ZoneForPhase(DayPhase phase, IReadOnlyList<ZoneRule> zones)
        {
            foreach (var z in zones)
                foreach (var p in z.Phases)
                    if (p == phase)
                        return z.ZoneId;
            return null;
        }

        /// <summary>하루 시작 지역 (새 게임 · 불러오기). 없으면 첫 지역.</summary>
        public static string DayStartZone(IReadOnlyList<ZoneRule> zones)
        {
            foreach (var z in zones)
                if (z.IsDayStart)
                    return z.ZoneId;
            return zones.Count > 0 ? zones[0].ZoneId : null;
        }
    }
}
