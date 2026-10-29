// Aerial perspective (2026-10-29, background post step 2): a Full Screen Pass renderer feature runs this before
// post-processing, first of the screen passes. GroundFog only reaches the ground and is light at night, so trees,
// cliffs and the lighthouse kept the same violet value at every distance and the layers ran together. Here every
// opaque pixel (up to the cloud layer) is pulled toward the horizon haze colour with distance (lighter and softer far away, keeping a share
// of its own light and shade so shapes still read), and the near band is deepened a little so the foreground frames
// the view. Skips the sky and keeps HDR glows. Off while the camera is in the shop box (InteriorFill); the shop window view, rendered from outside, keeps it. All numbers are my own values.
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

            float4 _HazeColor;
            float _HazeStart, _HazeEnd, _HazeAmount, _HazeCurve, _DetailKeep, _NearEnd, _NearDeepen, _NearSaturate, _GlowKeep, _SkyFrom, _SkyFull;
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
                t *= 1 - smoothstep(_SkyFrom, _SkyFull, d);   // cloud and sky meshes far out keep their painted look
                float hazeLum = dot(_HazeColor.rgb, float3(0.2126, 0.7152, 0.0722));
                half3 hazed = _HazeColor.rgb * lerp(1.0, lum / max(hazeLum, 1e-3), _DetailKeep * 0.5);
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
