// Horizon silhouettes (2026-10-30, user: "more sense of space" - the far layer was empty, the painted sky sat right
// behind the forest). A ring of low ridges outside the terrain, flat painted colour like a background layer in a
// Ghibli-style painting: darker foot, lighter crest, warm towards the sun's azimuth (day side desert mesas), cool away
// from it (night side ice ridges), a thin lit crest on the sun side and pale snow caps on the ice side.
// Unlit (a far layer reads as one flat shape); AerialPerspective hazes it further and adds its sunset rim.
// uv.x = azimuth 0..1 round the ring, uv.y = 0 at the foot .. 1 at the crest (HorizonRingBuilder). Numbers are mine.
Shader "DSEWA/HorizonSilhouette"
{
    Properties
    {
        _FootColor ("Foot Colour", Color) = (0.24, 0.2, 0.42, 1)
        _CrestColor ("Crest Colour", Color) = (0.42, 0.36, 0.62, 1)
        _SunColor ("Sun Side Tint", Color) = (0.78, 0.5, 0.45, 1)
        _IceColor ("Far Side Tint", Color) = (0.35, 0.48, 0.72, 1)
        _SideSpread ("Side Falloff (higher = narrower)", Range(0.5, 8)) = 1.6
        _SideAmount ("Side Tint Amount", Range(0, 1)) = 0.6
        [HDR] _CrestLight ("Lit Crest (sun side)", Color) = (1.0, 0.62, 0.42, 1)
        _CrestWidth ("Lit Crest Width (0-1 of height)", Range(0.005, 0.3)) = 0.06
        _SnowColor ("Snow Caps (far side)", Color) = (0.72, 0.8, 0.95, 1)
        _SnowLine ("Snow Line (0-1 of height)", Range(0, 1)) = 0.7
        _Bands ("Painted Value Bands (0 = smooth)", Range(0, 8)) = 4
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+50" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off   // a single wall; seen from inside the ring only
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _FootColor, _CrestColor, _SunColor, _IceColor, _CrestLight, _SnowColor;
                half _SideSpread, _SideAmount, _CrestWidth, _SnowLine, _Bands;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half h = saturate(i.uv.y);
                half hb = _Bands > 0.5 ? (floor(h * _Bands) + smoothstep(0.85, 1.0, frac(h * _Bands))) / _Bands : h;
                half3 col = lerp(_FootColor.rgb, _CrestColor.rgb, hb);
                float2 v = i.positionWS.xz - _WorldSpaceCameraPos.xz;
                float az = saturate(dot(normalize(v + 1e-5), normalize(_MainLightPosition.xz + 1e-5)) * 0.5 + 0.5);
                float sunW = pow(az, _SideSpread), iceW = pow(1 - az, _SideSpread);
                col = lerp(col, col * _SunColor.rgb * 1.6, sunW * _SideAmount);
                col = lerp(col, col * _IceColor.rgb * 1.6, iceW * _SideAmount);
                // ice side: pale caps above the snow line
                col = lerp(col, _SnowColor.rgb, smoothstep(_SnowLine, _SnowLine + 0.04, h) * iceW);
                // sun side: thin lit crest
                col = lerp(col, _CrestLight.rgb, smoothstep(1 - _CrestWidth, 1 - _CrestWidth * 0.3, h) * sunW);
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
        // the renderer primes depth from the depth-normals prepass (screen outline needs normals): without this pass the
        // walls never reach the depth buffer and the forward pass (depth Equal) draws nothing
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
