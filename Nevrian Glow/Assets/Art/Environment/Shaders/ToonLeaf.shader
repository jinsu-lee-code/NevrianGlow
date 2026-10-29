// Toon leaf for plant props (lantern fruit): a flat quad-strip leaf, two-sided, shaded with the same two-tone ramp
// as ToonFoliage / SDCatToon (threshold / softness / ambient, cloud + toon cast shadow). Mesh inputs (flower.blend
// export): UV0 = planar leaf uv (u 0.5 = midrib, v 0 root -> 1 tip), COLOR.r = 0 root -> 1 tip, COLOR.g = per-leaf
// random phase, COLOR.b = 1 on the outline vertices (edge line). Ramp values are the research values; the vein,
// edge, back-light, normal-up and wind numbers are my own.
Shader "DSEWA/ToonLeaf"
{
    Properties
    {
        _BaseMap ("Silhouette (alpha)", 2D) = "white" {}
        [ToggleUI] _WakeExempt ("Keep Colour Outside The Waking Circle (crops)", Float) = 0
        _Cutoff ("Alpha Clip", Range(0, 1)) = 0.5
        _RootColor ("Root Color", Color) = (0.02, 0.04, 0.16, 1)
        _TipColor ("Tip Color", Color) = (0.20, 0.34, 0.91, 1)
        _ShadowTint ("Shadow Tint (multiply)", Color) = (0.16, 0.18, 0.43, 1)
        _Threshold ("Shadow Threshold", Range(-1, 1.5)) = 0.393
        _Softness ("Shadow Edge Width", Range(0.001, 0.3)) = 0.053
        _ShadowBlur ("Cast Shadow Smoothing (texels, 0 = URP)", Range(0, 4)) = 1.5
        _AmbientShade ("Ambient Shade", Range(0, 1)) = 0.105
        _ReceiveShadowBias ("Receive Shadow Bias (m, toward the light)", Range(0, 0.1)) = 0.03
        [Header(Two Sided)]
        _BackFaceDarken ("Back Face Darken", Range(0, 1)) = 0.35
        _NormalUp ("Normal Toward Up (canopy shading)", Range(0, 1)) = 0.5
        [Header(Paint Detail)]
        _PaintMap ("Paint Detail (leaf uv, linear grey, 0.5 = none)", 2D) = "grey" {}
        _PaintStrength ("Paint Strength (0 = off)", Range(0, 2)) = 0
        [Header(Texture Leaf Detail for painted leaf cards)]
        _TexDetail ("Painted Value From Texture (0 = alpha only)", Range(0, 1)) = 0
        _TexDetailContrast ("Painted Value Contrast", Range(0.5, 4)) = 1.8
        _TexDetailMid ("Painted Value Average (texture mean)", Range(0.05, 1)) = 0.5
        _TexDetailFloor ("Painted Value Floor (darkest the texture can make a leaf)", Range(0, 1)) = 0
        _SphereNormal ("Round Bush Normal (0 = off; plain cards light as one ball from the object centre)", Range(0, 1)) = 0
        _SphereCenterY ("Round Bush Centre Height (object units)", Float) = 0.5
        _CardNormal ("Card Facing Mix (each card catches its own light)", Range(0, 1)) = 0
        _LeafOutline ("Dark Outline Round Each Leaf (from alpha)", Range(0, 1)) = 0
        _LeafOutlineWidth ("Outline Width (alpha range)", Range(0.02, 0.5)) = 0.18
        [Header(Vein Mask for painted leaf cards)]
        _VeinMap ("Vein Mask (linear grey, Substance leaf_veins.sbs)", 2D) = "black" {}
        _VeinMapStrength ("Vein Brighten (0 = off)", Range(0, 1)) = 0
        _VeinMapNear ("Vein Full Until (m from camera)", Float) = 8
        _VeinMapFar ("Vein Gone From (m from camera)", Float) = 16
        [Header(Vein)]
        _VeinColor ("Midrib Color (multiply)", Color) = (0.55, 0.60, 0.95, 1)
        _VeinWidth ("Midrib Half Width (uv)", Range(0, 0.1)) = 0.028
        _VeinTaper ("Midrib Taper To Tip", Range(0, 1)) = 0.7
        _VeinStrength ("Midrib Strength", Range(0, 1)) = 0.7
        [Header(Edge)]
        _EdgeColor ("Edge Color (multiply)", Color) = (0.45, 0.45, 0.80, 1)
        _EdgeWidth ("Edge Width", Range(0, 1)) = 0.45
        _EdgeStrength ("Edge Strength", Range(0, 1)) = 0.6
        [Header(Backlight)]
        _BacklightColor ("Backlight Color", Color) = (0.55, 0.70, 1.0, 1)
        _BacklightStrength ("Backlight Strength", Range(0, 2)) = 0.6
        _BacklightPower ("Backlight Focus", Range(1, 16)) = 4
        [Header(Crown only on tree cards with vertex alpha 0)]
        _CrownSkyColor ("Crown Top Tint (sky light)", Color) = (0.55, 0.78, 1, 1)
        _CrownSky ("Crown Top Tint Amount", Range(0, 1)) = 0.35
        _CrownUnderColor ("Crown Underside Tint", Color) = (0.42, 0.36, 0.75, 1)
        _CrownUnder ("Crown Underside Amount", Range(0, 1)) = 0.4
        _CrownRimColor ("Crown Silhouette Rim", Color) = (0.7, 0.85, 1, 1)
        _CrownRim ("Crown Rim Strength", Range(0, 2)) = 0.6
        _CrownRimWidth ("Crown Rim Width", Range(0.01, 0.6)) = 0.22
        [Header(Wind)]
        _WindDir ("Wind Direction (xz)", Vector) = (1, 0, 0.35, 0)
        _WindStrength ("Sway (m at the tip)", Float) = 0.02
        _WindSpeed ("Sway Speed", Float) = 1.6
        _WindFreq ("Sway Wave Frequency", Float) = 0.35
        _Flutter ("Flutter (m)", Float) = 0.004
        _FlutterSpeed ("Flutter Speed", Float) = 4.0
        [Header(Color Controls)]
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Desaturation ("Desaturation (negative = more saturated)", Range(-1, 1)) = 0
        _Brightness ("Brightness", Range(0, 3)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half _Cutoff, _PaintStrength;
            half _TexDetail, _TexDetailContrast, _LeafOutline, _LeafOutlineWidth, _TexDetailMid, _CardNormal, _TexDetailFloor, _SphereNormal;
            float _SphereCenterY;
            half4 _RootColor, _TipColor, _ShadowTint;
            half _Threshold, _Softness, _ShadowBlur, _AmbientShade, _ReceiveShadowBias;
            half _BackFaceDarken, _NormalUp;
            half4 _VeinColor;
            half _VeinWidth, _VeinTaper, _VeinStrength;
            half4 _EdgeColor;
            half _EdgeWidth, _EdgeStrength;
            half4 _BacklightColor;
            half _BacklightStrength, _BacklightPower;
            float4 _WindDir;
            float _WindStrength, _WindSpeed, _WindFreq, _Flutter, _FlutterSpeed;
            half4 _Tint;
            half _Desaturation, _Brightness;
            half4 _CrownSkyColor, _CrownUnderColor, _CrownRimColor;
            half _CrownSky, _CrownUnder, _CrownRim, _CrownRimWidth;

            half _WakeExempt;
            half _VeinMapStrength;
            float _VeinMapNear, _VeinMapFar;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_PaintMap); SAMPLER(sampler_PaintMap);
        TEXTURE2D(_VeinMap); SAMPLER(sampler_VeinMap);

        // Whole-leaf sway about the pivot (the leaf base), stronger toward the tip, plus a small up/down flutter.
        float3 LeafBend(float3 positionWS, float4 color)
        {
            float3 pivot = TransformObjectToWorld(float3(0, 0, 0));
            float h = color.r * color.r;
            float2 dir = normalize(_WindDir.xz + 1e-5);
            float phase = _Time.y * _WindSpeed + color.g * 6.2832 + dot(pivot.xz, dir) * _WindFreq;
            float2 offset = dir * (sin(phase) * 0.7 + sin(phase * 2.3 + 1.7) * 0.3) * _WindStrength * h;
            positionWS.xz += offset;
            positionWS.y += sin(_Time.y * _FlutterSpeed + color.g * 6.2832 + color.r * 3.0) * _Flutter * h;
            return positionWS;
        }

        void ClipAlpha(float2 uv)
        {
            clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a - _Cutoff);
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
            #include "Assets/Art/Environment/Shaders/GroundFog.hlsl"      // same distance haze as terrain / props

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 color : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 positionWS = LeafBend(TransformObjectToWorld(input.positionOS.xyz), input.color);
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.color = input.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half3 ColorControls(half3 c)
            {
                c *= _Tint.rgb;
                half lum = dot(c, half3(0.2126, 0.7152, 0.0722));
                c = lerp(c, lum.xxx, _Desaturation);
                return c * _Brightness;
            }

            half4 frag(Varyings i, bool isFront : SV_IsFrontFace) : SV_Target
            {
                ClipAlpha(i.uv);
                float3 N = normalize(i.normalWS);
                // tree cards carry one outward crown normal (vertex alpha 0): flipping it for the back face pointed it into
                // the crown, so a lit leaf went dark whenever its back was seen (2026-10-27, user: "leaves fall into shadow
                // depending on the view angle as I pass"). Only flip plain two-sided cards.
                half crownCard = step(0.5, 1 - i.color.a);
                bool flip = !isFront && crownCard < 0.5;
                // round bushes (2026-10-27, user: "fix the bushes too"): plain two-sided cards have no crown normal, so the
                // back-face flip made them change with the view. Point them away from the bush centre instead - the
                // same for every view - and light the clump as one ball.
                float3 bushOut = 0;
                if (_SphereNormal > 0 && crownCard < 0.5)
                {
                    float3 ctr = TransformObjectToWorld(float3(0, _SphereCenterY, 0));
                    bushOut = normalize(i.positionWS - ctr + float3(0, 1e-4, 0));
                    flip = false;
                    N *= sign(dot(N, bushOut) + 1e-4);
                    N = normalize(lerp(N, bushOut, _SphereNormal));
                }
                if (flip) N = -N;
                // Canopy shading: pull the normal toward straight up (or down for the underside) so the leaves of a
                // plant read as one lit mass instead of flickering individually.
                // card facing (2026-10-27): mix in the card's own flat normal so neighbouring cards break the crown's
                // single light / shadow into leaf-sized pieces
                float3 cardN = normalize(cross(ddy(i.positionWS), ddx(i.positionWS)));
                cardN *= sign(dot(cardN, N) + 1e-4);
                N = normalize(lerp(N, cardN, _CardNormal));
                float3 up = float3(0, N.y >= 0 ? 1 : -1, 0);
                N = normalize(lerp(N, up, _NormalUp));
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);

                Light light = GetMainLight();
                // A flat two-sided leaf shadows itself (acne) when the shadow map is read at its own surface:
                // read it a little toward the light instead.
                float3 shadowPos = i.positionWS + light.direction * _ReceiveShadowBias;
                half direct = ToonMainLightShadow(shadowPos, _ShadowBlur) * EvaluateCloudShadow(i.positionWS);
                float ndl = saturate(dot(N, light.direction));
                float shade = ndl * direct + _AmbientShade;
                float lit = smoothstep(_Threshold, _Threshold + _Softness, shade);

                half h = i.color.r;
                half3 base = lerp(_RootColor.rgb, _TipColor.rgb, h);
                half3 shadowTint = SceneShadowMul(_ShadowTint.rgb, half3(1, 1, 1));
                half3 color = lerp(base * shadowTint, base, lit);
                if (flip) color = lerp(color, color * shadowTint, _BackFaceDarken);
                // Hand-painted detail (side veins, brush strokes) in leaf uv: grey 0.5 leaves the colour alone
                // painted leaf cards (2026-10-27, user: "the leaf grain doesn't show"): the colour map's own value
                // (brush strokes, leaf light / dark sides) modulates the palette colour, and a dark line follows the
                // alpha edge round every leaf, so single leaves read inside the clump
                half4 texC = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half texV = max(texC.r, max(texC.g, texC.b));
                // 2026-10-27 (user: "leaves turn black even in the light"): high contrast pushed the darkest texels to 0,
                // which no light can lift; the floor keeps them as a darker shade instead of black
                half detail = max(_TexDetailFloor, 1 + (texV / _TexDetailMid - 1) * _TexDetailContrast);
                color *= lerp(1, detail, _TexDetail);
                color *= 1 - _LeafOutline * (1 - smoothstep(_Cutoff, _Cutoff + _LeafOutlineWidth, texC.a));
                half3 paint = SAMPLE_TEXTURE2D(_PaintMap, sampler_PaintMap, i.uv).rgb;
                color *= lerp(half3(1, 1, 1), paint * 2.0h, _PaintStrength);
                // vein mask (2026-10-29, user: dense veins on every leaf can read as creepy / trypophobic): a midrib and one
                // short pair of side strokes from the Substance graph, a little lighter in the leaf's own colour, and only on
                // leaves near the camera, so the canopy stays clean colour shapes at a distance
                if (_VeinMapStrength > 0)
                {
                    half vm = SAMPLE_TEXTURE2D(_VeinMap, sampler_VeinMap, i.uv).r;
                    half vfade = 1 - smoothstep(_VeinMapNear, _VeinMapFar, distance(i.positionWS, _WorldSpaceCameraPos));
                    color *= 1 + _VeinMapStrength * vm * vfade;
                }

                // Midrib: a line down u = 0.5, narrowing toward the tip, one-pixel anti-aliased.
                float d = abs(i.uv.x - 0.5);
                float w = _VeinWidth * (1.0 - i.uv.y * _VeinTaper);
                float aa = fwidth(d) * 1.5;
                float vein = 1.0 - smoothstep(w, w + aa, d);
                color = lerp(color, color * _VeinColor.rgb, vein * _VeinStrength);

                // Edge: the outline vertices carry COLOR.b = 1; it fades inward across the edge quads.
                float edge = smoothstep(1.0 - _EdgeWidth, 1.0, i.color.b);
                color = lerp(color, color * _EdgeColor.rgb, edge * _EdgeStrength);

                // Back-light: with the light behind the leaf (toward the viewer) the thin tip glows through.
                // crown (2026-10-27, foliage research step 3): tree cards carry one crown-wide normal (tree_kit crown=0.8);
                // tint its top toward the sky, its underside cooler, and draw a thin toon rim round the crown silhouette
                half crownK = 1 - i.color.a;
                float3 Nc = normalize(i.normalWS) * (flip ? -1 : 1);
                color = lerp(color, color * _CrownSkyColor.rgb * 1.6, saturate(Nc.y) * _CrownSky * crownK);
                color = lerp(color, color * _CrownUnderColor.rgb, saturate(-Nc.y) * _CrownUnder * crownK);
                float rimT = 1 - saturate(abs(dot(Nc, V)));
                float rimAA = fwidth(rimT) + 1e-3;
                color += _CrownRimColor.rgb * smoothstep(1 - _CrownRimWidth - rimAA, 1 - _CrownRimWidth + rimAA, rimT) * _CrownRim * (0.35 + 0.65 * lit) * crownK;
                float bl = pow(saturate(dot(-V, light.direction)), _BacklightPower) * direct * h;
                color += _BacklightColor.rgb * bl * _BacklightStrength;

                color = ColorControls(color);
                color = lerp(ApplyWorldWake(color, i.positionWS), color, _WakeExempt);
                color = ApplyGroundFog(color, i.positionWS);   // far canopies melt into the haze like the hills
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

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings o;
                float3 positionWS = LeafBend(TransformObjectToWorld(input.positionOS.xyz), input.color);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - positionWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                if (dot(normalWS, lightDir) < 0) normalWS = -normalWS;   // two-sided: bias along the lit side
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
            #if UNITY_REVERSED_Z
                o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target { ClipAlpha(i.uv); return 0; }
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

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings o;
                o.positionCS = TransformWorldToHClip(LeafBend(TransformObjectToWorld(input.positionOS.xyz), input.color));
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half frag(Varyings i) : SV_Target { ClipAlpha(i.uv); return 0; }
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

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings o;
                o.positionCS = TransformWorldToHClip(LeafBend(TransformObjectToWorld(input.positionOS.xyz), input.color));
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i, bool isFront : SV_IsFrontFace) : SV_Target
            {
                ClipAlpha(i.uv);
                float3 n = normalize(i.normalWS);
                if (!isFront) n = -n;
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
