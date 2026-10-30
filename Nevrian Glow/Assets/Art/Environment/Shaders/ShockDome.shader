// Ending shock dome (2026-10-30, user: "like in films, a round dome spreading out"): a see-through sphere that swells
// out of the lighthouse with the last wave. Additive, both sides: a bright fresnel rim, a glowing line where it cuts
// through the ground / trees (scene depth), faint drifting bands on the shell. EndingDirector scales it and sets _Fade.
// All numbers are my own values.
Shader "DSEWA/ShockDome"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (1.6, 1.1, 0.55, 1)
        _RimPower ("Rim Power (higher = thinner rim)", Range(0.5, 8)) = 3
        _RimStrength ("Rim Strength", Range(0, 4)) = 1.2
        _Fill ("Shell Fill", Range(0, 1)) = 0.04
        _IntersectWidth ("Ground Cut Width (m)", Float) = 3
        _IntersectStrength ("Ground Cut Strength", Range(0, 6)) = 2.5
        _BandScale ("Shell Bands Scale", Float) = 24
        _BandAmount ("Shell Bands", Range(0, 1)) = 0.35
        _Fade ("Fade (script)", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Dome"
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _RimPower, _RimStrength, _Fill, _IntersectWidth, _IntersectStrength, _BandScale, _BandAmount, _Fade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float2 uv : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.screenPos = ComputeScreenPos(p.positionCS);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 N = normalize(i.normalWS) * (front ? 1 : -1);
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half rim = pow(1 - saturate(abs(dot(N, V))), _RimPower) * _RimStrength;
                // where the shell cuts through the ground / trees
                float2 suv = i.screenPos.xy / i.screenPos.w;
                float sceneZ = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float fragZ = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                half cut = 1 - saturate(abs(sceneZ - fragZ) / max(_IntersectWidth, 0.01));
                cut = cut * cut * _IntersectStrength;
                // faint latitude bands drifting up the shell
                half band = 0.5 + 0.5 * sin((i.uv.y * _BandScale - _Time.y * 1.5) * 6.2832);
                half shell = _Fill * (1 + band * _BandAmount * 4);
                return half4(_Color.rgb * (rim + cut + shell) * _Fade, 1);
            }
            ENDHLSL
        }
    }
}
