using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 오프닝 편지 (TASKS P-7, P0 — 회신2 4절 #3~7 "타이틀 · 오프닝 편지 · 설정 · 일시정지 P0"). 새 게임을 시작할 때만 한 번 뜬다(이어하기는 안 뜸).
    /// 닫으면 1일차 상점이 이어서 열린다 (ShopScreen은 다른 화면이 닫힐 때까지 기다린다).
    /// 문구는 인스펙터 값 — 기획 문구(튜토리얼 대본과 함께 10/23 전달 예정)가 오면 값만 바꾼다. 지금은 임시 문구.
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
        [Tooltip("닫기 입력 (Player/EndPhase Enter · UI/Cancel Esc) — 비우면 버튼으로만")]
        [SerializeField] private InputActionReference[] closeActions = new InputActionReference[0];

        [Header("조정값")]
        [Tooltip("편지 제목 (게임 화면 — '—' 쓰지 않음)")]
        [SerializeField] private string letterTitle = "농장에서 온 편지";
        [Tooltip("편지 본문 — 임시 문구 (교체 대기: 기획 오프닝 편지 10/23). 보낸 사람 · 설정은 기획 문구를 따른다")]
        [SerializeField, TextArea(6, 14)] private string letterBody =
            "새 주인에게\n\n" +
            "황혼이 끝나지 않는 이 별의 농장과 작은 가게를 이제 당신에게 맡깁니다.\n" +
            "오래 비워 둔 탓에 가게는 어질러져 있고 밭은 굳어 있을 거예요.\n\n" +
            "괭이로 밭을 갈고, 씨앗을 심고, 물을 주세요. 밤사이 작물이 빛을 내며 자랍니다.\n" +
            "수확한 작물을 가게에 진열하면 손님들이 찾아올 거예요.\n\n" +
            "이 농장이 다시 빛나기를.\n\n" +
            "이전 농장 주인";
        [Tooltip("버튼 글자")]
        [SerializeField] private string startLabel = "농장 시작하기";

        public static OpeningLetterScreen Instance { get; private set; }

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

        public void Open()
        {
            if (!enabled)
                return;
            Show();
        }

        protected override void OnOpened()
        {
            titleLabel.text = letterTitle;
            bodyLabel.text = letterBody;
            var label = startButton.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = startLabel;
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
