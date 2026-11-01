// DSEWA/ToonMountain: far rock mountains in painted layers (2026-11-01, user: real mountains, and they must read as far away).
// One material per layer. Each layer sets its own air: colour sinks towards the air colour (warm on the sun side, cold on the
// far side), the feet more than the peaks, and the light / shadow contrast flattens, so the farthest layer is nearly a
// paler shape against the sky but never brighter than it. The shared haze cannot do this: it stops at 320 m and lets go
// again past 420 m to keep the clouds clean.
// All defaults are my own starting values.
Shader "DSEWA/ToonMountain"
{
    Properties
    {
        [Header(Rock)]
        _FootColor ("Foot Colour", Color) = (0.2, 0.2, 0.42, 1)
        _TopColor ("Top Colour", Color) = (0.44, 0.43, 0.72, 1)
        _HeightRange ("Gradient Foot Y / Top Y (world)", Vector) = (-10, 150, 0, 0)
        _Shade ("Shadow Side (multiply)", Range(0, 1)) = 0.6
        _Threshold ("Light Threshold (half-Lambert)", Range(0, 1)) = 0.5
        _Edge ("Light Edge Softness", Range(0.001, 0.3)) = 0.03
        [Header(Air)]
        _AirColor ("Air Colour", Color) = (0.52, 0.46, 0.72, 1)
        _SunAirColor ("Air Towards The Sun", Color) = (0.84, 0.56, 0.5, 1)
        _IceAirColor ("Air Away From The Sun", Color) = (0.38, 0.5, 0.78, 1)
        _SideSpread ("Side Falloff (higher = narrower)", Range(0.5, 8)) = 1.6
        _Air ("Air In Front Of This Layer", Range(0, 1)) = 0.5
        _FootAir ("Extra Air At The Feet", Range(0, 1)) = 0.5
        _FootAirHeight ("Feet Air Fades Out By (0-1 of the gradient)", Range(0.05, 1)) = 0.45
        _Contrast ("Light / Shadow Kept", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+50" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off   // the meshes are open shells seen from outside the terrain's edge
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Art/Environment/Shaders/GroundFog.hlsl"   // the far ranges grey with the sleeping world too
            CBUFFER_START(UnityPerMaterial)
                half4 _FootColor, _TopColor, _AirColor, _SunAirColor, _IceAirColor;
                float4 _HeightRange;
                half _Shade, _Threshold, _Edge, _SideSpread, _Air, _FootAir, _FootAirHeight, _Contrast;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float h = saturate((i.positionWS.y - _HeightRange.x) / max(_HeightRange.y - _HeightRange.x, 0.01));
                half3 rock = lerp(_FootColor.rgb, _TopColor.rgb, h);

                float3 L = normalize(_MainLightPosition.xyz);
                float ndl = dot(normalize(i.normalWS), L) * 0.5 + 0.5;
                half lit = smoothstep(_Threshold - _Edge, _Threshold + _Edge, ndl);
                half shade = lerp(lerp(_Shade, 1, 0.5), lerp(_Shade, 1, lit), _Contrast);   // contrast 0 = one flat middle value
                half3 col = rock * shade;

                float2 v = normalize(i.positionWS.xz - _WorldSpaceCameraPos.xz + 1e-5);
                float az = saturate(dot(v, normalize(L.xz + 1e-5)) * 0.5 + 0.5);
                half3 air = lerp(_AirColor.rgb, _SunAirColor.rgb, pow(az, _SideSpread));
                air = lerp(air, _IceAirColor.rgb, pow(1 - az, _SideSpread));
                float foot = 1 - smoothstep(0, _FootAirHeight, h);
                col = lerp(col, air, saturate(_Air + (1 - _Air) * foot * _FootAir));

                col = ApplyWorldWakeBand(col, i.positionWS, 0.35h);
                return half4(col, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 vert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }
        // the renderer primes depth from the depth-normals prepass (see DSEWA/HorizonSilhouette): without this pass the
        // forward pass (depth Equal) draws nothing
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            V vert(A v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(v.normalOS); return o; }
            half4 frag(V i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0); }
            ENDHLSL
        }
    }
}
