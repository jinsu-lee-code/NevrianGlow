// Light spreading through the air of a room from one point light. Drawn on the inside of a box round the room. For each
// pixel it takes the stretch of the view ray that is in the air (camera to the wall or whatever stands in the way) and
// adds up the light along it. That sum has an exact answer for a point light, 1/(d*d + a*a) integrated along a line, so
// there is no ray marching: one atan per pixel, the same at any quality. Solid objects cut the ray, so they stand dark in
// front of the glow. LightVolume.cs hands over the light.
Shader "DSEWA/LightVolume"
{
    Properties
    {
        _Density ("Air Density", Range(0, 0.05)) = 0.0016
        _Softness ("Size Of The Bright Middle (m)", Range(0.05, 3)) = 1
        _FloorFade ("Thinner Below This Height Over The Light (m)", Float) = 3.5
        _NearClear ("Clear Air Round The Camera (m)", Range(0, 8)) = 2
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _MaxGlow ("Brightest It Gets (stays under the bloom)", Range(0, 2)) = 0.45
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+5" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Volume"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float4 _LightPos, _LightColor;       // from LightVolume.cs: xyz ; rgb x intensity
                float _Density, _Softness, _FloorFade, _MaxGlow, _NearClear;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 eye = _WorldSpaceCameraPos;
                float3 toExit = i.positionWS - eye;
                float boxDistance = length(toExit);
                float3 dir = toExit / max(boxDistance, 1e-4);

                float2 uv = i.positionCS.xy / _ScaledScreenParams.xy;
                float3 scene = ComputeWorldSpacePosition(uv, SampleSceneDepth(uv), UNITY_MATRIX_I_VP);
                float length01 = min(boxDistance, distance(scene, eye));

                // the integral of 1 / (distance^2 + a^2) along the ray, from the end of the clear air to length01
                float3 toLight = _LightPos.xyz - eye;
                float along = dot(toLight, dir);
                float side = sqrt(max(dot(toLight, toLight) - along * along, 0) + _Softness * _Softness);
                float start = min(_NearClear, length01);
                float inScatter = (atan((length01 - along) / side) - atan((start - along) / side)) / side;

                // air settles towards the top of the room, so the floor stays clear
                float height = saturate(1 - (_LightPos.y - lerp(eye.y, min(scene.y, i.positionWS.y), 0.5)) / max(_FloorFade, 0.01));
                float3 glow = _LightColor.rgb * _Tint.rgb * inScatter * _Density * lerp(0.35, 1, height);
                return half4(min(glow, _MaxGlow), 0);
            }
            ENDHLSL
        }
    }
}
