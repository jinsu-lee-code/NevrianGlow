using NevrianGlowFarm.Logic;
using NevrianGlowFarm.Logic.Audio;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    public class SoundRulesTests
    {
        [Test]
        public void PickVariant_NeverRepeatsLast_WhenMoreThanOne()
        {
            var random = new SeededRandom(7);
            int last = -1;
            for (int i = 0; i < 200; i++)
            {
                int pick = SoundRules.PickVariant(3, last, random);
                Assert.That(pick, Is.InRange(0, 2));
                Assert.AreNotEqual(last, pick);
                last = pick;
            }
        }

        [Test]
        public void PickVariant_SingleAndEmpty()
        {
            var random = new SeededRandom(1);
            Assert.AreEqual(0, SoundRules.PickVariant(1, 0, random));
            Assert.AreEqual(-1, SoundRules.PickVariant(0, -1, random));
        }

        [Test]
        public void PickVariant_UsesEveryVariant()
        {
            var random = new SeededRandom(3);
            var seen = new bool[3];
            int last = -1;
            for (int i = 0; i < 60; i++)
                seen[last = SoundRules.PickVariant(3, last, random)] = true;
            Assert.IsTrue(seen[0] && seen[1] && seen[2]);
        }

        [Test]
        public void Pitch_StaysWithinJitter()
        {
            var random = new SeededRandom(11);
            for (int i = 0; i < 200; i++)
                Assert.That(SoundRules.Pitch(0.05f, random), Is.InRange(0.95f, 1.05f));
            Assert.AreEqual(1f, SoundRules.Pitch(0f, random));
        }

        [TestCase("sfx_water_pour_02", "sfx_water_pour")]
        [TestCase("sfx_mop_loop", "sfx_mop_loop")]
        [TestCase("sfx_checkout_coin", "sfx_checkout_coin")]
        [TestCase("sfx_ui_click", "sfx_ui_click")]
        [TestCase("decal_stain_1", "decal_stain_1")]
        public void IdFromFileName_StripsOnlyTwoDigitVariant(string file, string id)
        {
            Assert.AreEqual(id, SoundRules.IdFromFileName(file));
        }

        [Test]
        public void Cooldown_BlocksSameIdUntilElapsed_OtherIdsFree()
        {
            var cooldowns = new SoundCooldowns();
            Assert.IsTrue(cooldowns.TryUse("a", 0.0, 0.1));
            Assert.IsFalse(cooldowns.TryUse("a", 0.05, 0.1));
            Assert.IsTrue(cooldowns.TryUse("b", 0.05, 0.1));
            Assert.IsTrue(cooldowns.TryUse("a", 0.11, 0.1));
            Assert.IsTrue(cooldowns.TryUse("a", 0.11, 0));
        }
    }
}
