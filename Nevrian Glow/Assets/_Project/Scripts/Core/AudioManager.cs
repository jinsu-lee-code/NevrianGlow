using System.Collections;
using System.Collections.Generic;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic;
using NevrianGlowFarm.Logic.Audio;
using UnityEngine;

namespace NevrianGlowFarm.Core
{
    /// <summary>
    /// 소리 재생 (TASKS 5-5). 코드는 소리 id로만 부른다 — 클립 연결은 SoundLibraryData.
    /// 클립이 없으면 조용히 넘어가고 일반 로그 1회 (Warning 아님 — 버그 로그 오염 방지).
    /// 효과음: 변형 무작위 · 음높이 ±jitter · 쿨다운 · 2D/3D. 루프: 시작 → 핸들 → 정지(짧은 페이드). 음악 · 환경음: 교차 페이드.
    /// AudioManager가 없는 상황(테스트 · 측정 봇)에서도 정적 호출은 아무 일 없이 넘어간다.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class AudioManager : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("소리 id → 클립 (Data/Audio/SoundLibrary)")]
        [SerializeField] private SoundLibraryData library;

        [Header("조정값")]
        [Tooltip("동시에 낼 수 있는 효과음 수 (개)")]
        [SerializeField, Min(4)] private int voiceCount = 24;
        [Tooltip("전체 볼륨 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [Tooltip("효과음 볼륨 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
        [Tooltip("UI 소리 볼륨 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float uiVolume = 0.8f;
        [Tooltip("배경음 볼륨 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.6f;
        [Tooltip("환경음 볼륨 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.5f;
        [Tooltip("배경음 · 환경음 교차 시간 (초, GDD 4-7)")]
        [SerializeField, Min(0f)] private float musicCrossfade = 1.5f;
        [Tooltip("루프 효과음을 멈출 때 줄이는 시간 (초)")]
        [SerializeField, Min(0f)] private float loopFadeOut = 0.1f;

        [Header("디버그")]
        [Tooltip("마지막으로 낸 소리 id (읽기 전용)")]
        [SerializeField] private string lastPlayedId;

        private static AudioManager instance;

        private readonly Dictionary<string, SoundLibraryData.Entry> entries = new Dictionary<string, SoundLibraryData.Entry>();
        private readonly Dictionary<string, int> lastVariant = new Dictionary<string, int>();
        private readonly HashSet<string> reportedMissing = new HashSet<string>();
        private readonly SoundCooldowns cooldowns = new SoundCooldowns();
        private readonly Dictionary<int, AudioSource> loops = new Dictionary<int, AudioSource>();
        private readonly HashSet<AudioSource> loopSources = new HashSet<AudioSource>();
        private AudioSource[] voices;
        private AudioSource[] musicSources;
        private AudioSource[] ambienceSources;
        private int musicActive;
        private int ambienceActive;
        private string musicId;
        private string ambienceId;
        private int nextLoopHandle = 1;
        private IRandomSource random;

        public static AudioManager Instance => instance;

        // ── 정적 호출 (어디서든) ─────────────────────────────

        /// <summary>2D 효과음 (플레이어 본인 행동 · UI).</summary>
        public static void Play(string id)
        {
            if (instance != null) instance.PlayInternal(id, null);
        }

        /// <summary>위치에서 나는 효과음. 라이브러리에서 3D가 아니면 2D로 난다.</summary>
        public static void Play(string id, Vector3 position)
        {
            if (instance != null) instance.PlayInternal(id, position);
        }

        /// <summary>루프 효과음 시작 → 핸들 (0 = 실패). StopLoop로 멈춘다.</summary>
        public static int StartLoop(string id)
        {
            return instance != null ? instance.StartLoopInternal(id) : 0;
        }

        public static void StopLoop(int handle)
        {
            if (instance != null) instance.StopLoopInternal(handle);
        }

        /// <summary>배경음을 바꾼다 (교차 페이드). 같은 id면 무시, null · 빈 문자열이면 멈춤.</summary>
        public static void PlayMusic(string id)
        {
            if (instance != null) instance.Crossfade(ref instance.musicId, id, instance.musicSources, ref instance.musicActive, SoundBus.Bgm);
        }

        /// <summary>환경음을 바꾼다 (교차 페이드).</summary>
        public static void PlayAmbience(string id)
        {
            if (instance != null) instance.Crossfade(ref instance.ambienceId, id, instance.ambienceSources, ref instance.ambienceActive, SoundBus.Ambience);
        }

        // ── 수명 ─────────────────────────────

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogError("[AudioManager] 두 개 있음 — Core 씬에 하나만 둔다.", this);
                enabled = false;
                return;
            }
            instance = this;
            random = new SeededRandom(System.Environment.TickCount);

            if (library != null)
            {
                foreach (var e in library.Entries)
                    if (e != null && !string.IsNullOrEmpty(e.Id))
                        entries[e.Id] = e;
            }
            else
            {
                Debug.Log("[AudioManager] SoundLibrary가 비어 있음 — 모든 소리가 무음");
            }

            voices = new AudioSource[voiceCount];
            for (int i = 0; i < voiceCount; i++)
                voices[i] = CreateSource($"Voice{i:00}");
            musicSources = new[] { CreateSource("MusicA"), CreateSource("MusicB") };
            ambienceSources = new[] { CreateSource("AmbienceA"), CreateSource("AmbienceB") };
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        // 게임 이벤트로 나는 소리 — 보내는 쪽 코드를 건드리지 않는다.
        private void OnEnable()
        {
            GameEvents.LeveledUp += OnLeveledUp;
            GameEvents.SkillLevelChanged += OnSkillLevelChanged;
            GameEvents.DayChanged += OnDayChanged;
        }

        private void OnDisable()
        {
            GameEvents.LeveledUp -= OnLeveledUp;
            GameEvents.SkillLevelChanged -= OnSkillLevelChanged;
            GameEvents.DayChanged -= OnDayChanged;
        }

        private readonly Dictionary<string, int> skillLevels = new Dictionary<string, int>();
        private bool dayStartedOnce;

        private void OnLeveledUp(int level, int gained) => Play(SoundCues.UiLevelUp);

        private void OnSkillLevelChanged(string skillId, int level)
        {
            skillLevels.TryGetValue(skillId, out int before);
            skillLevels[skillId] = level;
            if (level > before)
                Play(SoundCues.UiSkillLearn); // 단계가 오를 때만 (디버그로 내리는 건 무음)
        }

        private void OnDayChanged(int day)
        {
            // 새 게임 시작 때 HUD용으로 한 번 오는 첫 알림은 건너뛴다 (GameEvents 설명).
            if (dayStartedOnce)
                Play(SoundCues.DayStart);
            dayStartedOnce = true;
        }

        private AudioSource CreateSource(string sourceName)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = library != null ? library.MinDistance : 1f;
            source.maxDistance = library != null ? library.MaxDistance : 15f;
            return source;
        }

        // ── 효과음 ─────────────────────────────

        private void PlayInternal(string id, Vector3? position)
        {
            if (!TryGetClip(id, out var entry, out var clip))
                return;
            if (!cooldowns.TryUse(id, Time.unscaledTimeAsDouble, entry.Cooldown))
                return;

            var source = FreeVoice();
            if (source == null)
                return; // 동시 재생 수가 다 찼으면 이번 소리는 버린다

            bool spatial = entry.Spatial && position.HasValue;
            source.transform.position = spatial ? position.Value : transform.position;
            source.spatialBlend = spatial ? 1f : 0f;
            source.clip = clip;
            source.loop = false;
            source.pitch = SoundRules.Pitch(entry.PitchJitter, random);
            source.volume = entry.Volume * BusVolume(entry.Bus);
            source.Play();
            lastPlayedId = id;
        }

        private int StartLoopInternal(string id)
        {
            if (!TryGetClip(id, out var entry, out var clip))
                return 0;
            var source = FreeVoice();
            if (source == null)
                return 0;

            source.spatialBlend = 0f;
            source.clip = clip;
            source.loop = true;
            source.pitch = 1f;
            source.volume = entry.Volume * BusVolume(entry.Bus);
            source.Play();
            int handle = nextLoopHandle++;
            loops[handle] = source;
            loopSources.Add(source);
            lastPlayedId = id;
            return handle;
        }

        private void StopLoopInternal(int handle)
        {
            if (!loops.TryGetValue(handle, out var source))
                return;
            loops.Remove(handle);
            StartCoroutine(FadeOutAndRelease(source, loopFadeOut));
        }

        private IEnumerator FadeOutAndRelease(AudioSource source, float duration)
        {
            float start = source.volume;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                source.volume = Mathf.Lerp(start, 0f, t / duration);
                yield return null;
            }
            source.Stop();
            source.loop = false;
            loopSources.Remove(source);
        }

        private AudioSource FreeVoice()
        {
            foreach (var v in voices)
                if (!v.isPlaying && !loopSources.Contains(v))
                    return v;
            return null;
        }

        // ── 음악 · 환경음 ─────────────────────────────

        private void Crossfade(ref string currentId, string id, AudioSource[] pair, ref int active, SoundBus bus)
        {
            if (currentId == id)
                return;
            currentId = id;

            var from = pair[active];
            AudioSource to = null;
            if (!string.IsNullOrEmpty(id) && TryGetClip(id, out var entry, out var clip))
            {
                active = 1 - active;
                to = pair[active];
                to.clip = clip;
                to.loop = true;
                to.spatialBlend = 0f;
                to.volume = 0f;
                to.Play();
                StartCoroutine(FadeTo(to, entry.Volume * BusVolume(bus), musicCrossfade, false));
            }
            if (from.isPlaying && from != to)
                StartCoroutine(FadeTo(from, 0f, musicCrossfade, true));
        }

        private static IEnumerator FadeTo(AudioSource source, float target, float duration, bool stopAtEnd)
        {
            float start = source.volume;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                source.volume = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
            source.volume = target;
            if (stopAtEnd)
                source.Stop();
        }

        // ── 공통 ─────────────────────────────

        private bool TryGetClip(string id, out SoundLibraryData.Entry entry, out AudioClip clip)
        {
            clip = null;
            if (string.IsNullOrEmpty(id) || !entries.TryGetValue(id, out entry) || entry.Clips.Count == 0)
            {
                entry = null;
                if (!string.IsNullOrEmpty(id) && reportedMissing.Add(id))
                    Debug.Log($"[AudioManager] 소리 없음: '{id}' — 무음으로 넘어감 (파일 도착 후 Build Sound Library)");
                return false;
            }

            if (!lastVariant.TryGetValue(id, out int last))
                last = -1;
            int index = SoundRules.PickVariant(entry.Clips.Count, last, random);
            lastVariant[id] = index;
            clip = entry.Clips[index];
            return clip != null;
        }

        private float BusVolume(SoundBus bus)
        {
            switch (bus)
            {
                case SoundBus.Ui: return masterVolume * uiVolume;
                case SoundBus.Bgm: return masterVolume * bgmVolume;
                case SoundBus.Ambience: return masterVolume * ambienceVolume;
                default: return masterVolume * sfxVolume;
            }
        }
    }
}
