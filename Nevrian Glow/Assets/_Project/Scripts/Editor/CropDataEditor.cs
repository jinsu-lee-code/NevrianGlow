#if UNITY_EDITOR
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Farm;
using UnityEditor;
using UnityEngine;

namespace NevrianGlowFarm.Editor
{
    /// <summary>
    /// 작물 인스펙터 (노션 GDD 12.5): 기본 필드 + 계산값(칸당 일일 이익 · 한 포기 순이익 · 전체 날 수, BALANCE 3-5)
    /// + 성장 타임라인 막대(첫 성장 · 재수확 구간 · 수확 날). 계산은 Logic CropEconomics — 측정 봇과 같은 식.
    /// </summary>
    [CustomEditor(typeof(CropData))]
    public class CropDataEditor : UnityEditor.Editor
    {
        private static readonly Color GrowColor = new Color(0.38f, 0.95f, 0.88f, 0.85f);   // 시안 — 첫 성장
        private static readonly Color RegrowColor = new Color(0.63f, 0.55f, 1f, 0.85f);    // 보라 — 재수확
        private static readonly Color HarvestColor = new Color(1f, 0.82f, 0.48f);          // 금빛 — 수확 날
        private const float BarHeight = 18f;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var crop = (CropData)target;
            var spec = crop.ToSpec();
            int days = CropEconomics.TotalDays(spec);
            int harvests = CropEconomics.HarvestCount(spec);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("계산값 (BALANCE 3-5, 스킬 · 비료 · 거대 작물 제외)", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("칸당 일일 이익", $"{CropEconomics.ProfitPerCellPerDay(spec, crop.SeedPrice, crop.SellPrice):0.##} 달러/일");
                EditorGUILayout.TextField("한 포기 순이익", $"{CropEconomics.ProfitPerPlant(spec, crop.SeedPrice, crop.SellPrice)} 달러 (수확 {harvests}회 × {spec.HarvestYield}개)");
                EditorGUILayout.TextField("심기 → 마지막 수확", $"{days}일");
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("성장 타임라인 (심은 날 = 0일, 매일 물을 준다고 가정)", EditorStyles.boldLabel);
            DrawTimeline(spec, days, harvests);
        }

        private static void DrawTimeline(CropSpec spec, int days, int harvests)
        {
            Rect bar = GUILayoutUtility.GetRect(0f, BarHeight + 16f, GUILayout.ExpandWidth(true));
            bar.height = BarHeight;
            EditorGUI.DrawRect(bar, new Color(0f, 0f, 0f, 0.25f));
            if (days <= 0)
                return;

            float perDay = bar.width / days;
            float x = bar.x;
            DrawSegment(ref x, bar, spec.GrowDays * perDay, GrowColor, $"성장 {spec.GrowDays}일");
            for (int i = 1; i < harvests; i++)
                DrawSegment(ref x, bar, spec.RegrowDays * perDay, RegrowColor, $"+{spec.RegrowDays}");

            // 수확 날 표시 (구간 끝마다)
            var label = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperCenter };
            int day = spec.GrowDays;
            for (int i = 0; i < harvests; i++)
            {
                float hx = bar.x + day * perDay;
                EditorGUI.DrawRect(new Rect(hx - 1f, bar.y - 2f, 2f, BarHeight + 4f), HarvestColor);
                GUI.Label(new Rect(hx - 20f, bar.yMax, 40f, 14f), $"{day}일", label);
                day += spec.RegrowDays;
            }
        }

        private static void DrawSegment(ref float x, Rect bar, float width, Color color, string text)
        {
            var r = new Rect(x, bar.y, Mathf.Max(0f, width - 1f), bar.height);
            EditorGUI.DrawRect(r, color);
            var style = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.black } };
            if (r.width > 30f)
                GUI.Label(r, text, style);
            x += width;
        }
    }
}
#endif
