using System;

namespace NevrianGlowFarm.Logic
{
    /// <summary>
    /// 난수 (CLAUDE 코딩 규칙: 인터페이스로 주입하고 용도별로 분리 — 거대 작물 · 손님 구매 · 쓰레기 배치).
    /// 같은 시드면 같은 결과 → 테스트 · 측정 봇 · 버그 재현에 쓴다.
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>0 이상 1 미만.</summary>
        double NextDouble();

        /// <summary>min 이상 maxExclusive 미만.</summary>
        int NextInt(int min, int maxExclusive);
    }

    /// <summary>System.Random 기반 시드 난수.</summary>
    public sealed class SeededRandom : IRandomSource
    {
        private readonly Random random;

        public SeededRandom(int seed) => random = new Random(seed);

        public double NextDouble() => random.NextDouble();
        public int NextInt(int min, int maxExclusive) => random.Next(min, maxExclusive);

        /// <summary>용도 · 일차별 시드 (용도마다 흐름이 섞이지 않게).</summary>
        public static int SeedFor(int baseSeed, int purpose, int day) => unchecked((baseSeed * 397) ^ (purpose * 7919) ^ (day * 104729));
    }
}
