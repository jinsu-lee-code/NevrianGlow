using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 확인 창 (TASKS 5-6): 질문 한 줄 + 예 · 아니오. 타이틀(새 게임이 저장을 덮음) · 일시정지(타이틀로 · 종료하면 오늘 진행이 사라짐)가 쓴다.
    /// </summary>
    public class ConfirmDialog : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("질문 글자")]
        [SerializeField] private TMP_Text messageLabel;
        [Tooltip("'예' 버튼")]
        [SerializeField] private Button yesButton;
        [Tooltip("'예' 버튼 글자 (질문마다 바뀜)")]
        [SerializeField] private TMP_Text yesLabel;
        [Tooltip("'아니오' 버튼")]
        [SerializeField] private Button noButton;

        private Action onYes;
        private Action onNo;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            if (messageLabel == null || yesButton == null || yesLabel == null || noButton == null)
            {
                Debug.LogError("[ConfirmDialog] 참조가 비어 있음 - 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            yesButton.onClick.AddListener(Yes);
            noButton.onClick.AddListener(Cancel);
        }

        /// <summary>묻는다. '예' → yes, '아니오' · Esc → no (둘 다 창을 닫은 뒤 부른다).</summary>
        public void Ask(string message, string yesText, Action yes, Action no)
        {
            messageLabel.text = message;
            yesLabel.text = yesText;
            onYes = yes;
            onNo = no;
            gameObject.SetActive(true);
        }

        public void Yes()
        {
            if (!IsOpen)
                return;
            var callback = onYes;
            Hide();
            callback?.Invoke();
        }

        /// <summary>'아니오' (Esc도 띄운 화면이 이걸 부른다).</summary>
        public void Cancel()
        {
            if (!IsOpen)
                return;
            var callback = onNo;
            Hide();
            callback?.Invoke();
        }

        public void Hide()
        {
            onYes = null;
            onNo = null;
            gameObject.SetActive(false);
        }
    }
}
