using System.Collections.Generic;
using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>
    /// VFX id → 파티클 프리팹 (TASKS 5-8). 코드는 id(Core/VfxCues)로만 부르고, 프리팹 연결은 이 에셋에서 한다 — 소리의 SoundLibraryData와 같은 방식.
    /// </summary>
    [CreateAssetMenu(menuName = "Glow Farm/Data/VFX Library", fileName = "VfxLibrary")]
    public class VfxLibraryData : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            [Tooltip("VFX id (vfx_sparkle 등 — Core/VfxCues)")]
            [SerializeField] private string id;
            [Tooltip("파티클 프리팹 (아트 Environment/FX/<id>/pf_vfx_<id>)")]
            [SerializeField] private GameObject prefab;
            [Tooltip("1회 효과를 지우기까지 시간 — 반복 효과는 0 (초)")]
            [SerializeField, Min(0f)] private float lifetime = 2f;
            [Tooltip("크기 배율 (배)")]
            [SerializeField, Min(0.01f)] private float scale = 1f;

            public string Id => id;
            public GameObject Prefab => prefab;
            public float Lifetime => lifetime;
            public float Scale => scale;

            public Entry() { }
            public Entry(string id, GameObject prefab, float lifetime, float scale = 1f)
            {
                this.id = id;
                this.prefab = prefab;
                this.lifetime = lifetime;
                this.scale = scale;
            }
        }

        [Tooltip("VFX 목록")]
        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;
    }
}
