// Toon waterfall ribbon (2026-10-26, user: "make hills, cliffs and a waterfall"; v2 after the reference research:
// Unity "nature shaders", RiME-style Unity series (80.lv), Season "how we made the waterfall" (realtimevfx)).
//  - streaks: the Designer mask (R = vertical streaks) panned down twice, the second copy flipped in U and faster,
//    added (RiME series), cut into three flat tones (deep / mid / foam)
//  - acceleration: v is raised to a power < 1 before panning, so the pattern is squeezed at the lip and runs faster
//    lower down (Season)
//  - foam: round blurred blobs (mask G, Season's "hand-painted round tiles") at the lip and at the bottom
//  - edges: alpha-tested, not blended - the side edges are eaten by the erosion noise (mask B), so the sheet tears
//    instead of fading and there is no transparency sorting (RiME series, Season)
//  - volume: side shade + highlight band (v1), vertex wobble along the normal from the erosion noise, stronger at the
//    bottom (RiME series), a capped fresnel glow for light through thin water (RiME series)
// Mesh UV: u across (0..1), v down the fall (0 = lip, 1 = bottom). All defaults are my own values.
Shader "DSEWA/ToonWaterfall"
{
    Properties
    {
        _MaskTex ("Mask (R streaks, G foam blobs, B erosion)", 2D) = "gray" {}
        _DeepColor ("Deep Water", Color) = (0.16, 0.28, 0.70, 1)
        _MidColor ("Mid Water", Color) = (0.38, 0.58, 0.95, 1)
        _FoamColor ("Foam", Color) = (0.85, 0.94, 1, 1)
        _MidLevel ("Mid Tone Above", Range(0, 1)) = 0.42
        _FoamLevel ("Foam Above", Range(0, 1)) = 0.68
        _StreakScale ("Streak Tiling (x across, y along)", Vector) = (2, 1.5, 0, 0)
        _Speed ("Fall Speed (v per second)", Float) = 0.9
        _Layer2 ("Second Streak Layer (0 = one layer, 1 = two crossed layers)", Range(0, 1)) = 1
        _VPower ("Acceleration (v power, < 1 = faster lower down)", Range(0.3, 1)) = 0.65
        _FoamTop ("Foam At Lip (v)", Range(0, 0.5)) = 0.07
        _FoamBottom ("Foam At Bottom (v from the end)", Range(0, 0.5)) = 0.14
        _EdgeSoft ("Side Edge Width (u)", Range(0.01, 0.5)) = 0.14
        _EdgeBreak ("Edge Tearing (erosion noise)", Range(0, 1)) = 0.45
        _StreakCut ("Streak Cutout (highlight layer: keep only bright streaks)", Range(0, 1)) = 0
        _Glow ("Glow (night readability)", Range(0, 2)) = 0.35
        [Header(Volume)]
        _SideShade ("Side Shade (darker towards the sides)", Range(0, 1)) = 0
        _HighlightU ("Highlight Band Position (u)", Range(0, 1)) = 0.38
        _HighlightWidth ("Highlight Band Width", Range(0, 0.3)) = 0.07
        _HighlightAmount ("Highlight Band Amount", Range(0, 1)) = 0
        _Wobble ("Vertex Wobble (m, along the normal)", Range(0, 0.5)) = 0
        _FresnelAmount ("Fresnel Glow (thin water)", Range(0, 1)) = 0
        _FresnelMax ("Fresnel Glow Cap", Range(0, 1)) = 0.35
        // unused since v2 (kept so older materials keep their values)
        [HideInInspector] _Alpha ("Opacity", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" }
        // shared by the colour pass and the depth passes (URP primes depth with DepthOnly / DepthNormals and then
        // draws opaque / alpha-tested objects with an Equal depth test: without matching depth passes the sheet vanishes)
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Art/Environment/Shaders/GroundFog.hlsl"      // same distance haze as terrain / props

            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MaskTex_ST;
                half4 _DeepColor, _MidColor, _FoamColor;
                half _MidLevel, _FoamLevel, _FoamTop, _FoamBottom, _EdgeSoft, _EdgeBreak, _StreakCut, _Glow, _Alpha;
                half _SideShade, _HighlightU, _HighlightWidth, _HighlightAmount, _Wobble, _FresnelAmount, _FresnelMax;
                float4 _StreakScale;
                float _Speed, _VPower, _Layer2;
            CBUFFER_END

            // uv2.x (optional) = rapids: extra foam where the river breaks up before the falls (0 on meshes without uv2)
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 normalWS : TEXCOORD2; float rapids : TEXCOORD3; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float t = _Time.y * _Speed;
                // wobble: erosion noise panned with the water, 0.5 = no move, bottom moves most
                float w = SAMPLE_TEXTURE2D_LOD(_MaskTex, sampler_MaskTex, float2(v.uv.x * 1.3, v.uv.y * 0.8 - t * 0.6), 0).b;
                float3 pos = v.positionOS.xyz + v.normalOS * (w - 0.5) * 2 * _Wobble * (0.25 + 0.75 * v.uv.y);
                o.positionWS = TransformObjectToWorld(pos);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.rapids = v.uv2.x;
                return o;
            }

            // streak value (with foam bands) and the alpha test; returns n
            float WaterStreaks(float2 uv, float rapids)
            {
                float t = _Time.y * _Speed;
                float va = pow(max(uv.y, 0), _VPower);                                       // acceleration (not clamped: the river mesh runs v 0..14)
                float s1 = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, float2(uv.x * _StreakScale.x, va * _StreakScale.y - t)).r;
                float s2 = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, float2(-uv.x * _StreakScale.x * 1.37 + 0.31, va * _StreakScale.y * 0.83 - t * 1.31)).r;
                float n = lerp(s1, (s1 + s2) * 0.5, _Layer2);                              // fewer overlapping layers = calmer
                float blobs = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, float2(uv.x * 2.5, va * 2.0 - t * 0.7)).g;
                // foam bands only where they are switched on (a zero width would turn every v > 1 into foam - the
                // river mesh runs v 0..14)
                float band = (_FoamTop > 0 ? 1 - smoothstep(0, _FoamTop, uv.y) : 0)
                           + (_FoamBottom > 0 ? smoothstep(1 - _FoamBottom, 1, uv.y) : 0);
                band += rapids;
                n = saturate(n + band * (0.35 + blobs * 0.65));
                // alpha test: side edges torn by the erosion noise; optional streak cutout for the highlight layer
                float ero = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, float2(uv.x * 1.7 + 0.5, va * 1.1 - t * 0.8)).b;
                float edge = min(uv.x, 1 - uv.x) / max(_EdgeSoft, 1e-3);
                clip(edge - 0.5 + (ero - 0.5) * 2 * _EdgeBreak);
                clip(n - _StreakCut);
                return n;
            }
        ENDHLSL

        Pass
        {
            Name "Waterfall"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            half4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float n = WaterStreaks(i.uv, i.rapids);
                float w = fwidth(n) + 0.01;
                half3 col = lerp(_DeepColor.rgb, _MidColor.rgb, smoothstep(_MidLevel - w, _MidLevel + w, n));
                col = lerp(col, _FoamColor.rgb, smoothstep(_FoamLevel - w, _FoamLevel + w, n));
                // volume: side shade step + highlight band
                float side = abs(i.uv.x - 0.5) * 2;
                col *= 1 - _SideShade * step(0.62, side + (n - 0.5) * 0.25);
                float hl = 1 - smoothstep(_HighlightWidth * 0.6, _HighlightWidth, abs(i.uv.x - _HighlightU) + (n - 0.5) * 0.05);
                col = lerp(col, _FoamColor.rgb, hl * _HighlightAmount);
                // thin-water fresnel glow (capped)
                float3 N = normalize(i.normalWS) * (front ? 1 : -1);
                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                col += _FoamColor.rgb * min(pow(1 - saturate(dot(N, V)), 3) * _FresnelAmount, _FresnelMax);
                col *= 1 + _Glow;
                col = ApplyGroundFog(col, i.positionWS);
                return half4(col, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragDepth
            #pragma multi_compile_instancing
            half4 fragDepth(Varyings i) : SV_Target { WaterStreaks(i.uv, i.rapids); return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragNormals
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 fragNormals(Varyings i) : SV_Target
            {
                WaterStreaks(i.uv, i.rapids);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = PackNormalOctQuadEncode(normalize(i.normalWS));
                return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
            #else
                return half4(NormalizeNormalPerPixel(i.normalWS), 0);
            #endif
            }
            ENDHLSL
        }
    }
}
