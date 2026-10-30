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
        [Header(Valley Mist)]
        _MistAmount ("Mist Amount", Range(0, 1)) = 0
        [HDR] _MistColor ("Mist Colour (linear)", Color) = (0.62, 0.58, 0.85, 1)
        _MistStart ("Mist From (m)", Float) = 55
        _MistFade ("Mist Fade In Over (m)", Float) = 40
        _MistBase ("Mist Floor Height (world y)", Float) = -1
        _MistHeight ("Mist Depth (m above the floor)", Float) = 5
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
            float _LayerColAmount;
            float4 _LayerColMid, _LayerColFar, _LayerColHorizon;
            float _RayAmount, _RayLength, _RayDecay, _RayFalloff, _RayOnSky, _CloudRayBoost;
            float4 _CloudRayColor;
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

            half SunRays(float2 uv)
            {
                float3 L = normalize(_MainLightPosition.xyz);
                float4 sc = mul(UNITY_MATRIX_VP, float4(_WorldSpaceCameraPos + L * 1000.0, 1));
                if (sc.w <= 0) return 0;
                float2 sunUV = sc.xy / sc.w;
            #if UNITY_UV_STARTS_AT_TOP
                sunUV.y = -sunUV.y;
            #endif
                sunUV = sunUV * 0.5 + 0.5;
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
                // the sun may sit a little off screen and still throw rays in
                float onScreen = 1 - smoothstep(0.15, 0.6, max(max(-sunUV.x, sunUV.x - 1), max(-sunUV.y, sunUV.y - 1)));
                return lit * near * onScreen;
            }

            float _DSEWA_HazeCut;   // 0 = as authored; LightUpDirector's high top view thins the haze (1 = off)
            float4 _DSEWA_InteriorBoxMin, _DSEWA_InteriorBoxMax;   // shop box from InteriorFill: background-only effect, off while the camera is inside

            bool CameraInShop()
            {
                float3 c = _WorldSpaceCameraPos;
                return _DSEWA_InteriorBoxMax.x > _DSEWA_InteriorBoxMin.x && all(c >= _DSEWA_InteriorBoxMin.xyz) && all(c <= _DSEWA_InteriorBoxMax.xyz);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (CameraInShop()) return col;
                float raw = SampleSceneDepth(uv);
                _CloudGap = 0;
                half rays = _RayAmount > 0 ? SunRays(uv) * _RayAmount * (1 - saturate(_DSEWA_HazeCut)) : 0;
                half3 rayCol = lerp(_RayColor.rgb, _CloudRayColor.rgb, saturate(_CloudGap * 1.5));
            #if UNITY_REVERSED_Z
                if (raw <= 0.0000001) { col.rgb += rayCol * rays * _RayOnSky; return col; }          // sky
            #else
                if (raw >= 0.9999999) { col.rgb += rayCol * rays * _RayOnSky; return col; }
            #endif
                float d = LinearEyeDepth(raw, _ZBufferParams);
                if (_DSEWA_CloudCount > 0 && InCloud(ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP)))
                { col.rgb += rayCol * rays * _RayOnSky; return col; }   // a cloud: sky treatment (rays only)
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
                float wake = WorldWakeFogScale(ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP)) * (1 - saturate(_DSEWA_HazeCut));
                float skyKeep = 1 - smoothstep(_SkyFrom, _SkyFull, d);
                if (_LayerMode > 0.5 && _LayerDesat > 0)   // each layer a little greyer
                {
                    float lg = dot(col.rgb, float3(0.2126, 0.7152, 0.0722));
                    col.rgb = lerp(col.rgb, lg.xxx, saturate(LayerIndex(d) * _LayerDesat) * (1 - glow) * skyKeep);
                }

                // valley mist: a low pale sheet from the mid/far boundary on, lying in the low ground, so the foot of each
                // far layer (the cliff base, the far forest floor) goes pale and the layer above stands off it
                if (_MistAmount > 0)
                {
                    float3 wpm = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                    float low = 1 - smoothstep(_MistBase, _MistBase + max(_MistHeight, 0.1), wpm.y);
                    float m = smoothstep(_MistStart, _MistStart + max(_MistFade, 1), d) * low * low * _MistAmount * (1 - glow) * wake * skyKeep;
                    half3 mc = lerp(_MistColor.rgb, hazeCol * 1.6, 0.35);
                    col.rgb = lerp(col.rgb, mc, m);
                }

                // sunset rim: far silhouettes that have open sky just above them get a thin bright edge, warm towards
                // the sun's azimuth (day side), faint and cool on the far side
                if (_RimAmount > 0 && d > _RimStart * 0.5)
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
                        float k2 = rim * smoothstep(_RimStart * 0.5, _RimStart, d) * _RimAmount * (1 - glow) * (_RimNight + (1 - _RimNight) * sunW);
                        col.rgb = lerp(col.rgb, rc, saturate(k2));
                    }
                }

                // sun rays over the scene: stronger in the air far away than on the near ground
                col.rgb += rayCol * rays * lerp(0.35, 1.0, smoothstep(5, 60, d));

                // near: a little deeper and richer
                float n = (1 - smoothstep(0, _NearEnd, d)) * (1 - glow);
                float l2 = dot(col.rgb, float3(0.2126, 0.7152, 0.0722));
                col.rgb = lerp(l2.xxx, col.rgb, 1 + _NearSaturate * n);
                col.rgb *= 1 - _NearDeepen * n;
                return col;
            }
            ENDHLSL
        }
    }
}
