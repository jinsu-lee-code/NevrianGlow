#if UNITY_EDITOR
using NevrianGlowFarm.Systems.Cleaning;
using UnityEditor;
using UnityEngine;

namespace NevrianGlowFarm.Editor
{
    /// <summary>청소 인스펙터 (노션 GDD 12.4): "배치 미리보기" — 그날 쓰레기 · 얼룩이 놓일 자리를 씬 기즈모로만 보여 준다.</summary>
    [CustomEditor(typeof(CleaningSystem))]
    public class CleaningSystemEditor : UnityEditor.Editor
    {
        private int previewDay = 1;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var cleaning = (CleaningSystem)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("배치 미리보기 (씬 뷰 초록 점 = 쓰레기, 초록 원 = 얼룩)", EditorStyles.boldLabel);
            if (cleaning.Settings == null)
            {
                EditorGUILayout.HelpBox("CleaningSettings를 먼저 연결한다.", MessageType.Warning);
                return;
            }

            previewDay = Mathf.Max(1, EditorGUILayout.IntField("일차", previewDay));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("배치 미리보기"))
                {
                    cleaning.Preview(previewDay);
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("지우기"))
                {
                    cleaning.ClearPreview();
                    SceneView.RepaintAll();
                }
            }
            if (cleaning.PreviewTrashCount + cleaning.PreviewStainCount > 0)
                EditorGUILayout.LabelField("미리보기", $"쓰레기 {cleaning.PreviewTrashCount}개 · 얼룩 {cleaning.PreviewStainCount}개");

            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("지금 청결도", $"{cleaning.CurrentCleanliness():P0} (바닥 쓰레기 {cleaning.FloorTrashCount()} · 얼룩 양 {cleaning.StainAmountSum():0.##})");
                Repaint();
            }
        }
    }
}
#endif
