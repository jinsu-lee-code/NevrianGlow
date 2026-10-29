// Toon rock bedding (2026-10-27, user: "when a rock is buried, the dirt / mud round it should be pushed up following
// the buried shape"). Draws the soil skirt meshes baked by RockBedding: the colour is the ground under it from the
// runtime virtual texture (same formula as the ToonCliff foot blend, so the outer edge disappears into the terrain),
// darkened into wet mud towards the rock (vertex colour r = 1 at the contact line) with small lighter crumbs.
// No outline, no shadow casting. All numbers are my own values.
Shader "DSEWA/ToonBedding"
{
    Properties
    {
        _FallbackColor ("Ground Colour If No RVT", Color) = (0.2, 0.22, 0.45, 1)
        _MudColor ("Mud Multiply At The Rock", Color) = (0.55, 0.5, 0.62, 1)
        _MudWidth ("Mud Band (0 = only at the rock, 1 = whole skirt)", Range(0.05, 1)) = 0.45
        _CrumbColor ("Crumb Multiply", Color) = (1.25, 1.2, 1.3, 1)
        _CrumbScale ("Crumbs Per Metre", Range(2, 40)) = 14
        _CrumbAmount ("Crumb Amount", Range(0, 1)) = 0.35
        _LitColor ("Lit Multiply", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Multiply", Color) = (0.75, 0.75, 0.85, 1)
        _Threshold ("Shadow Threshold", Range(-1, 1.5)) = 0.3
        _Softness ("Shadow Edge Width", Range(0.001, 0.5)) = 0.15
        _ShadowBlur ("Cast Shadow Smoothing (texels)", Range(0, 4)) = 1.5
        _AmbientShade ("Ambient Shade", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _FallbackColor, _MudColor, _CrumbColor, _LitColor, _ShadowColor;
            half _MudWidth, _CrumbScale, _CrumbAmount, _Threshold, _Softness, _ShadowBlur, _AmbientShade;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Art/Environment/Shaders/CloudShadow.hlsl"
            #include "Assets/Art/Environment/Shaders/ToonShadow.hlsl"
            #include "Assets/Art/Environment/Shaders/GroundFog.hlsl"
            float4 _DSEWA_GroundLitColor;
            float4 _DSEWA_GroundLight;
            #include "Assets/Art/Environment/Shaders/RuntimeVirtualTexture.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; half mud : TEXCOORD2; half fogFactor : TEXCOORD3; };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.mud = input.color.r;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float Hash21(float2 p) { float3 p3 = frac(p.xyx * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return frac((p3.x + p3.y) * p3.z); }
            float Noise2(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), f.x), lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), f.x), f.y);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 P = i.positionWS;
                float3 N = normalize(i.normalWS);
                half4 g;
                half3 ground = SampleRVT(P.xz, g) ? g.rgb * (_DSEWA_GroundLight.w > 0 ? _DSEWA_GroundLitColor.rgb : half3(1, 1, 1)) : _FallbackColor.rgb;

                // wet mud towards the rock, edge broken by noise
                float n = Noise2(P.xz * 3.1) * 0.6 + Noise2(P.xz * 9.7) * 0.4;
                half mud = smoothstep(1 - _MudWidth, 1, i.mud + (n - 0.5) * 0.35);
                half3 albedo = ground * lerp(half3(1, 1, 1), _MudColor.rgb, mud);
                // crumbs: small lighter clods
                half crumb = step(1 - _CrumbAmount * 0.5, Noise2(P.xz * _CrumbScale)) * saturate(i.mud * 2);
                albedo *= lerp(half3(1, 1, 1), _CrumbColor.rgb, crumb);

                Light light = GetMainLight(TransformWorldToShadowCoord(P));
                half direct = ToonMainLightShadow(P, _ShadowBlur) * EvaluateCloudShadow(P);
                float lit = smoothstep(_Threshold, _Threshold + _Softness, saturate(dot(N, light.direction)) * direct + _AmbientShade);
                // same lighting as the ToonCliff foot blend, plus the facet ramp of the mound
                half3 color = albedo * lerp(0.75, 1, lit) * lerp(_ShadowColor.rgb, _LitColor.rgb, lit);
                color = ApplyGroundFog(color, P);
                color = MixFog(color, i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings vert(Attributes input) { Varyings o; o.positionCS = TransformObjectToHClip(input.positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(input.normalOS); return o; }
            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n) * 0.5 + 0.5)), 0);
            #else
                return half4(n, 0);
            #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}
