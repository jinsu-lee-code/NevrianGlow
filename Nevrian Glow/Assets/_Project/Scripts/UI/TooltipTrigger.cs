using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 마우스를 올리면 TooltipView를 띄우는 칸 · 줄 (5-4c). 내용은 화면이 Bind로 넘긴 함수가 그때그때 만든다 (산 뒤 보유 수가 바뀌는 등).
    /// 벗어나거나 꺼지면 숨긴다. 같은 Image가 Raycast Target이어야 한다 (버튼 칸은 이미 그렇다).
    /// </summary>
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Func<TooltipContent> provider;
        private bool hovered;

        /// <summary>내용을 만드는 함수를 건다. 이미 올라가 있으면 바로 다시 그린다.</summary>
        public void Bind(Func<TooltipContent> contentProvider)
        {
            provider = contentProvider;
            if (hovered)
                Refresh();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            Refresh();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            HideMine();
        }

        private void OnDisable()
        {
            if (!hovered)
                return;
            hovered = false;
            HideMine();
        }

        private void Refresh()
        {
            var view = TooltipView.Instance;
            if (view == null)
                return;
            if (provider == null)
            {
                view.Hide(this);
                return;
            }
            view.Show(provider(), this);
        }

        private void HideMine()
        {
            var view = TooltipView.Instance;
            if (view != null)
                view.Hide(this);
        }
    }
}
