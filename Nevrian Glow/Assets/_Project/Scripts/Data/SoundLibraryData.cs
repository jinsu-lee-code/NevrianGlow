using System;
using System.Collections.Generic;
using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>소리가 지나가는 볼륨 버스 (설정 화면 5-6에서 조절). 끝에만 추가한다.</summary>
    public enum SoundBus
    {
        Sfx,
        Ui,
        Bgm,
        Ambience,
    }

    /// <summary>
    /// 소리 id → 클립 묶음 (TASKS 5-5). 코드는 id로만 부르고, 파일은 여기서 연결한다 (파일 이름 = id + 변형 번호).
    /// 메뉴 `Glow Farm/Setup/Build Sound Library`가 `Assets/_Project/Audio/`를 읽어 채운다 — 손으로 고친 설정값은 다시 실행해도 유지.
    /// </summary>
    [CreateAssetMenu(menuName = "Glow Farm/Data/Sound Library", fileName = "SoundLibrary")]
    public class SoundLibraryData : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("소리 id — 코드가 부르는 이름 (예: sfx_water_pour)")]
            [SerializeField] private string id;
            [Tooltip("변형 클립 — 여러 개면 무작위 (바로 전과 같은 것 제외)")]
            [SerializeField] private List<AudioClip> clips = new List<AudioClip>();
            [Tooltip("볼륨 버스")]
            [SerializeField] private SoundBus bus;
            [Tooltip("기본 볼륨 (0~1)")]
            [SerializeField, Range(0f, 1f)] private float volume = 1f;
            [Tooltip("켜면 위치에서 나는 3D 소리 (손님 · 계산대 등)")]
            [SerializeField] private bool spatial;
            [Tooltip("반복 재생 (루프 · 배경음)")]
            [SerializeField] private bool loop;
            [Tooltip("음높이 무작위 폭 (±배율, 0.05 = ±5%)")]
            [SerializeField, Range(0f, 0.3f)] private float pitchJitter;
            [Tooltip("같은 소리가 다시 나기까지 최소 간격 (초)")]
            [SerializeField, Min(0f)] private float cooldown;

            public string Id => id;
            public IReadOnlyList<AudioClip> Clips => clips;
            public SoundBus Bus => bus;
            public float Volume => volume;
            public bool Spatial => spatial;
            public bool Loop => loop;
            public float PitchJitter => pitchJitter;
            public float Cooldown => cooldown;
        }

        [Header("소리 목록")]
        [SerializeField] private List<Entry> entries = new List<Entry>();

        [Header("3D 소리 (asset_placement 6번)")]
        [Tooltip("이 거리까지는 최대 크기 (m)")]
        [SerializeField, Min(0.1f)] private float minDistance = 1f;
        [Tooltip("이 거리 밖에서는 들리지 않음 (m)")]
        [SerializeField, Min(0.1f)] private float maxDistance = 15f;

        public IReadOnlyList<Entry> Entries => entries;
        public float MinDistance => minDistance;
        public float MaxDistance => maxDistance;
    }
}
