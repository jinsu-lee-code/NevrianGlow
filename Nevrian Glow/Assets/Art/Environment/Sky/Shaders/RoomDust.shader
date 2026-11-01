// Dust hanging in a room. Like DSEWA/AirMotes all the motion is in the vertex shader, but the specks stay in a fixed box
// and take their light from the room's lamps (RoomDust.cs publishes them). Each speck scatters every lamp by the angle
// between the lamp, the speck and the eye: looking towards a lamp the dust round it lights up, with the lamp behind you it
// nearly vanishes.
Shader "DSEWA/RoomDust"
{
    Properties
    {
        [HDR] _Color ("Dust Colour", Color) = (1, 0.93, 0.82, 1)
        _Intensity ("Intensity", Range(0, 2)) = 0.12
        _Ambient ("Light Without Any Scattering", Range(0, 1)) = 0.08
        _ScatterG ("Scattering Towards The Eye", Range(0, 0.95)) = 0.78
        _RoomSize ("Room Size (m, centred on the object)", Vector) = (12, 3.6, 10, 0)
        _LampColour ("Takes The Lamp's Colour", Range(0, 1)) = 0.3
        _Size ("Speck Size (min, max, m)", Vector) = (0.004, 0.01, 0, 0)
        _Drift ("Drift (m per s)", Float) = 0.05
        _Wobble ("Wobble (m)", Float) = 0.18
        _Twinkle ("Twinkle", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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
                half4 _Color;
                float4 _RoomSize, _Size;
                float _Drift, _Wobble;
                half _Intensity, _Ambient, _ScatterG, _Twinkle, _LampColour;
            CBUFFER_END

            // from RoomDust.cs
            float _DSEWA_RoomLightCount;
            float4 _DSEWA_RoomLightPos[6];     // xyz, w = range
            float4 _DSEWA_RoomLightColor[6];   // rgb x intensity

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

            // Henyey-Greenstein: how much light turns by this angle towards the eye
            float Phase(float cosAngle)
            {
                float g = _ScatterG;
                float d = 1 + g * g - 2 * g * cosAngle;
                return (1 - g * g) / max(d * sqrt(d), 1e-3);
            }

            Varyings vert(Attributes input)
            {
                Varyings o;
                float4 seed = input.seed;
                float t = _Time.y;
                float3 centre = TransformObjectToWorld(float3(0, 0, 0));
                float phase = seed.w * 40;
                float3 p = seed.xyz + float3(0.7, -0.25, 0.4) * _Drift * t / _RoomSize.xyz;
                p += float3(sin(t * 0.29 + phase), sin(t * 0.21 + phase * 1.7), cos(t * 0.25 + phase * 0.6)) * _Wobble / _RoomSize.xyz;
                p = frac(p);
                float3 world = centre + (p - 0.5) * _RoomSize.xyz;

                float3 edge = smoothstep(0, 0.05, p) * (1 - smoothstep(0.95, 1, p));
                float3 toSpeck = world - _WorldSpaceCameraPos;
                float distance = length(toSpeck);
                float3 dir = toSpeck / max(distance, 1e-3);
                float fade = edge.x * edge.y * edge.z * smoothstep(0.3, 0.9, distance);

                float3 light = 0;
                for (int i = 0; i < (int)_DSEWA_RoomLightCount; i++)
                {
                    float3 toLight = _DSEWA_RoomLightPos[i].xyz - world;
                    float d = length(toLight);
                    float reach = saturate(1 - d * d / max(_DSEWA_RoomLightPos[i].w * _DSEWA_RoomLightPos[i].w, 1e-3));
                    light += _DSEWA_RoomLightColor[i].rgb * reach * reach * (_Ambient + Phase(dot(dir, toLight / max(d, 1e-3))));
                }

                light = lerp(dot(light, float3(0.3, 0.6, 0.1)), light, _LampColour);   // dust keeps its own pale colour, not the lamp's
                float twinkle = lerp(1, sin(t * (0.6 + seed.x * 1.4) + phase) * 0.5 + 0.5, _Twinkle);
                o.color = _Color.rgb * light * _Intensity * fade * twinkle;
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
