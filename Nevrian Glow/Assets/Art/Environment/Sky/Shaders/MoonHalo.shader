// A faint round glow behind the moon: sunlight scattering round it, so it reads as a ball hanging in air. Stronger and a
// little warmer on the side towards the sun. A billboard just behind the moon, so the moon stands in front of it and the
// 3D clouds and trees cut it. MoonHalo.cs draws it. Numbers are mine.
Shader "DSEWA/MoonHalo"
{
    Properties
    {
        [HDR] _Color ("Glow Colour", Color) = (0.62, 0.72, 1, 1)
        [HDR] _SunColor ("Colour On The Sun Side", Color) = (1, 0.84, 0.8, 1)
        _Intensity ("Intensity", Range(0, 1)) = 0.16
        _Falloff ("Falloff (higher = closer to the moon)", Range(1, 8)) = 3
        _SunSide ("How Much It Leans To The Sun Side", Range(0, 1)) = 0.7
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Halo"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _SunColor;
                half _Intensity, _Falloff, _SunSide;
                float4 _HaloCentre;   // from MoonHalo.cs: xyz world, w half size
                float _MoonShare;     // the moon's radius as a share of the halo's
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 p : TEXCOORD0; float2 sun : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.p = v.uv * 2 - 1;
                float3 view = TransformWorldToView(_HaloCentre.xyz);
                view.xy += o.p * _HaloCentre.w;
                o.positionCS = TransformWViewToHClip(view);
                o.sun = normalize(mul((float3x3)UNITY_MATRIX_V, _MainLightPosition.xyz).xy + 1e-5);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float r = length(i.p);
                float glow = pow(saturate(1 - (r - _MoonShare) / max(1 - _MoonShare, 1e-3)), _Falloff);
                float side = dot(i.p / max(r, 1e-3), i.sun) * 0.5 + 0.5;
                float lit = lerp(1, side * side * 1.6, _SunSide);
                half3 col = lerp(_Color.rgb, _SunColor.rgb, side * side);
                return half4(col * glow * lit * _Intensity, 0);
            }
            ENDHLSL
        }
    }
}
