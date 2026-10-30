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
            #if UNITY_REVERSED_Z
                if (raw <= 0.0000001) return col;          // sky
            #else
                if (raw >= 0.9999999) return col;
            #endif
                float d = LinearEyeDepth(raw, _ZBufferParams);
                float lum = dot(col.rgb, float3(0.2126, 0.7152, 0.0722));
                float glow = smoothstep(_GlowKeep, _GlowKeep * 1.6, lum);

                // far: toward the haze, keeping part of the pixel's own value around the haze colour
                float t = pow(smoothstep(_HazeStart, _HazeEnd, d), _HazeCurve) * _HazeAmount * (1 - glow);
                t *= 1 - saturate(_DSEWA_HazeCut);
                t *= WorldWakeFogScale(ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP));
                t *= 1 - smoothstep(_SkyFrom, _SkyFull, d);   // cloud and sky meshes far out keep their painted look
                // eyeball planet (2026-10-30): warmer haze towards the sun's azimuth (day side desert), colder away from it
                // (night side ice)
                half3 hazeCol = _HazeColor.rgb;
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
