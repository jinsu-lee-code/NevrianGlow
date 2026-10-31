// DSEWA/ToonSoft: "chewy" pastel shading for props (the soft clay / gummy look of the Mixar renders of the Varco props),
// next to the hard two-tone DSEWA toon shaders.
//   - wrapped light with a wide, soft terminator (no hard cel step)
//   - sky / ground ambient: up-facing surfaces catch the sky colour, so top and sides separate even in shadow
//   - pastel base (lifted toward white) and a deeper, more saturated shadow (light seems to sink into the material)
//   - a round, soft-edged gloss blob from the main light plus one that follows the view (always visible, like a toy
//     under studio light), and a soft rim
//   - main-light colour, toon cast + cloud shadow, ground fog, inverted-hull outline, depth passes for the forced depth
//     priming + SSAO DepthNormals prepass (without them an opaque shader is not drawn here)
// All defaults are my own starting values.
Shader "DSEWA/ToonSoft"
{
    Properties
    {
        _BaseMap ("Base Map (optional)", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (0.6, 0.8, 0.8, 1)
        _Pastel ("Pastel (lift toward white)", Range(0, 1)) = 0.25
        [Header(Surface Detail)]
        [Normal] _BumpMap ("Normal Map (optional, OpenGL, same tiling as the base map)", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 2)) = 1
        _OcclusionMap ("Occlusion Map (optional)", 2D) = "white" {}
        _OcclusionStrength ("Occlusion On Ambient", Range(0, 1)) = 1
        _OcclusionDirect ("Occlusion On Direct Light", Range(0, 1)) = 0.5
        [Header(Soft Light)]
        _Wrap ("Light Wrap", Range(0, 1)) = 0.5
        _Softness ("Terminator Softness", Range(0.01, 0.5)) = 0.3
        _ShadowTint ("Shadow Tint", Color) = (0.62, 0.6, 0.9, 1)
        _ShadowSaturation ("Shadow Saturation", Range(0.5, 2)) = 1.3
        _LightColorAmount ("Light Color Influence", Range(0, 1)) = 0.5
        _PointLightStrength ("Point / Spot Light Strength (0 = main light only)", Range(0, 2)) = 1
        _PointBands ("Point Light Toon Steps (0 = smooth falloff)", Range(0, 6)) = 3
        _PointBandSoft ("Point Light Step Edge Softness", Range(0.01, 1)) = 0.75
        _PointBandGain ("Point Light Falloff Gain Before Steps", Range(1, 10)) = 2.5
        _PointLightCap ("Point Lights Add At Most x Surface Colour (0 = no cap)", Range(0, 4)) = 0.9
        [Header(Cel Detail 2026 10 27 all zero means off)]
        _MidBand ("Mid Tone Band Width (0 = two tones)", Range(0, 0.5)) = 0
        _MidTone ("Mid Tone Brightness (0 shadow .. 1 lit)", Range(0, 1)) = 0.55
        _EdgeBreakup ("Painted Edge Wobble", Range(0, 0.5)) = 0
        _EdgeNoiseScale ("Painted Edge Noise Size (per m)", Range(0.5, 40)) = 9
        _TermColor ("Terminator Colour Line", Color) = (0.95, 0.45, 0.55, 1)
        _TermStrength ("Terminator Line Strength", Range(0, 1)) = 0
        _TermWidth ("Terminator Line Width", Range(0.01, 0.3)) = 0.08
        _CavityCut ("Cavity Cut (AO below this goes flat dark, 0 = off)", Range(0, 1)) = 0
        _AlbedoSteps ("Texture Brightness Steps (0 = off, toon posterize)", Range(0, 8)) = 0
        _PointLightHue ("Lamp Hue Over Surface Colour (0 = multiply, 1 = lamp colour x brightness)", Range(0, 1)) = 0
        [ToggleUI] _ReceiveShadows ("Receive Shadows (cast + cloud)", Float) = 1
        _ShadowBlur ("Cast Shadow Smoothing (texels)", Range(0, 4)) = 1.5
        [Header(Height Gradient (2026 10 27 shop shading pass))]
        _HeightGrad ("Height Gradient Amount (0 = off)", Range(0, 1)) = 0
        _HeightRange ("World Y: Bottom / Top", Vector) = (0, 2.6, 0, 0)
        _HeightBottom ("Bottom Tint (multiply)", Color) = (0.62, 0.6, 0.82, 1)
        _HeightTopLift ("Top Lift", Range(0, 0.5)) = 0.12
        [Header(Sky and Ground Ambient)]
        _SkyColor ("Sky Color (up)", Color) = (0.72, 0.78, 1, 1)
        _GroundColor ("Ground Color (down)", Color) = (0.3, 0.26, 0.45, 1)
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.4
        [ToggleUI] _SceneAmbient ("Use The Scene's Ambient (not the colours above)", Float) = 0
        [Header(Gloss Blob)]
        _GlossColor ("Gloss Color", Color) = (1, 1, 1, 1)
        _GlossSize ("Gloss Size", Range(0.001, 0.3)) = 0.06
        _GlossSoftness ("Gloss Edge Softness", Range(0.001, 0.2)) = 0.04
        _GlossStrength ("Gloss From The Light", Range(0, 2)) = 0.5
        _ViewGloss ("Gloss Following The View", Range(0, 2)) = 0.35
        [Header(Rim)]
        _RimColor ("Rim Color", Color) = (0.85, 0.9, 1, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.3
        _RimPower ("Rim Falloff", Range(0.5, 8)) = 2.5
        [Header(Emission)]
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        _TexGlowStrength ("Texture Glow Strength (bright map parts, 0 = off)", Range(0, 4)) = 0
        _TexGlowThreshold ("Brightness Where Glow Starts", Range(0, 1)) = 0.5
        _TexGlowSoftness ("Glow Edge Softness", Range(0.01, 0.5)) = 0.2
        _TexGlowPulse ("Glow Min / Max Over The Breath (1, 1 = steady)", Vector) = (1, 1, 0, 0)
        [NoScaleOffset] _GlowMask ("Texture Glow Mask (optional, white = may glow)", 2D) = "white" {}
        [Toggle] _GlowNoShadow ("Glowing Parts Cast No Shadow (needs a glow mask)", Float) = 0
        _PulsePeriod ("Breath Period (s)", Range(0.2, 20)) = 4.5
        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0.1, 0.1, 0.26, 1)
        _OutlineWidth ("Outline Width (m at the model's own size, 0 = off)", Float) = 0.004
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float _GlowNoShadow;
            half4 _BaseColor;
            half _Pastel, _Wrap, _Softness;
            half _BumpScale, _OcclusionStrength, _OcclusionDirect;
            half4 _ShadowTint;
            half _ShadowSaturation, _LightColorAmount, _ReceiveShadows, _ShadowBlur;
            half _PointLightStrength;
            half _PointBands;
            half _PointBandSoft;
            half _PointBandGain;
            half _PointLightCap;
            half _PointLightHue;
            half _AlbedoSteps;
            half _MidBand, _MidTone, _EdgeBreakup, _EdgeNoiseScale, _TermStrength, _TermWidth, _CavityCut;
            half4 _TermColor;
            half _HeightGrad, _HeightTopLift;
            float4 _HeightRange;
            half4 _HeightBottom;
            half4 _SkyColor, _GroundColor;
            half _AmbientStrength;
            half _SceneAmbient;
            half4 _GlossColor;
            half _GlossSize, _GlossSoftness, _GlossStrength, _ViewGloss;
            half4 _RimColor;
            half _RimStrength, _RimPower;
            half4 _EmissionColor;
            half _TexGlowStrength, _TexGlowThreshold, _TexGlowSoftness;
            float4 _TexGlowPulse;
            float _PulsePeriod;
            half4 _OutlineColor;
            float _OutlineWidth;
        CBUFFER_END
        // interior bounce globals (InteriorFill.cs): box min.xyz + edge fade (m), box max, up / down bounce colours (Up.a = amount)
        float4 _DSEWA_InteriorBoxMin, _DSEWA_InteriorBoxMax;
        half4 _DSEWA_InteriorFillUp, _DSEWA_InteriorFillDown;
        float4 _DSEWA_InteriorKeyPos;   // xyz = key point (centre orb), w = strength (0 = off)
        half4 _DSEWA_InteriorKeyColor;
                // outline width is in metres at the model's own size: a prefab scaled to 0.18 gets a 0.18x line
        // (geometric mean of the object scale, so non-uniform boxes do not blow up)
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
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS   // lamp shadows (2026-10-30: shop orb lamps)
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Assets/Art/Environment/Shaders/CloudShadow.hlsl"
            #include "Assets/Art/Environment/Shaders/ToonShadow.hlsl"
            #include "Assets/Art/Environment/Shaders/GroundFog.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_GlowMask); SAMPLER(sampler_GlowMask);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                float4 tangentWS : TEXCOORD4;   // w = bitangent sign
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                VertexNormalInputs n = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                o.normalWS = n.normalWS;
                o.tangentWS = float4(n.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // ---- cel detail helpers (2026-10-27, user: "detailed toon shading")
            float CelHash(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float CelNoise(float3 x)   // value noise, -0.5..0.5
            {
                float3 i0 = floor(x); float3 f = frac(x); f = f * f * (3.0 - 2.0 * f);
                float n = lerp(lerp(lerp(CelHash(i0), CelHash(i0 + float3(1,0,0)), f.x), lerp(CelHash(i0 + float3(0,1,0)), CelHash(i0 + float3(1,1,0)), f.x), f.y),
                               lerp(lerp(CelHash(i0 + float3(0,0,1)), CelHash(i0 + float3(1,0,1)), f.x), lerp(CelHash(i0 + float3(0,1,1)), CelHash(i0 + float3(1,1,1)), f.x), f.y), f.z);
                return n - 0.5;
            }
            // wrapped N.L -> cel steps: shadow (0), optional mid tone, lit (1); edge is pushed by a painted wobble
            half CelLit(half w, float3 posWS)
            {
                w += _EdgeBreakup * (CelNoise(posWS * _EdgeNoiseScale) + 0.5 * CelNoise(posWS * _EdgeNoiseScale * 2.7));
                if (_MidBand <= 0) return smoothstep(0.5 - _Softness, 0.5 + _Softness, w);
                half lo = smoothstep(0.5 - _MidBand - _Softness, 0.5 - _MidBand + _Softness, w);
                half hi = smoothstep(0.5 + _MidBand - _Softness, 0.5 + _MidBand + _Softness, w);
                return lo * _MidTone + hi * (1.0h - _MidTone);
            }
            half CelTerm(half w, float3 posWS)   // 1 on the light/shadow boundary
            {
                w += _EdgeBreakup * (CelNoise(posWS * _EdgeNoiseScale) + 0.5 * CelNoise(posWS * _EdgeNoiseScale * 2.7));
                half d = abs(w - (_MidBand > 0 ? 0.5 - _MidBand : 0.5));
                return 1.0h - smoothstep(_TermWidth * 0.5, _TermWidth, d);
            }

            half Blob(half nDotH)   // round soft-edged highlight
            {
                return smoothstep(1.0 - _GlossSize - _GlossSoftness, 1.0 - _GlossSize + _GlossSoftness, nDotH);
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 Ngeo = normalize(i.normalWS);
                // normal map (planks bevel, grain ridges): bends the lighting only; the rim keeps the mesh normal
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                float3 bitangent = i.tangentWS.w * cross(Ngeo, i.tangentWS.xyz);
                float3 N = normalize(TransformTangentToWorld(nTS, float3x3(i.tangentWS.xyz, bitangent, Ngeo)));
                half occ = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, i.uv).r;
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half shadow = lerp(1.0h, ToonMainLightShadow(i.positionWS, _ShadowBlur) * EvaluateCloudShadow(i.positionWS), _ReceiveShadows);

                half3 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                // texture posterize (2026-10-27, user: "more toon"): the map's brightness is cut into a few flat steps,
                // hue and saturation kept, so painted grain reads as cel bands instead of photo gradients
                if (_AlbedoSteps > 0.5)
                {
                    half tl = dot(tex, half3(0.299h, 0.587h, 0.114h));
                    half tq = (floor(tl * _AlbedoSteps) + 0.5h) / _AlbedoSteps;
                    // dark texels are left alone: one fixed step would lift a navy board or dark stone into a flat
                    // pale band, so the posterize fades in only above ~0.2 brightness (wood grain, painted accents)
                    tex = lerp(tex, tex * (tq / max(tl, 1e-3h)), saturate((tl - 0.16h) / 0.12h));
                }
                half3 base = tex * _BaseColor.rgb;
                base = lerp(base, half3(1, 1, 1), _Pastel);
                half baseBright = max(base.r, max(base.g, base.b));   // before the height gradient (lamp hue below)
                // height gradient (hand-painted rule: darker, cooler bottom, lighter top) so props sit on the floor
                if (_HeightGrad > 0)
                {
                    half hh = smoothstep(_HeightRange.x, _HeightRange.y, i.positionWS.y);
                    base *= lerp(half3(1, 1, 1), lerp(_HeightBottom.rgb, half3(1, 1, 1) * (1.0h + _HeightTopLift), hh), _HeightGrad);
                }

                // soft wrapped terminator
                if (_CavityCut > 0) occ = lerp(0.35h, 1.0h, smoothstep(_CavityCut - 0.04h, _CavityCut + 0.04h, occ));
                half wrap = (dot(N, light.direction) + _Wrap) / (1.0 + _Wrap);
                half lit = CelLit(wrap, i.positionWS) * shadow * lerp(1.0h, occ, _OcclusionDirect);

                // shadow side: tinted and more saturated, the colour "sinks in"
                half3 hsv = RgbToHsv(base * _ShadowTint.rgb);
                hsv.y = saturate(hsv.y * _ShadowSaturation);
                half3 shadowCol = HsvToRgb(hsv);
                half3 col = lerp(shadowCol, base, lit);
                col *= lerp(half3(1, 1, 1), light.color, _LightColorAmount);
                half termAll = 0;   // strongest light/shadow boundary over all lights (colour line drawn once below)
                if (_TermStrength > 0) termAll = CelTerm(wrap, i.positionWS) * shadow;

                // point / spot lights (shop lamps): the same soft wrapped terminator, added in the light's own colour
                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                {
                    InputData inputData = (InputData)0;
                    inputData.positionWS = i.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                    half3 addLight = 0;
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        // the shadow-mask overload is the one that reads the lamp's own shadow map
                        Light al = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                        al.shadowAttenuation = lerp(1.0h, al.shadowAttenuation, _ReceiveShadows);
                        half aw = (dot(N, al.direction) + _Wrap) / (1.0 + _Wrap);
                        half alit = CelLit(aw, i.positionWS) * lerp(1.0h, occ, _OcclusionDirect);
                        if (_TermStrength > 0) termAll = max(termAll, CelTerm(aw, i.positionWS) * saturate(al.distanceAttenuation * 4.0));
                        // toon: the falloff is cut into a few flat steps and capped at 1, so a lamp gives rings of
                        // flat colour instead of a smooth hot spot (2026-10-26, user: rim and shine fight the toon look)
                        half at = al.distanceAttenuation * al.shadowAttenuation;
                        if (_PointBands > 0.5)
                        {
                            half sB = saturate(at * _PointBandGain) * _PointBands;   // gain first, so a faint far light still reaches a step
                            half fwB = 0.02h;   // no derivatives inside the light loop (varying trip count)
                            at = (floor(sB) + smoothstep(1.0h - _PointBandSoft - fwB, 1.0h, frac(sB))) / _PointBands;
                        }
                        addLight += al.color * (at * alit);
                    LIGHT_LOOP_END
                    // cap (2026-10-27, user: "the counter is far too bright"): two lamps right on top of a prop added
                    // up to several times its own colour and burnt it white. The summed lamp light is scaled down
                    // (hue kept) so it never adds more than _PointLightCap x the surface colour. 0 = no cap
                    half addMax = max(addLight.r, max(addLight.g, addLight.b)) * _PointLightStrength;
                    if (_PointLightCap > 0 && addMax > _PointLightCap) addLight *= _PointLightCap / addMax;
                    // lamp hue (2026-10-27, warm shop pools): 0 = lamp colour x surface colour (a warm lamp on navy
                    // planks goes grey), 1 = lamp colour x surface brightness, so the pool reads in the lamp's hue
                    // brightness taken before the height gradient and lifted (sqrt) so a dark floor still shows a pool
                    half3 lampSurf = lerp(base, sqrt(baseBright).xxx, _PointLightHue);
                    col += lampSurf * addLight * _PointLightStrength;
                }
                #endif

                // sky above, ground below
                half3 hemi = lerp(_GroundColor.rgb, _SkyColor.rgb, N.y * 0.5 + 0.5);
                // or the scene's own ambient (spherical harmonics), so a prop dropped into another scene (the shop)
                // follows that scene's brightness instead of the field's night sky colours
                hemi = lerp(hemi, SampleSH(N), _SceneAmbient);
                col += base * hemi * _AmbientStrength * lerp(1.0h, occ, _OcclusionStrength);


                // interior bounce (2026-10-27, after the URP light-bake tutorial: the room is filled by baked indirect
                // light, which a team scene cannot store). InteriorFill.cs publishes a world box and two bounce colours;
                // inside the box, faces looking up get the ceiling bounce, faces looking down the floor bounce
                if (_DSEWA_InteriorFillUp.a > 0)
                {
                    float3 dIn = min(i.positionWS - _DSEWA_InteriorBoxMin.xyz, _DSEWA_InteriorBoxMax.xyz - i.positionWS);
                    // fade only toward the walls; height is a plain in/out test so the floor (box bottom) is fully lit
                    half inside = saturate(min(dIn.x, dIn.z) / max(_DSEWA_InteriorBoxMin.w, 1e-3)) * step(-0.05, dIn.y);
                    // indoors the moon never reaches the props, so this up/down fill is what shapes them: cut it into the
                    // same cel steps (mid band, painted edge, colour line) when the cel detail options are on
                    half fw = N.y * 0.5 + 0.5;
                    half fstep = (_MidBand > 0 || _EdgeBreakup > 0) ? CelLit(fw, i.positionWS) : fw;
                    half3 bounce = lerp(_DSEWA_InteriorFillDown.rgb, _DSEWA_InteriorFillUp.rgb, fstep);
                    if (_TermStrength > 0) termAll = max(termAll, CelTerm(fw, i.positionWS) * inside);
                    // key: one fake light from the centre orb (no light-count cost) so props get a real lit / shadow side
                    // indoors; it is the light the cel steps, painted edge and colour line actually show on
                    if (_DSEWA_InteriorKeyPos.w > 0)
                    {
                        float3 kL = normalize(_DSEWA_InteriorKeyPos.xyz - i.positionWS);
                        half kw = (dot(N, kL) + _Wrap) / (1.0 + _Wrap);
                        half kl = (_MidBand > 0 || _EdgeBreakup > 0) ? CelLit(kw, i.positionWS) : smoothstep(0.5 - _Softness, 0.5 + _Softness, kw);
                        col += base * _DSEWA_InteriorKeyColor.rgb * (kl * _DSEWA_InteriorKeyPos.w * inside) * lerp(1.0h, occ, _OcclusionDirect);
                        if (_TermStrength > 0) termAll = max(termAll, CelTerm(kw, i.positionWS) * inside);
                    }
                    col += base * bounce * (_DSEWA_InteriorFillUp.a * inside) * lerp(1.0h, occ, _OcclusionStrength);
                }

                // terminator colour line: a thin saturated band where light turns to shadow (cel painting "blood line")
                if (_TermStrength > 0) col = lerp(col, col * (1.0h + _TermColor.rgb * 1.5h), saturate(termAll) * _TermStrength);

                // gloss blobs: one from the main light, one from just above-left of the camera (always in view)
                half glossLight = Blob(saturate(dot(N, normalize(light.direction + V)))) * lit;
                float3 camUp = UNITY_MATRIX_V[1].xyz, camRight = UNITY_MATRIX_V[0].xyz;
                float3 viewKey = normalize(V + camUp * 0.55 - camRight * 0.35);
                half glossView = Blob(saturate(dot(N, normalize(viewKey + V))));
                col += _GlossColor.rgb * (glossLight * _GlossStrength * Luminance(light.color) + glossView * _ViewGloss);

                // soft rim
                col += _RimColor.rgb * pow(1.0 - saturate(dot(Ngeo, V)), _RimPower) * _RimStrength;
                col += _EmissionColor.rgb;
                // bright painted parts of the map glow in their own colour, breathing slowly per object
                float3 objPos = GetObjectToWorldMatrix()._m03_m13_m23;
                float phase = frac(sin(dot(objPos, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                float breath = 0.5 + 0.5 * sin((_Time.y / max(_PulsePeriod, 0.01) + phase) * TWO_PI);
                half texLum = max(tex.r, max(tex.g, tex.b));
                // the optional mask limits the glow to painted strokes (white in the mask); default white = whole map
                half glowMask = SAMPLE_TEXTURE2D(_GlowMask, sampler_GlowMask, i.uv).r;
                col += tex * smoothstep(_TexGlowThreshold, _TexGlowThreshold + _TexGlowSoftness, texLum) * glowMask
                       * _TexGlowStrength * lerp(_TexGlowPulse.x, _TexGlowPulse.y, breath);

                col = ApplyGroundFog(col, i.positionWS);
                col = MixFog(col, i.fogFactor);
                return half4(col, 1);
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
            struct Varyings { float4 positionCS : SV_POSITION; half fogFactor : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS += TransformObjectToWorldNormal(input.normalOS) * _OutlineWidth * OutlineObjectScale();
                o.positionCS = TransformWorldToHClip(positionWS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(Varyings i) : SV_Target { UNITY_SETUP_INSTANCE_ID(i); return half4(MixFog(_OutlineColor.rgb, i.fogFactor), 1); }
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
            // glowing bits (crystal sprouts on the shelves) give light, so they cast no shadow: a sprout right under a low
            // lamp threw a blade-shaped shadow across the floor (2026-10-30)
            TEXTURE2D(_GlowMask); SAMPLER(sampler_GlowMask);
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct ShadowVaryings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            ShadowVaryings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - positionWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                ShadowVaryings o;
                o.positionCS = positionCS;
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);   // same uv the forward pass samples the mask with
                return o;
            }
            half4 frag(ShadowVaryings i) : SV_Target
            {
                if (_GlowNoShadow > 0.5) clip(0.5 - SAMPLE_TEXTURE2D(_GlowMask, sampler_GlowMask, i.uv).r);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = PackNormalOctQuadEncode(n);
                return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
            #else
                return half4(n, 0);
            #endif
            }
            ENDHLSL
        }
    }
}
