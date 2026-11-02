// Glowing sparks drifting down round a chandelier in slow circles, as if the crystals were shedding light. Like
// DSEWA/AirMotes every speck is a seeded quad from AirMotes.cs and all the motion lives in the vertex shader. Centred on
// the object, which hangs below the fitting.
Shader "DSEWA/OrbitMotes"
{
    Properties
    {
        [HDR] _ColorA ("Colour A", Color) = (0.95, 1.2, 1.3, 1)
        [HDR] _ColorB ("Colour B", Color) = (1.3, 1.05, 1.2, 1)
        [HDR] _ColorC ("Colour C", Color) = (1.1, 1.05, 1.35, 1)
        _Intensity ("Intensity", Range(0, 4)) = 0.8
        _Radius ("Circle Radius (from, to, m)", Vector) = (0.5, 2.1, 0, 0)
        _Fall ("Fall Height (m, from the object down)", Float) = 2.6
        _FallTime ("Seconds To Fall", Float) = 26
        _Turn ("Turns Per Minute", Float) = 0.8
        _Size ("Speck Size (min, max, m)", Vector) = (0.008, 0.02, 0, 0)
        _Twinkle ("Twinkle", Range(0, 1)) = 0.4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+12" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off ZWrite Off ZTest LEqual
            Blend One One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorA, _ColorB, _ColorC;
                float4 _Radius, _Size;
                float _Fall, _FallTime, _Turn;
                half _Intensity, _Twinkle;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 seed : TEXCOORD1;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                float4 seed = input.seed;
                float t = _Time.y;
                float fall = frac(seed.z + t / (_FallTime * (0.8 + seed.w * 0.5)));         // 0 top .. 1 floor of the fall
                float angle = seed.x * TWO_PI + t * _Turn / 60 * TWO_PI * (0.7 + seed.y * 0.6);
                float radius = lerp(_Radius.x, _Radius.y, seed.y) * (1 + sin(t * 0.17 + seed.w * 30) * 0.08);
                float3 world = TransformObjectToWorld(float3(cos(angle) * radius, -fall * _Fall, sin(angle) * radius));

                float fade = smoothstep(0, 0.12, fall) * (1 - smoothstep(0.6, 1, fall)) * smoothstep(1, 3, distance(world, _WorldSpaceCameraPos));
                float twinkle = lerp(1, pow(sin(t * (1.2 + seed.x * 2) + seed.w * 40) * 0.5 + 0.5, 3), _Twinkle);
                float pick = seed.w * 3;
                half3 tint = pick < 1 ? _ColorA.rgb : (pick < 2 ? _ColorB.rgb : _ColorC.rgb);
                o.color = tint * _Intensity * fade * twinkle;

                float3 view = TransformWorldToView(world);
                view.xy += (input.uv * 2 - 1) * lerp(_Size.x, _Size.y, seed.z);
                o.positionCS = TransformWViewToHClip(view);
                o.uv = input.uv * 2 - 1;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float soft = saturate(1 - dot(i.uv, i.uv));
                return half4(i.color * soft * soft, 0);
            }
            ENDHLSL
        }
    }
}
