using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NevrianGlowFarm.UI
{
    /// <summary>핫바 한 칸 (그레이박스: 단색 패널 + 글자, 아이콘은 5-4).</summary>
    public class HotbarSlotView : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("칸 배경 — 선택 여부로 색을 바꾼다")]
        [SerializeField] private Image background;
        [Tooltip("숫자 키 글자")]
        [SerializeField] private TMP_Text keyLabel;
        [Tooltip("아이템 이름 글자")]
        [SerializeField] private TMP_Text nameLabel;

        public void SetContent(int keyNumber, string itemName)
        {
            keyLabel.text = keyNumber.ToString();
            nameLabel.text = itemName;
        }

        public void SetSelected(bool selected, Color normalColor, Color selectedColor)
        {
            background.color = selected ? selectedColor : normalColor;
        }
    }
}
