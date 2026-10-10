using System.Collections.Generic;
using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>
    /// 모든 콘텐츠 에셋(작물 · 씨앗 · 도구 · 상품 · 스킬 · 손님 …)을 id로 찾는 목록 (노션 GDD 12.5, CLAUDE 핵심 규칙 6).
    /// 목록은 에디터가 Data 폴더에서 자동으로 채운다(에셋을 추가 · 삭제하면 갱신) — 손으로 넣지 않는다.
    /// 빌드에서는 폴더를 뒤질 수 없으므로 이 에셋이 참조를 들고 간다.
    /// </summary>
    [CreateAssetMenu(menuName = "Glow Farm/Data/Content Database", fileName = "ContentDatabase")]
    public class ContentDatabase : ScriptableObject
    {
        [Header("디버그")]
        [Tooltip("자동 수집된 콘텐츠 (읽기 전용 — Glow Farm/Data/Refresh Content Database)")]
        [SerializeField] private List<ContentData> contents = new List<ContentData>();

        private Dictionary<string, ContentData> byId;

        public IReadOnlyList<ContentData> Contents => contents;

        private void OnEnable() => byId = null;

        /// <summary>id로 찾는다. 없거나 종류가 다르면 null.</summary>
        public T Get<T>(string id) where T : ContentData => TryGet(id, out T found) ? found : null;

        public bool TryGet<T>(string id, out T found) where T : ContentData
        {
            found = null;
            if (string.IsNullOrEmpty(id))
                return false;
            EnsureIndex();
            if (byId.TryGetValue(id, out var content) && content is T typed)
                found = typed;
            return found != null;
        }

        /// <summary>종류별 전체 (목록 순서 = 에셋 경로 순서).</summary>
        public IEnumerable<T> All<T>() where T : ContentData
        {
            foreach (var content in contents)
                if (content is T typed)
                    yield return typed;
        }

        private void EnsureIndex()
        {
            if (byId != null)
                return;
            byId = new Dictionary<string, ContentData>();
            foreach (var content in contents)
                if (content != null && !string.IsNullOrEmpty(content.Id) && !byId.ContainsKey(content.Id))
                    byId.Add(content.Id, content);
        }

#if UNITY_EDITOR
        /// <summary>에디터 전용: 수집 결과를 넣는다. 바뀐 것이 없으면 false.</summary>
        public bool EditorSetContents(List<ContentData> collected)
        {
            bool same = collected.Count == contents.Count;
            for (int i = 0; same && i < collected.Count; i++)
                same = collected[i] == contents[i];
            if (same)
                return false;
            contents = collected;
            byId = null;
            return true;
        }
#endif
    }
}
