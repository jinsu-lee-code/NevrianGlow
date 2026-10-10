using NevrianGlowFarm.Core;
using UnityEngine;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 게임 화면(가방 · 창고, 이후 상점 · 정산 · 스킬) 공통: 열면 플레이어 조작을 잠그고 커서를 보이게, 닫으면 푼다.
    /// 한 번에 하나만 열린다 — 다른 화면을 열면 먼저 열린 화면이 닫힌다(닫을 수 없는 상태면 새 화면을 열지 않는다).
    /// 같은 프레임에 닫힌 화면이 있으면 그 프레임의 열기 키는 무시한다 (Tab 한 번에 닫혔다 다시 열리지 않게).
    /// </summary>
    public abstract class GameScreen : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("보이고 숨길 화면 패널 (이 오브젝트의 자식)")]
        [SerializeField] private GameObject panel;

        private static GameScreen current;
        private static int lastClosedFrame = -1;

        public bool IsOpen => current == this;

        /// <summary>지금 열린 화면 (없으면 null).</summary>
        public static GameScreen Current => current;

        /// <summary>이번 프레임에 화면이 닫혔는지 — 열기 키 처리에서 확인한다.</summary>
        protected static bool ClosedThisFrame => lastClosedFrame == Time.frameCount;

        protected virtual void Awake()
        {
            if (panel == null)
            {
                Debug.LogError($"[{GetType().Name}] 패널 참조가 비어 있음", this);
                enabled = false;
                return;
            }
            panel.SetActive(false);
        }

        protected virtual void OnDestroy()
        {
            if (current == this)
            {
                current = null;
                PlayerInputGate.Release(this);
            }
        }

        protected void Show()
        {
            if (current == this)
                return;
            if (current != null)
            {
                current.Close();
                if (current != null)
                    return; // 먼저 열린 화면이 닫히지 않으면(정산 · 손에 든 묶음) 열지 않는다
            }
            current = this;
            panel.SetActive(true);
            PlayerInputGate.Block(this);
            OnOpened();
        }

        /// <summary>닫는다. 닫을 수 없는 상태면(OnClosing이 false) 열린 채로 둔다.</summary>
        public void Close()
        {
            if (current != this || !OnClosing())
                return;
            panel.SetActive(false);
            current = null;
            lastClosedFrame = Time.frameCount;
            PlayerInputGate.Release(this);
        }

        protected virtual void OnOpened() { }

        /// <summary>닫기 직전. false면 닫지 않는다 (예: 손에 든 묶음을 둘 곳이 없음).</summary>
        protected virtual bool OnClosing() => true;
    }
}
