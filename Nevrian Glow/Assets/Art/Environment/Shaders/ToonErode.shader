// Toon erosion particles for the waterfall splash / foam (2026-10-26, research: Season "how we made the waterfall" -
// two noises x radial gradient, erosion driven over the particle's life; realtimevfx stylized splashes - noise +
// erosion value clamped into the opacity mask; a torus-like crown mesh eroded from its tips).
// Particle colour alpha is the EROSION value: 1 = whole shape, 0 = fully eaten (animate it with Colour over Lifetime).
// Shape: billboards use a radial gradient from the quad centre; the crown mesh (_Shape = 1) uses v (0 bottom, 1 rim)
// so the rim tips go first. Two flat tones (edge / core). The Designer mask: G = round foam blobs, B = erosion noise.
// All defaults are my own values.
Shader "DSEWA/ToonErode"
{
    Properties
    {
        _MaskTex ("Mask (R streaks, G foam blobs, B erosion)", 2D) = "gray" {}
        _EdgeColor ("Edge Tone", Color) = (0.55, 0.78, 1, 1)
        _NoiseTiling ("Noise Tiling", Float) = 0.6
        _NoiseScroll ("Noise Scroll (per second)", Vector) = (0, -0.3, 0, 0)
        [Toggle] _Shape ("Shape From V (crown mesh) instead of radial", Float) = 0
        _EdgeWidth ("Edge Tone Width", Range(0, 0.3)) = 0.12
        _Glow ("Glow", Range(0, 2)) = 0.3
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Art/Environment/Shaders/GroundFog.hlsl"

            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MaskTex_ST;
                half4 _EdgeColor;
                float _NoiseTiling, _Shape;
                float4 _NoiseScroll;
                half _EdgeWidth, _Glow;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; half4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 sc = _NoiseScroll.xy * _Time.y;
                float ero = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv * _NoiseTiling + sc).b;
                float blob = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv * _NoiseTiling * 1.7 + sc * 1.6 + 0.27).g;
                float shape = _Shape > 0.5 ? 1 - i.uv.y * 0.85 : saturate(1 - length(i.uv - 0.5) * 2);
                float m = shape * (0.45 + 0.35 * ero + 0.2 * blob);                  // 0..1 "how solid"
                float erosion = 1 - i.color.a;                                          // grows over the life
                float v = m - erosion * 0.9;
                float w = fwidth(v) + 0.005;
                half a = smoothstep(0.08 - w, 0.08 + w, v);
                half core = smoothstep(0.08 + _EdgeWidth - w, 0.08 + _EdgeWidth + w, v);
                half3 col = lerp(_EdgeColor.rgb, i.color.rgb, core) * (1 + _Glow);
                col = ApplyGroundFog(col, i.positionWS);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
