using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>
    /// 콘텐츠 에셋(작물 · 상품 · 스킬 · 손님 등)의 공통 부분. 저장 · CSV는 바뀌지 않는 문자열 id로 연결된다.
    /// </summary>
    public abstract class ContentData : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("바뀌지 않는 문자열 id — 저장 · CSV 연결용 (예: crop_moonroot)")]
        [SerializeField] private string id;
        [Tooltip("화면에 보이는 이름")]
        [SerializeField] private string displayName;

        public string Id => id;
        public string DisplayName => displayName;
    }
}
