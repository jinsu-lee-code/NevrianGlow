using NevrianGlowFarm.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 버튼 · 칸에 마우스를 올리면 작은 소리 (사운드 납품 10.30 `sfx_ui_hover`). UiButtonSounds가 버튼마다 붙인다 — 화면마다 소리 코드를 넣지 않는다.
    /// 누를 수 없는 버튼은 조용하다. 연달아 훑어도 소리 목록의 쿨다운이 겹침을 막는다.
    /// </summary>
    public class UiHoverSound : MonoBehaviour, IPointerEnterHandler
    {
        private Selectable selectable;

        private void Awake() => TryGetComponent(out selectable);

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (selectable == null || selectable.IsInteractable())
                AudioManager.Play(SoundCues.UiHover);
        }
    }
}
