using System;
using NevrianGlowFarm.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 편지 화면 (TASKS P-7 오프닝 편지 → 5-7 할머니의 편지 꾸러미). 새 게임을 시작할 때 편지 ①이 뜨고(이어하기는 안 뜸),
    /// 튜토리얼(TutorialSystem)이 단계에 맞춰 편지 ② · ③을 같은 화면으로 연다. 닫으면(버튼 · Enter · E · Esc) 편지 id로 알린다(GameEvents.LetterClosed).
    /// 문구: 튜토리얼 데이터(TutorialStepsData.letters)에 그 id의 편지가 있으면 그것을, 없으면 인스펙터 값(편지 ① 임시 문구).
    /// 닫으면 1일차 상점이 이어서 열린다 (ShopScreen은 다른 화면이 닫힐 때까지 기다린다).
    /// </summary>
    public class OpeningLetterScreen : GameScreen
    {
        [Header("참조")]
        [Tooltip("편지 제목 글자")]
        [SerializeField] private TMP_Text titleLabel;
        [Tooltip("편지 본문 글자")]
        [SerializeField] private TMP_Text bodyLabel;
        [Tooltip("'시작하기' 버튼")]
        [SerializeField] private Button startButton;
        [Tooltip("닫기 입력 (Player/EndPhase Enter · Player/Interact E · UI/Cancel Esc) — 비우면 버튼으로만")]
        [SerializeField] private InputActionReference[] closeActions = new InputActionReference[0];

        [Header("조정값")]
        [Tooltip("새 게임 편지의 id (튜토리얼 완료 이벤트 letter_closed:<id>)")]
        [SerializeField] private string letterId = "letter_1";
        [Tooltip("편지 제목 (게임 화면 — '—' 쓰지 않음) — 튜토리얼 데이터에 같은 id의 편지가 있으면 그쪽을 쓴다")]
        [SerializeField] private string letterTitle = "농장에서 온 편지";
        [Tooltip("편지 본문 — 임시 문구 (튜토리얼 데이터에 같은 id의 편지가 있으면 그쪽을 쓴다)")]
        [SerializeField, TextArea(6, 14)] private string letterBody =
            "새 주인에게\n\n" +
            "황혼이 끝나지 않는 이 별의 농장과 작은 가게를 이제 당신에게 맡깁니다.\n" +
            "오래 비워 둔 탓에 가게는 어질러져 있고 밭은 굳어 있을 거예요.\n\n" +
            "괭이로 밭을 갈고, 씨앗을 심고, 물을 주세요. 밤사이 작물이 빛을 내며 자랍니다.\n" +
            "수확한 작물을 가게에 진열하면 손님들이 찾아올 거예요.\n\n" +
            "이 농장이 다시 빛나기를.\n\n" +
            "이전 농장 주인";
        [Tooltip("새 게임 편지의 버튼 글자")]
        [SerializeField] private string startLabel = "농장 시작하기";
        [Tooltip("그 뒤 편지(② · ③)의 버튼 글자 (게임 화면 - '—' 쓰지 않음)")]
        [SerializeField] private string closeLabel = "읽었어요";

        private string currentId;
        private string currentTitle;
        private string currentBody;

        public static OpeningLetterScreen Instance { get; private set; }

        /// <summary>편지 id → (제목, 본문). 튜토리얼 시스템이 데이터로 건다. 없거나 null을 돌려주면 인스펙터 문구.</summary>
        public static Func<string, (string Title, string Body)?> LetterSource { get; set; }

        /// <summary>지금(또는 마지막으로) 연 편지 id.</summary>
        public string CurrentLetterId => currentId;

        protected override void Awake()
        {
            base.Awake();
            if (titleLabel == null || bodyLabel == null || startButton == null)
            {
                Debug.LogError("[OpeningLetterScreen] 참조가 비어 있음 - 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            Instance = this;
            startButton.onClick.AddListener(Close);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this)
                Instance = null;
        }

        private void OnEnable()
        {
            foreach (var a in closeActions)
                if (a != null)
                    a.action.Enable();
        }

        /// <summary>새 게임 편지(letterId)를 연다. 튜토리얼 데이터에 그 편지가 있으면 그 문구로.</summary>
        public void Open() => Open(letterId);

        /// <summary>id의 편지를 연다 (문구는 LetterSource → 없으면 인스펙터 값). 열렸으면 true.</summary>
        public bool Open(string id)
        {
            if (!enabled)
                return false;
            var fromData = LetterSource?.Invoke(id);
            currentId = id;
            currentTitle = fromData.HasValue ? fromData.Value.Title : letterTitle;
            currentBody = fromData.HasValue ? fromData.Value.Body : letterBody;
            Show();
            return IsOpen;
        }

        protected override void OnOpened()
        {
            titleLabel.text = currentTitle;
            bodyLabel.text = currentBody;
            var label = startButton.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = currentId == letterId ? startLabel : closeLabel;
        }

        protected override void OnClosed()
        {
            if (!string.IsNullOrEmpty(currentId))
                GameEvents.RaiseLetterClosed(currentId);
        }

        private void Update()
        {
            if (!IsOpen)
                return;
            foreach (var a in closeActions)
                if (a != null && a.action.WasPressedThisFrame())
                {
                    Close();
                    return;
                }
        }
    }
}
