using System.Collections.Generic;
using NevrianGlowFarm.Logic.Game;
using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>
    /// 지역 하나 (노션 GDD 11장, GDD 4-7): 분위기(앰비언트 · 안개 · 하늘 · BGM)와 이 지역에 있어야 하는 하루 단계.
    /// 지역 추가(사구 · 동토, 2차)는 에셋 추가 + 그 씬에 ZoneSpawn만으로.
    /// </summary>
    [CreateAssetMenu(menuName = "Glow Farm/Data/Zone Profile", fileName = "zone_new")]
    public class ZoneProfileData : ContentData
    {
        [Header("이동 규칙")]
        [Tooltip("이 단계일 때 플레이어가 이 지역에 있어야 한다 — 단계가 바뀌면 자동으로 옮긴다")]
        [SerializeField] private List<DayPhase> phases = new List<DayPhase>();
        [Tooltip("날이 바뀌면 이 지역에서 시작한다 (한 지역만 켠다)")]
        [SerializeField] private bool isDayStart;

        [Header("분위기 (BALANCE 2-10 임시, 5-1 조명 · 후처리에서 확정)")]
        [Tooltip("앰비언트 색 (단색 모드)")]
        [SerializeField] private Color ambientColor = Color.gray;
        [Tooltip("안개 켜기")]
        [SerializeField] private bool fog;
        [Tooltip("안개 색")]
        [SerializeField] private Color fogColor = Color.gray;
        [Tooltip("안개 밀도 (지수, 0~0.2)")]
        [SerializeField, Range(0f, 0.2f)] private float fogDensity;
        [Tooltip("하늘 재질 — 비우면 바꾸지 않는다")]
        [SerializeField] private Material skybox;
        [Tooltip("배경음 id — AudioManager(5-5)가 재생, 비우면 바꾸지 않는다")]
        [SerializeField] private string bgmId;

        public IReadOnlyList<DayPhase> Phases => phases;
        public bool IsDayStart => isDayStart;
        public Color AmbientColor => ambientColor;
        public bool Fog => fog;
        public Color FogColor => fogColor;
        public float FogDensity => fogDensity;
        public Material Skybox => skybox;
        public string BgmId => bgmId;

        public ZoneRule ToRule() => new ZoneRule(Id, phases, isDayStart);
    }
}
