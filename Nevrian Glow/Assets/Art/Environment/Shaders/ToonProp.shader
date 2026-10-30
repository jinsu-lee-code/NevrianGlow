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
        [Header(Pearl   nacre bands on the lit face   default off)]
        _PearlAmount ("Pearl Amount (0 = off)", Range(0, 1)) = 0
        [NoScaleOffset] _PearlBase ("Nacre Base Colour (Substance nacre_layers)", 2D) = "white" {}
        [NoScaleOffset][Normal] _PearlNormal ("Nacre Normal", 2D) = "bump" {}
        [NoScaleOffset] _PearlRough ("Nacre Roughness", 2D) = "black" {}
        _PearlBandSize ("Texture Tile Height (m)", Float) = 4
        _PearlAround ("Texture Tiles Around", Float) = 4
        _PearlNormalStrength ("Normal Strength", Range(0, 2)) = 1
        _PearlTintGain ("Base Colour Variation x", Range(0, 6)) = 3
        _PearlBlur ("Soften (reads blurrier mips, 0 = sharp)", Range(0, 6)) = 0
        _PearlGrainAngle ("Grain Angle From Vertical (deg)", Range(-80, 80)) = 0
        _PearlRefRadius ("Grain Angle Measured At Radius (m)", Float) = 4
        _PearlDepth ("Inner Layer Depth (parallax, tile units)", Range(0, 0.5)) = 0.12
        _PearlInner ("Inner Layer Show-Through", Range(0, 1)) = 0.6
        _PearlSilver ("Body Lustre Colour (silver / cream)", Color) = (0.93, 0.92, 0.98, 1)
        _PearlCore ("Body Lustre (bright core face-on)", Range(0, 1)) = 0.35
        _PearlOrient ("Orient (overtone ring round the highlight)", Range(0, 2)) = 0.6
        _PearlHueSpread ("Hue Change Between Bands", Range(0, 1)) = 0.37
        _PearlViewShift ("Hue Shift With View Angle", Range(0, 3)) = 1.2
        _PearlPastel ("Pastel (1 = white, 0 = full rainbow)", Range(0, 1)) = 0.55
        _PearlFacing ("Face-On Only (power)", Range(0, 6)) = 1.5
        _PearlSheen ("Lustre Highlight", Range(0, 2)) = 0.4
        _PearlShadowSide ("On The Shadow Side (sky light)", Range(0, 1)) = 0.4
        [Header(Reveal By Height   LighthouseGoal   default off)]
        _RevealY ("Lit Below World Y (99999 = all lit)", Float) = 99999
        _RevealSoft ("Reveal Edge (m)", Float) = 0.6
        _RevealDark ("Unlit Colour (multiply)", Color) = (0.12, 0.12, 0.2, 1)
        _RevealFlash ("Bright Band At The Front", Range(0, 6)) = 0
        _RevealFlicker ("Unlit Faint Flicker", Range(0, 1)) = 0
        _RevealGrey ("Unlit Desaturate (GrandmaSleep)", Range(0, 1)) = 0
        [Header(World Awakening   WorldAwakening   default off)]
        _AwakeResponse ("Glow Follows The World Waking (0 = off)", Range(0, 1)) = 0
        [ToggleUI] _WakeExempt ("Keep Colour Outside The Waking Circle (lighthouse)", Float) = 0
        [ToggleUI] _DeadInGrey ("Gone Outside The Waking Circle (dead trees: no fruit)", Float) = 0
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
        #include "Assets/Art/Environment/Shaders/GroundFog.hlsl"   // world waking (dead in the grey) in every pass

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
            float _RevealY, _RevealSoft, _RevealFlash, _RevealFlicker, _RevealGrey;
            half4 _RevealDark;
            half _AwakeResponse, _WakeExempt, _DeadInGrey;
            half4 _PearlSilver;
            float _PearlAmount, _PearlBandSize, _PearlAround, _PearlNormalStrength, _PearlTintGain, _PearlBlur, _PearlGrainAngle, _PearlRefRadius, _PearlDepth, _PearlInner, _PearlCore, _PearlOrient, _PearlHueSpread, _PearlViewShift, _PearlPastel, _PearlFacing, _PearlSheen, _PearlShadowSide;
        CBUFFER_END
        // set by WorldAwakening: 1 = the world still asleep (glows dimmed), 0 = awake (unset = 0, so nothing changes)
        float _DSEWA_WorldSleep;
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
            TEXTURE2D(_PearlBase); SAMPLER(sampler_PearlBase);
            TEXTURE2D(_PearlNormal); TEXTURE2D(_PearlRough);

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
                DSEWA_DEAD_VERT(o.positionCS, pos.positionWS)
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
                if (_DeadInGrey > 0.5) clip(WorldWakeDeadClip(i.positionWS));   // fruit grows back as the wave passes
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

                // pearl / nacre (2026-10-30; depth pass after "looks like a cheap pearl"): the Substance nacre_layers maps
                // wrapped round the object (grain rings the tower), in layers like a real pearl -
                //  inner layer: the nacre colour read again shifted by the view (parallax), so a second pattern drifts
                //               under the surface as you walk and the shell reads as translucent layers;
                //  body lustre: face-on the colour lifts toward silver / cream (the bright core of a good pearl);
                //  surface: a sharp highlight over a broad soft glow, following the nacre normal;
                //  orient: the pink / green overtone only in a ring round the highlight, not over the whole face.
                // Strongest face-on and on the lit face; the shade side keeps a little.
                if (_PearlAmount > 0)
                {
                    float3 rel = i.positionWS - GetObjectToWorldMatrix()._m03_m13_m23;
                    float around = (atan2(rel.x, rel.z) / TWO_PI + 0.5) * max(round(_PearlAround), 1.0);
                    // texture x (the grain direction) runs up the object, y round it (2026-10-30, user: grain pointing up)
                    // slanted grain (2026-10-30, user: 45 degrees): a shear, not a rotation - the wrap jump stays a whole
                    // number of tiles, so no seam. The angle holds at Ref Radius (wider parts lean less, narrower more)
                    float hgt = rel.y / max(_PearlBandSize, 0.05);
                    float tilesAround = max(round(_PearlAround), 1.0);
                    float tiltS = tan(radians(_PearlGrainAngle)) * max(_PearlBandSize, 0.05) / (TWO_PI * max(_PearlRefRadius, 0.1) / tilesAround);
                    float2 puv = float2(hgt, around + tiltS * hgt);
                    // derivatives without the jump where atan wraps (no seam line from the mip pick)
                    float2 puv2 = float2(puv.x, frac(around + 0.5) + tiltS * hgt);
                    float2 dx = ddx(puv), dy = ddy(puv), dx2 = ddx(puv2), dy2 = ddy(puv2);
                    dx.y = abs(dx.y) < abs(dx2.y) ? dx.y : dx2.y;
                    dy.y = abs(dy.y) < abs(dy2.y) ? dy.y : dy2.y;
                    float blurK = exp2(_PearlBlur);   // softened: pretend the pixel covers more texture, so a blurrier mip is read
                    dx *= blurK; dy *= blurK;
                    // tangent frame of the wrap: texture x = up (Bw), texture y = round the object (Tw)
                    float3 Tw0 = normalize(float3(rel.z, 0, -rel.x) + 1e-5);
                    float ga = radians(_PearlGrainAngle);
                    float3 Bw = normalize(float3(0, cos(ga), 0) - Tw0 * sin(ga));   // along the grain
                    float3 Tw = normalize(Tw0 * cos(ga) + float3(0, sin(ga), 0));  // across it
                    half3 nBase = SAMPLE_TEXTURE2D_GRAD(_PearlBase, sampler_PearlBase, puv, dx, dy).rgb;
                    half3 nTs = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(_PearlNormal, sampler_PearlBase, puv, dx, dy));
                    half nRough = SAMPLE_TEXTURE2D_GRAD(_PearlRough, sampler_PearlBase, puv, dx, dy).r;
                    float3 Np = normalize(N + (Bw * nTs.x + Tw * nTs.y) * _PearlNormalStrength);
                    float nv = saturate(dot(Np, V));
                    // inner layer, seen through the surface: parallax along the view, deeper at grazing angles
                    float2 vt = float2(dot(V, Bw), dot(V, Tw));
                    float2 iuv = puv - vt * (_PearlDepth / max(nv, 0.3));
                    half3 iBase = SAMPLE_TEXTURE2D_GRAD(_PearlBase, sampler_PearlBase, iuv * 1.37 + 0.31, dx * 2.0, dy * 2.0).rgb;
                    float ihue = (iBase.r - iBase.b) * _PearlHueSpread * 6.0 + nv * _PearlViewShift;
                    half3 iRain = lerp(0.5h + 0.5h * cos(TWO_PI * (ihue + half3(0.0h, 0.33h, 0.67h))), 1.0h, _PearlPastel);
                    half3 tint = 1.0h + (nBase / max(dot(nBase, 0.3333h), 1e-3h) - 1.0h) * _PearlTintGain;
                    half facing = pow(nv, _PearlFacing);
                    half k = _PearlAmount * lerp(_PearlShadowSide, 1.0h, lit) * facing;
                    // inner layer showing through, then the body lustre lifting the core toward silver / cream
                    color = lerp(color, color * tint * iRain * 1.15h, k * _PearlInner);
                    half core = pow(nv, 3.0h) * _PearlCore * _PearlAmount * lerp(0.35h, 1.0h, lit);
                    color = lerp(color, max(color, _PearlSilver.rgb * (0.55h + 0.45h * lit)), core);
                    // surface: sharp highlight over a broad soft glow
                    float3 H = normalize(light.direction + V);
                    half nh = saturate(dot(Np, H));
                    half sharpness = lerp(80.0h, 12.0h, saturate(nRough * 3.0h));
                    half spec = (pow(nh, sharpness) * 0.7h + pow(nh, 5.0h) * 0.3h) * direct;
                    color += _PearlSilver.rgb * light.color * spec * _PearlSheen * _PearlAmount * lit * (1.0h - nRough);
                    // orient: overtone ring round the highlight
                    float ohue = nh * 2.5 + dot(Np, V) * _PearlViewShift + (nBase.r - nBase.b) * _PearlHueSpread * 4.0;
                    half3 oRain = 0.5h + 0.5h * cos(TWO_PI * (ohue + half3(0.0h, 0.33h, 0.67h)));
                    half ring = smoothstep(0.55h, 0.85h, nh) * (1.0h - smoothstep(0.9h, 0.99h, nh));
                    color += (oRain - 0.35h) * ring * _PearlOrient * 0.25h * _PearlAmount * lit * direct;
                }

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
                // world awakening (2026-10-28): opted-in glows (flowers, mushrooms, tree fruit) start dim and wake
                // with the lighthouse progress
                half sleepK = _AwakeResponse * WorldWakeOutside(i.positionWS) * (1.0h - _WakeExempt);   // asleep outside the colour circle
                half awakeK = 1.0h - 0.95h * sleepK;
                color *= 1.0h - 0.7h * sleepK * mask;   // the painted glowing parts themselves sink too, not only the added glow
                half3 preGlow = color;   // the lit surface without its glow (reveal-by-height keeps it, see below)
                color += glowCol * glowTint * mask * _TexGlowStrength * 0.6 * glowK * distK * awakeK;
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
                    color += em * _GlowMaskStrength * distK * awakeK;
                }
                // reveal by height (2026-10-28, LighthouseGoal): only the part below _RevealY is lit, the rest sits in
                // _RevealDark with an optional faint flicker; a bright band rides the front while it climbs
                if (_RevealY < 99990)
                {
                    float above = (i.positionWS.y - _RevealY) / max(_RevealSoft, 1e-3);
                    half litK = 1 - smoothstep(-0.5, 0.5, above);
                    half flick = _RevealFlicker * (0.5 + 0.5 * sin(_Time.y * 2.3 + phase * 6.2832)) * (0.6 + 0.4 * sin(_Time.y * 7.1));
                    // above the lit front: the surface keeps its colour x _RevealDark (white = untouched, 2026-10-29 user:
                    // the dormant tower must not look grey) and only the glow is gone
                    half3 dark = lerp(preGlow, dot(preGlow, half3(0.3, 0.59, 0.11)).xxx, _RevealGrey) * _RevealDark.rgb
                               + glowCol * glowTint * flick * 0.25;
                    color = lerp(dark, color, litK);
                    color += glowCol * glowTint * _RevealFlash * exp(-above * above * 2.0);
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

                color = lerp(ApplyWorldWake(color, i.positionWS), color, _WakeExempt);
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
                DSEWA_DEAD_VERT(o.positionCS, positionWS)
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
                DSEWA_DEAD_VERT(positionCS, positionWS)
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
                float3 deadWS = TransformObjectToWorld(Deform(input.positionOS.xyz, input.color));
                float4 deadCS = TransformWorldToHClip(deadWS);
                DSEWA_DEAD_VERT(deadCS, deadWS)
                return deadCS;
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
                float3 deadWS = TransformObjectToWorld(Deform(input.positionOS.xyz, input.color));
                o.positionCS = TransformWorldToHClip(deadWS);
                DSEWA_DEAD_VERT(o.positionCS, deadWS)
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
