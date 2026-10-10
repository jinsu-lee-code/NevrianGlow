using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Systems.Interaction;
using NevrianGlowFarm.UI;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Storage
{
    /// <summary>
    /// 창고 접근 지점 (농장 문 옆 · 가게 창고 구역, BALANCE 9-1). E로 창고 화면을 연다 — 두 곳 모두 같은 창고(StorageSystem).
    /// 접근 반경(InventorySettings, 2 m) 밖에서는 "더 가까이"만 보여 준다. 기즈모 시안 원 = 접근 반경.
    /// </summary>
    public class StorageAccessPoint : MonoBehaviour, IHandInteractable
    {
        [Header("참조")]
        [Tooltip("접근 반경 (Data/Stats/InventorySettings)")]
        [SerializeField] private InventorySettingsData settings;

        [Header("조정값")]
        [Tooltip("화면 · 프롬프트에 보이는 이름")]
        [SerializeField] private string displayName = "창고";

        private Transform player;

        public string GetHandPrompt()
        {
            if (settings == null)
                return null;
            return InReach() ? $"{displayName} 열기" : $"{displayName} - 더 가까이 가기";
        }

        public void InteractHand()
        {
            if (!InReach())
                return;
            if (StorageScreen.Instance == null)
            {
                Debug.LogError("[StorageAccessPoint] 창고 화면(StorageScreen)이 없음 — Core 씬 UICanvas 확인", this);
                return;
            }
            StorageScreen.Instance.Open(displayName);
        }

        // 플레이어는 Core 씬에 있어 씬 사이 참조를 걸 수 없다 → 처음 쓸 때 한 번 찾는다.
        private bool InReach()
        {
            if (player == null)
            {
                var inventory = Systems.Player.PlayerInventory.Instance;
                if (inventory == null)
                    return false;
                player = inventory.transform;
            }
            Vector3 d = player.position - transform.position;
            d.y = 0f;
            return d.magnitude <= settings.StorageAccessRadius;
        }

        private void OnDrawGizmos()
        {
            if (settings != null)
                GizmoKit.WirePoint(transform.position, settings.StorageAccessRadius, GizmoKit.Interaction);
        }
    }
}
