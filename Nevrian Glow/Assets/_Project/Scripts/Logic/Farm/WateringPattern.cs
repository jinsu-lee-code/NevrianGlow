using System.Collections.Generic;

namespace NevrianGlowFarm.Logic.Farm
{
    /// <summary>
    /// 물뿌리개 범위 (GDD 4-2, BALANCE 2-11): 1칸 / 옆 칸 2칸 / 같은 밭 4칸 / 같은 줄 이웃 밭 8칸.
    /// 칸 번호 규칙(FarmLayout): 같은 밭 짝 = i^1, 같은 밭 = (i &amp; ~3)+k, 같은 줄 이웃 밭 = (i &amp; ~7)+k
    /// → 칸 수가 2의 거듭제곱 n이면 (i &amp; ~(n−1)) + k (k = 0 … n−1). 2의 거듭제곱이 아니면 그 아래 거듭제곱으로 본다.
    /// 열린 칸 수를 넘는 번호는 뺀다. 엔진 의존 없음.
    /// </summary>
    public static class WateringPattern
    {
        public static List<int> Cells(int index, int cellCount, int totalCells)
        {
            var cells = new List<int>();
            if (index < 0 || index >= totalCells)
                return cells;
            int block = LargestPowerOfTwo(cellCount < 1 ? 1 : cellCount);
            int start = index & ~(block - 1);
            for (int k = 0; k < block; k++)
            {
                int i = start + k;
                if (i < totalCells)
                    cells.Add(i);
            }
            return cells;
        }

        private static int LargestPowerOfTwo(int n)
        {
            int p = 1;
            while (p * 2 <= n)
                p *= 2;
            return p;
        }
    }
}
