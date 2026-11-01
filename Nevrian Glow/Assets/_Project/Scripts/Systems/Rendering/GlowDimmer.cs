using UnityEngine;

namespace NevrianGlowFarm.Systems.Rendering
{
    /// <summary>
    /// 아트 발광 재질의 세기를 이 오브젝트 아래에서만 줄인다 (버그 리포트 10/30 — 발광 도구 · 농장 문 입구 빛이 너무 셈).
    /// 재질 파일은 그대로 두고 렌더러 · 재질 칸마다 속성 블록으로 _EmissionColor · _TexGlowStrength(아트 툰 셰이더) ·
    /// 1을 넘는 _BaseColor(빛나는 Unlit — 문 위 등불)에 곱한다. 손에 든 도구(HeldItemView)도 같은 함수를 쓴다.
    /// </summary>
    public class GlowDimmer : MonoBehaviour
    {
        private static readonly int TexGlowId = Shader.PropertyToID("_TexGlowStrength");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("조정값")]
        [Tooltip("발광 세기 — 아트 재질 값에 곱한다 (배)")]
        [SerializeField, Range(0f, 1f)] private float scale = 0.45f;

        private void Start() => Apply(gameObject, scale);

        /// <summary>root 아래 렌더러의 발광 재질 세기에 scale을 곱한다 (파티클 제외, 발광 없는 재질은 그대로).</summary>
        public static void Apply(GameObject root, float scale)
        {
            if (root == null || scale >= 1f)
                return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer)
                    continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null)
                        continue;
                    bool glow = m.HasProperty(TexGlowId) && m.GetFloat(TexGlowId) > 0f;
                    bool emission = m.HasProperty(EmissionId) && m.GetColor(EmissionId).maxColorComponent > 0f;
                    bool hdrBase = m.HasProperty(BaseColorId) && m.GetColor(BaseColorId).maxColorComponent > 1f;
                    if (!glow && !emission && !hdrBase)
                        continue;
                    var block = new MaterialPropertyBlock();
                    r.GetPropertyBlock(block, i);
                    if (glow)
                        block.SetFloat(TexGlowId, m.GetFloat(TexGlowId) * scale);
                    if (emission)
                        block.SetColor(EmissionId, m.GetColor(EmissionId) * scale);
                    if (hdrBase)
                    {
                        var c = m.GetColor(BaseColorId);
                        block.SetColor(BaseColorId, new Color(c.r * scale, c.g * scale, c.b * scale, c.a));
                    }
                    r.SetPropertyBlock(block, i);
                }
            }
        }
    }
}
