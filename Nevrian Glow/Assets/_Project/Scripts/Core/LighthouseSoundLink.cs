using UnityEngine;

namespace NevrianGlowFarm.Core
{
    /// <summary>
    /// 등대 공명음 연결 (사운드 납품 10/29, BRANCH_SOUND 4-4). 아트 등대(LighthouseGoal, 배경 씬)의 소리 칸이 비어 있으면 실행 중에 채우고,
    /// 소리를 낼 2D AudioSource를 넘겨 설정 화면의 효과음 볼륨을 따르게 한다 — 등대는 AudioManager를 거치지 않고 직접 소리를 내기 때문.
    /// 아트가 프리팹 칸을 채우면 그 값을 그대로 둔다 (빈 칸만). 파트 폴더(Art)는 고치지 않는다. Core 씬 AudioManager 옆에 둔다.
    /// </summary>
    public class LighthouseSoundLink : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("등대 한 층 점등 — 레벨업 다음 날 아침 (Audio/SFX/sfx_lighthouse_floor, 4.0초)")]
        [SerializeField] private AudioClip floorClip;
        [Tooltip("엔딩 — 빛기둥 · 첫 해돋이 (Audio/SFX/sfx_lighthouse_ending, 10.9초)")]
        [SerializeField] private AudioClip endingClip;

        [Header("디버그")]
        [Tooltip("연결한 등대 (읽기 전용)")]
        [SerializeField] private string linkedName;

        private AudioSource source;
        private LighthouseGoal linked;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 멀리 있지만 들려야 하는 소리 (아트 설명)
        }

        // 배경 씬은 늦게 올라오고 구역 이동 · 이어하기에도 등대가 바뀔 수 있다 → 지금 등대가 바뀔 때만 연결
        private void Update()
        {
            var goal = LighthouseGoal.Active;
            if (goal != null && goal != linked)
            {
                if (goal.floorClip == null)
                    goal.floorClip = floorClip;
                if (goal.endingClip == null)
                    goal.endingClip = endingClip;
                if (goal.audioSource == null)
                    goal.audioSource = source;
                linked = goal;
                linkedName = goal.name;
            }
            source.volume = AudioManager.SfxBusVolume;
        }
    }
}
