// DSEWA/ToonProp: shared shader for textured props coming from AI generators (VARCO / Mixar: painted base colour with
// lighting already drawn in). Turns the painted texture into the scene's cel look so it sits next to the hand-made
// toon assets (crystals, lantern fruit, door):
//   1. Delight     - flatten the brightness painted into the texture, keep its hue / saturation (the cel ramp below
//                    does the shading, so shading is not doubled)
//   2. Posterize   - cut the remaining brightness into a few steps so it reads as flat colour areas
//   3. Palette     - remap the texture's brightness onto the scene palette (dark navy -> lavender -> sky) and blend
//                    that over the texture colour, so every generated asset lands in the same colour family
//   4. Same lighting language as the other DSEWA toon shaders: two-tone ramp, toon cast + cloud shadow, rim, ground
//      fog, inverted-hull outline (DSEWAOutline pass), DayNightBlend-friendly colour properties
//   5. Texture glow: the brightest painted parts (measured on the ORIGINAL texture) emit their own colour, with an
//      optional slow per-object breath
// All defaults are my own starting values.
Shader "DSEWA/ToonProp"
{
    Properties
    {
        _BaseMap ("Base Color", 2D) = "white" {}
        [Header(Texture Clean Up)]
        _Delight ("Remove Painted Shading", Range(0, 1)) = 0.7
        _DelightLevel ("Flattened Brightness", Range(0.2, 1)) = 0.75
        _PosterizeSteps ("Posterize Steps (0 = off)", Range(0, 8)) = 4
        _Saturation ("Saturation", Range(0, 2)) = 1
        [Header(Scene Palette)]
        _PaletteMix ("Palette Blend", Range(0, 1)) = 0.45
        _PaletteDark ("Palette Dark", Color) = (0.12, 0.14, 0.34, 1)
        _PaletteMid ("Palette Mid", Color) = (0.45, 0.45, 0.86, 1)
        _PaletteLight ("Palette Light", Color) = (0.76, 0.86, 1, 1)
        [Header(Toon Lighting)]
        _LitColor ("Lit Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Color", Color) = (0.42, 0.43, 0.72, 1)
        _Threshold ("Shadow Threshold", Range(-1, 1.5)) = 0.35
        _Softness ("Shadow Edge Width", Range(0.001, 0.3)) = 0.04
        _ShadowBlur ("Cast Shadow Smoothing (texels, 0 = URP)", Range(0, 4)) = 1.5
        _AmbientShade ("Ambient Shade", Range(0, 1)) = 0.1
        [ToggleUI] _ReceiveShadows ("Receive Shadows (cast + cloud)", Float) = 1
        _RimColor ("Rim Color", Color) = (0.75, 0.8, 1, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.15
        _RimMin ("Rim Start", Range(0, 1)) = 0.78
        _RimMax ("Rim End", Range(0, 1)) = 0.84
        [Header(Texture Glow (bright painted parts))]
        _TexGlowStrength ("Texture Glow Strength (0 = off)", Range(0, 4)) = 0
        _TexGlowThreshold ("Brightness Where Glow Starts", Range(0, 1)) = 0.7
        _TexGlowSoftness ("Glow Edge Softness", Range(0.01, 0.5)) = 0.14
        [HDR] _TexGlowTint ("Texture Glow Tint", Color) = (1, 1, 1, 1)
        _TexGlowPulse ("Glow Min / Max Over The Breath (1, 1 = steady)", Vector) = (1, 1, 0, 0)
        _PulsePeriod ("Breath Period (s)", Range(0.2, 20)) = 4.5
        [Header(Glow Mask Pulse   R veins  G dots  B halo  same UV as Base Map)]
        [NoScaleOffset] _GlowMaskMap ("Glow Mask (black = off)", 2D) = "black" {}
        _GlowMaskStrength ("Mask Glow Strength (0 = off)", Range(0, 6)) = 0
        [HDR] _GlowMaskColor ("Vein Colour", Color) = (0.45, 1, 1, 1)
        [HDR] _GlowMaskDotColor ("Dot Colour", Color) = (0.8, 0.6, 1, 1)
        _GlowMaskWave ("Wave: per UV-v, speed (UV-v / s), sharpness, base", Vector) = (1.5, 0.35, 6, 0.2)
        _GlowMaskDotBreath ("Dot Breath Period (s)", Range(0.5, 10)) = 3
        _DistGlowBoost ("Glow x At Distance (1 = off)", Range(1, 6)) = 1
        _DistGlowRange ("Distance Boost Start / End (m)", Vector) = (10, 60, 0, 0)
        _GlowMax ("Brightest Channel Cap (0 = off)", Range(0, 8)) = 0
        _FootBlend ("Foot Blend Into Ground (m above terrain, 0 = off)", Range(0, 2)) = 0
        [Header(Transparency   blend One Zero and ZWrite 1 is opaque)]
        _Opacity ("Opacity (face middle)", Range(0, 1)) = 1
        _OpacityEdge ("Extra Opacity At The Silhouette", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
        [Toggle] _GlowPalette ("Glow Colour Per Object From Palette", Float) = 0
        [Toggle] _GlowRandomHue ("Random Hue Per Object Instead Of The Palette", Float) = 0
        _GlowRandomSat ("Random Hue Saturation", Range(0, 1)) = 0.75
        _GlowHueRange ("Random Hue Range (from, to; >1 wraps to red)", Vector) = (0, 1, 0, 0)
        [HDR] _GlowColor1 ("Palette 1", Color) = (0.1, 1, 0.8, 1)
        [HDR] _GlowColor2 ("Palette 2", Color) = (0.25, 0.6, 1, 1)
        [HDR] _GlowColor3 ("Palette 3", Color) = (0.65, 0.35, 1, 1)
        [HDR] _GlowColor4 ("Palette 4", Color) = (1, 0.35, 0.75, 1)
        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0.1, 0.1, 0.26, 1)
        _OutlineWidth ("Outline Width (m at the model's own size, 0 = off)", Float) = 0.006
        [Header(Cap Flutter for mushroom caps   0 is off)]
        _CapFlutter ("Sway Tilt (radians, 0 = off)", Range(0, 0.4)) = 0
        _CapStart ("Stem Top Height (object y, the cap pivot)", Range(0, 1)) = 0.45
        _CapRadius ("Cap Rim Radius (object units)", Range(0.05, 1)) = 0.35
        _CapSpeed ("Flutter Speed", Range(0, 5)) = 1.3
        _CapWaves ("Waves Round The Rim", Range(0, 6)) = 2
        _CapBreath ("Breathing (whole rim up / down)", Range(0, 1)) = 0.5
        [Header(Fruit Sway for hanging fruit   0 is off)]
        _FruitSway ("Swing (radians, 0 = off)", Range(0, 0.6)) = 0
        _FruitSwaySpeed ("Swing Speed", Range(0, 5)) = 1.4
        _FruitWindDir ("Wind Direction (world xz)", Vector) = (1, 0, 0.3, 0)
        [Header(Plant Sway for flowers   0 is off)]
        _PlantSway ("Sway (top offset per unit of height at 1 unit tall, 0 = off)", Range(0, 0.5)) = 0
        _PlantSwaySpeed ("Sway Speed", Range(0, 5)) = 1.1
        _PlantNod ("Head Nod (extra, upper part only)", Range(0, 0.5)) = 0
        _PlantNodSpeed ("Nod Speed", Range(0, 6)) = 2.4
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half _Delight, _DelightLevel, _PosterizeSteps, _Saturation;
            half _PaletteMix;
            half4 _PaletteDark, _PaletteMid, _PaletteLight;
            half4 _LitColor, _ShadowColor;
            half _Threshold, _Softness, _ShadowBlur, _AmbientShade, _ReceiveShadows;
            half4 _RimColor;
            half _RimStrength, _RimMin, _RimMax;
            half _TexGlowStrength, _TexGlowThreshold, _TexGlowSoftness;
            half4 _TexGlowTint;
            float4 _TexGlowPulse;
            float _PulsePeriod;
            half _GlowMaskStrength, _GlowMaskDotBreath;
            half4 _GlowMaskColor, _GlowMaskDotColor;
            float4 _GlowMaskWave;
            half _DistGlowBoost, _GlowMax, _FootBlend;
            half _GlowPalette, _Opacity, _OpacityEdge, _GlowRandomHue, _GlowRandomSat;
            float4 _GlowHueRange;
            half4 _GlowColor1, _GlowColor2, _GlowColor3, _GlowColor4;
            float4 _DistGlowRange;
            half4 _OutlineColor;
            float _OutlineWidth;
            float _CapFlutter, _CapStart, _CapRadius, _CapSpeed, _CapWaves, _CapBreath;
            float _FruitSway, _FruitSwaySpeed;
            float4 _FruitWindDir;
            float _PlantSway, _PlantSwaySpeed, _PlantNod, _PlantNodSpeed;
        CBUFFER_END
                // outline width is in metres at the model's own size: a prefab scaled to 0.18 gets a 0.18x line
        // (geometric mean of the object scale, so non-uniform boxes do not blow up)
        float OutlineObjectScale()
        {
            float3x3 m = (float3x3)GetObjectToWorldMatrix();
            return pow(abs(determinant(m)), 1.0 / 3.0);
        }

        // cap sway (2026-10-27, user: "caps that keep flapping", then "the jelly wobble is no good"): the cap moves
        // as one solid piece. It tilts round the top of the stem, the stem bending into it gradually (no deforming of
        // the cap itself), with a slow nod plus a smaller cross sway, and a slight bob. Each mushroom has its own phase
        // from its position. _CapFlutter = tilt in radians at full sway; 0 = off (rocks, trunks and props untouched)
        float3 CapFlutter(float3 p)
        {
            if (_CapFlutter <= 0) return p;
            float3 piv = GetObjectToWorldMatrix()._m03_m13_m23;
            float ph = frac(sin(dot(piv.xz, float2(12.9898, 78.233))) * 43758.5453) * 6.2832;
            float t = _Time.y * _CapSpeed + ph;
            float h = saturate(p.y / max(_CapStart, 0.05));                      // stem: 0 at the ground, 1 at the cap
            float bend = h * h;                                                  // the stem bends more near the top
            float ax = (sin(t) * 0.75 + sin(t * 2.3 + 1.3) * 0.25) * _CapFlutter * bend;
            float az = sin(t * 0.8 + 2.1) * 0.6 * _CapFlutter * bend;
            float3 c = float3(0, _CapStart * h, 0);                              // pivot follows the stem
            float3 q = p - c;
            float ca = cos(ax), sa = sin(ax);
            q = float3(q.x, q.y * ca - q.z * sa, q.y * sa + q.z * ca);
            ca = cos(az); sa = sin(az);
            q = float3(q.x * ca - q.y * sa, q.x * sa + q.y * ca, q.z);
            q.y += sin(t * 1.6) * _CapFlutter * 0.08 * bend;                      // slight bob
            return q + c;
        }

        // fruit sway (2026-10-26, user: "fruit swinging with the wind"): hanging fruit and their stalks swing like a
        // pendulum from the point where the stalk meets the branch. The tree export writes per vertex colour
        // r = 1 on fruit / stalk, g = phase, b = distance below that point / 2 (model units). A steady lean downwind,
        // a swing and a smaller cross swing, with slow gusts. Trunk and branches (r = 0) do not move.
        float3 FruitSway(float3 p, half4 col)
        {
            if (_FruitSway <= 0 || col.r < 0.5) return p;
            float d = col.b * 2.0;
            float3 piv = GetObjectToWorldMatrix()._m03_m13_m23;
            float ph = col.g * 6.2832 + frac(sin(dot(piv.xz, float2(12.9898, 78.233))) * 43758.5453) * 6.2832;
            float t = _Time.y * _FruitSwaySpeed + ph;
            float2 dirW = normalize(_FruitWindDir.xz + 1e-5);
            float gust = 0.6 + 0.4 * sin(_Time.y * 0.37 + dot(piv.xz, dirW) * 0.05);
            float a = ((sin(t) * 0.7 + sin(t * 2.3 + 1.7) * 0.3) + 0.35) * _FruitSway * gust;
            float b = sin(t * 0.83 + 2.1) * 0.45 * _FruitSway * gust;
            float3 upO = normalize(TransformWorldToObjectDir(float3(0, 1, 0)));     // object up (Z-up FBX meshes too)
            float3 dirO = normalize(TransformWorldToObjectDir(float3(dirW.x, 0, dirW.y)));
            float3 sideO = normalize(cross(upO, dirO));
            p += dirO * sin(a) * d + sideO * sin(b) * d;
            p += upO * d * ((1 - cos(a)) + (1 - cos(b)));
            return p;
        }

        // plant sway (2026-10-27, user: "make all the flowers sway or nod"): the whole plant bends from the ground,
        // more towards the top (offset grows with height squared, in object units, so every part of one plant -
        // stem, leaves, petals, glow - moves together whatever material it uses). A lean downwind, a sway and a
        // smaller cross sway with slow gusts, plus a quicker nod of the upper part. Phase per plant from its position.
        float3 PlantSway(float3 p)
        {
            if (_PlantSway <= 0 && _PlantNod <= 0) return p;
            float3 piv = GetObjectToWorldMatrix()._m03_m13_m23;
            float3 p3 = frac(piv * 0.1031); p3 += dot(p3, p3.yzx + 33.33);
            float ph = frac((p3.x + p3.y) * p3.z) * 6.2832;
            float2 dirW = normalize(_FruitWindDir.xz + 1e-5);
            float3 upO = normalize(TransformWorldToObjectDir(float3(0, 1, 0)));
            float3 dirO = normalize(TransformWorldToObjectDir(float3(dirW.x, 0, dirW.y)));
            float3 sideO = normalize(cross(upO, dirO));
            float h = max(dot(p, upO), 0);
            float t = _Time.y * _PlantSwaySpeed + ph;
            float gust = 0.55 + 0.45 * sin(_Time.y * 0.31 + dot(piv.xz, dirW) * 0.06);
            float bend = h * h / (h + 0.15);                                     // small plants still move
            float a = ((sin(t) * 0.7 + sin(t * 2.1 + 1.3) * 0.3) + 0.4) * _PlantSway * gust * bend;
            float b = sin(t * 0.77 + 2.3) * 0.4 * _PlantSway * gust * bend;
            float tn = _Time.y * _PlantNodSpeed + ph * 1.7;
            float n = sin(tn) * _PlantNod * bend * saturate(h * 4);                           // nod: only the upper part, quicker
            float2 off = float2(a + n, b);
            p += dirO * off.x + sideO * off.y;
            p -= upO * dot(off, off) / max(2 * h, 0.02);                            // keep the stem length (tip dips)
            return p;
        }

        float3 Deform(float3 p, half4 col) { return PlantSway(FruitSway(CapFlutter(p), col)); }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Assets/Art/Environment/Shaders/CloudShadow.hlsl"
            #include "Assets/Art/Environment/Shaders/ToonShadow.hlsl"
            #include "Assets/Art/Environment/Shaders/GroundFog.hlsl"
            float4 _DSEWA_GroundLitColor;
            float4 _DSEWA_GroundLight;
            #include "Assets/Art/Environment/Shaders/RuntimeVirtualTexture.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_GlowMaskMap);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs pos = GetVertexPositionInputs(Deform(input.positionOS.xyz, input.color));
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half3 Palette(half t)
            {
                return t < 0.5 ? lerp(_PaletteDark.rgb, _PaletteMid.rgb, t * 2.0) : lerp(_PaletteMid.rgb, _PaletteLight.rgb, t * 2.0 - 1.0);
            }

            half4 frag(Varyings i, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                half3 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                half texV = max(tex.r, max(tex.g, tex.b));

                // 1 + 2: flatten the painted brightness, then posterize what is left
                half3 hsv = RgbToHsv(tex);
                hsv.z = lerp(hsv.z, _DelightLevel * (0.75 + 0.25 * hsv.z), _Delight);
                if (_PosterizeSteps >= 1) hsv.z = (floor(hsv.z * _PosterizeSteps) + 0.5) / _PosterizeSteps;
                hsv.y = saturate(hsv.y * _Saturation);
                half3 albedo = HsvToRgb(hsv);
                // 3: scene palette by the texture's own brightness (keeps light/dark areas of the painting)
                half pT = smoothstep(0.15, 0.9, texV);
                if (_PosterizeSteps >= 1) pT = (floor(pT * _PosterizeSteps) + 0.5) / _PosterizeSteps;
                albedo = lerp(albedo, Palette(pT) * (0.6 + 0.4 * albedo / max(max(albedo.r, max(albedo.g, albedo.b)), 1e-3)), _PaletteMix);

                // 4: toon ramp
                float3 N = normalize(i.normalWS) * (frontFace ? 1 : -1);
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half direct = lerp(1.0h, ToonMainLightShadow(i.positionWS, _ShadowBlur) * EvaluateCloudShadow(i.positionWS), _ReceiveShadows);
                float shade = saturate(dot(N, light.direction)) * direct + _AmbientShade;
                float lit = smoothstep(_Threshold, _Threshold + _Softness, shade);
                half3 color = lerp(SceneShadowMul(_ShadowColor.rgb, _LitColor.rgb), _LitColor.rgb, lit) * albedo;
                // glow palette (2026-10-26, user: flowers glowing in several colours): each object picks one of four
                // colours from its position; the glowing part's own colour and its glow tint both take it
                half3 glowTint = _TexGlowTint.rgb;
                if (_GlowPalette > 0)
                {
                    float3 op = GetObjectToWorldMatrix()._m03_m13_m23;
                    float3 p3 = frac(op * 0.1031);                                  // hash without sine (stable in float)
                    p3 += dot(p3, p3.yzx + 33.33);
                    float pick = frac((p3.x + p3.y) * p3.z);
                    half3 pc = pick < 0.25 ? _GlowColor1.rgb : (pick < 0.5 ? _GlowColor2.rgb : (pick < 0.75 ? _GlowColor3.rgb : _GlowColor4.rgb));
                    if (_GlowRandomHue > 0) pc = HsvToRgb(half3(frac(lerp(_GlowHueRange.x, _GlowHueRange.y, frac(pick * 7.13 + 0.37))), _GlowRandomSat, 1));   // hue inside the range (2026-10-27: no lime / mustard)
                    half3 pn = pc / max(max(pc.r, max(pc.g, pc.b)), 1e-3h);
                    color = pn * lerp(0.35h, 0.7h, lit) * albedo;
                    // squared so the weaker channels drop: a strong glow keeps its hue instead of tonemapping to white
                    glowTint = pn * pn * max(_TexGlowTint.r, max(_TexGlowTint.g, _TexGlowTint.b));
                }
                // far away the glow (and the glowing rim) gets stronger, so small distant fruit still read as lights
                // (2026-10-26, user: "the further the tree fruit, the brighter it should glow")
                half distK = lerp(1.0h, _DistGlowBoost, smoothstep(_DistGlowRange.x, max(_DistGlowRange.y, _DistGlowRange.x + 0.01), distance(i.positionWS, _WorldSpaceCameraPos)));
                color += _RimColor.rgb * smoothstep(_RimMin, _RimMax, 1.0 - saturate(dot(N, V))) * _RimStrength * distK;

                // 5: glow from the original painting's brightest parts
                float3 objPos = GetObjectToWorldMatrix()._m03_m13_m23;
                float phase = frac(sin(dot(objPos, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                float p01 = 0.5 + 0.5 * sin((_Time.y / max(_PulsePeriod, 0.01) + phase) * TWO_PI);
                float glowK = lerp(_TexGlowPulse.x, _TexGlowPulse.y, p01);
                // glow colour from the cleaned-up albedo (not the raw painting, which would paint every brush stroke back
                // in at 3x); the mask still comes from the painting so the patterns / petal rims are what lights up
                half3 glowCol = albedo / max(max(albedo.r, max(albedo.g, albedo.b)), 1e-3);
                half mask = smoothstep(_TexGlowThreshold, _TexGlowThreshold + _TexGlowSoftness, texV);
                if (_PosterizeSteps >= 1) mask = step(0.5, mask);   // hard-edged glow shapes, like painted cel highlights
                color += glowCol * glowTint * mask * _TexGlowStrength * 0.6 * glowK * distK;
                // cap the brightest channel, keeping the hue: past ~2 the tonemapper turns teal into white and the
                // few pixels of a far fruit sparkle under bloom (2026-10-26 check: 29 white pixels, max 4.4)
                // glow mask pulse (2026-10-27, user: "make the mask in Substance Designer and let it pulse"): veins carry
                // bright waves that travel up the trunk (UV v runs up the trunk), dots breathe on their own beat, the
                // soft halo follows the waves. Mask from DSEWA_63/Art/Environment/Substance/bark_alien.
                if (_GlowMaskStrength > 0)
                {
                    half3 gm = SAMPLE_TEXTURE2D(_GlowMaskMap, sampler_BaseMap, i.uv).rgb;
                    float w = frac(i.uv.y * _GlowMaskWave.x - _Time.y * _GlowMaskWave.y + phase);
                    w = pow(saturate(1 - abs(w * 2 - 1)), _GlowMaskWave.z);                // a narrow bright band
                    float wave = _GlowMaskWave.w + (1 - _GlowMaskWave.w) * w;
                    float dotK = 0.35 + 0.65 * (0.5 + 0.5 * sin((_Time.y / max(_GlowMaskDotBreath, 0.01) + phase + i.uv.x * 3.1 + i.uv.y * 1.7) * TWO_PI));
                    half3 em = _GlowMaskColor.rgb * gm.r * wave + _GlowMaskDotColor.rgb * gm.g * dotK + _GlowMaskColor.rgb * gm.b * (0.25 + 0.75 * w) * 0.6;
                    color += em * _GlowMaskStrength * distK;
                }
                if (_GlowMax > 0) color *= min(1.0h, _GlowMax / max(max(color.r, max(color.g, color.b)), 1e-3h));

                // foot blend (2026-10-26, rocks on the meadow): the lowest _FootBlend metres above the real terrain take
                // the ground colour from the runtime virtual texture, so the rock grows out of the ground
                if (_FootBlend > 0)
                {
                    half4 g; float gy;
                    if (SampleRVT(i.positionWS.xz, g) && TerrainGroundY(i.positionWS.xz, gy))
                    {
                        half3 ground = g.rgb * (_DSEWA_GroundLight.w > 0 ? _DSEWA_GroundLitColor.rgb : half3(1, 1, 1));
                        float fb = 1 - smoothstep(0, _FootBlend, i.positionWS.y - gy);
                        color = lerp(color, ground * lerp(0.75h, 1.0h, lit), fb * saturate(1 - N.y * 0.3));
                    }
                }

                color = ApplyGroundFog(color, i.positionWS);
                color = MixFog(color, i.fogFactor);
                // see-through (2026-10-26, user: the jellyfish flower's body transparent): with the transparent blend
                // set on the material, _Opacity in the middle of a face, more solid towards the silhouette
                half alpha = saturate(_Opacity + pow(1.0h - saturate(dot(N, V)), 2.0h) * _OpacityEdge);
                return half4(color, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            // drawn by the outline RenderObjects feature (see DSEWA/ToonCharacter); width 0 = nothing visible
            Tags { "LightMode" = "DSEWAOutline" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half fogFactor : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 positionWS = TransformObjectToWorld(Deform(input.positionOS.xyz, input.color));
                positionWS += TransformObjectToWorldNormal(input.normalOS) * _OutlineWidth * OutlineObjectScale();
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

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };

            float4 vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(Deform(input.positionOS.xyz, input.color));
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

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };

            float4 vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(Deform(input.positionOS.xyz, input.color));
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

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings o;
                o.positionCS = TransformObjectToHClip(Deform(input.positionOS.xyz, input.color));
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
