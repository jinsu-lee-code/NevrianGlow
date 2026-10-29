#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Audio;
using UnityEditor;
using UnityEngine;

namespace NevrianGlowFarm.Editor
{
    /// <summary>
    /// `Assets/_Project/Audio/`의 소리 파일로 SoundLibrary를 채운다 (TASKS 5-5). 파일 이름 = 소리 id + 변형 번호(_01~03).
    /// 여러 번 실행해도 결과가 같다: 클립 목록은 파일 기준으로 다시 맞추고, 손으로 바꾼 볼륨 · 버스 · 3D · 쿨다운은 유지한다.
    /// 처음 보는 id는 asset_placement 6번 기준 기본값으로 만든다. 파일이 모두 사라진 id는 목록에서 뺀다.
    /// 원본(사운드 파트 납품)은 `Assets/Audio/` — 게임용으로 다듬은 사본만 `_Project/Audio/`에 둔다.
    /// </summary>
    public static class SoundLibrarySetup
    {
        private const string AudioRoot = "Assets/_Project/Audio";
        private const string LibraryPath = "Assets/_Project/Data/Audio/SoundLibrary.asset";

        // asset_placement 6번: 손님 · 가게 · 거대 작물은 위치에서 나는 3D
        private static readonly string[] SpatialPrefixes = { "sfx_customer_", "sfx_basket_", "sfx_checkout_", "sfx_giant_" };
        // sound.md B 체크: 손님 6명이 동시에 있어도 시끄럽지 않게
        private static readonly Dictionary<string, float> QuietIds = new Dictionary<string, float>
        {
            { "sfx_customer_step", 0.45f },
            { "sfx_basket_put", 0.6f },
        };

        [MenuItem("Glow Farm/Setup/Build Sound Library")]
        private static void BuildMenu()
        {
            Debug.Log("[Setup] 소리 목록: " + Build() + " · " + AttachPlayerFootsteps() + " · " + AttachLighthouseSound());
        }

        // 등대 공명음 (사운드 납품 10.29, BRANCH_SOUND 4-4) — 아트 등대 소리 칸은 라이브러리가 아니라 원본 파일 그대로. Core 씬 AudioManager 옆에 연결 컴포넌트
        private const string LighthouseFloorClip = "Assets/Audio/SFX/sfx_lighthouse_floor.wav";
        private const string LighthouseEndingClip = "Assets/Audio/SFX/sfx_lighthouse_ending.wav";

        private static string AttachLighthouseSound()
        {
            var audio = Object.FindFirstObjectByType<NevrianGlowFarm.Core.AudioManager>(FindObjectsInactive.Include);
            if (audio == null)
                return "등대 소리: Core 씬이 열려 있지 않아 건너뜀";
            var link = audio.GetComponent<NevrianGlowFarm.Core.LighthouseSoundLink>();
            bool added = link == null;
            if (added)
                link = Undo.AddComponent<NevrianGlowFarm.Core.LighthouseSoundLink>(audio.gameObject);
            var so = new SerializedObject(link);
            so.FindProperty("floorClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(LighthouseFloorClip);
            so.FindProperty("endingClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(LighthouseEndingClip);
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(audio.gameObject.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(audio.gameObject.scene);
            bool ok = so.FindProperty("floorClip").objectReferenceValue != null && so.FindProperty("endingClip").objectReferenceValue != null;
            return (added ? "등대 소리 붙임" : "등대 소리 유지") + (ok ? string.Empty : " (소리 파일 없음 — Sound 브랜치 가져오기 확인)");
        }

        // 플레이어 발소리 (사운드 납품 10.23) — Core 씬이 열려 있으면 Player에 PlayerFootsteps를 붙인다 (있으면 그대로)
        private static string AttachPlayerFootsteps()
        {
            var player = Object.FindFirstObjectByType<NevrianGlowFarm.Systems.Player.PlayerController>(FindObjectsInactive.Include);
            if (player == null)
                return "발소리: Core 씬이 열려 있지 않아 건너뜀";
            if (player.GetComponent<NevrianGlowFarm.Systems.Player.PlayerFootsteps>() != null)
                return "발소리 유지";
            Undo.AddComponent<NevrianGlowFarm.Systems.Player.PlayerFootsteps>(player.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(player.gameObject.scene);
            return "발소리 붙임 (Player)";
        }

        /// <summary>소리 목록을 만든다/갱신한다. 결과 요약을 돌려준다.</summary>
        public static string Build()
        {
            var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p, System.StringComparer.Ordinal)
                .ToList();

            int imported = 0;
            foreach (var path in clips)
                if (ApplyImportSettings(path))
                    imported++;

            var groups = new SortedDictionary<string, List<AudioClip>>(System.StringComparer.Ordinal);
            foreach (var path in clips)
            {
                string id = SoundRules.IdFromFileName(Path.GetFileNameWithoutExtension(path));
                if (!groups.TryGetValue(id, out var list))
                    groups[id] = list = new List<AudioClip>();
                list.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
            }

            var library = AssetDatabase.LoadAssetAtPath<SoundLibraryData>(LibraryPath);
            bool createdAsset = false;
            if (library == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
                library = ScriptableObject.CreateInstance<SoundLibraryData>();
                AssetDatabase.CreateAsset(library, LibraryPath);
                createdAsset = true;
            }

            var so = new SerializedObject(library);
            var entries = so.FindProperty("entries");

            // 기존 항목: 파일이 남아 있으면 클립만 다시 맞추고, 없으면 뺀다
            var existing = new HashSet<string>();
            int removed = 0;
            for (int i = entries.arraySize - 1; i >= 0; i--)
            {
                var e = entries.GetArrayElementAtIndex(i);
                string id = e.FindPropertyRelative("id").stringValue;
                if (!groups.TryGetValue(id, out var list))
                {
                    entries.DeleteArrayElementAtIndex(i);
                    removed++;
                    continue;
                }
                SetClips(e.FindPropertyRelative("clips"), list);
                existing.Add(id);
            }

            // 새 항목: 기본값
            int added = 0;
            foreach (var pair in groups)
            {
                if (existing.Contains(pair.Key))
                    continue;
                entries.InsertArrayElementAtIndex(entries.arraySize);
                var e = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                ApplyDefaults(e, pair.Key);
                SetClips(e.FindPropertyRelative("clips"), pair.Value);
                added++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return $"{(createdAsset ? "에셋 생성 · " : "")}id {groups.Count}개 (새로 {added} · 유지 {existing.Count} · 뺌 {removed}), 클립 {clips.Count}개, 임포트 설정 바꿈 {imported}";
        }

        private static void SetClips(SerializedProperty prop, List<AudioClip> clips)
        {
            prop.ClearArray();
            for (int i = 0; i < clips.Count; i++)
            {
                prop.InsertArrayElementAtIndex(i);
                prop.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            }
        }

        private static void ApplyDefaults(SerializedProperty e, string id)
        {
            bool ui = id.StartsWith("sfx_ui_");
            bool bgm = id.StartsWith("bgm_");
            bool amb = id.StartsWith("amb_");
            bool loop = bgm || amb || id.EndsWith("_loop");

            e.FindPropertyRelative("id").stringValue = id;
            e.FindPropertyRelative("bus").enumValueIndex = (int)(bgm ? SoundBus.Bgm : amb ? SoundBus.Ambience : ui ? SoundBus.Ui : SoundBus.Sfx);
            e.FindPropertyRelative("volume").floatValue = QuietIds.TryGetValue(id, out float v) ? v : 1f;
            e.FindPropertyRelative("spatial").boolValue = SpatialPrefixes.Any(id.StartsWith);
            e.FindPropertyRelative("loop").boolValue = loop;
            e.FindPropertyRelative("pitchJitter").floatValue = (ui || loop) ? 0f : 0.05f; // sound.md 3번 ±5%
            e.FindPropertyRelative("cooldown").floatValue = loop ? 0f : 0.05f;
        }

        /// <summary>sound.md 4번: 효과음 · UI = 모노 · 메모리에 풀어 둠, 배경음 · 환경음 = 스트리밍. 바꿨으면 true.</summary>
        private static bool ApplyImportSettings(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer))
                return false;
            string name = Path.GetFileNameWithoutExtension(path);
            bool stream = name.StartsWith("bgm_") || name.StartsWith("amb_");

            var s = importer.defaultSampleSettings;
            var want = s;
            want.loadType = stream ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            want.compressionFormat = stream ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            want.quality = stream ? 0.7f : 1f;
            bool mono = !stream;

            if (s.loadType == want.loadType && s.compressionFormat == want.compressionFormat && Mathf.Approximately(s.quality, want.quality)
                && importer.forceToMono == mono && !importer.loadInBackground == !stream)
                return false;

            importer.defaultSampleSettings = want;
            importer.forceToMono = mono;
            importer.loadInBackground = stream;
            importer.SaveAndReimport();
            return true;
        }
    }
}
#endif
