// Leaf shell for the inner blob of the background trees and bushes (2026-10-27, user: "replace the round mesh in the
// leaves and bushes", option A). The blob is there so the sky never shows through the leaf cards, but as a smooth
// toon sphere it read as a dark stone. This shader keeps the blob but turns its surface into leaves:
//  - a 3D cellular pattern in world space (fixed to the tree's pivot, so it never swims) makes one cell per leaf
//  - each leaf is shaded with its own tilted normal (sphere normal + the direction out of its cell), so the toon
//    light / shadow cut breaks up leaf by leaf instead of one round terminator
//  - towards the silhouette (where the sphere turns away from the camera) the leaves shrink and are cut out with
//    alpha clip, so the outline is ragged leaves, not a circle; the middle stays solid and keeps blocking the sky
//  - colours, toon ramp, cloud / ground shadow and haze are the same as DSEWA/ToonLeaf, so shell and cards match
// Has DepthOnly / DepthNormals (URP depth priming) and a clipped ShadowCaster. All numbers are my own values.
Shader "DSEWA/ToonLeafShell"
{
    Properties
    {
        _RootColor ("Inner Color", Color) = (0.07, 0.13, 0.40, 1)
        _TipColor ("Outer Color", Color) = (0.36, 0.52, 0.92, 1)
        _ShadowTint ("Shadow Tint (multiply)", Color) = (0.45, 0.44, 0.85, 1)
        _Threshold ("Shadow Threshold", Range(-1, 1.5)) = 0.39
        _Softness ("Shadow Edge Width", Range(0.001, 0.3)) = 0.05
        _ShadowBlur ("Cast Shadow Smoothing (texels, 0 = URP)", Range(0, 4)) = 1.5
        _AmbientShade ("Ambient Shade", Range(0, 1)) = 0.11
        _ReceiveShadowBias ("Receive Shadow Bias (m)", Range(0, 0.3)) = 0.08
        [Header(Leaves)]
        _LeafDensity ("Leaves Per Metre", Range(0.5, 12)) = 2.2
        _LeafTilt ("Leaf Tilt (per leaf shading)", Range(0, 3)) = 1.2
        _LeafValueVar ("Leaf Brightness Variation", Range(0, 0.5)) = 0.18
        _LeafHueVar ("Leaf Hue Variation", Range(0, 0.1)) = 0.03
        _LeafEdgeDark ("Leaf Edge Darkening", Range(0, 1)) = 0.35
        [Header(Silhouette)]
        _RimStart ("Cut Starts (1 - N.V)", Range(0, 1)) = 0.35
        _EdgeRadius ("Leaf Size At The Silhouette", Range(0.1, 1)) = 0.42
        [Header(Backlight)]
        _BacklightColor ("Backlight Color", Color) = (0.62, 0.75, 1, 1)
        _BacklightStrength ("Backlight Strength", Range(0, 2)) = 0.8
        _BacklightPower ("Backlight Focus", Range(1, 16)) = 2.5
        [Header(Color Controls)]
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Desaturation ("Desaturation", Range(-1, 1)) = 0.15
        _Brightness ("Brightness", Range(0, 3)) = 0.8
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }
        Cull Back

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _RootColor, _TipColor, _ShadowTint;
            half _Threshold, _Softness, _ShadowBlur, _AmbientShade, _ReceiveShadowBias;
            float _LeafDensity, _LeafTilt, _LeafValueVar, _LeafHueVar, _LeafEdgeDark;
            float _RimStart, _EdgeRadius;
            half4 _BacklightColor;
            half _BacklightStrength, _BacklightPower;
            half4 _Tint;
            half _Desaturation, _Brightness;
        CBUFFER_END

        float3 Hash33(float3 p)
        {
            p = float3(dot(p, float3(127.1, 311.7, 74.7)), dot(p, float3(269.5, 183.3, 246.1)), dot(p, float3(113.5, 271.9, 124.6)));
            return frac(sin(p) * 43758.5453);
        }

        // nearest leaf cell: x = distance (cell units), yzw = offset from the leaf centre to p; id via out
        float4 LeafCell(float3 p, out float id)
        {
            float3 i = floor(p), f = frac(p);
            float best = 8; float3 off = 0; id = 0;
            [unroll] for (int z = -1; z <= 1; z++)
            [unroll] for (int y = -1; y <= 1; y++)
            [unroll] for (int x = -1; x <= 1; x++)
            {
                float3 g = float3(x, y, z);
                float3 h = Hash33(i + g);
                float3 d = g + h * 0.8 + 0.1 - f;
                float dd = dot(d, d);
                if (dd < best) { best = dd; off = -d; id = h.x; }
            }
            return float4(sqrt(best), off);
        }

        // world position relative to the object's pivot, in leaf cells
        float3 LeafSpace(float3 positionWS)
        {
            return (positionWS - TransformObjectToWorld(float3(0, 0, 0))) * _LeafDensity;
        }

        // leaf radius here: full (solid) facing the camera, shrinking to _EdgeRadius at the silhouette
        float LeafRadius(float3 N, float3 V)
        {
            float rim = 1 - saturate(dot(N, V));
            return lerp(1.2, _EdgeRadius, smoothstep(_RimStart, 1, rim));
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

            half3 HueShift(half3 c, half a)
            {
                half3 k = half3(0.57735, 0.57735, 0.57735);
                half ca = cos(a);
                return c * ca + cross(k, c) * sin(a) + k * dot(k, c) * (1 - ca);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 N = normalize(i.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float id;
                float4 cell = LeafCell(LeafSpace(i.positionWS), id);
                float radius = LeafRadius(N, V);
                clip(radius - cell.x);

                // each leaf tilts out of its cell: its own light / shadow cut
                float3 Nl = normalize(N + normalize(cell.yzw + 1e-4) * _LeafTilt * saturate(cell.x * 2));
                Light light = GetMainLight();
                half direct = ToonMainLightShadow(i.positionWS + light.direction * _ReceiveShadowBias, _ShadowBlur) * EvaluateCloudShadow(i.positionWS);
                float lit = smoothstep(_Threshold, _Threshold + _Softness, saturate(dot(Nl, light.direction)) * direct + _AmbientShade);

                half outer = saturate(N.y * 0.35 + 0.65);                            // top of the crown lighter, like the card tips
                half3 base = lerp(_RootColor.rgb, _TipColor.rgb, outer);
                base = HueShift(base, (id - 0.5) * 2 * _LeafHueVar * 6.2832);
                base *= 1 + (frac(id * 17.31) - 0.5) * 2 * _LeafValueVar;
                half3 color = lerp(base * _ShadowTint.rgb, base, lit);
                color *= 1 - _LeafEdgeDark * smoothstep(min(radius, 1) * 0.55, min(radius, 1), cell.x);   // a dark line between leaves

                float bl = pow(saturate(dot(-V, light.direction)), _BacklightPower) * direct * outer;
                color += _BacklightColor.rgb * bl * _BacklightStrength;

                color *= _Tint.rgb;
                half lum = dot(color, half3(0.2126, 0.7152, 0.0722));
                color = lerp(color, lum.xxx, _Desaturation) * _Brightness;
                color = ApplyGroundFog(color, i.positionWS);
                color = MixFog(color, i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
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
                o.positionWS = positionWS;
                return o;
            }
            half4 frag(Varyings i) : SV_Target { float id; clip(0.75 - LeafCell(LeafSpace(i.positionWS), id).x); return 0; }
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
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };
            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }
            half frag(Varyings i) : SV_Target
            {
                float id; float3 N = normalize(i.normalWS);
                clip(LeafRadius(N, GetWorldSpaceNormalizeViewDir(i.positionWS)) - LeafCell(LeafSpace(i.positionWS), id).x);
                return 0;
            }
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
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };
            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float id; float3 n = normalize(i.normalWS);
                clip(LeafRadius(n, GetWorldSpaceNormalizeViewDir(i.positionWS)) - LeafCell(LeafSpace(i.positionWS), id).x);
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
