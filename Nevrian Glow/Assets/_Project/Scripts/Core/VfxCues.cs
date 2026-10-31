namespace NevrianGlowFarm.Core
{
    /// <summary>
    /// 코드가 부르는 VFX id 목록 (TASKS 5-8 · 아트 요청서 H). 이름을 바꿀 때는 여기 한 곳만 고친다.
    /// 라이브러리에 없으면 VfxManager가 아무것도 내지 않고 넘어간다.
    /// </summary>
    public static class VfxCues
    {
        // 1회
        public const string Sparkle = "vfx_sparkle";        // 수확 · 진열
        public const string LightPuff = "vfx_light_puff";   // 쓰레기를 쓰레기통에
        public const string RingGold = "vfx_ring_gold";     // 레벨업
        public const string MergeBurst = "vfx_merge_burst"; // 거대 작물 (4-4, P1)
        public const string SeedDust = "vfx_seed_dust";     // 씨앗 뿌리기 가루 0.4초 (기획서 v2.0 — 개발 임시 입자, 아트 교체 대기)

        // 반복 (켜고 끈다)
        public const string WaterStream = "vfx_water_drop"; // 물뿌리개 물줄기 — 루트 +Z = 주둥이 방향
        public const string BubbleFoam = "vfx_bubble_foam"; // 대걸레질 거품
        public const string GlowPulse = "vfx_glow_pulse";   // 다 자란 작물 위
    }
}
