using System;
using System.Collections.Generic;

namespace NevrianGlowFarm.Logic.Audio
{
    /// <summary>
    /// 소리 재생 규칙 (TASKS 5-5, sound.md 3번): 변형 무작위(같은 변형 연속 금지) · 음높이 무작위 · 같은 소리 쿨다운 · 파일 이름 → 소리 id.
    /// </summary>
    public static class SoundRules
    {
        /// <summary>
        /// 변형 하나를 고른다. 변형이 2개 이상이면 바로 전과 같은 것은 고르지 않는다.
        /// count가 0이면 -1.
        /// </summary>
        public static int PickVariant(int count, int lastIndex, IRandomSource random)
        {
            if (count <= 0)
                return -1;
            if (count == 1)
                return 0;
            if (lastIndex < 0 || lastIndex >= count)
                return random.NextInt(0, count);
            int pick = random.NextInt(0, count - 1);
            return pick >= lastIndex ? pick + 1 : pick;
        }

        /// <summary>1 ± jitter 사이의 음높이 배율 (jitter 0.05 = ±5%).</summary>
        public static float Pitch(float jitter, IRandomSource random)
        {
            if (jitter <= 0f)
                return 1f;
            return 1f + (float)((random.NextDouble() * 2.0 - 1.0) * jitter);
        }

        /// <summary>
        /// 파일 이름에서 소리 id를 얻는다. 끝의 변형 번호 `_01` ~ `_99`만 뗀다 (`sfx_water_pour_02` → `sfx_water_pour`).
        /// `_loop` 등 다른 꼬리는 그대로 둔다.
        /// </summary>
        public static string IdFromFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return fileName;
            int n = fileName.Length;
            if (n > 3 && fileName[n - 3] == '_' && char.IsDigit(fileName[n - 2]) && char.IsDigit(fileName[n - 1]))
                return fileName.Substring(0, n - 3);
            return fileName;
        }
    }

    /// <summary>같은 소리가 너무 빨리 겹쳐 나지 않게 막는다 (소리 id별 마지막 재생 시각).</summary>
    public sealed class SoundCooldowns
    {
        private readonly Dictionary<string, double> lastPlayed = new Dictionary<string, double>();

        /// <summary>쿨다운이 지났으면 지금 시각을 기록하고 true.</summary>
        public bool TryUse(string id, double now, double cooldown)
        {
            if (cooldown > 0 && lastPlayed.TryGetValue(id, out double last) && now - last < cooldown)
                return false;
            lastPlayed[id] = now;
            return true;
        }

        public void Clear() => lastPlayed.Clear();
    }
}
