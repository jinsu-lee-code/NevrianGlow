using NevrianGlowFarm.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 이 오브젝트 아래의 모든 버튼에 클릭 소리와 마우스 올림 소리(UiHoverSound, 사운드 납품 10.30)를 단다 (UICanvas에 하나). 화면마다 소리 코드를 넣지 않아도 된다.
    /// 나중에 복제되는 칸 버튼(HotbarSlotView)은 칸이 스스로 클릭 소리를 낸다 — 클릭만 건너뛴다. 꺼진 줄 틀(템플릿)에 붙인 올림 소리는 복제본도 함께 가진다.
    /// </summary>
    public class UiButtonSounds : MonoBehaviour
    {
        private void Start()
        {
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                if (!button.TryGetComponent<UiHoverSound>(out _))
                    button.gameObject.AddComponent<UiHoverSound>();
                if (button.GetComponentInParent<HotbarSlotView>(true) != null)
                    continue;
                button.onClick.AddListener(PlayClick);
            }
        }

        private static void PlayClick() => AudioManager.Play(SoundCues.UiClick);
    }
}
