#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Data;
using UnityEditor;
using UnityEngine;

namespace NevrianGlowFarm.Editor
{
    /// <summary>
    /// Data 폴더의 콘텐츠 에셋을 ContentDatabase에 자동으로 모으고 id를 검사한다 (CLAUDE 핵심 규칙 5 · 6).
    /// Data 폴더에서 에셋이 추가 · 삭제 · 이동되면 저절로 다시 모은다. 결과가 같으면 아무것도 바꾸지 않는다(멱등).
    /// </summary>
    public static class ContentDatabaseBuilder
    {
        public const string DataRoot = "Assets/_Project/Data";
        public const string DatabasePath = DataRoot + "/ContentDatabase.asset";

        [MenuItem("Glow Farm/Data/Refresh Content Database")]
        private static void RefreshMenu()
        {
            var (count, problems, changed) = Refresh();
            Debug.Log($"[ContentDatabase] 콘텐츠 {count}개 · {(changed ? "갱신" : "변화 없음")} · 문제 {problems.Count}건");
        }

        /// <summary>데이터베이스 에셋을 찾는다 (없으면 만든다).</summary>
        public static ContentDatabase LoadOrCreate()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(DatabasePath);
            if (db != null)
                return db;
            db = ScriptableObject.CreateInstance<ContentDatabase>();
            AssetDatabase.CreateAsset(db, DatabasePath);
            return db;
        }

        /// <summary>다시 모으고 검사한다. 문제는 콘솔 Error로 한 줄씩 남긴다.</summary>
        public static (int Count, List<ContentProblem> Problems, bool Changed) Refresh()
        {
            var db = LoadOrCreate();
            var collected = AssetDatabase.FindAssets("t:ContentData", new[] { DataRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p, System.StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<ContentData>)
                .Where(c => c != null)
                .ToList();

            var problems = ContentChecks.CheckIds(collected.Select(c => (c.Id, AssetDatabase.GetAssetPath(c))));
            foreach (var p in problems)
                Debug.LogError($"[ContentDatabase] {p}", AssetDatabase.LoadMainAssetAtPath(p.Source));

            bool changed = db.EditorSetContents(collected);
            if (changed)
            {
                EditorUtility.SetDirty(db);
                AssetDatabase.SaveAssetIfDirty(db);
            }
            return (collected.Count, problems, changed);
        }

        // Data 폴더가 바뀌면 바로 다시 모은다. (delayCall은 에디터 창이 포커스를 잃으면 돌지 않아 쓰지 않는다 — TASKS 트러블슈팅)
        // 데이터베이스 에셋 자체의 저장은 다시 모으기를 부르지 않으므로 되풀이되지 않는다.
        private sealed class Watcher : AssetPostprocessor
        {
            private static bool refreshing;

            private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                if (refreshing || EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                bool touchesData = imported.Concat(deleted).Concat(moved).Concat(movedFrom)
                    .Any(p => p.StartsWith(DataRoot + "/", System.StringComparison.Ordinal) && p != DatabasePath);
                if (!touchesData)
                    return;
                refreshing = true;
                try { Refresh(); }
                finally { refreshing = false; }
            }
        }

        // 안전망: 플레이 진입 직전과 빌드 직전에도 다시 모은다 (빌드는 이 목록을 그대로 들고 간다).
        [InitializeOnLoad]
        private static class PlayModeHook
        {
            static PlayModeHook()
            {
                EditorApplication.playModeStateChanged += state =>
                {
                    if (state == PlayModeStateChange.ExitingEditMode)
                        Refresh();
                };
            }
        }

        private sealed class BuildHook : UnityEditor.Build.IPreprocessBuildWithReport
        {
            public int callbackOrder => 0;

            public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report)
            {
                var (_, problems, _) = Refresh();
                if (problems.Count > 0)
                    throw new UnityEditor.Build.BuildFailedException($"콘텐츠 id 문제 {problems.Count}건 — 콘솔의 [ContentDatabase] 오류 확인");
            }
        }
    }
}
#endif
