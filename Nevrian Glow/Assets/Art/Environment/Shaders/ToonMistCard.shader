// DSEWA/ToonMistCard: a bank of mist lying in low ground, as a small volume (2026-11-01; camera-facing cards turned with the
// view and flat sheets vanished edge-on, so the view ray now marches through a dome fixed in the world). Put it on a cube:
// the dome fills the box, its top rises and falls with drifting noise. The march stops at the scene depth, so the bank
// melts into the ground by itself and thins as the camera walks in, with nothing cleared round the camera.
// All defaults are my own starting values.
Shader "DSEWA/ToonMistCard"
{
    Properties
    {
        _NoiseMap ("Drift Noise", 2D) = "gray" {}
        [HDR] _Color ("Bottom Colour (linear)", Color) = (0.5, 0.46, 0.76, 1)
        [HDR] _TopColor ("Top Colour", Color) = (0.68, 0.6, 0.84, 1)
        _Density ("Density (per metre)", Range(0, 0.5)) = 0.06
        _TopMin ("Top Height Where The Noise Is Lowest (fraction)", Range(0, 1)) = 0.3
        _NoiseScale ("Noise Size (m per tile)", Float) = 26
        _NoiseDrift ("Noise Drift (m per second)", Float) = 0.5
        _Scatter ("Sun Scatter (glow looking towards the sun)", Range(0, 3)) = 0.4
        _ScatterTight ("Scatter Tightness", Range(0, 0.95)) = 0.62
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ToonMistCard"
            Tags { "LightMode" = "UniversalForward" }
            Cull Front ZWrite Off ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/Art/Environment/Shaders/GroundFog.hlsl"   // the mist lifts with the grey (WorldWakeFogScale)

            #define MIST_STEPS 14

            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseMap_ST, _Color, _TopColor;
                float _Density, _TopMin, _NoiseScale, _NoiseDrift, _Scatter, _ScatterTight;
            CBUFFER_END
            float _DSEWA_HazeCut;   // LightUpDirector's high top view thins the haze (1 = off)

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            float MistPhase(float3 viewDir)
            {
                float g = _ScatterTight, c = dot(viewDir, normalize(_MainLightPosition.xyz));
                return pow((1 - g) * (1 - g) / max(1 + g * g - 2 * g * c, 1e-4), 1.5);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(i.positionCS);
            #if UNITY_REVERSED_Z
                float raw = SampleSceneDepth(uv);
            #else
                float raw = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
            #endif
                float3 wp = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);

                // the ray in the box's space, t = 0 at the camera, 1 at the scene
                float3 ro = TransformWorldToObject(_WorldSpaceCameraPos);
                float3 rd = TransformWorldToObject(wp) - ro;
                float3 inv = 1 / (rd + 1e-6);
                float3 ta = (-0.5 - ro) * inv, tb = (0.5 - ro) * inv, tmin = min(ta, tb), tmax = max(ta, tb);
                float tn = max(max(max(tmin.x, tmin.y), tmin.z), 0), tf = min(min(min(tmax.x, tmax.y), tmax.z), 1);
                if (tf <= tn) discard;

                float jitter = frac(52.9829189 * frac(dot(i.positionCS.xy, float2(0.06711056, 0.00583715))));
                float dt = (tf - tn) * distance(wp, _WorldSpaceCameraPos) / MIST_STEPS;
                float tau = 0, heightSum = 0;
                for (int s = 0; s < MIST_STEPS; s++)
                {
                    float3 p = ro + rd * lerp(tn, tf, (s + jitter) / MIST_STEPS);
                    float3 w = TransformObjectToWorld(p);
                    float2 nuv = (w.xz + _Time.y * _NoiseDrift) / max(_NoiseScale, 0.1);
                    float n = SAMPLE_TEXTURE2D_LOD(_NoiseMap, sampler_NoiseMap, nuv, 0).r;
                    float r = length(p.xz) * 2, h = p.y + 0.5;
                    float top = saturate(1 - r * r) * lerp(_TopMin, 1, smoothstep(0.3, 0.9, n));
                    float dens = saturate((top - h) / max(top * 0.7, 0.001));
                    tau += dens * _Density * dt;
                    heightSum += dens * h;
                }
                float a = (1 - exp(-tau)) * WorldWakeFogScale(wp) * (1 - saturate(_DSEWA_HazeCut));
                if (a <= 0.002) discard;

                float h = saturate(heightSum * _Density * dt / max(tau, 1e-4) * 1.6);
                half3 col = lerp(_Color.rgb, _TopColor.rgb, h);
                col += lerp(_MainLightColor.rgb, _TopColor.rgb, 0.5) * MistPhase(normalize(wp - _WorldSpaceCameraPos)) * _Scatter * 0.5;
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
