using System;
using NevrianGlowFarm.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 아이템 칸 하나 (그레이박스: 단색 패널 + 글자, 아이콘은 5-4). HUD 핫바 · 가방 화면 · 창고 화면이 같이 쓴다.
    /// 버튼이 있으면 클릭을 알린다 (가방 · 창고 화면).
    /// </summary>
    public class HotbarSlotView : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("칸 배경 — 선택 여부로 색을 바꾼다")]
        [SerializeField] private Image background;
        [Tooltip("숫자 키 글자 (비우면 표시 안 함)")]
        [SerializeField] private TMP_Text keyLabel;
        [Tooltip("아이템 이름 글자")]
        [SerializeField] private TMP_Text nameLabel;
        [Tooltip("클릭 버튼 (HUD 핫바는 비움)")]
        [SerializeField] private Button button;

        /// <summary>버튼을 눌렀을 때.</summary>
        public event Action Clicked;

        private void Awake()
        {
            if (button != null)
                button.onClick.AddListener(() =>
                {
                    AudioManager.Play(SoundCues.UiClick);
                    Clicked?.Invoke();
                });
        }

        public void SetContent(int keyNumber, string itemName)
        {
            if (keyLabel != null)
            {
                keyLabel.gameObject.SetActive(keyNumber > 0);
                keyLabel.text = keyNumber > 0 ? keyNumber.ToString() : string.Empty;
            }
            nameLabel.text = itemName;
        }

        public void SetSelected(bool selected, Color normalColor, Color selectedColor)
        {
            background.color = selected ? selectedColor : normalColor;
        }
    }
}
