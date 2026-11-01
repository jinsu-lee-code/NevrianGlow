// A lantern's own glow: light spreading through the air in a ball round it, with no light behind it. Drawn on the inside of
// a sphere mesh, scaled to the size of the glow. Same sum as DSEWA/LightVolume: for each pixel it adds up 1/(d*d + a*a)
// along the part of the view ray that is inside the ball and in front of whatever stands there, which has an exact answer,
// so there is no ray marching. It also works with the camera inside the ball. Each lantern breathes on its own beat.
Shader "DSEWA/LanternGlow"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (0.2, 1, 0.8, 1)
        _Intensity ("Intensity", Range(0, 0.2)) = 0.016
        _Softness ("Size Of The Bright Middle (m)", Range(0.02, 1)) = 0.1
        _MaxGlow ("Brightest It Gets", Range(0, 3)) = 0.8
        _Breath ("Breathing Amount", Range(0, 1)) = 0.2
        _BreathSeconds ("Seconds For One Breath", Float) = 9
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+6" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Glow"
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
                half4 _Color;
                float _Intensity, _Softness, _MaxGlow, _Breath, _BreathSeconds;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 centre : TEXCOORD1;
                float radius : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.centre = TransformObjectToWorld(float3(0, 0, 0));
                o.radius = length(TransformObjectToWorld(float3(0.5, 0, 0)) - o.centre);   // the built-in sphere is 1 wide
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 eye = _WorldSpaceCameraPos;
                float3 dir = normalize(i.positionWS - eye);
                float3 toCentre = i.centre - eye;
                float along = dot(toCentre, dir);
                float miss = dot(toCentre, toCentre) - along * along;                // squared distance of the ray from the centre
                float inside = i.radius * i.radius - miss;
                if (inside <= 0) return 0;

                float2 uv = i.positionCS.xy / _ScaledScreenParams.xy;
                float3 scene = ComputeWorldSpacePosition(uv, SampleSceneDepth(uv), UNITY_MATRIX_I_VP);
                float half0 = sqrt(inside);
                float from = max(along - half0, 0);
                float to = min(along + half0, distance(scene, eye));
                if (to <= from) return 0;

                float side = sqrt(miss + _Softness * _Softness);
                float sum = (atan((to - along) / side) - atan((from - along) / side)) / side;
                float edge = inside / (i.radius * i.radius);                         // fades to nothing at the rim of the ball
                float beat = 1 + sin(_Time.y * TWO_PI / _BreathSeconds + dot(i.centre, float3(1.7, 0.9, 2.3))) * _Breath;
                return half4(min(_Color.rgb * _Intensity * sum * edge * edge * beat, _MaxGlow), 0);
            }
            ENDHLSL
        }
    }
}
