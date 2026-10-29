using NevrianGlowFarm.Logic.Game;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    /// <summary>로딩 화면 진행 막대 (최적화 2026-10-30).</summary>
    public class LoadingProgressTests
    {
        [Test]
        public void Combine_NormalizesUnityProgressAndCountsLoadedScenes()
        {
            // Unity 비동기 로드 0.9 = 읽기 끝(켜기 직전) → 그 씬은 다 된 것
            Assert.AreEqual(1f, LoadingProgress.Combine(new[] { 0.9f }, 0), 1e-5f);
            Assert.AreEqual(0.5f, LoadingProgress.Combine(new[] { 0.45f }, 0), 1e-5f);
            // 이미 올라와 있던 씬 1개 + 반쯤 읽은 씬 1개 = 3/4
            Assert.AreEqual(0.75f, LoadingProgress.Combine(new[] { 0.45f }, 1), 1e-5f);
            Assert.AreEqual(0f, LoadingProgress.Combine(new[] { 0f, 0f, 0f }, 0), 1e-5f);
        }

        [Test]
        public void Combine_NothingToLoad_IsDone_AndOutOfRangeIsClamped()
        {
            Assert.AreEqual(1f, LoadingProgress.Combine(new float[0], 0), "불러올 씬이 없으면 끝");
            Assert.AreEqual(1f, LoadingProgress.Combine(null, 2), "모두 이미 올라와 있음");
            Assert.AreEqual(1f, LoadingProgress.Combine(new[] { 1f }, 0), "켜진 씬(1)도 1을 넘지 않음");
            Assert.AreEqual(0f, LoadingProgress.Combine(new[] { -1f }, 0), 1e-5f);
        }
    }
}
