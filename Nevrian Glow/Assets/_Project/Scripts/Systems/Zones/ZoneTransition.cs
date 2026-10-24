using System;
using System.Collections;
using System.Collections.Generic;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Systems.Player;
using NevrianGlowFarm.UI;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Zones
{
    /// <summary>
    /// 장소 이동 (노션 GDD 11장, GDD 4-7, CLAUDE 핵심 규칙 1). Core 씬에 하나.
    /// 순서: 입력 잠금 → 페이드 아웃 → 단계 변경 → 플레이어 이동 → 지역 분위기 적용 → 잠깐 대기 → 페이드 인 → 입력 해제.
    /// 문(ZoneDoor)이 이동을 요청하면 위 순서대로, 다른 곳(디버그 · 정산 화면 등)에서 단계가 먼저 바뀌면
    /// 그 단계를 맡은 지역으로 같은 순서(단계 변경만 이미 끝난 채)로 옮긴다 — 어느 쪽이든 "단계 = 지역"이 어긋나지 않는다.
    /// </summary>
    public class ZoneTransition : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("구역 씬 로드 완료를 알려 주는 부트스트래퍼 (Core)")]
        [SerializeField] private SceneBootstrapper bootstrapper;
        [Tooltip("옮길 플레이어 (Core Player)")]
        [SerializeField] private PlayerController player;
        [Tooltip("화면 페이드 (Core UICanvas/ScreenFader)")]
        [SerializeField] private ScreenFader fader;
        [Tooltip("지역 목록 (Data/Zones) — 단계 → 지역 규칙은 각 지역 데이터에")]
        [SerializeField] private List<ZoneProfileData> zones = new List<ZoneProfileData>();

        [Header("조정값 (BALANCE 2-10)")]
        [Tooltip("페이드 아웃 (초)")]
        [SerializeField, Min(0f)] private float fadeOut = 0.35f;
        [Tooltip("이동 후 페이드 인 전 대기 (초)")]
        [SerializeField, Min(0f)] private float hold = 0.1f;
        [Tooltip("페이드 인 (초)")]
        [SerializeField, Min(0f)] private float fadeIn = 0.35f;
        [Tooltip("배경음 교차 시간 — AudioManager(5-5)에 넘긴다 (초)")]
        [SerializeField, Min(0f)] private float bgmCrossfade = 1.5f;

        [Header("디버그")]
        [Tooltip("지금 지역 id (읽기 전용)")]
        [SerializeField] private string currentZoneId;
        [Tooltip("이동 중인지 (읽기 전용)")]
        [SerializeField] private bool transitioning;

        private static ZoneTransition instance;
        private readonly Dictionary<string, ZoneSpawn> spawns = new Dictionary<string, ZoneSpawn>();
        private ZoneRule[] rules;
        private ZoneProfileData current;

        public static ZoneTransition Instance => instance;
        public ZoneProfileData Current => current;
        public bool IsTransitioning => transitioning;
        public float BgmCrossfade => bgmCrossfade;

        /// <summary>지역에 들어간 직후 (페이드 인 전) — AudioManager가 BGM을 바꾼다.</summary>
        public event Action<ZoneProfileData> ZoneChanged;

        private void Awake()
        {
            if (bootstrapper == null || player == null || fader == null || zones.Count == 0)
            {
                Debug.LogError("[ZoneTransition] 참조가 비어 있음 — 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            instance = this;
            rules = zones.ConvertAll(z => z.ToRule()).ToArray();
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
            if (bootstrapper != null)
                bootstrapper.Ready -= OnZonesReady;
        }

        private void OnEnable() => GameEvents.PhaseChanged += OnPhaseChanged;
        private void OnDisable() => GameEvents.PhaseChanged -= OnPhaseChanged;

        private void Start()
        {
            if (!enabled)
                return;
            if (bootstrapper.IsReady)
                OnZonesReady();
            else
                bootstrapper.Ready += OnZonesReady;
        }

        // 구역 씬이 다 올라오면 출발 지점을 모으고 하루 시작 지역에 바로 세운다 (페이드 없음).
        private void OnZonesReady()
        {
            bootstrapper.Ready -= OnZonesReady;
            spawns.Clear();
            foreach (var spawn in FindObjectsByType<ZoneSpawn>(FindObjectsSortMode.None))
            {
                if (spawn.Zone == null)
                    Debug.LogError("[ZoneTransition] 지역이 비어 있는 출발 지점", spawn);
                else if (spawns.ContainsKey(spawn.Zone.Id))
                    Debug.LogError($"[ZoneTransition] 지역 '{spawn.Zone.Id}'의 출발 지점이 두 개", spawn);
                else
                    spawns.Add(spawn.Zone.Id, spawn);
            }
            foreach (var zone in zones)
                if (!spawns.ContainsKey(zone.Id))
                    Debug.LogError($"[ZoneTransition] 지역 '{zone.Id}'의 출발 지점(ZoneSpawn)이 씬에 없음", this);

            var start = Find(ZoneRules.DayStartZone(rules));
            if (start != null)
                Arrive(start);
        }

        /// <summary>
        /// 문 등이 이동을 요청한다. midpoint(예: 단계 넘기기)는 화면이 다 어두워진 뒤 한 번 불린다.
        /// 이미 이동 중이면 false.
        /// </summary>
        public bool Request(ZoneProfileData target, Action midpoint)
        {
            if (!enabled || transitioning || target == null)
                return false;
            StartCoroutine(Run(target, midpoint));
            return true;
        }

        private void OnPhaseChanged(PhaseChange change)
        {
            // 문이 요청한 이동 중이면 그 이동이 끝까지 맡는다 (도착할 때 음악도).
            if (transitioning || current == null)
                return;
            UpdateMusic();
            var target = Find(ZoneRules.TargetZone(change, current.Id, rules));
            if (target != null && target != current)
                StartCoroutine(Run(target, null));
        }

        private IEnumerator Run(ZoneProfileData target, Action midpoint)
        {
            transitioning = true;
            PlayerInputGate.Block(this);
            AudioManager.Play(SoundCues.UiFade);
            yield return fader.Fade(1f, fadeOut);

            midpoint?.Invoke();
            Arrive(target);

            yield return new WaitForSecondsRealtime(hold);
            yield return fader.Fade(0f, fadeIn);
            PlayerInputGate.Release(this);
            transitioning = false;
        }

        /// <summary>
        /// 불러오기 (6-1): 하루 시작 지역의 출발 지점에 바로 세운다 (페이드 없음).
        /// 이동 중이었으면 그 이동을 멈추고 화면 · 입력 잠금을 푼 뒤 세운다.
        /// </summary>
        public bool ArriveAtDayStart()
        {
            if (!enabled || rules == null)
                return false;
            if (transitioning)
            {
                StopAllCoroutines();
                fader.Set(0f);
                PlayerInputGate.Release(this);
                transitioning = false;
            }
            var start = Find(ZoneRules.DayStartZone(rules));
            if (start == null)
                return false;
            Arrive(start);
            return true;
        }

        private void Arrive(ZoneProfileData zone)
        {
            if (spawns.TryGetValue(zone.Id, out var spawn))
            {
                if (spawn.IsBlocked(player.transform))
                    Debug.LogWarning($"[ZoneTransition] '{zone.Id}' 출발 지점이 다른 물체와 겹침 — 씬 기즈모(빨강) 확인", spawn);
                player.TeleportTo(spawn.Position, spawn.Yaw);
            }
            Apply(zone);
            current = zone;
            currentZoneId = zone.Id;
            if (LogCollector.Instance != null)
                LogCollector.Instance.SetZone(zone.Id);
            UpdateMusic();
            ZoneChanged?.Invoke(zone);
            GameEvents.RaiseZoneArrived(zone.Id);
        }

        /// <summary>
        /// 지금 지역 · 단계의 배경음 (같은 곡이면 그대로 — AudioManager 교차 페이드). 게임 시작 전(타이틀)에는 타이틀 음악을 두고 건드리지 않는다.
        /// </summary>
        public void UpdateMusic()
        {
            var game = GameManager.Instance;
            if (current == null || game == null || !game.IsStarted)
                return;
            AudioManager.PlayMusic(current.BgmFor(game.Phase));
        }

        private static void Apply(ZoneProfileData zone)
        {
            RenderSettings.fog = zone.Fog;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = zone.FogColor;
            RenderSettings.fogDensity = zone.FogDensity;
            if (zone.Skybox != null)
                RenderSettings.skybox = zone.Skybox;

            if (zone.UseSkyboxAmbient)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
                DynamicGI.UpdateEnvironment(); // 하늘이 바뀌었으면 앰비언트 · 반사를 다시 계산
            }
            else
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = zone.AmbientColor;
            }
        }

        private ZoneProfileData Find(string id)
        {
            foreach (var z in zones)
                if (z != null && z.Id == id)
                    return z;
            return null;
        }
    }
}
