// Small glowing specks that hang in the air round the camera. All the motion lives in the vertex shader: every speck is
// a quad with a random seed (AirMotes.cs builds the mesh), drifts with the wind and wraps inside a box that follows the
// camera, so there is no simulation on the CPU and the air is the same wherever the player walks.
Shader "DSEWA/AirMotes"
{
    Properties
    {
        [HDR] _ColorA ("Colour A", Color) = (1, 0.92, 0.78, 1)
        [HDR] _ColorB ("Colour B", Color) = (0.75, 0.85, 1, 1)
        _Intensity ("Intensity", Range(0, 8)) = 0.9
        _LightFollow ("Follow The Sun's Strength", Range(0, 1)) = 1

        [Header(Box)]
        _BoxWidth ("Box Width (m)", Float) = 40
        _BoxHeight ("Box Height (m)", Float) = 14
        _FloorOffset ("Box Floor Below The Camera (m)", Float) = -6
        _Size ("Speck Size (min, max, m)", Vector) = (0.025, 0.06, 0, 0)

        [Header(Motion)]
        _Wind ("Wind (m per s, same way as the clouds on the sun side)", Float) = 0.35
        _Rise ("Rise (m per s)", Float) = 0.02
        _Wobble ("Wobble (m)", Float) = 0.25
        _Twinkle ("Twinkle", Range(0, 1)) = 0.5

        [Header(Where It Shows)]
        _SunGlow ("Extra Glow Looking Towards The Sun", Range(0, 20)) = 6
        _SunFocus ("Sun Glow Focus", Range(1, 64)) = 8
        _DayAmount ("Amount On The Sun Side", Range(0, 1)) = 1
        _NightAmount ("Amount On The Night Side", Range(0, 1)) = 0.5
        _SideRange ("Side Blend (dot sun azimuth, from, to)", Vector) = (-0.35, 0.25, 0, 0)
        _Wake ("Share Of Specks Awake (AirMotes.cs)", Range(0, 1)) = 1
        _AsleepAmount ("Amount In The Grey Area (colour is always gone there)", Range(0, 1)) = 1
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Art/Environment/Shaders/GroundFog.hlsl"   // the grey area (WorldWakeOutside)

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorA, _ColorB;
                float4 _Size, _SideRange;
                float _BoxWidth, _BoxHeight, _FloorOffset, _Wind, _Rise, _Wobble;
                half _Intensity, _LightFollow, _Twinkle, _SunGlow, _SunFocus, _DayAmount, _NightAmount, _Wake, _AsleepAmount;
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
                float3 eye = _WorldSpaceCameraPos;
                float3 sun = _MainLightPosition.xyz;
                float2 sunFlat = normalize(sun.xz + 1e-5);

                float3 p = seed.xyz * float3(_BoxWidth, _BoxHeight, _BoxWidth);
                p.xz += float2(-sunFlat.y, sunFlat.x) * -_Wind * t;     // tangent of the ring on the sun side
                p.y += _Rise * t;
                float phase = seed.w * 40;
                p += float3(sin(t * 0.31 + phase), sin(t * 0.23 + phase * 1.7), cos(t * 0.27 + phase * 0.6)) * _Wobble;

                // wrap inside the box round the camera
                float2 flat = frac((p.xz - eye.xz) / _BoxWidth + 0.5) - 0.5;
                float floorY = eye.y + _FloorOffset;
                float h = frac((p.y - floorY) / _BoxHeight);
                float3 world = float3(eye.x + flat.x * _BoxWidth, floorY + h * _BoxHeight, eye.z + flat.y * _BoxWidth);

                float3 toSpeck = world - eye;
                float distance = length(toSpeck);
                float3 dir = toSpeck / max(distance, 1e-3);
                float fade = (1 - smoothstep(0.35, 0.5, length(flat))) * smoothstep(0, 0.15, h) * (1 - smoothstep(0.65, 1, h)) * smoothstep(1.5, 4.5, distance);

                float night = 1 - smoothstep(_SideRange.x, _SideRange.y, dot(normalize(dir.xz + 1e-5), sunFlat));
                float amount = lerp(_DayAmount, _NightAmount, night);
                float lobe = pow(saturate(dot(dir, sun)), _SunFocus) * _SunGlow;
                float twinkle = lerp(1, sin(t * (0.8 + seed.x * 1.6) + phase) * 0.5 + 0.5, _Twinkle);
                float light = lerp(1, saturate(dot(_MainLightColor.rgb, float3(0.3, 0.6, 0.1))), _LightFollow);
                float awake = step(seed.w, _Wake);

                // the grey area has no colour at all (WorldAwakening): a speck there loses its colour, or sleeps with _AsleepAmount 0
                float outside = WorldWakeOutside(world);
                float3 tint = lerp(_ColorA.rgb, _ColorB.rgb, seed.y);
                tint = lerp(tint, dot(tint, float3(0.299, 0.587, 0.114)) * float3(0.78, 0.82, 0.95) * 0.72, outside);
                amount *= lerp(1, _AsleepAmount, outside);

                o.color = tint * _Intensity * (1 + lobe) * amount * fade * twinkle * light * awake;
                float size = lerp(_Size.x, _Size.y, seed.z) * awake;
                float3 view = TransformWorldToView(world);
                view.xy += (input.uv * 2 - 1) * size;
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
