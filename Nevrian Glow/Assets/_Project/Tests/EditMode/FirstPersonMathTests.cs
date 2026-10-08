using NevrianGlowFarm.Logic.Player;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    public class FirstPersonMathTests
    {
        private const float Sensitivity = 0.14f;
        private const float PitchLimit = 83f;

        [Test]
        public void ApplyLook_MouseRightAndUp_TurnsRightAndLooksUp()
        {
            float yaw = 0f, pitch = 0f;

            FirstPersonMath.ApplyLook(ref yaw, ref pitch, 100f, 50f, Sensitivity, PitchLimit);

            Assert.AreEqual(14f, yaw, 1e-4f);
            Assert.AreEqual(-7f, pitch, 1e-4f);
        }

        [Test]
        public void ApplyLook_PitchIsClampedToLimit()
        {
            float yaw = 0f, pitch = 0f;

            FirstPersonMath.ApplyLook(ref yaw, ref pitch, 0f, 10000f, Sensitivity, PitchLimit);
            Assert.AreEqual(-PitchLimit, pitch, 1e-4f);

            FirstPersonMath.ApplyLook(ref yaw, ref pitch, 0f, -20000f, Sensitivity, PitchLimit);
            Assert.AreEqual(PitchLimit, pitch, 1e-4f);
        }

        [Test]
        public void ApplyLook_YawWrapsInto0To360()
        {
            float yaw = 350f, pitch = 0f;
            FirstPersonMath.ApplyLook(ref yaw, ref pitch, 100f, 0f, Sensitivity, PitchLimit);
            Assert.AreEqual(4f, yaw, 1e-3f);

            yaw = 5f;
            FirstPersonMath.ApplyLook(ref yaw, ref pitch, -100f, 0f, Sensitivity, PitchLimit);
            Assert.AreEqual(351f, yaw, 1e-3f);
        }

        [Test]
        public void BobPhase_AdvancesWhileMoving_AndResetsWhenStopped()
        {
            float phase = FirstPersonMath.AdvanceBobPhase(0f, 0.8f, 1f, 0.5f, true);
            Assert.AreEqual(0.4f, phase, 1e-5f);

            phase = FirstPersonMath.AdvanceBobPhase(phase, 0.8f, 1f, 1f, true);
            Assert.AreEqual(0.2f, phase, 1e-5f); // 1.2 → 0.2 순환

            phase = FirstPersonMath.AdvanceBobPhase(phase, 0.8f, 1f, 0.1f, false);
            Assert.AreEqual(0f, phase);
        }

        [Test]
        public void BobOffset_PeaksAtQuarterPhase()
        {
            Assert.AreEqual(0f, FirstPersonMath.BobOffset(0f, 0.008f), 1e-6f);
            Assert.AreEqual(0.008f, FirstPersonMath.BobOffset(0.25f, 0.008f), 1e-6f);
            Assert.AreEqual(-0.008f, FirstPersonMath.BobOffset(0.75f, 0.008f), 1e-6f);
        }
    }
}
