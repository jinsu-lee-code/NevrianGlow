namespace NevrianGlowFarm.Logic.Player
{
    /// <summary>핫바 선택 규칙. 칸 수는 고정하지 않는다 (데이터 · 스킬로 바뀜).</summary>
    public static class HotbarSelection
    {
        /// <summary>
        /// 숫자 키 번호(1부터)를 칸 번호(0부터)로 바꿔 선택을 시도한다.
        /// 없는 칸이거나 이미 선택된 칸이면 false (선택 변화 없음).
        /// </summary>
        public static bool TrySelectByKeyNumber(int keyNumber, int slotCount, ref int selectedIndex)
        {
            return TrySelect(keyNumber - 1, slotCount, ref selectedIndex);
        }

        public static bool TrySelect(int index, int slotCount, ref int selectedIndex)
        {
            if (index < 0 || index >= slotCount || index == selectedIndex)
                return false;
            selectedIndex = index;
            return true;
        }

        /// <summary>
        /// 휠 한 칸만큼 옮긴다. step &gt; 0이면 다음 칸, &lt; 0이면 이전 칸, 끝에서는 반대쪽 끝으로 돈다.
        /// step이 0이거나 칸이 1개 이하면 false.
        /// </summary>
        public static bool TryCycle(int step, int slotCount, ref int selectedIndex)
        {
            if (step == 0 || slotCount <= 1)
                return false;
            int direction = step > 0 ? 1 : -1;
            int next = ((selectedIndex + direction) % slotCount + slotCount) % slotCount;
            return TrySelect(next, slotCount, ref selectedIndex);
        }

        /// <summary>
        /// 칸 수가 바뀐 뒤 선택을 맞춘다. 선택한 칸이 남아 있으면 그대로, 없어졌으면 마지막 칸.
        /// 선택이 바뀌었으면 true.
        /// </summary>
        public static bool ClampToSlotCount(int slotCount, ref int selectedIndex)
        {
            int clamped = slotCount <= 0 ? 0 : (selectedIndex < 0 ? 0 : (selectedIndex >= slotCount ? slotCount - 1 : selectedIndex));
            if (clamped == selectedIndex)
                return false;
            selectedIndex = clamped;
            return true;
        }
    }
}
