using System.Collections.Generic;

namespace NevrianGlowFarm.Logic.Sales
{
    /// <summary>
    /// 계산대 줄 (노션 GDD 12.4 SalesSimulator 6): 들어온 순서대로 줄 칸 0(계산대 앞) · 1 · 2 …에 선다.
    /// 앞 손님이 계산을 마치고 나가면 뒤 손님이 한 칸씩 앞으로 온다. 엔진 의존 없음 — 손님은 번호(int)로 구분.
    /// </summary>
    public sealed class CheckoutLine
    {
        private readonly List<int> line = new List<int>();

        public int Count => line.Count;

        /// <summary>줄 맨 뒤에 선다. 이미 서 있으면 그 자리. 선 칸 번호를 돌려준다.</summary>
        public int Join(int customer)
        {
            int index = line.IndexOf(customer);
            if (index >= 0)
                return index;
            line.Add(customer);
            return line.Count - 1;
        }

        /// <summary>줄 칸 번호 (0 = 계산 차례). 줄에 없으면 −1.</summary>
        public int PositionOf(int customer) => line.IndexOf(customer);

        /// <summary>계산 차례인지.</summary>
        public bool IsFront(int customer) => line.Count > 0 && line[0] == customer;

        /// <summary>줄에서 빠진다 (계산을 마쳤거나 장사가 끝남). 뒤 손님들이 한 칸씩 앞으로.</summary>
        public bool Leave(int customer) => line.Remove(customer);

        public void Clear() => line.Clear();
    }
}
