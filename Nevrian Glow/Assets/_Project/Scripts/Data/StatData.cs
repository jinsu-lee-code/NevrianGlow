using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>
    /// 능력치 하나 = 에셋 하나 (판매가 배율 · 성장일 · 핫바 칸 · 손님 보너스 …). 스킬 효과가 이 에셋을 가리키고,
    /// 값을 쓰는 시스템도 같은 에셋을 참조로 들고 있다 → 능력치 추가는 에셋 추가만으로 (CLAUDE 핵심 규칙 5 · 6).
    /// 기본값은 쓰는 쪽이 넘긴다 (작물 판매가 · 설정의 칸 수 등 — 원본 수치는 그 데이터에).
    /// </summary>
    [CreateAssetMenu(menuName = "Glow Farm/Data/Stat", fileName = "stat_new")]
    public class StatData : ContentData
    {
        [Header("설명")]
        [Tooltip("이 능력치가 무엇을 바꾸는지 (스킬 화면 · 인스펙터 안내)")]
        [SerializeField, TextArea] private string description;

        public string Description => description;
    }
}
