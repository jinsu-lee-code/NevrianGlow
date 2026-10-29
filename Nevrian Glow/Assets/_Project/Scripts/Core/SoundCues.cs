namespace NevrianGlowFarm.Core
{
    /// <summary>
    /// 코드가 부르는 소리 id 목록 (asset_placement 6번 · sound.md). 파일 이름 = id (+ 변형 번호 _01~03).
    /// 이름을 바꿀 때는 여기 한 곳만 고친다. 파일이 아직 없으면 AudioManager가 무음으로 넘어간다.
    /// </summary>
    public static class SoundCues
    {
        // 핵심 행동 (2D)
        public const string SeedPlant = "sfx_seed_plant";
        public const string WaterPour = "sfx_water_pour";
        public const string HarvestPull = "sfx_harvest_pull";
        public const string FertilizerSparkle = "sfx_fertilizer_sparkle";
        public const string TrashPickup = "sfx_trash_pickup";
        public const string TrashDrop = "sfx_trash_drop";
        public const string StainClear = "sfx_stain_clear";
        public const string MopLoop = "sfx_mop_loop";

        // 알림 (2D)
        public const string CropMature = "sfx_crop_mature";
        public const string DayStart = "sfx_day_start";

        // 플레이어 발소리 (2D, 사운드 납품 10.23 — 지역 바닥은 ZoneProfileData.footstepId, 밭 · 길 위는 흙)
        public const string StepSoil = "sfx_step_soil";

        // 손님 · 가게 (3D)
        public const string CustomerEnter = "sfx_customer_enter";
        public const string CustomerExit = "sfx_customer_exit";
        public const string CustomerStep = "sfx_customer_step";
        public const string BasketPick = "sfx_basket_pick";
        public const string BasketPut = "sfx_basket_put";
        public const string CustomerHappy = "sfx_customer_happy";
        public const string CheckoutCoin = "sfx_checkout_coin";
        public const string GiantMerge = "sfx_giant_merge";

        // 진열 · 창고 (2D)
        public const string ShelfPlace = "sfx_shelf_place";
        public const string ShelfTake = "sfx_shelf_take";
        public const string StorageOpen = "sfx_storage_open";
        public const string StoragePut = "sfx_storage_put";

        // UI (2D, UI 버스)
        public const string UiClick = "sfx_ui_click";
        public const string UiBuySuccess = "sfx_ui_buy_success";
        public const string UiBuyFail = "sfx_ui_buy_fail";
        public const string UiSkillLearn = "sfx_ui_skill_learn";
        public const string UiLevelUp = "sfx_ui_levelup";
        public const string UiFade = "sfx_ui_fade";
        public const string UiToolSwitch = "sfx_ui_tool_switch";

        // 배경음 (구역 배경음은 ZoneProfileData)
        public const string TitleMusic = "bgm_title_loop";
    }
}
