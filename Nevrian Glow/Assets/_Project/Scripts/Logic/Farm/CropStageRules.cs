namespace NevrianGlowFarm.Logic.Farm
{
    /// <summary>
    /// 작물 겉모습 단계 고르기 (TASKS 5-2, asset_placement 2번). 단계 목록 = CropData의 단계 프리팹 칸:
    /// [0] 심은 직후(공용 흙 둔덕) · [1 ~ 끝-1] 자라는 중 · [끝] 다 자람(수확 가능). 단계 수는 데이터 길이를 따른다.
    /// </summary>
    public static class CropStageRules
    {
        /// <summary>
        /// 보여 줄 단계 번호. 단계가 없으면 -1.
        /// growth = 성장 비율(0 = 막 심음, 1 = 다 자람), ready = 수확 가능.
        /// </summary>
        public static int StageIndex(int stageCount, float growth, bool ready) => StageIndex(stageCount, growth, ready, false);

        /// <summary>
        /// regrowing = 재수확 작물을 한 번 이상 거둔 뒤 다시 열리는 중 — 나무는 그대로 두고 마지막 단계(열매는 모델 애니메이션이 감춤),
        /// 등불 열매 완성 모델의 Harvest(열매 → 봉오리) · Regrow (아트 2026-10-21).
        /// </summary>
        public static int StageIndex(int stageCount, float growth, bool ready, bool regrowing)
        {
            if (stageCount <= 0)
                return -1;
            int last = stageCount - 1;
            if (ready || regrowing)
                return last;
            if (growth <= 0f || stageCount <= 2)
                return 0;
            // 자라는 중: 1 ~ last-1 을 성장 비율로 고르게 나눈다
            int middle = stageCount - 2;
            int i = 1 + (int)(growth * middle);
            return i > last - 1 ? last - 1 : i;
        }
    }
}
