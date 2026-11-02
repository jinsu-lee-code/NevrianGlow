// Rare shooting stars on the night side. Each quad of the AirMotes.cs mesh is one star: time cut into periods picks a new
// random spot and heading in the sky for every period, and the star only lives for the first moments of it. Drawn before
// the clouds and further out than they are, so clouds and hills pass in front of it.
Shader "DSEWA/ShootingStar"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (2.4, 2.7, 3.2, 1)
        _Period ("Seconds Between Tries Of One Star", Float) = 30
        _Duration ("Seconds It Lives", Float) = 1.1
        _ArcWidth ("Width Of The Sky It Falls In (degrees round the night side)", Range(10, 300)) = 130
        _Elevation ("Elevation (from, to, degrees)", Vector) = (18, 55, 0, 0)
        _Travel ("Travel Across The Sky (radians)", Range(0.05, 1)) = 0.4
        _Tail ("Tail Length (share of the travel)", Range(0.05, 1)) = 0.4
        _Width ("Width (m at the far distance)", Float) = 5
        _Distance ("Distance (behind the clouds, inside the far plane)", Float) = 2000
        _Phase ("Phase Offset", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _Elevation;
                float _Period, _Duration, _ArcWidth, _Travel, _Tail, _Width, _Distance, _Phase, _Wake;
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
                half fade : TEXCOORD1;
            };

            float Hash(float x) { return frac(sin(x * 12.9898) * 43758.5453); }

            Varyings vert(Attributes input)
            {
                Varyings o;
                float3 eye = _WorldSpaceCameraPos;
                float cycle = (_Time.y + _Phase) / _Period + input.seed.x * 7;
                float index = floor(cycle) + input.seed.y * 100;
                float life = frac(cycle) * _Period / _Duration;                       // 0..1 while alive, above 1 asleep
                float alive = step(life, 1);

                float2 sunFlat = normalize(_MainLightPosition.xz + 1e-5);
                float azimuth = atan2(-sunFlat.x, -sunFlat.y) + (Hash(index) - 0.5) * radians(_ArcWidth);
                float elevation = radians(lerp(_Elevation.x, _Elevation.y, Hash(index + 3.1)));
                float3 start = float3(sin(azimuth) * cos(elevation), sin(elevation), cos(azimuth) * cos(elevation));
                float3 right = normalize(cross(float3(0, 1, 0), start));
                float3 down = cross(right, start);
                float3 heading = normalize(right * (Hash(index + 7.7) * 2 - 1) + down * 0.6);

                float3 head = eye + normalize(start + heading * _Travel * life) * _Distance;
                float3 tail = eye + normalize(start + heading * _Travel * max(life - _Tail, 0)) * _Distance;
                float3 side = normalize(cross(head - tail + heading * 1e-3, head - eye));
                float3 world = lerp(tail, head, input.uv.x) + side * (input.uv.y * 2 - 1) * _Width * alive;

                o.positionCS = TransformWorldToHClip(world);
                o.uv = input.uv;
                o.fade = sin(saturate(life) * PI) * alive;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float across = 1 - abs(i.uv.y * 2 - 1);
                return half4(_Color.rgb * i.uv.x * i.uv.x * across * across * i.fade, 0);
            }
            ENDHLSL
        }
    }
}
