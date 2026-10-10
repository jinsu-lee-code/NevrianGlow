using System.Collections;
using UnityEngine;

namespace NevrianGlowFarm.UI
{
    /// <summary>화면 전체를 덮는 검은 판의 투명도를 바꾼다 (장소 이동 페이드). 덮여 있는 동안 클릭을 막는다.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ScreenFader : MonoBehaviour
    {
        private CanvasGroup group;

        public float Alpha => group != null ? group.alpha : 0f;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            Set(0f);
        }

        /// <summary>to까지 duration초 동안 바꾼다. 게임 시간 정지와 무관하게 흐른다.</summary>
        public IEnumerator Fade(float to, float duration)
        {
            float from = group.alpha;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                Set(Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            Set(to);
        }

        public void Set(float alpha)
        {
            group.alpha = alpha;
            group.blocksRaycasts = alpha > 0.001f;
        }
    }
}
