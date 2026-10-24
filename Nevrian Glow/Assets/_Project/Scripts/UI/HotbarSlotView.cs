using System;
using NevrianGlowFarm.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 아이템 칸 하나 또는 목록 줄 하나. HUD 핫바 · 가방 · 창고 칸(아이콘 + 이름/개수)과 상점 · 스킬 · 창고 목록 줄(아이콘 · 이름 · 둘째 줄 · 오른쪽 값, 5-4c)이 같이 쓴다.
    /// 참조가 비어 있는 부분은 그냥 건너뛴다 — 칸은 둘째 줄 · 값이 없고, HUD 핫바는 버튼 · 툴팁이 없다.
    /// 버튼이 있으면 클릭을 알리고, 툴팁이 있으면 마우스를 올렸을 때 설명을 띄운다.
    /// </summary>
    public class HotbarSlotView : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("칸 배경 — 선택 여부로 색을 바꾼다")]
        [SerializeField] private Image background;
        [Tooltip("숫자 키 글자 (비우면 표시 안 함). 목록 줄에서는 아이콘 자리의 표시(스킬 단계)로도 쓴다")]
        [SerializeField] private TMP_Text keyLabel;
        [Tooltip("아이템 이름 글자")]
        [SerializeField] private TMP_Text nameLabel;
        [Tooltip("클릭 버튼 (HUD 핫바는 비움)")]
        [SerializeField] private Button button;
        [Tooltip("아이템 아이콘 (비우면 표시 안 함 — 5-4)")]
        [SerializeField] private Image icon;
        [Tooltip("둘째 줄 글자 — 목록 줄의 상태 · 조건 (비우면 한 줄짜리, 5-4c)")]
        [SerializeField] private TMP_Text detailLabel;
        [Tooltip("오른쪽 값 글자 — 가격 · 개수 · SP (비우면 표시 안 함, 5-4c)")]
        [SerializeField] private TMP_Text valueLabel;
        [Tooltip("값 옆 아이콘 — 달러 · SP (비우면 표시 안 함, 5-4c)")]
        [SerializeField] private Image valueIcon;
        [Tooltip("마우스를 올리면 설명을 띄우는 트리거 (비우면 설명 없음, 5-4c)")]
        [SerializeField] private TooltipTrigger tooltip;

        [Header("조정값")]
        [Tooltip("배경 그림 — 비우면 지금 그림 그대로 (5-4a UI 스킨)")]
        [SerializeField] private Sprite normalSprite;
        [Tooltip("선택된 칸 · 탭의 배경 그림 — 비우면 normalSprite")]
        [SerializeField] private Sprite selectedSprite;

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

        /// <summary>숫자 키 자리에 아무 글자나 (스킬 줄의 "1/3"). 비우면 숨김.</summary>
        public void SetKeyText(string text)
        {
            if (keyLabel == null)
                return;
            bool has = !string.IsNullOrEmpty(text);
            keyLabel.gameObject.SetActive(has);
            keyLabel.text = has ? text : string.Empty;
        }

        /// <summary>아이콘 (null이면 숨김). 아이콘 칸이 없는 줄은 아무 일 없음.</summary>
        public void SetIcon(Sprite sprite)
        {
            if (icon == null)
                return;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        /// <summary>둘째 줄 (비우면 숨기고 이름을 세로 가운데로). 둘째 줄 글자가 없는 칸은 아무 일 없음.</summary>
        public void SetDetail(string text)
        {
            if (detailLabel == null)
                return;
            bool has = !string.IsNullOrEmpty(text);
            detailLabel.gameObject.SetActive(has);
            detailLabel.text = has ? text : string.Empty;
            // 이름 글자 상자는 줄 높이 전체 — 둘째 줄이 있으면 위쪽에, 없으면 가운데에
            nameLabel.alignment = has ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft;
        }

        /// <summary>오른쪽 값 (가격 · 개수)과 옆 아이콘. 비우면 둘 다 숨김. 값 글자가 없는 칸은 아무 일 없음.</summary>
        public void SetValue(string text, Sprite valueSprite = null)
        {
            if (valueLabel == null)
                return;
            bool has = !string.IsNullOrEmpty(text);
            valueLabel.gameObject.SetActive(has);
            valueLabel.text = has ? text : string.Empty;
            if (valueIcon != null)
            {
                valueIcon.sprite = valueSprite;
                valueIcon.enabled = has && valueSprite != null;
            }
        }

        /// <summary>마우스를 올렸을 때 보일 설명을 만드는 함수 (null이면 설명 없음). 툴팁이 없는 칸은 아무 일 없음.</summary>
        public void BindTooltip(Func<TooltipContent> provider)
        {
            if (tooltip != null)
                tooltip.Bind(provider);
        }

        public void SetSelected(bool selected, Color normalColor, Color selectedColor)
        {
            background.color = selected ? selectedColor : normalColor;
            if (normalSprite != null)
                background.sprite = selected && selectedSprite != null ? selectedSprite : normalSprite;
        }

        /// <summary>살 수 없음 · 잠김 줄: 아이콘 · 이름 · 값을 흐리게 (배경색과 별개로 글자 대비를 낮춰 "지금은 아님"을 보여 준다, 5-4c).</summary>
        public void SetDimmed(bool dimmed)
        {
            float alpha = dimmed ? 0.55f : 1f;
            nameLabel.alpha = alpha;
            if (valueLabel != null)
                valueLabel.alpha = alpha;
            if (icon != null)
                icon.color = dimmed ? new Color(0.6f, 0.6f, 0.65f, 1f) : Color.white;
            if (valueIcon != null)
                valueIcon.color = dimmed ? new Color(0.6f, 0.6f, 0.65f, 1f) : Color.white;
        }
    }
}
