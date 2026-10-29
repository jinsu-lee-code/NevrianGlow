using NevrianGlowFarm.Core;
using UnityEngine;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 게임 화면(가방 · 창고, 이후 상점 · 정산 · 스킬) 공통: 열면 플레이어 조작을 잠그고 커서를 보이게, 닫으면 푼다.
    /// 한 번에 하나만 열린다 — 다른 화면을 열면 먼저 열린 화면이 닫힌다(닫을 수 없는 상태면 새 화면을 열지 않는다).
    /// 같은 프레임에 닫힌 화면이 있으면 그 프레임의 열기 키는 무시한다 (Tab 한 번에 닫혔다 다시 열리지 않게).
    /// 열고 닫을 때 패널이 짧게 페이드 인 · 아웃한다 (수정사항 10/28 "화면 전환 시 페이드") — 열림 · 닫힘 상태와 입력 잠금은 바로 바뀌고 모습만 따라간다.
    /// </summary>
    public abstract class GameScreen : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("보이고 숨길 화면 패널 (이 오브젝트의 자식)")]
        [SerializeField] private GameObject panel;

        [Header("조정값")]
        [Tooltip("열고 닫을 때 페이드 시간 — 0이면 바로 (초, 실제 시간이라 일시정지 중에도)")]
        [SerializeField, Min(0f)] private float fadeTime = 0.15f;

        private static GameScreen current;
        private static int lastClosedFrame = -1;
        private CanvasGroup fade;
        private Coroutine fading;

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
            // 페이드용 — 패널에 없으면 붙인다 (화면들은 패널 투명도를 따로 쓰지 않음)
            fade = panel.GetComponent<CanvasGroup>();
            if (fade == null)
                fade = panel.AddComponent<CanvasGroup>();
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
            FadeTo(1f);
            PlayerInputGate.Block(this);
            OnOpened();
        }

        /// <summary>닫는다. 닫을 수 없는 상태면(OnClosing이 false) 열린 채로 둔다.</summary>
        public void Close()
        {
            if (current != this || !OnClosing())
                return;
            FadeTo(0f);
            current = null;
            lastClosedFrame = Time.frameCount;
            PlayerInputGate.Release(this);
            OnClosed();
        }

        // 패널 투명도를 target으로 — 0이 되면 패널을 끈다. 닫히는 중에는 클릭을 받지 않는다.
        private void FadeTo(float target)
        {
            if (fading != null)
                StopCoroutine(fading);
            fading = null;
            if (fade == null)
            {
                panel.SetActive(target > 0f);
                return;
            }
            fade.blocksRaycasts = target > 0f;
            if (fadeTime <= 0f || !isActiveAndEnabled)
            {
                fade.alpha = target;
                panel.SetActive(target > 0f);
                return;
            }
            if (target > 0f && fade.alpha >= 1f)
                fade.alpha = 0f; // 새로 열림 (닫히던 중이면 지금 투명도에서 이어서)
            fading = StartCoroutine(Fade(target));
        }

        private System.Collections.IEnumerator Fade(float target)
        {
            float from = fade.alpha;
            for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                fade.alpha = Mathf.Lerp(from, target, t / fadeTime);
                yield return null;
            }
            fade.alpha = target;
            if (target <= 0f)
                panel.SetActive(false);
            fading = null;
        }

        protected virtual void OnOpened() { }

        /// <summary>닫힌 뒤 (입력 잠금이 풀린 다음).</summary>
        protected virtual void OnClosed() { }

        /// <summary>닫기 직전. false면 닫지 않는다 (예: 손에 든 묶음을 둘 곳이 없음).</summary>
        protected virtual bool OnClosing() => true;
    }
}
