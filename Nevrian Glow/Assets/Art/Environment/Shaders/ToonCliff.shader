// Toon cliff (2026-10-27, user: "how should the cliffs change" -> step 1 colour / shading, step 4 the base melting
// into the ground). The background cliffs were one flat lavender with dark facets and read as painted boards.
// World-space, no UVs needed:
//  - height gradient: deep blue-violet at the foot, lighter towards the top (the cliff reads tall)
//  - rock strata: faint horizontal bands that wobble with noise (layered rock instead of a smooth wall)
//  - moss: faces turning up (ledges, the top lip) get a moss / grass colour with a hard toon edge broken by noise
//  - undersides (faces turning down) darker, like crevices and overhangs
//  - painted value strokes (world noise) so big faces are not flat fills
//  - foot: the last _BaseBlend metres fade into the ground colour from the runtime virtual texture (RuntimeVirtualTexture),
//    so the cliff grows out of the ground instead of standing on it
// Toon ramp, cloud / cast shadows, ground haze and the outline pass work as in DSEWA/ToonProp. All numbers my own.
Shader "DSEWA/ToonCliff"
{
    Properties
    {
        [Header(Colour)]
        _BaseColor ("Foot Colour", Color) = (0.13, 0.13, 0.32, 1)
        _TopColor ("Top Colour", Color) = (0.42, 0.42, 0.70, 1)
        _HeightRange ("Gradient Foot Y / Top Y (world)", Vector) = (-2, 14, 0, 0)
        _LitColor ("Lit Multiply", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Multiply", Color) = (0.42, 0.42, 0.7, 1)
        _Threshold ("Shadow Threshold", Range(-1, 1.5)) = 0.35
        _Softness ("Shadow Edge Width", Range(0.001, 0.3)) = 0.04
        _ShadowBlur ("Cast Shadow Smoothing (texels)", Range(0, 4)) = 1.5
        _AmbientShade ("Ambient Shade", Range(0, 1)) = 0.12
        [Header(Strata)]
        _StrataScale ("Bands Per Metre", Range(0.05, 2)) = 0.45
        _StrataStrength ("Band Darkening", Range(0, 0.6)) = 0.18
        _StrataWobble ("Band Wobble", Range(0, 3)) = 1.2
        [Header(Moss On Top Faces)]
        _MossColor ("Moss Colour", Color) = (0.16, 0.36, 0.62, 1)
        _MossStart ("Moss From Up Facing (N.y)", Range(-1, 1)) = 0.45
        _MossNoise ("Moss Edge Noise", Range(0, 1)) = 0.35
        _MossAmount ("Moss Amount", Range(0, 1)) = 1
        [Header(Undersides And Strokes)]
        _UnderDark ("Downward Faces Darken", Range(0, 1)) = 0.45
        _StrokeScale ("Stroke Size (per metre)", Range(0.05, 3)) = 0.6
        _StrokeStrength ("Stroke Value Variation", Range(0, 0.5)) = 0.14
        [Header(Foot Blend Into The Ground)]
        _BaseBlend ("Blend Height (m above the foot)", Range(0, 4)) = 1.4
        _GroundY ("Ground Y Around The Foot", Float) = 0
        [Toggle] _GroundFromTerrain ("Use Terrain Height (rocks on uneven ground)", Float) = 0
        [Header(Rim And Outline)]
        _RimColor ("Rim Color", Color) = (0.75, 0.8, 1, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0
        _OutlineColor ("Outline Color", Color) = (0.08, 0.08, 0.22, 1)
        _OutlineWidth ("Outline Width (m at the model's own size)", Float) = 0.03
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _TopColor, _LitColor, _ShadowColor, _MossColor, _RimColor, _OutlineColor;
            float4 _HeightRange;
            half _Threshold, _Softness, _ShadowBlur, _AmbientShade;
            float _StrataScale, _StrataStrength, _StrataWobble;
            half _MossStart, _MossNoise, _MossAmount, _UnderDark;
            float _StrokeScale, _StrokeStrength, _BaseBlend, _GroundY, _RimStrength, _OutlineWidth;
            float _GroundFromTerrain;
        CBUFFER_END

        float OutlineObjectScale()
        {
            float3x3 m = (float3x3)GetObjectToWorldMatrix();
            return pow(abs(determinant(m)), 1.0 / 3.0);
        }
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
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Art/Environment/Shaders/CloudShadow.hlsl"
            #include "Assets/Art/Environment/Shaders/ToonShadow.hlsl"
            #include "Assets/Art/Environment/Shaders/GroundFog.hlsl"
            float4 _DSEWA_GroundLitColor;
            float4 _DSEWA_GroundLight;
            #include "Assets/Art/Environment/Shaders/RuntimeVirtualTexture.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; half fogFactor : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float Hash31(float3 p) { p = frac(p * 0.1031); p += dot(p, p.yzx + 33.33); return frac((p.x + p.y) * p.z); }
            float Noise3(float3 p)
            {
                float3 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(Hash31(i), Hash31(i + float3(1,0,0)), f.x), lerp(Hash31(i + float3(0,1,0)), Hash31(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(Hash31(i + float3(0,0,1)), Hash31(i + float3(1,0,1)), f.x), lerp(Hash31(i + float3(0,1,1)), Hash31(i + float3(1,1,1)), f.x), f.y), f.z);
            }

            half4 frag(Varyings i, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                float3 P = i.positionWS;
                // flat facets (the cliff meshes are faceted low poly)
                float3 N = normalize(cross(ddy(P), ddx(P)));
                float3 Ns = normalize(i.normalWS) * (frontFace ? 1 : -1);
                N *= sign(dot(N, Ns) + 1e-5);
                float3 V = GetWorldSpaceNormalizeViewDir(P);

                // 1) height gradient
                float hT = saturate((P.y - _HeightRange.x) / max(_HeightRange.y - _HeightRange.x, 0.01));
                half3 albedo = lerp(_BaseColor.rgb, _TopColor.rgb, smoothstep(0, 1, hT));
                // 2) strata bands
                float n1 = Noise3(P * 0.15);
                float band = sin((P.y + (n1 - 0.5) * 2 * _StrataWobble) * _StrataScale * 6.2832);
                albedo *= 1 - _StrataStrength * step(0.55, band);
                // 3) painted strokes: stretched noise, value only
                float st = Noise3(float3(P.x, P.y * 0.35, P.z) * _StrokeScale);
                albedo *= 1 + (st - 0.5) * 2 * _StrokeStrength;
                // 4) undersides darker
                albedo *= 1 - _UnderDark * saturate(-N.y * 1.5);
                // 5) moss on faces turning up, hard toon edge broken by noise
                float mn = Noise3(P * 0.9) * 0.6 + Noise3(P * 2.7) * 0.4;
                half moss = step(_MossStart, N.y + (mn - 0.5) * _MossNoise) * _MossAmount;
                albedo = lerp(albedo, _MossColor.rgb, moss);

                // toon ramp
                Light light = GetMainLight(TransformWorldToShadowCoord(P));
                half direct = ToonMainLightShadow(P, _ShadowBlur) * EvaluateCloudShadow(P);
                float lit = smoothstep(_Threshold, _Threshold + _Softness, saturate(dot(N, light.direction)) * direct + _AmbientShade);
                half3 color = albedo * lerp(_ShadowColor.rgb, _LitColor.rgb, lit) * lerp(half3(1, 1, 1), light.color, 0.35);
                color += _RimColor.rgb * pow(1 - saturate(dot(N, V)), 4) * _RimStrength;

                // 6) foot fades into the ground colour (runtime virtual texture), broken by noise
                half4 g;
                if (_BaseBlend > 0 && SampleRVT(P.xz, g))
                {
                    half3 ground = g.rgb * (_DSEWA_GroundLight.w > 0 ? _DSEWA_GroundLitColor.rgb : half3(1, 1, 1));
                    float gy = _GroundY, ty;
                    if (_GroundFromTerrain > 0 && TerrainGroundY(P.xz, ty)) gy = ty;   // 2026-10-26: real ground under each rock
                    float fb = 1 - smoothstep(0, _BaseBlend, P.y - gy + (mn - 0.5) * _BaseBlend * 0.8);
                    color = lerp(color, ground * lerp(0.75, 1, lit), fb * saturate(1 - N.y * 0.3));
                }

                color = ApplyGroundFog(color, P);
                color = MixFog(color, i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "DSEWAOutline" }
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half fogFactor : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings o;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz) + TransformObjectToWorldNormal(input.normalOS) * _OutlineWidth * OutlineObjectScale();
                o.positionCS = TransformWorldToHClip(positionWS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return half4(MixFog(_OutlineColor.rgb, i.fogFactor), 1); }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings o;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - positionWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
            #if UNITY_REVERSED_Z
                o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return 0; }
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
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 vert(Attributes input) : SV_POSITION { UNITY_SETUP_INSTANCE_ID(input); return TransformObjectToHClip(input.positionOS.xyz); }
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
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings vert(Attributes input) { UNITY_SETUP_INSTANCE_ID(input); Varyings o; o.positionCS = TransformObjectToHClip(input.positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(input.normalOS); return o; }
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
