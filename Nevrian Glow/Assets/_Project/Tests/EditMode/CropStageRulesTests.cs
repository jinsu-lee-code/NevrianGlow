using NevrianGlowFarm.Logic.Farm;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    public class CropStageRulesTests
    {
        [Test]
        public void NoStages_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, CropStageRules.StageIndex(0, 0.5f, false));
        }

        [Test]
        public void JustPlanted_IsFirstStage_Ready_IsLast()
        {
            Assert.AreEqual(0, CropStageRules.StageIndex(3, 0f, false));
            Assert.AreEqual(2, CropStageRules.StageIndex(3, 1f, true));
            Assert.AreEqual(3, CropStageRules.StageIndex(4, 0.4f, true), "수확 가능이면 성장 비율과 관계없이 마지막");
        }

        [Test]
        public void Growing_SpreadsOverMiddleStages()
        {
            // 등불 열매: 둔덕 · 묘목 · 줄기 · 완성 (성장 8일)
            Assert.AreEqual(1, CropStageRules.StageIndex(4, 1f / 8f, false));
            Assert.AreEqual(1, CropStageRules.StageIndex(4, 3f / 8f, false));
            Assert.AreEqual(2, CropStageRules.StageIndex(4, 4f / 8f, false));
            Assert.AreEqual(2, CropStageRules.StageIndex(4, 7f / 8f, false));
        }

        [Test]
        public void GrowthAtOneButNotReady_StaysBeforeLast()
        {
            // 물이 모자라 성장이 멈춘 경우 등 — 수확 가능 전에는 마지막 단계를 보이지 않는다
            Assert.AreEqual(1, CropStageRules.StageIndex(3, 1f, false));
        }

        [Test]
        public void TwoStages_GrowingShowsFirst()
        {
            Assert.AreEqual(0, CropStageRules.StageIndex(2, 0.6f, false));
            Assert.AreEqual(1, CropStageRules.StageIndex(2, 0.6f, true));
        }

        [Test]
        public void Regrowing_KeepsMatureStage()
        {
            // 등불 열매(0 둔덕 · 1 묘목 · 2 줄기 · 3 완성): 한 번 거둔 뒤 다시 열리는 중이면 나무(완성)를 유지 — 열매만 모델 애니메이션으로
            Assert.AreEqual(3, CropStageRules.StageIndex(4, 0f, false, regrowing: true));
            Assert.AreEqual(3, CropStageRules.StageIndex(4, 0.5f, false, regrowing: true));
            Assert.AreEqual(0, CropStageRules.StageIndex(4, 0f, false, regrowing: false), "처음 자랄 때는 그대로");
            Assert.AreEqual(-1, CropStageRules.StageIndex(0, 0f, false, regrowing: true));
        }
    }
}
