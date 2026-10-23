using System.Collections.Generic;
using NevrianGlowFarm.Data;
using UnityEngine;

namespace NevrianGlowFarm.Core
{
    /// <summary>
    /// VFX 재생 (TASKS 5-8). 코드는 VFX id(VfxCues)로만 부른다 — 프리팹 연결은 VfxLibraryData.
    /// 1회: 위치에 만들고 lifetime 뒤 지운다. 반복: 부모 아래에 만들어 돌려주고, 부르는 쪽이 SetPlaying으로 켜고 끈다.
    /// VfxManager가 없거나 id가 없으면 아무 일 없이 넘어간다 (테스트 · 측정 봇 · 프리팹 연결 전).
    /// 레벨업 고리는 GameEvents.LeveledUp으로 여기서 직접 낸다 (플레이어 발밑).
    /// </summary>
    public class VfxManager : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("VFX id → 파티클 프리팹 (Data/Effects/VfxLibrary)")]
        [SerializeField] private VfxLibraryData library;

        [Header("디버그")]
        [Tooltip("마지막으로 낸 VFX id (읽기 전용)")]
        [SerializeField] private string lastPlayedId;

        private static VfxManager instance;
        private readonly Dictionary<string, VfxLibraryData.Entry> entries = new Dictionary<string, VfxLibraryData.Entry>();

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;
            if (library != null)
                foreach (var e in library.Entries)
                    if (e != null && !string.IsNullOrEmpty(e.Id) && e.Prefab != null)
                        entries[e.Id] = e;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void OnEnable() => GameEvents.LeveledUp += OnLeveledUp;
        private void OnDisable() => GameEvents.LeveledUp -= OnLeveledUp;

        // 레벨업: 플레이어(카메라) 발밑에 금빛 고리
        private void OnLeveledUp(int from, int to)
        {
            var cam = Camera.main;
            if (cam == null)
                return;
            Vector3 feet = cam.transform.position;
            if (Physics.Raycast(feet, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore))
                feet = hit.point;
            Play(VfxCues.RingGold, feet);
        }

        /// <summary>1회 효과를 위치에 낸다.</summary>
        public static void Play(string id, Vector3 position) => Play(id, position, Quaternion.identity);

        public static void Play(string id, Vector3 position, Quaternion rotation)
        {
            if (instance == null || !instance.entries.TryGetValue(id, out var e))
                return;
            var go = Instantiate(e.Prefab, position, rotation);
            go.transform.localScale *= e.Scale;
            instance.lastPlayedId = id;
            Destroy(go, e.Lifetime > 0f ? e.Lifetime : 2f);
        }

        /// <summary>반복 효과를 parent 아래(로컬 원점, null이면 월드)에 만든다 — 꺼진 채로. 없으면 null. 지우는 것은 부르는 쪽.</summary>
        public static GameObject Attach(string id, Transform parent)
        {
            if (instance == null || !instance.entries.TryGetValue(id, out var e))
                return null;
            var go = Instantiate(e.Prefab, parent, false);
            go.transform.localScale *= e.Scale;
            // '깨어날 때 재생'은 다음 프레임에 시작되므로 끄고 나서 멈춘다 (안 그러면 만들자마자 켜짐)
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.playOnAwake = false;
            }
            SetPlaying(go, false, clear: true);
            return go;
        }

        /// <summary>반복 효과를 켜고 끈다. 끌 때 이미 나온 입자는 그대로 사라지게 둔다 (clear면 바로 지움).</summary>
        public static void SetPlaying(GameObject vfx, bool playing, bool clear = false)
        {
            if (vfx == null)
                return;
            foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (playing)
                {
                    if (!ps.isPlaying)
                        ps.Play(false);
                }
                else
                    ps.Stop(false, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
            }
            // 리본(TrailRenderer · LineRenderer) 등 파티클이 아닌 것은 켜고 끈다
            foreach (var r in vfx.GetComponentsInChildren<Renderer>(true))
                if (!(r is ParticleSystemRenderer))
                    r.enabled = playing;
        }
    }
}
