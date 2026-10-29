// Toon foam ripples on the water where the waterfall lands (2026-10-26, research: RiME-style Unity series part 1 -
// a disc mesh with radial UVs and a panned texture; Unity "nature shaders" - ripple disc with a tiled noise mask).
// Mesh (Mixar, env_waterfall_bg_ripple): u = angle (4 repeats), v = sqrt(radius) so a v-pan runs fast at the centre and
// slows towards the rim; vertex colour = rim fade. The Designer mask's round foam blobs (G) are panned outwards and
// cut into flat foam patches; the erosion noise (B) eats them more and more towards the rim.
// All defaults are my own values.
Shader "DSEWA/ToonRipple"
{
    Properties
    {
        _MaskTex ("Mask (R streaks, G foam blobs, B erosion)", 2D) = "gray" {}
        _FoamColor ("Foam", Color) = (0.85, 0.94, 1, 1)
        _EdgeColor ("Foam Edge (second tone)", Color) = (0.55, 0.78, 1, 1)
        _Tiling ("Blob Tiling (x round, y outwards)", Vector) = (1, 1.6, 0, 0)
        _Speed ("Outward Speed (v per second)", Float) = 0.35
        _Cut ("Foam Cut (higher = less foam)", Range(0, 1)) = 0.55
        _EdgeWidth ("Second Tone Width", Range(0, 0.3)) = 0.08
        _RimErode ("Rim Erosion", Range(0, 2)) = 1
        _Glow ("Glow", Range(0, 2)) = 0.3
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-10" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
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
                half4 _FoamColor, _EdgeColor;
                float4 _Tiling;
                float _Speed;
                half _Cut, _EdgeWidth, _RimErode, _Glow;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; half fade : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                o.fade = v.color.r;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 uv = float2(i.uv.x * _Tiling.x, i.uv.y * _Tiling.y - t);
                float blobs = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, uv).g;
                float blobs2 = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, uv * float2(1.3, 0.8) + float2(0.37, -t * 0.4)).g;
                float ero = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv * float2(1.5, 1.2) + float2(0, -t * 0.5)).b;
                float m = max(blobs, blobs2 * 0.9);
                // eaten towards the rim (vertex colour fade + erosion noise)
                m -= (1 - i.fade) * _RimErode * (0.6 + ero * 0.8);
                float w = fwidth(m) + 0.005;
                half a = smoothstep(_Cut - w, _Cut + w, m);
                half inner = smoothstep(_Cut + _EdgeWidth - w, _Cut + _EdgeWidth + w, m);
                half3 col = lerp(_EdgeColor.rgb, _FoamColor.rgb, inner) * (1 + _Glow);
                col = ApplyGroundFog(col, i.positionWS);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
