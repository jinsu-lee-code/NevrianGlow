using NevrianGlowFarm.Logic.Game;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    public class ZoneRulesTests
    {
        // BALANCE 2-10 임시: 농장 = 농사 단계 · 하루 시작, 가게 = 청소·진열 · 장사 · 정산
        private static readonly ZoneRule[] Zones =
        {
            new ZoneRule("zone_farm", new[] { DayPhase.Farming }, true),
            new ZoneRule("zone_shop", new[] { DayPhase.CleanDisplay, DayPhase.Sales, DayPhase.Settlement }, false),
        };

        private static PhaseChange Change(int fromDay, DayPhase from, int toDay, DayPhase to) => new PhaseChange(fromDay, from, toDay, to);

        [Test]
        public void EndFarming_GoesToShop()
        {
            Assert.AreEqual("zone_shop", ZoneRules.TargetZone(Change(1, DayPhase.Farming, 1, DayPhase.CleanDisplay), "zone_farm", Zones));
        }

        [Test]
        public void NextDay_FromShop_GoesToFarm()
        {
            Assert.AreEqual("zone_farm", ZoneRules.TargetZone(Change(1, DayPhase.Skill, 2, DayPhase.Shop), "zone_shop", Zones));
            Assert.AreEqual("zone_farm", ZoneRules.TargetZone(Change(1, DayPhase.Settlement, 2, DayPhase.Shop), "zone_shop", Zones), "다음 날 건너뛰기도 농장");
        }

        [Test]
        public void PhasesWithoutZone_StayWhereYouAre()
        {
            Assert.AreEqual("zone_shop", ZoneRules.TargetZone(Change(1, DayPhase.Settlement, 1, DayPhase.Skill), "zone_shop", Zones), "스킬 화면은 그 자리");
            Assert.AreEqual("zone_farm", ZoneRules.TargetZone(Change(2, DayPhase.Shop, 2, DayPhase.Shop), "zone_farm", Zones), "상점 화면은 그 자리");
        }

        [Test]
        public void SamePhaseZone_NoMove()
        {
            Assert.AreEqual("zone_shop", ZoneRules.TargetZone(Change(1, DayPhase.CleanDisplay, 1, DayPhase.Sales), "zone_shop", Zones));
        }

        [Test]
        public void DayStartZone_IsFarm_OrFirstWhenNoneMarked()
        {
            Assert.AreEqual("zone_farm", ZoneRules.DayStartZone(Zones));
            var unmarked = new[] { new ZoneRule("zone_a", new[] { DayPhase.Sales }, false) };
            Assert.AreEqual("zone_a", ZoneRules.DayStartZone(unmarked));
            Assert.IsNull(ZoneRules.ZoneForPhase(DayPhase.Shop, Zones));
        }
    }
}
