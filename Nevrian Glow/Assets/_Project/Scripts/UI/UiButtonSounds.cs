using NevrianGlowFarm.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 이 오브젝트 아래의 모든 버튼에 클릭 소리를 단다 (UICanvas에 하나). 화면마다 소리 코드를 넣지 않아도 된다.
    /// 나중에 복제되는 칸 버튼(HotbarSlotView)은 칸이 스스로 소리를 낸다 — 여기서는 건너뛴다.
    /// </summary>
    public class UiButtonSounds : MonoBehaviour
    {
        private void Start()
        {
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                if (button.GetComponentInParent<HotbarSlotView>(true) != null)
                    continue;
                button.onClick.AddListener(PlayClick);
            }
        }

        private static void PlayClick() => AudioManager.Play(SoundCues.UiClick);
    }
}
