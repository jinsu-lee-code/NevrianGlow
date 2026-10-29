using NevrianGlowFarm.Systems.Interaction;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Farm
{
    /// <summary>
    /// 나무형 작물(등불 열매)의 수확 창구 — 모델(나무 · 열매)을 감싸는 트리거 콜라이더에 붙는다 (수정사항 댓글 10/29 "밭이 아니라 나무나 열매를 타겟팅").
    /// FarmCellView가 모델을 만들 때 붙인다. E(수확)만 맡고, 좌클릭(물 · 비료) · 조준점은 부모 FarmCellView가 그대로 받는다.
    /// </summary>
    public class CropModelTarget : MonoBehaviour, IHandInteractable
    {
        private FarmGrid grid;
        private int index;

        public void Bind(FarmGrid owner, int cellIndex)
        {
            grid = owner;
            index = cellIndex;
        }

        public string GetHandPrompt() => grid != null ? grid.GetHandPrompt(index, true) : null;
        public void InteractHand() { if (grid != null) grid.InteractHand(index, true); }
    }
}
