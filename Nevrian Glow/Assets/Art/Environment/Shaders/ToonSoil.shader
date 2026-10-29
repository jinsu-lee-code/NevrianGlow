// DSEWA/ToonSoil: ToonSoft for the farm-cell soil, with the tilling / planting / wet states done in the shader so one
// mesh covers every state and each cell can be at its own stage. The game's wet colour still comes in through _BaseColor.
//   _Till (0..1)        whole-cell dig amount (the game's till progress) - furrows rise, crust turns into loose clods
//   _Hit0.._Hit3        hoe hits in object space: xy = local x/z, w = 0..1 (a round dug patch that spreads from there)
//   _Hole / _Mound      planting: a pit opens in the middle, then closes into a small covered mound
//   _Wet (0..1)         watered: a dark wet patch spreads from _WetOrigin (local x/z) with a ragged edge; wet soil is
//                       darker and bluer, glossy, the furrow troughs hold little puddles, and grains glint (2026-10-28)
// Everything else (lighting, outline, depth passes) is ToonSoft. SoilFX.cs drives these through a MaterialPropertyBlock
// and keeps the game's _BaseColor. Furrow / dig / pit numbers are my own values.
Shader "DSEWA/ToonSoil"
{
    Properties
    {
        _BaseMap ("Base Map (optional)", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (0.6, 0.8, 0.8, 1)
        _WorldTile ("World Tiling (m per texture repeat, 0 = mesh UV)", Float) = 0
        _Pastel ("Pastel (lift toward white)", Range(0, 1)) = 0.25
        [Header(Soil State)]
        _Till ("Till (whole cell, 0 = crust, 1 = dug)", Range(0, 1)) = 0
        _Hit0 ("Hoe Hit 0 (x, z, -, strength)", Vector) = (0, 0, 0, 0)
        _Hit1 ("Hoe Hit 1", Vector) = (0, 0, 0, 0)
        _Hit2 ("Hoe Hit 2", Vector) = (0, 0, 0, 0)
        _Hit3 ("Hoe Hit 3", Vector) = (0, 0, 0, 0)
        _DigRadius ("Dug Patch Radius (m)", Range(0.1, 1.2)) = 0.55
        _FurrowHeight ("Furrow Height (m)", Range(0, 0.1)) = 0.035
        _FurrowCount ("Furrows Across The Cell", Range(1, 6)) = 3
        _CellHalf ("Cell Half Size (m)", Float) = 0.8
        _EdgeFade ("Flat Rim Width (fraction of the half size)", Range(0.01, 0.5)) = 0.18
        _EdgeOpen ("Open Sides (+x, -x, +z, -z: 1 = a cell snapped there, no rim; set by SoilFX)", Vector) = (0, 0, 0, 0)
        _CrustContrast ("Crust Texture Contrast", Range(0, 1)) = 0.35
        _Hole ("Planting Pit (0..1)", Range(0, 1)) = 0
        _Mound ("Covered Seed Mound (0..1)", Range(0, 1)) = 0
        [Header(Wet)]
        _Wet ("Wet (0 = dry, 1 = soaked; spreads from the origin)", Range(0, 1)) = 0
        _WetOrigin ("Wet Origin (local x, z)", Vector) = (0, 0, 0, 0)
        _WetDarken ("Wet Darkening", Range(0.2, 1)) = 0.45
        _WetTint ("Wet Tint", Color) = (0.72, 0.78, 1, 1)
        _WetGloss ("Wet Sheen", Range(0, 3)) = 1.1
        _WetPuddle ("Puddles In The Furrows", Range(0, 1)) = 0.75
        _WetPuddleColor ("Puddle Color", Color) = (0.32, 0.42, 0.85, 1)
        _WetSparkle ("Wet Glints", Range(0, 4)) = 1.6
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
        [ToggleUI] _ReceiveShadows ("Receive Shadows (cast + cloud)", Float) = 1
        _ShadowBlur ("Cast Shadow Smoothing (texels)", Range(0, 4)) = 1.5
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
            float _WorldTile;
            half4 _BaseColor;
            half _Pastel, _Wrap, _Softness;
            float _Till, _DigRadius, _FurrowHeight, _FurrowCount, _CellHalf, _CrustContrast, _Hole, _Mound, _EdgeFade;
            float4 _EdgeOpen;
            float4 _Hit0, _Hit1, _Hit2, _Hit3;
            float _Wet, _WetDarken, _WetGloss, _WetPuddle, _WetSparkle;
            float4 _WetOrigin;
            half4 _WetTint, _WetPuddleColor;
            half _BumpScale, _OcclusionStrength, _OcclusionDirect;
            half4 _ShadowTint;
            half _ShadowSaturation, _LightColorAmount, _ReceiveShadows, _ShadowBlur;
            half _PointLightStrength;
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
                // outline width is in metres at the model's own size: a prefab scaled to 0.18 gets a 0.18x line
        // (geometric mean of the object scale, so non-uniform boxes do not blow up)
        float OutlineObjectScale()
        {
            float3x3 m = (float3x3)GetObjectToWorldMatrix();
            return pow(abs(determinant(m)), 1.0 / 3.0);
        }
                // ---- soil state, shared by every pass so outline / shadow / depth follow the dug surface
        float SoilDig(float2 p)
        {
            float m = _Till;
            float4 hits[4] = { _Hit0, _Hit1, _Hit2, _Hit3 };
            [unroll] for (int k = 0; k < 4; k++)
                m = max(m, hits[k].w * smoothstep(_DigRadius, _DigRadius * 0.35, distance(p, hits[k].xy)));
            return saturate(m);
        }
        // flat rim round the dug area, only on the sides with no cell snapped next to it (2026-10-28, user: "when cells
        // snap together the lines should disappear") - joined cells read as one field with a rim round the outside
        float SoilEdge(float2 p)
        {
            float h = _CellHalf;
            float d = min(min(lerp(h - p.x, 1e3, _EdgeOpen.x), lerp(h + p.x, 1e3, _EdgeOpen.y)),
                          min(lerp(h - p.y, 1e3, _EdgeOpen.z), lerp(h + p.y, 1e3, _EdgeOpen.w)));
            return saturate(d / h / max(_EdgeFade, 0.01));
        }
        float SoilFurrow(float2 p) { return cos(p.y / _CellHalf * 3.14159 * _FurrowCount); }
        float SoilHeight(float2 p)
        {
            float m = SoilDig(p) * SoilEdge(p);
            float h = m * (SoilFurrow(p) * 0.5 * _FurrowHeight + 0.008);
            float d = length(p);
            h -= _Hole * 0.11 * smoothstep(0.32, 0.0, d);
            h += _Mound * 0.085 * smoothstep(0.3, 0.0, d);
            return h;
        }
        float SoilHash(float2 p)
        {
            float3 p3 = frac(float3(p.xyx) * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }
        float SoilNoise(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(SoilHash(i), SoilHash(i + float2(1, 0)), f.x), lerp(SoilHash(i + float2(0, 1)), SoilHash(i + 1), f.x), f.y);
        }
        // wet patch: a ragged front that grows from the watering point and covers the whole cell at _Wet = 1
        float SoilWet(float2 p)
        {
            if (_Wet <= 0.0) return 0.0;
            float n = SoilNoise(p * 6.0) * 0.65 + SoilNoise(p * 15.0) * 0.35;
            float d = distance(p, _WetOrigin.xy) / (_CellHalf * 1.6) + (n - 0.5) * 0.35;
            float r = _Wet * 1.35;
            return 1.0 - smoothstep(r - 0.1, r, d);
        }
        void SoilDisplace(inout float3 posOS, inout float3 nOS)
        {
            float up = saturate(nOS.y * 2.0 - 0.3);
            if (up <= 0.0) return;
            float2 p = posOS.xz;
            const float e = 0.01;
            float h = SoilHeight(p);
            float hx = SoilHeight(p + float2(e, 0)) - h;
            float hz = SoilHeight(p + float2(0, e)) - h;
            posOS.y += h * up;
            nOS = normalize(nOS + float3(-hx / e, 0, -hz / e) * up);
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

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                float4 tangentWS : TEXCOORD4;   // w = bitangent sign
                float3 positionOS : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionOS = input.positionOS.xyz;
                SoilDisplace(input.positionOS.xyz, input.normalOS);
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

            half Blob(half nDotH)   // round soft-edged highlight
            {
                return smoothstep(1.0 - _GlossSize - _GlossSoftness, 1.0 - _GlossSize + _GlossSoftness, nDotH);
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 Ngeo = normalize(i.normalWS);
                // normal map (planks bevel, grain ridges): bends the lighting only; the rim keeps the mesh normal
                // world-space texture (2026-10-28, team: "match the farm soil to the ground round it"): the same terrain
                // layer texture at the terrain's tiling, continuous across neighbouring cells
                float2 buv = _WorldTile > 0 ? i.positionWS.xz / _WorldTile : i.uv;
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, buv), _BumpScale);
                float3 bitangent = i.tangentWS.w * cross(Ngeo, i.tangentWS.xyz);
                float3 N = normalize(TransformTangentToWorld(nTS, float3x3(i.tangentWS.xyz, bitangent, Ngeo)));
                half occ = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, buv).r;
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half shadow = lerp(1.0h, ToonMainLightShadow(i.positionWS, _ShadowBlur) * EvaluateCloudShadow(i.positionWS), _ReceiveShadows);

                half3 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, buv).rgb;
                // crust (flat, faint clods) -> dug (full-contrast clods, darker in the furrow troughs)
                float dig = SoilDig(i.positionOS.xz) * SoilEdge(i.positionOS.xz);
                half3 crust = lerp(half3(0.85, 0.85, 0.85), tex, _CrustContrast);
                float trough = 0.5 - 0.5 * SoilFurrow(i.positionOS.xz);
                tex = lerp(crust, tex * lerp(1.12, 0.5, trough), dig);
                float pit = saturate(_Hole * smoothstep(0.32, 0.0, length(i.positionOS.xz)));
                tex *= 1.0 + 0.25 * _Mound * smoothstep(0.3, 0.0, length(i.positionOS.xz));   // fresh covered soil, a touch lighter
                tex *= lerp(1.0, 0.3, pit);
                half3 base = tex * _BaseColor.rgb;
                // wet: darker, bluer, less chalky; the furrow troughs hold water
                half wet = SoilWet(i.positionOS.xz);
                base *= lerp(half3(1, 1, 1), _WetTint.rgb * _WetDarken, wet);
                base = lerp(base, half3(1, 1, 1), _Pastel * (1.0 - 0.7 * wet));
                half puddle = wet * _WetPuddle * smoothstep(0.62, 0.92, trough * dig + pit * 0.6);

                // soft wrapped terminator
                half wrap = (dot(N, light.direction) + _Wrap) / (1.0 + _Wrap);
                half lit = smoothstep(0.5 - _Softness, 0.5 + _Softness, wrap) * shadow * lerp(1.0h, occ, _OcclusionDirect);

                // shadow side: tinted and more saturated, the colour "sinks in"
                half3 hsv = RgbToHsv(base * _ShadowTint.rgb);
                hsv.y = saturate(hsv.y * _ShadowSaturation);
                half3 shadowCol = HsvToRgb(hsv);
                half3 col = lerp(shadowCol, base, lit);
                col *= lerp(half3(1, 1, 1), light.color, _LightColorAmount);

                // point / spot lights (shop lamps): the same soft wrapped terminator, added in the light's own colour
                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                {
                    InputData inputData = (InputData)0;
                    inputData.positionWS = i.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                    half3 addCol = 0;
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light al = GetAdditionalLight(lightIndex, i.positionWS);
                        half aw = (dot(N, al.direction) + _Wrap) / (1.0 + _Wrap);
                        half alit = smoothstep(0.5 - _Softness, 0.5 + _Softness, aw) * lerp(1.0h, occ, _OcclusionDirect);
                        addCol += base * al.color * (al.distanceAttenuation * al.shadowAttenuation * alit);
                    LIGHT_LOOP_END
                    col += addCol * _PointLightStrength;
                }
                #endif

                // sky above, ground below
                half3 hemi = lerp(_GroundColor.rgb, _SkyColor.rgb, N.y * 0.5 + 0.5);
                // or the scene's own ambient (spherical harmonics), so a prop dropped into another scene (the shop)
                // follows that scene's brightness instead of the field's night sky colours
                hemi = lerp(hemi, SampleSH(N), _SceneAmbient);
                col += base * hemi * _AmbientStrength * lerp(1.0h, occ, _OcclusionStrength);

                // gloss blobs: one from the main light, one from just above-left of the camera (always in view)
                half glossLight = Blob(saturate(dot(N, normalize(light.direction + V)))) * lit;
                float3 camUp = UNITY_MATRIX_V[1].xyz, camRight = UNITY_MATRIX_V[0].xyz;
                float3 viewKey = normalize(V + camUp * 0.55 - camRight * 0.35);
                half glossView = Blob(saturate(dot(N, normalize(viewKey + V))));
                col += _GlossColor.rgb * (glossLight * _GlossStrength * Luminance(light.color) + glossView * _ViewGloss);

                // wet sheen: small sharp highlights on the bumpy wet soil (light + camera key)
                if (wet > 0.001)
                {
                    // broken into flecks by a fine noise, so smooth furrow ridges do not turn into white bars
                    half fleck = smoothstep(0.45, 0.75, SoilNoise(i.positionOS.xz * 38.0));
                    half sharpL = smoothstep(0.992, 0.998, saturate(dot(N, normalize(light.direction + V)))) * lit;
                    half sharpV = smoothstep(0.99, 0.997, saturate(dot(N, normalize(viewKey + V))));
                    col += _GlossColor.rgb * (sharpL * Luminance(light.color) + sharpV * 0.5) * _WetGloss * wet * fleck;
                    // puddles: flat water reflecting the sky, brighter at grazing angles
                    half fres = 0.35 + 0.65 * pow(1.0 - saturate(dot(Ngeo, V)), 3.0);
                    half3 sky = lerp(_SkyColor.rgb, SampleSH(reflect(-V, Ngeo)), _SceneAmbient);
                    half3 water = _WetPuddleColor.rgb * (0.55 + 0.45 * sky) + _GlossColor.rgb * glossView * 0.35;
                    col = lerp(col, water, puddle * fres);
                    // glints: sparse grains catching the light, twinkling as the view moves
                    float2 g = i.positionOS.xz * 55.0;
                    float2 gi = floor(g);
                    float hg = SoilHash(gi);
                    half dotG = smoothstep(0.32, 0.0, length(frac(g) - 0.5 - (SoilHash(gi + 7.0) - 0.5) * 0.3));
                    half tw = pow(saturate(sin(_Time.y * 2.2 + hg * 60.0 + dot(V, float3(9, 5, 7)) * 6.0)), 6.0);
                    col += _GlossColor.rgb * dotG * tw * step(0.93, hg) * _WetSparkle * wet;
                }

                // soft rim
                col += _RimColor.rgb * pow(1.0 - saturate(dot(Ngeo, V)), _RimPower) * _RimStrength;
                col += _EmissionColor.rgb;
                // bright painted parts of the map glow in their own colour, breathing slowly per object
                float3 objPos = GetObjectToWorldMatrix()._m03_m13_m23;
                float phase = frac(sin(dot(objPos, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                float breath = 0.5 + 0.5 * sin((_Time.y / max(_PulsePeriod, 0.01) + phase) * TWO_PI);
                half texLum = max(tex.r, max(tex.g, tex.b));
                col += tex * smoothstep(_TexGlowThreshold, _TexGlowThreshold + _TexGlowSoftness, texLum)
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
                SoilDisplace(input.positionOS.xyz, input.normalOS);
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
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SoilDisplace(input.positionOS.xyz, input.normalOS);
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
                return positionCS;
            }
            half4 frag() : SV_Target { return 0; }
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
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SoilDisplace(input.positionOS.xyz, input.normalOS);
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
                SoilDisplace(input.positionOS.xyz, input.normalOS);
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
