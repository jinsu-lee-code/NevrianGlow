using System;
using System.Collections.Generic;

namespace NevrianGlowFarm.Logic.Game
{
    /// <summary>
    /// 씬 불러오기 진행률 (로딩 화면 막대, 최적화 2026-10-30). Unity 비동기 로드는 진행 값이 0 ~ 0.9까지 오르고 씬이 켜지면 1이 된다.
    /// </summary>
    public static class LoadingProgress
    {
        /// <summary>읽기가 끝나고 씬을 켜기 직전의 진행 값 (Unity AsyncOperation).</summary>
        public const float ActivationPoint = 0.9f;

        /// <summary>여러 씬의 진행 값을 0 ~ 1 하나로 — 이미 올라와 있던 씬은 끝난 것으로 센다. 씬이 하나도 없으면 1.</summary>
        public static float Combine(IReadOnlyList<float> raw, int alreadyLoaded)
        {
            int done = Math.Max(0, alreadyLoaded);
            int total = (raw != null ? raw.Count : 0) + done;
            if (total == 0)
                return 1f;
            float sum = done;
            if (raw != null)
                foreach (var p in raw)
                    sum += Math.Min(1f, Math.Max(0f, p) / ActivationPoint);
            return sum / total;
        }
    }
}
