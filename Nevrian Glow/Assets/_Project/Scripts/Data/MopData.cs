using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>대걸레 한 등급 (BALANCE 2-11). 등급마다 닦는 반경과 속도가 함께 오른다.</summary>
    [CreateAssetMenu(menuName = "Glow Farm/Data/Tool/Mop", fileName = "tool_mop_new")]
    public class MopData : ToolData
    {
        [Header("대걸레")]
        [Tooltip("닦는 반경 (m)")]
        [SerializeField, Min(0.1f)] private float radius = 0.1f;
        [Tooltip("닦는 속도 (얼룩 진하기/초)")]
        [SerializeField, Min(0.01f)] private float speed = 0.01f;

        public float Radius => radius;
        public float Speed => speed;
    }
}
