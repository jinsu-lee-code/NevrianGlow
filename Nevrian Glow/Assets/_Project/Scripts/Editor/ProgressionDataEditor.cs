#if UNITY_EDITOR
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Game;
using UnityEditor;
using UnityEngine;

namespace NevrianGlowFarm.Editor
{
    /// <summary>경험치 · 레벨 인스펙터 (노션 GDD 12.4): 기본 필드 + Lv1~10 표 (다음까지 · 누적 경험치 = 누적 매출 · 누적 SP, BALANCE 3-2).</summary>
    [CustomEditor(typeof(ProgressionData))]
    public class ProgressionDataEditor : UnityEditor.Editor
    {
        private const int MaxLevel = 10;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var data = (ProgressionData)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"레벨 표 (Lv1~{MaxLevel}, 필요량 = 기본 + 레벨당 증가 × 현재 레벨)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("레벨", EditorStyles.miniBoldLabel, GUILayout.Width(50));
                EditorGUILayout.LabelField("다음까지", EditorStyles.miniBoldLabel, GUILayout.Width(80));
                EditorGUILayout.LabelField("누적 경험치(=누적 매출)", EditorStyles.miniBoldLabel, GUILayout.Width(150));
                EditorGUILayout.LabelField("누적 SP", EditorStyles.miniBoldLabel, GUILayout.Width(60));
            }
            foreach (var row in Progression.Table(data.ToCurve(), MaxLevel))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(row.Level.ToString(), GUILayout.Width(50));
                    EditorGUILayout.LabelField(row.ToNext.ToString("N0"), GUILayout.Width(80));
                    EditorGUILayout.LabelField(row.CumulativeExp.ToString("N0"), GUILayout.Width(150));
                    EditorGUILayout.LabelField(row.CumulativeSkillPoints.ToString(), GUILayout.Width(60));
                }
            }
        }
    }
}
#endif
