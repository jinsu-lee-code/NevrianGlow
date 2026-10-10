#if UNITY_EDITOR
using System;
using System.IO;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Systems.Player;
using UnityEditor;
using UnityEngine;

namespace NevrianGlowFarm.Editor
{
    /// <summary>"Glow Farm 디버그" 창. 디버그 기능은 게임 화면이 아니라 이 창에만 둔다.</summary>
    public class GlowFarmDebugWindow : EditorWindow
    {
        [MenuItem("Glow Farm/디버그 창")]
        private static void Open()
        {
            GetWindow<GlowFarmDebugWindow>("Glow Farm 디버그");
        }

        private Hotbar hotbar;

        private void OnGUI()
        {
            DrawLogSection();
            EditorGUILayout.Space();
            DrawHotbarSection();
        }

        private void DrawHotbarSection()
        {
            EditorGUILayout.LabelField("핫바", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                hotbar = null;
                EditorGUILayout.HelpBox("플레이 중일 때 핫바 칸 수를 바꿀 수 있다 (스킬 핫바 확장 확인용).", MessageType.None);
                return;
            }

            if (hotbar == null)
                hotbar = FindFirstObjectByType<Hotbar>();
            if (hotbar == null)
            {
                EditorGUILayout.HelpBox("씬에 Hotbar가 없다.", MessageType.Warning);
                return;
            }

            var item = hotbar.SelectedItem;
            EditorGUILayout.LabelField("칸 수 · 선택", $"{hotbar.SlotCount}칸 · {hotbar.SelectedIndex + 1}번 ({(item != null ? item.DisplayName : "빈 칸")})");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("칸 −1"))
                    hotbar.SetSlotCount(hotbar.SlotCount - 1);
                if (GUILayout.Button("칸 +1"))
                    hotbar.SetSlotCount(hotbar.SlotCount + 1);
            }
        }

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying)
                Repaint();
        }

        private void DrawLogSection()
        {
            EditorGUILayout.LabelField("로그 (LogCollector)", EditorStyles.boldLabel);

            string folder = LogCollector.GetLogFolder();
            EditorGUILayout.SelectableLabel(folder, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

            if (GUILayout.Button("로그 폴더 열기"))
            {
                Directory.CreateDirectory(folder);
                Application.OpenURL("file:///" + folder.Replace('\\', '/'));
            }

            var collector = LogCollector.Instance;
            if (!Application.isPlaying || collector == null)
            {
                EditorGUILayout.HelpBox("플레이 중(Core 씬 포함)일 때 이번 세션 파일과 테스트 버튼이 보인다.", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField("이번 세션 파일", Path.GetFileName(collector.SessionFilePath));

            if (GUILayout.Button("테스트 예외 발생 (같은 예외 1회 추가)"))
                Debug.LogException(new InvalidOperationException("[테스트] LogCollector 확인용 예외"));
        }
    }
}
#endif
