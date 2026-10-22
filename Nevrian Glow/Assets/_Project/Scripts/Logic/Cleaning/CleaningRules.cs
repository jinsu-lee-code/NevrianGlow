using System;
using System.Collections.Generic;

namespace NevrianGlowFarm.Logic.Cleaning
{
    /// <summary>바닥 위 한 점 (x, z).</summary>
    public readonly struct FloorPoint
    {
        public double X { get; }
        public double Z { get; }

        public FloorPoint(double x, double z)
        {
            X = x;
            Z = z;
        }

        public double DistanceTo(FloorPoint other)
        {
            double dx = X - other.X, dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }
    }

    /// <summary>청소 규칙 (GDD 4-4). 하루에 생기는 양은 가게 정리 v2(TidyRules, P-3). 엔진 의존 없음.</summary>
    public static class CleaningRules
    {
        /// <summary>
        /// 청결도 = 1 − (가게 바닥 쓰레기 수 + 남은 얼룩 양 합) ÷ (그날 처음 쓰레기 수 + 처음 얼룩 수), 0~1.
        /// 처음 개수가 0이면 1 (치울 것이 없음, 임시 규칙 2026-10-10).
        /// </summary>
        public static double Cleanliness(int initialTrash, int initialStains, int floorTrash, double stainAmountSum)
        {
            int total = Math.Max(0, initialTrash) + Math.Max(0, initialStains);
            if (total == 0)
                return 1.0;
            double dirty = Math.Max(0, floorTrash) + Math.Max(0.0, stainAmountSum);
            double value = 1.0 - dirty / total;
            return value < 0 ? 0 : value > 1 ? 1 : value;
        }

        /// <summary>
        /// 포아송 디스크 방식(다트 던지기) 배치: 사각형 안에 count개를, 서로 · 이미 있는 점(바닥 아이템 등)과 minSpacing 이상 떨어지게 놓는다.
        /// 한 개마다 attempts번 시도해 자리를 못 찾으면 그 개는 놓지 않는다(돌려준 개수가 count보다 적을 수 있다).
        /// allowed가 false인 곳(금지 구역)에는 놓지 않는다.
        /// </summary>
        public static List<FloorPoint> Scatter(int count, double minX, double minZ, double maxX, double maxZ, double minSpacing, int attempts,
            IRandomSource random, Func<FloorPoint, bool> allowed = null, IReadOnlyList<FloorPoint> occupied = null)
        {
            var placed = new List<FloorPoint>(Math.Max(0, count));
            for (int n = 0; n < count; n++)
            {
                for (int a = 0; a < Math.Max(1, attempts); a++)
                {
                    var p = new FloorPoint(minX + random.NextDouble() * (maxX - minX), minZ + random.NextDouble() * (maxZ - minZ));
                    if (allowed != null && !allowed(p))
                        continue;
                    if (TooClose(p, placed, minSpacing) || (occupied != null && TooClose(p, occupied, minSpacing)))
                        continue;
                    placed.Add(p);
                    break;
                }
            }
            return placed;
        }

        private static bool TooClose(FloorPoint p, IReadOnlyList<FloorPoint> others, double minSpacing)
        {
            foreach (var o in others)
                if (p.DistanceTo(o) < minSpacing)
                    return true;
            return false;
        }

        /// <summary>대걸레 한 번(한 프레임): 얼룩 양을 amount만큼 줄인다. 0 아래로는 안 내려간다.</summary>
        public static double Wipe(double stainAmount, double amount) => Math.Max(0.0, stainAmount - Math.Max(0.0, amount));
    }
}
