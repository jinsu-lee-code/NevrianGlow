using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Systems.Interaction;
using NevrianGlowFarm.Systems.Player;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Cleaning
{
    /// <summary>쓰레기통 (GDD 4-4): E를 누르면 핫바 · 가방에 든 쓰레기를 모두 비운다.</summary>
    public class TrashBin : MonoBehaviour, IHandInteractable
    {
        [Header("참조")]
        [Tooltip("쓰레기 아이템을 알려 주는 설정 (Data/Stats/CleaningSettings)")]
        [SerializeField] private CleaningSettingsData settings;

        public string GetHandPrompt()
        {
            var inventory = PlayerInventory.Instance;
            if (settings == null || settings.TrashItem == null || inventory == null)
                return null;
            int count = inventory.CountOf(settings.TrashItem);
            return count > 0 ? $"쓰레기 버리기 ×{count}" : "쓰레기통 - 버릴 쓰레기 없음";
        }

        public void InteractHand()
        {
            var inventory = PlayerInventory.Instance;
            if (settings != null && inventory != null && inventory.RemoveAll(settings.TrashItem) > 0)
            {
                AudioManager.Play(SoundCues.TrashDrop, transform.position);
                VfxManager.Play(VfxCues.LightPuff, transform.position + Vector3.up * 0.8f); // 통 입구 위
            }
        }
    }
}
