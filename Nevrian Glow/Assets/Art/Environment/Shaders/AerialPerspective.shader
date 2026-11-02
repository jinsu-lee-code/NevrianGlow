// Aerial perspective (2026-10-29, background post step 2): a Full Screen Pass renderer feature runs this before
// post-processing, first of the screen passes. GroundFog only reaches the ground and is light at night, so trees,
// cliffs and the lighthouse kept the same violet value at every distance and the layers ran together. Here every
// opaque pixel (up to the cloud layer) is pulled toward the horizon haze colour with distance (lighter and softer far away, keeping a share
// of its own light and shade so shapes still read), and the near band is deepened a little so the foreground frames
// the view. Skips the sky and keeps HDR glows. The haze leans warm towards the sun's azimuth and cold away from it (eyeball planet, 2026-10-30). Off while the camera is in the shop box (InteriorFill); the shop window view, rendered from outside, keeps it. All numbers are my own values.
Shader "Hidden/DSEWA/AerialPerspective"
{
    Properties
    {
        [HDR] _HazeColor ("Haze Colour (linear)", Color) = (0.36, 0.32, 0.58, 1)
        _HazeStart ("Haze Start (m)", Float) = 30
        _HazeEnd ("Haze Full (m)", Float) = 220
        _HazeAmount ("Haze Amount", Range(0, 1)) = 0.4
        _HazeCurve ("Haze Curve", Range(0.3, 3)) = 1.2
        _DetailKeep ("Keep Shading In Haze", Range(0, 1)) = 0.7
        _NearEnd ("Near Band Until (m)", Float) = 10
        _NearDeepen ("Near Band Darkening", Range(0, 0.5)) = 0.12
        _NearSaturate ("Near Band Saturation", Range(0, 0.5)) = 0.1
        _GlowKeep ("Keep Glows Above (HDR luminance)", Float) = 1.1
        _SkyFrom ("Treat As Sky From (m)", Float) = 380
        _SkyFull ("Treat As Sky Beyond (m)", Float) = 520
        [Header(Eyeball planet sides)]
        [HDR] _HazeSunColor ("Sun Side Haze (day side desert)", Color) = (0.62, 0.42, 0.34, 1)
        _HazeSunAmount ("Sun Side Amount", Range(0, 1)) = 0
        [HDR] _HazeIceColor ("Far Side Haze (night side ice)", Color) = (0.22, 0.34, 0.55, 1)
        _HazeIceAmount ("Far Side Amount", Range(0, 1)) = 0
        _HazeSideSpread ("Side Falloff (higher = narrower)", Range(0.5, 8)) = 2
        [Header(Depth Layers)]
        [Toggle] _LayerMode ("Stepped Layers (instead of one smooth ramp)", Float) = 0
        _LayerEdges ("Layer Starts (m): x mid, y far, z horizon", Vector) = (15, 80, 250, 0)
        _LayerHaze ("Haze Per Layer: x mid, y far, z horizon", Vector) = (0.1, 0.4, 0.7, 0)
        _LayerSoft ("Layer Edge Width (fraction of its start)", Range(0.02, 0.6)) = 0.18
        _LayerDesat ("Saturation Lost Per Layer", Range(0, 0.5)) = 0.12
        _LayerColAmount ("Layer Colour Amount (0 = one haze colour)", Range(0, 1)) = 0
        [HDR] _LayerColMid ("Mid Layer Haze Colour", Color) = (0.42, 0.34, 0.66, 1)
        [HDR] _LayerColFar ("Far Layer Haze Colour", Color) = (0.5, 0.44, 0.74, 1)
        [HDR] _LayerColHorizon ("Horizon Layer Haze Colour", Color) = (0.62, 0.56, 0.82, 1)
        [Header(Sun Rays)]
        _RayAmount ("Ray Amount (0 = off)", Range(0, 2)) = 0
        [HDR] _RayColor ("Ray Colour", Color) = (1.0, 0.66, 0.42, 1)
        _RayLength ("Ray Length (fraction of the way to the sun)", Range(0.1, 1)) = 0.75
        _RayDecay ("Ray Decay Per Sample", Range(0.85, 1)) = 0.965
        _RayFalloff ("Ray Spread Round The Sun (higher = tighter)", Range(0.5, 8)) = 2.2
        _RayOnSky ("Rays Over Open Sky (x)", Range(0, 1)) = 0.35
        _CloudRayBoost ("Cloud Gap Boost (rays through 3D cloud gaps)", Range(0, 4)) = 1.5
        [HDR] _CloudRayColor ("Cloud Gap Ray Colour", Color) = (1.0, 0.78, 0.9, 1)
        _MoonRayAmount ("Moon Ray Amount (0 = off, MoonLensFlare gives the direction)", Range(0, 2)) = 0
        [HDR] _MoonRayColor ("Moon Ray Colour", Color) = (0.62, 0.72, 1.0, 1)
        [Header(Valley Mist)]
        _VividSaturation ("Awake Colours: Extra Saturation (grey stays grey)", Range(0, 1)) = 0.28
        _VividLift ("Awake Colours: Midtone Lift", Range(0, 1)) = 0.22
        _ShopSaturation ("Shop: Extra Saturation", Range(0, 1)) = 0.2
        _ShopLift ("Shop: Midtone Lift", Range(0, 1)) = 0.34
        _ShopContrast ("Shop: Contrast", Range(0, 1)) = 0.1
        _ShopPivot ("Shop: Contrast Pivot", Range(0, 1)) = 0.08
        _ShopShadowTint ("Shop: Shadow Tint", Color) = (0.84, 0.88, 1.02, 1)
        _ShopShadowEnd ("Shop: Shadows End At (brightness)", Range(0.01, 1)) = 0.14
        _VividFadeStart ("Awake Colours Fade From (m)", Float) = 120
        _VividFadeEnd ("Awake Colours Gone At (m)", Float) = 260
        _FarAirAmount ("Far Hills Air (terrain rim, fixed to the world)", Range(0, 1)) = 0.62
        _FarAirFrom ("Far Hills Air From (m from the map centre)", Float) = 150
        _FarAirTo ("Far Hills Air Full At (m from the map centre)", Float) = 235
        _FarAirColor ("Far Hills Air Colour", Color) = (0.52, 0.46, 0.72, 1)
        _FarAirSunColor ("Far Hills Air Towards The Sun", Color) = (0.84, 0.56, 0.5, 1)
        _FarAirIceColor ("Far Hills Air Away From The Sun", Color) = (0.38, 0.5, 0.78, 1)
        _RimFadeStart ("Rim Fades From (m)", Float) = 90
        _RimFadeEnd ("Rim Gone At (m)", Float) = 150
        _MistAmount ("Mist Amount", Range(0, 1)) = 0
        [HDR] _MistColor ("Mist Colour (linear)", Color) = (0.62, 0.58, 0.85, 1)
        _MistStart ("Mist From (m)", Float) = 55
        _MistFade ("Mist Fade In Over (m)", Float) = 40
        _MistBase ("Mist Floor Height (world y)", Float) = -1
        _MistHeight ("Mist Depth (m above the floor)", Float) = 5
        _MistPatchSize ("Mist Patch Size (m, fixed to the world; 0 = even sheet)", Float) = 38
        _MistPatchDrift ("Mist Patch Drift (m per second)", Float) = 0.35
        _MistPatchEdge ("Mist Patch Softness", Range(0.05, 1)) = 0.55
        _MistScatter ("Mist Sun Scatter (glow looking towards the sun)", Range(0, 3)) = 1.1
        _MistScatterTight ("Mist Scatter Tightness (0 = wide, 0.95 = only near the sun)", Range(0, 0.95)) = 0.62
        _MistGlowBleed ("Mist Glow Bleed (bright things light the mist round them)", Range(0, 2)) = 0.8
        _MistGlowRadius ("Mist Glow Radius (of screen height)", Range(0.005, 0.12)) = 0.045
        [Header(Sunset Rim On Far Silhouettes)]
        _RimAmount ("Rim Amount", Range(0, 1)) = 0
        [HDR] _RimColor ("Rim Colour (sun side)", Color) = (1.0, 0.62, 0.38, 1)
        _RimStart ("Rim From (m)", Float) = 35
        _RimWidth ("Rim Width (pixels at 1080p)", Range(1, 12)) = 4
        _RimSpread ("Rim Sun-Side Falloff (higher = narrower)", Range(0.5, 8)) = 1.5
        _RimNight ("Rim On The Far Side (x of the colour, cool)", Range(0, 1)) = 0.25
        [HDR] _RimNightColor ("Rim Colour (far side)", Color) = (0.45, 0.6, 1.0, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off
        Pass
        {
            Name "AerialPerspective"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/Art/Environment/Shaders/GroundFog.hlsl"   // the haze lifts with the grey (WorldWakeFogScale)

            float4 _HazeColor;
            float4 _HazeSunColor, _HazeIceColor;
            float _HazeSunAmount, _HazeIceAmount, _HazeSideSpread;
            float _HazeStart, _HazeEnd, _HazeAmount, _HazeCurve, _DetailKeep, _NearEnd, _NearDeepen, _NearSaturate, _GlowKeep, _SkyFrom, _SkyFull;
            float _LayerMode, _LayerSoft, _LayerDesat;
            float4 _LayerEdges, _LayerHaze;
            float _MistAmount, _MistStart, _MistFade, _MistBase, _MistHeight;
            float _MistPatchSize, _MistPatchDrift, _MistPatchEdge, _MistScatter, _MistScatterTight, _MistGlowBleed, _MistGlowRadius;
            float _FarAirAmount, _FarAirFrom, _FarAirTo, _RimFadeStart, _RimFadeEnd;
            float _VividSaturation, _VividLift, _VividFadeStart, _VividFadeEnd, _ShopSaturation, _ShopLift, _ShopContrast, _ShopPivot, _ShopShadowEnd;
            float4 _ShopShadowTint;
            float4 _FarAirColor, _FarAirSunColor, _FarAirIceColor;

            // world-fixed mist patches: soft value noise, two octaves, drifting slowly (they stay where they are as you walk)
            float MistHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float MistNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(MistHash(i), MistHash(i + float2(1, 0)), f.x), lerp(MistHash(i + float2(0, 1)), MistHash(i + float2(1, 1)), f.x), f.y);
            }
            // forward scattering (Henyey-Greenstein, scaled to 1 looking straight at the light)
            float MistPhase(float3 viewDir)
            {
                float g = _MistScatterTight, c = dot(viewDir, normalize(_MainLightPosition.xyz));
                return pow((1 - g) * (1 - g) / max(1 + g * g - 2 * g * c, 1e-4), 1.5);
            }
            // light of bright pixels round this one, picked up by the mist (two rings of taps, only where there is mist)
            half3 MistGlowAround(float2 uv)
            {
                static const float2 dirs[8] = { float2(1, 0), float2(0.707, 0.707), float2(0, 1), float2(-0.707, 0.707), float2(-1, 0), float2(-0.707, -0.707), float2(0, -1), float2(0.707, -0.707) };
                float2 r = _MistGlowRadius * float2(_ScreenParams.y / _ScreenParams.x, 1);
                half3 sum = 0;
                for (int i = 0; i < 8; i++)
                {
                    sum += max(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + dirs[i] * r).rgb - _GlowKeep, 0);
                    sum += max(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + dirs[(i + 3) % 8] * r * 0.5).rgb - _GlowKeep, 0);
                }
                return sum / 16;
            }
            float MistPatch(float2 xz)
            {
                if (_MistPatchSize <= 0.001) return 1;
                float2 p = (xz + float2(_Time.y, _Time.y * 0.6) * _MistPatchDrift) / _MistPatchSize;
                float n = MistNoise(p) * 0.65 + MistNoise(p * 2.3 + 7.1) * 0.35;
                return smoothstep(0.5 - _MistPatchEdge * 0.5, 0.5 + _MistPatchEdge * 0.5, n);
            }
            float _LayerColAmount;
            float4 _LayerColMid, _LayerColFar, _LayerColHorizon;
            float _RayAmount, _RayLength, _RayDecay, _RayFalloff, _RayOnSky, _CloudRayBoost;
            float4 _CloudRayColor;
            float _MoonRayAmount;
            float4 _MoonRayColor;
            float4 _DSEWA_MoonDir;   // xyz towards the moon, w = 1 while it shows (MoonLensFlare)
            static float _CloudGap;   // per pixel: share of the blockers along the ray that were 3D clouds (set by SunRays)
            float4 _RayColor;
            float4 _MistColor;
            float _RimAmount, _RimStart, _RimWidth, _RimSpread, _RimNight;
            float4 _RimColor, _RimNightColor;

            // stepped haze: flat within a layer, a soft step at each layer start (painted-background look)
            float LayerStep(float d, float edge, float soft) { float w = max(edge * soft, 0.5); return smoothstep(edge - w, edge + w, d); }
            float LayerHaze(float d)
            {
                float m = LayerStep(d, _LayerEdges.x, _LayerSoft);
                float f = LayerStep(d, _LayerEdges.y, _LayerSoft);
                float h = LayerStep(d, _LayerEdges.z, _LayerSoft);
                return lerp(lerp(lerp(0, _LayerHaze.x, m), _LayerHaze.y, f), _LayerHaze.z, h);
            }
            float LayerIndex(float d)   // 0 near .. 3 horizon, soft
            {
                return LayerStep(d, _LayerEdges.x, _LayerSoft) + LayerStep(d, _LayerEdges.y, _LayerSoft) + LayerStep(d, _LayerEdges.z, _LayerSoft);
            }
            bool IsSky(float2 uv)
            {
                float r = SampleSceneDepth(uv);
            #if UNITY_REVERSED_Z
                return r <= 0.0000001;
            #else
                return r >= 0.9999999;
            #endif
            }

            half3 LayerColour(float d, half3 baseCol)
            {
                float m = LayerStep(d, _LayerEdges.x, _LayerSoft);
                float f = LayerStep(d, _LayerEdges.y, _LayerSoft);
                float h = LayerStep(d, _LayerEdges.z, _LayerSoft);
                half3 c = lerp(lerp(lerp(baseCol, _LayerColMid.rgb, m), _LayerColFar.rgb, f), _LayerColHorizon.rgb, h);
                return lerp(baseCol, c, _LayerColAmount);
            }

            // sun rays (screen-space light scattering, GPU Gems 3 ch. 13 style): march from the pixel towards the sun on
            // screen; open sky along the way lets light through, trees / the lighthouse block it -> shafts between them
            // 3D toon clouds (ToonCloud publishes them): they float above the ground haze and keep their own toon
            // light and shade, so the haze, mist, layer greying and rim skip them (2026-10-30, user)
            float4 _DSEWA_Clouds[16];   // xyz centre, w radius
            float _DSEWA_CloudCount;
            bool InCloud(float3 wp)
            {
                [loop] for (int c = 0; c < 16; c++)
                {
                    if (c >= (int)_DSEWA_CloudCount) break;
                    if (distance(wp, _DSEWA_Clouds[c].xyz) < _DSEWA_Clouds[c].w) return true;
                }
                return false;
            }

            half SunRays(float2 uv, float3 L)
            {
                float4 sc = mul(UNITY_MATRIX_VP, float4(_WorldSpaceCameraPos + L * 1000.0, 1));
                if (sc.w <= 0) return 0;
                float2 sunUV = sc.xy / sc.w;
            #if UNITY_UV_STARTS_AT_TOP
                sunUV.y = -sunUV.y;
            #endif
                sunUV = sunUV * 0.5 + 0.5;
                // the sun may sit a little off screen and still throw rays in
                float onScreen = 1 - smoothstep(0.15, 0.6, max(max(-sunUV.x, sunUV.x - 1), max(-sunUV.y, sunUV.y - 1)));
                if (onScreen <= 0) return 0;
                float2 aspect = float2(_ScreenParams.x / _ScreenParams.y, 1);
                float2 delta = (sunUV - uv) * _RayLength / 24.0;
                float2 p = uv;
                float w = 1, sum = 0, norm = 0, blocked = 0, cloudBlocked = 0;
                [loop] for (int i = 0; i < 24; i++)
                {
                    p += delta;
                    if (any(p < 0) || any(p > 1)) break;
                    float rawP = SampleSceneDepth(p);
                #if UNITY_REVERSED_Z
                    bool sky = rawP <= 0.0000001;
                #else
                    bool sky = rawP >= 0.9999999;
                #endif
                    sum += (sky ? 1.0 : 0.0) * w;
                    if (!sky && _DSEWA_CloudCount > 0)
                    {
                        blocked += w;
                        if (InCloud(ComputeWorldSpacePosition(p, rawP, UNITY_MATRIX_I_VP))) cloudBlocked += w;
                    }
                    norm += w;
                    w *= _RayDecay;
                }
                float lit = norm > 0 ? sum / norm : 0;
                // rays streaming through gaps between 3D clouds: open sky AND cloud edges along the way
                _CloudGap = blocked > 0 ? cloudBlocked / blocked : 0;
                lit *= 1 + _CloudRayBoost * _CloudGap * 4 * lit * (1 - lit);
                float distToSun = length((uv - sunUV) * aspect);
                float near = exp(-distToSun * _RayFalloff);
                return lit * near * onScreen;
            }

            float _DSEWA_HazeCut;   // 0 = as authored; LightUpDirector's high top view thins the haze (1 = off)
            float4 _DSEWA_InteriorBoxMin, _DSEWA_InteriorBoxMax;   // shop box from InteriorFill: background-only effect, off while the camera is inside

            bool CameraInShop()
            {
                float3 c = _WorldSpaceCameraPos;
                return _DSEWA_InteriorBoxMax.x > _DSEWA_InteriorBoxMin.x && all(c >= _DSEWA_InteriorBoxMin.xyz) && all(c <= _DSEWA_InteriorBoxMax.xyz);
            }

            // the shop has no haze, but it takes the same "awake colours" step as outdoors so the two feel like one world
            half4 ShopGrade(half4 col)
            {
                float lum = dot(col.rgb, float3(0.2126, 0.7152, 0.0722));
                float keep = 1 - smoothstep(_GlowKeep, _GlowKeep * 1.6, lum);
                // lift the brightness alone: a curve on each channel would wash the colours out
                float lifted = pow(max(lum, 1e-4), 1 / (1 + _ShopLift * keep));
                lifted = max(_ShopPivot + (lifted - _ShopPivot) * (1 + _ShopContrast * keep), 1e-4);   // on brightness too: per channel it turns dark walls blue
                col.rgb *= lifted / max(lum, 1e-4);
                // only the dark tones turn navy: lit faces keep their colour, so the room's tone does not stain the props
                float dark = 1 - smoothstep(0, _ShopShadowEnd, lifted);
                col.rgb *= lerp(float3(1, 1, 1), _ShopShadowTint.rgb, dark * keep);
                col.rgb = max(lerp(lifted.xxx, col.rgb, 1 + _ShopSaturation * keep), 0);
                return col;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (CameraInShop()) return ShopGrade(col);
                float raw = SampleSceneDepth(uv);
                _CloudGap = 0;
                half rays = _RayAmount > 0 ? SunRays(uv, normalize(_MainLightPosition.xyz)) * _RayAmount * (1 - saturate(_DSEWA_HazeCut)) : 0;
                half3 rayLight = lerp(_RayColor.rgb, _CloudRayColor.rgb, saturate(_CloudGap * 1.5)) * rays;
                if (_MoonRayAmount > 0 && _DSEWA_MoonDir.w > 0.5)
                    rayLight += _MoonRayColor.rgb * SunRays(uv, _DSEWA_MoonDir.xyz) * _MoonRayAmount * (1 - saturate(_DSEWA_HazeCut));
            #if UNITY_REVERSED_Z
                if (raw <= 0.0000001) { col.rgb += rayLight * _RayOnSky; return col; }          // sky
            #else
                if (raw >= 0.9999999) { col.rgb += rayLight * _RayOnSky; return col; }
            #endif
                float d = LinearEyeDepth(raw, _ZBufferParams);
                if (_DSEWA_CloudCount > 0 && InCloud(ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP)))
                { col.rgb += rayLight * _RayOnSky; return col; }   // a cloud: sky treatment (rays only)
                float lum = dot(col.rgb, float3(0.2126, 0.7152, 0.0722));
                float glow = smoothstep(_GlowKeep, _GlowKeep * 1.6, lum);

                // far: toward the haze, keeping part of the pixel's own value around the haze colour
                float t = pow(smoothstep(_HazeStart, _HazeEnd, d), _HazeCurve) * _HazeAmount;
                if (_LayerMode > 0.5) t = LayerHaze(d);
                t *= 1 - glow;
                t *= 1 - saturate(_DSEWA_HazeCut);
                t *= WorldWakeFogScale(ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP));
                t *= 1 - smoothstep(_SkyFrom, _SkyFull, d);   // cloud and sky meshes far out keep their painted look
                // eyeball planet (2026-10-30): warmer haze towards the sun's azimuth (day side desert), colder away from it
                // (night side ice)
                half3 hazeCol = _HazeColor.rgb;
                if (_LayerMode > 0.5 && _LayerColAmount > 0) hazeCol = LayerColour(d, hazeCol);   // each layer its own air colour (Firewatch-style colour-banded fog)
                if (_HazeSunAmount + _HazeIceAmount > 0)
                {
                    float3 wp = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                    float2 v = wp.xz - _WorldSpaceCameraPos.xz;
                    float az = saturate(dot(normalize(v + 1e-5), normalize(_MainLightPosition.xz + 1e-5)) * 0.5 + 0.5);
                    hazeCol = lerp(hazeCol, _HazeSunColor.rgb, pow(az, _HazeSideSpread) * _HazeSunAmount);
                    hazeCol = lerp(hazeCol, _HazeIceColor.rgb, pow(1 - az, _HazeSideSpread) * _HazeIceAmount);
                }
                float hazeLum = dot(hazeCol, float3(0.2126, 0.7152, 0.0722));
                half3 hazed = hazeCol * lerp(1.0, lum / max(hazeLum, 1e-3), _DetailKeep * 0.5);
                col.rgb = lerp(col.rgb, hazed, t);
                // far hills: the terrain's rim sinks into the same air as the mountains behind it. Fixed to the world round the map
                // centre, not to the camera, so nothing clears as you walk (2026-11-01, user)
                if (_FarAirAmount > 0)
                {
                    float3 wpa = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                    float airK = smoothstep(_FarAirFrom, _FarAirTo, length(wpa.xz)) * _FarAirAmount * (1 - glow) * (1 - saturate(_DSEWA_HazeCut)) * (1 - smoothstep(_SkyFrom, _SkyFull, d));
                    float2 va = normalize(wpa.xz - _WorldSpaceCameraPos.xz + 1e-5);
                    float azA = saturate(dot(va, normalize(_MainLightPosition.xz + 1e-5)) * 0.5 + 0.5);
                    half3 airCol = lerp(_FarAirColor.rgb, _FarAirSunColor.rgb, pow(azA, _HazeSideSpread));
                    airCol = lerp(airCol, _FarAirIceColor.rgb, pow(1 - azA, _HazeSideSpread));
                    float airLum = dot(airCol, float3(0.299, 0.587, 0.114));
                    airCol = lerp(airCol, airLum * half3(0.78, 0.82, 0.95) * 0.72, WorldWakeOutside(wpa));   // grey with the sleeping world
                    col.rgb = lerp(col.rgb, airCol, airK);
                }
                float wake = WorldWakeFogScale(ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP)) * (1 - saturate(_DSEWA_HazeCut));
                float skyKeep = 1 - smoothstep(_SkyFrom, _SkyFull, d);
                if (_LayerMode > 0.5 && _LayerDesat > 0)   // each layer a little greyer
                {
                    float lg = dot(col.rgb, float3(0.2126, 0.7152, 0.0722));
                    col.rgb = lerp(col.rgb, lg.xxx, saturate(LayerIndex(d) * _LayerDesat) * (1 - glow) * skyKeep);
                }

                // valley mist: low pale patches lying in the low ground, fixed to the world (2026-11-01, user: a clear ring that
                // follows the camera reads wrong in a dreamy background) - only the first metres stay open so it never fogs the lens
                if (_MistAmount > 0)
                {
                    float3 wpm = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                    float low = 1 - smoothstep(_MistBase, _MistBase + max(_MistHeight, 0.1), wpm.y);
                    float m = smoothstep(_MistStart, _MistStart + max(_MistFade, 1), d) * low * low * MistPatch(wpm.xz) * _MistAmount * (1 - glow) * wake * skyKeep;
                    half3 mc = lerp(_MistColor.rgb, hazeCol * 1.6, 0.35);
                    half3 lit = 0;
                    if (m > 0.002)
                    {
                        // the mist catches the light: warm towards the sun, and round anything glowing
                        float3 view = normalize(wpm - _WorldSpaceCameraPos);
                        half3 sunCol = lerp(_MainLightColor.rgb, _HazeSunColor.rgb, 0.5);
                        lit = sunCol * MistPhase(view) * _MistScatter;
                        if (_MistGlowBleed > 0) lit += MistGlowAround(uv) * _MistGlowBleed;
                    }
                    col.rgb = lerp(col.rgb, mc, m) + lit * m;
                }

                // sunset rim: far silhouettes that have open sky just above them get a thin bright edge, warm towards
                // the sun's azimuth (day side), faint and cool on the far side
                if (_RimAmount > 0 && d > _RimStart * 0.5 && d < _RimFadeEnd)
                {
                    float px = _RimWidth * (_ScreenParams.y / 1080.0);
                    float2 ofs = float2(0, px / _ScreenParams.y);
                    float rim = 0;
                    [unroll] for (int k = 1; k <= 4; k++)
                        if (rim == 0 && IsSky(uv + ofs * (k / 4.0))) rim = 1 - (k - 1) / 4.0;
                    if (rim > 0)
                    {
                        float3 wpr = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                        float2 vr = wpr.xz - _WorldSpaceCameraPos.xz;
                        float azr = saturate(dot(normalize(vr + 1e-5), normalize(_MainLightPosition.xz + 1e-5)) * 0.5 + 0.5);
                        float sunW = pow(azr, _RimSpread);
                        half3 rc = lerp(_RimNightColor.rgb, _RimColor.rgb, sunW);
                        float k2 = rim * smoothstep(_RimStart * 0.5, _RimStart, d) * (1 - smoothstep(_RimFadeStart, _RimFadeEnd, d)) * _RimAmount * (1 - glow) * (_RimNight + (1 - _RimNight) * sunW);
                        col.rgb = lerp(col.rgb, rc, saturate(k2));
                    }
                }

                // sun rays over the scene: stronger in the air far away than on the near ground
                col.rgb += rayLight * lerp(0.35, 1.0, smoothstep(5, 60, d));

                // near: a little deeper and richer
                float n = (1 - smoothstep(0, _NearEnd, d)) * (1 - glow);
                float l2 = dot(col.rgb, float3(0.2126, 0.7152, 0.0722));
                col.rgb = lerp(l2.xxx, col.rgb, 1 + _NearSaturate * n);
                col.rgb *= 1 - _NearDeepen * n;

                // awake colours: brighter and richer where the world is awake, the grey is left alone (2026-11-01, user).
                // Fades with distance so the far hills and mountains keep their air
                if (_VividSaturation + _VividLift > 0)
                {
                    float vivid = (1 - WorldWakeOutside(ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP))) * (1 - glow) * (1 - smoothstep(_VividFadeStart, _VividFadeEnd, d));
                    float l3 = dot(col.rgb, float3(0.2126, 0.7152, 0.0722));
                    col.rgb = max(lerp(l3.xxx, col.rgb, 1 + _VividSaturation * vivid), 0);
                    col.rgb = pow(col.rgb, 1 / (1 + _VividLift * vivid));
                }
                return col;
            }
            ENDHLSL
        }
    }
}
