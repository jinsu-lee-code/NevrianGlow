// Moon light shafts (2026-10-29, background post step 6): a Full Screen Pass renderer feature runs this before
// post-processing, after the star glare, so the bloom picks the rays up. MoonModel publishes the moon's viewport
// position per camera (_DSEWA_MoonScreen, z = 1 while the moon is drawn). Each pixel marches toward the moon and
// gathers the bright SKY behind it (depth past Sky From, so the cloud meshes count as sky): where trees, the
// lighthouse or cliffs cut the bright sky the gaps become rays. Fades as the moon leaves the screen, skips HDR
// glows' own pixels from feeding the rays (only the sky does), and is off while the camera is in the shop box
// (InteriorFill). All numbers are my own values.
Shader "Hidden/DSEWA/LightShafts"
{
    Properties
    {
        [HDR] _Tint ("Ray Colour", Color) = (0.75, 0.78, 1.0, 1)
        _Strength ("Strength", Range(0, 2)) = 0.35
        _Threshold ("Sky Brightness From (HDR luminance)", Float) = 0.35
        _ThresholdSoft ("Sky Brightness Soft", Float) = 0.35
        _Length ("Ray Length (fraction of the way to the moon)", Range(0.1, 1)) = 0.7
        _Decay ("Decay per Step", Range(0.8, 1)) = 0.95
        _SkyFrom ("Counts As Sky From (m)", Float) = 380
        _EdgeFade ("Fade As The Moon Leaves The Screen", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off
        Pass
        {
            Name "LightShafts"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #define STEPS 24
            float4 _Tint;
            float _Strength, _Threshold, _ThresholdSoft, _Length, _Decay, _SkyFrom, _EdgeFade;
            float4 _DSEWA_MoonScreen;
            float4 _DSEWA_InteriorBoxMin, _DSEWA_InteriorBoxMax;

            bool CameraInShop()
            {
                float3 c = _WorldSpaceCameraPos;
                return _DSEWA_InteriorBoxMax.x > _DSEWA_InteriorBoxMin.x && all(c >= _DSEWA_InteriorBoxMin.xyz) && all(c <= _DSEWA_InteriorBoxMax.xyz);
            }

            // bright sky at uv (0 on anything closer than the sky)
            float SkyLight(float2 uv, out float3 c)
            {
                c = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
                float raw = SampleSceneDepth(uv);
            #if UNITY_REVERSED_Z
                float sky = raw <= 0.0000001 ? 1 : step(_SkyFrom, LinearEyeDepth(raw, _ZBufferParams));
            #else
                float sky = raw >= 0.9999999 ? 1 : step(_SkyFrom, LinearEyeDepth(raw, _ZBufferParams));
            #endif
                float lum = dot(c, float3(0.2126, 0.7152, 0.0722));
                return sky * smoothstep(_Threshold, _Threshold + _ThresholdSoft, lum);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (_DSEWA_MoonScreen.z < 0.5 || CameraInShop()) return col;
                float2 moon = _DSEWA_MoonScreen.xy;
                // fade out as the moon leaves the screen (and is gone a little past the edge)
                float2 off = max(abs(moon - 0.5) - 0.5, 0);
                float onScreen = 1 - saturate(length(off) / max(_EdgeFade, 1e-3));
                if (onScreen <= 0) return col;

                float2 delta = (moon - uv) * _Length / STEPS;
                float2 p = uv; float w = 1; float3 acc = 0; float wsum = 0;
                [unroll] for (int i = 0; i < STEPS; i++)
                {
                    p += delta;
                    float3 c; float m = SkyLight(p, c);
                    acc += c * m * w; wsum += w; w *= _Decay;
                }
                acc /= max(wsum, 1e-4);
                col.rgb += acc * _Tint.rgb * _Strength * onScreen;
                return col;
            }
            ENDHLSL
        }
    }
}
