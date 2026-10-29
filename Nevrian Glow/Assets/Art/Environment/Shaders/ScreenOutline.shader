// Screen-space toon outline (2026-10-27, research step 3): a Full Screen Pass renderer feature runs this before
// post-processing. Edges come from the depth texture (silhouettes: a tree against the sky, a cliff against the
// forest) and the normals texture (creases: cliff ledges, rock faces), Roberts cross on 4 taps, scaled with distance
// so far shapes keep a thin line instead of turning into noise. The line colour is the scene colour darkened and
// pulled toward a night ink colour, so lines tint with what they sit on (not a flat black). Fades out with distance
// and on the sky. All numbers are my own values.
Shader "Hidden/DSEWA/ScreenOutline"
{
    Properties
    {
        _InkColor ("Ink Colour", Color) = (0.07, 0.05, 0.18, 1)
        _InkMix ("Ink Colour Mix", Range(0, 1)) = 0.55
        _Darken ("Line Darkening", Range(0, 1)) = 0.6
        _Thickness ("Line Thickness (px)", Range(0.5, 3)) = 1.2
        _DepthSens ("Depth Edge Sensitivity", Range(0, 20)) = 6
        _NormalSens ("Normal Edge Sensitivity", Range(0, 4)) = 1.2
        _NormalThreshold ("Normal Edge Threshold", Range(0, 1)) = 0.45
        _FadeStart ("Fade Start (m)", Float) = 60
        _FadeEnd ("Fade End (m)", Float) = 220
        _Strength ("Strength", Range(0, 1)) = 0.8
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off
        Pass
        {
            Name "ScreenOutline"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float4 _InkColor;
            float _InkMix, _Darken, _Thickness, _DepthSens, _NormalSens, _NormalThreshold, _FadeStart, _FadeEnd, _Strength;

            float EyeDepth(float2 uv) { return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams); }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float raw = SampleSceneDepth(uv);
            #if UNITY_REVERSED_Z
                if (raw <= 0.0000001) return col;          // sky
            #else
                if (raw >= 0.9999999) return col;
            #endif
                float d0 = LinearEyeDepth(raw, _ZBufferParams);
                float2 px = _Thickness / _ScaledScreenParams.xy;
                float2 o1 = float2(px.x, px.y), o2 = float2(-px.x, px.y);
                // Roberts cross on depth, relative to the distance (far = same threshold in proportion)
                float dA = EyeDepth(uv + o1), dB = EyeDepth(uv - o1), dC = EyeDepth(uv + o2), dD = EyeDepth(uv - o2);
                float dEdge = sqrt((dA - dB) * (dA - dB) + (dC - dD) * (dC - dD)) / max(d0, 0.1);
                dEdge = saturate((dEdge - 0.02) * _DepthSens);
                // normals
                float3 nA = SampleSceneNormals(uv + o1), nB = SampleSceneNormals(uv - o1), nC = SampleSceneNormals(uv + o2), nD = SampleSceneNormals(uv - o2);
                float nEdge = sqrt(dot(nA - nB, nA - nB) + dot(nC - nD, nC - nD));
                nEdge = saturate((nEdge - _NormalThreshold) * _NormalSens);
                float edge = max(dEdge, nEdge);
                edge *= 1 - smoothstep(_FadeStart, _FadeEnd, d0);
                half3 ink = lerp(col.rgb * (1 - _Darken), _InkColor.rgb, _InkMix);
                col.rgb = lerp(col.rgb, ink, edge * _Strength);
                return col;
            }
            ENDHLSL
        }
    }
}
