namespace NevrianGlowFarm.Logic.Player
{
    /// <summary>핫바 선택 규칙. 칸 수는 고정하지 않는다 (도구 목록 길이).</summary>
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
    }
}
