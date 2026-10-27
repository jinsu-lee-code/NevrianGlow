// Star glare (2026-10-26, user: "light spreading out radially, as a post effect"): a Full Screen Pass renderer feature
// runs this before post-processing on the HDR scene colour. Every pixel brighter than the threshold (the glowing
// flower orbs, fruit) throws thin rays in _Rays directions, like a camera star filter; each ray keeps the colour of
// the light it comes from and fades with distance. Bloom afterwards softens it. All numbers are my own values.
Shader "Hidden/DSEWA/StarGlare"
{
    Properties
    {
        _Threshold ("Brightness Threshold (HDR)", Float) = 1.6
        _Intensity ("Ray Intensity", Range(0, 4)) = 1
        _Length ("Ray Length (fraction of screen height)", Range(0.01, 0.3)) = 0.08
        _Rays ("Ray Directions (each gives two opposite rays)", Range(1, 6)) = 3
        _Rotation ("Rotation (degrees)", Range(0, 180)) = 15
        _Spin ("Slow Spin (degrees per second)", Float) = 2
        _Saturation ("Ray Saturation Boost", Range(0, 2)) = 1.2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off
        Pass
        {
            Name "StarGlare"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float _Threshold, _Intensity, _Length, _Rays, _Rotation, _Spin, _Saturation;
            float _DSEWA_GlareDim;   // set by GlareFatigue: 0 = full rays, 1 = gone (2026-10-27)

            half3 Bright(float2 uv)
            {
                float raw = SampleSceneDepth(uv);
            #if UNITY_REVERSED_Z
                if (raw <= 0.0000001) return 0;                     // the sky is not a light source here
            #else
                if (raw >= 0.9999999) return 0;
            #endif
                half3 c = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
                half m = max(c.r, max(c.g, c.b));
                return c * saturate((m - _Threshold) / max(m, 1e-3));   // keeps the hue of the light
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                const int STEPS = 12;
                int dirs = (int)clamp(_Rays, 1, 6);
                float aspect = _ScaledScreenParams.y / _ScaledScreenParams.x;
                float baseA = radians(_Rotation + _Time.y * _Spin);
                half3 acc = 0;
                if (_DSEWA_GlareDim >= 0.999) return col;
                [loop] for (int d = 0; d < dirs; d++)
                {
                    float a = baseA + d * PI / dirs;
                    float2 dir = float2(cos(a) * aspect, sin(a)) * (_Length / STEPS);
                    [unroll] for (int s = 1; s <= STEPS; s++)
                    {
                        float w = 1 - (s - 0.5) / STEPS; w *= w;
                        acc += (Bright(uv + dir * s) + Bright(uv - dir * s)) * w;
                    }
                }
                acc *= _Intensity / (dirs * STEPS * 0.35) * (1 - saturate(_DSEWA_GlareDim));
                half lum = dot(acc, half3(0.2126, 0.7152, 0.0722));
                acc = max(lerp(lum.xxx, acc, _Saturation), 0);
                col.rgb += acc;
                return col;
            }
            ENDHLSL
        }
    }
}
