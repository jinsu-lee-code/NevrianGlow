// Painterly brush filter (2026-10-29, background post step 1): a Full Screen Pass renderer feature runs this before
// post-processing, ahead of the screen outline. A generalised Kuwahara: around each pixel four overlapping quadrants
// (4 x 4 taps each, spaced Radius / 3 px apart) give a mean colour and a variance; quadrants are weighted by their
// variance relative to the calmest one, (variance / min)^-Sharpness, so the calmest side wins (a plain average would
// only blur). Flat areas keep their colour and busy detail (grass blades, leaf noise) settles
// into soft brush patches while real edges stay put. Strength follows distance (strong up close where the grass is
// busiest, light far away where the depth of field already softens), and skips the sky (painted already) and HDR
// glows (lighthouse windows, crystals, fruit) so they stay crisp for the bloom. Off while the camera is in the shop box (InteriorFill); the shop window view, rendered from outside, keeps it. All numbers are my own values.
Shader "Hidden/DSEWA/PaintBrush"
{
    Properties
    {
        _Radius ("Brush Radius (px at 1080p)", Range(1, 8)) = 3.5
        _Sharpness ("Pick Calmest Side", Range(1, 16)) = 8
        _NearStrength ("Strength Near", Range(0, 1)) = 0.85
        _FarStrength ("Strength Far", Range(0, 1)) = 0.35
        _FadeStart ("Near Until (m)", Float) = 8
        _FadeEnd ("Far From (m)", Float) = 90
        _GlowKeep ("Keep Glows Above (HDR luminance)", Float) = 1.1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off
        Pass
        {
            Name "PaintBrush"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float _Radius, _Sharpness, _NearStrength, _FarStrength, _FadeStart, _FadeEnd, _GlowKeep;
            float4 _DSEWA_InteriorBoxMin, _DSEWA_InteriorBoxMax;   // shop box from InteriorFill: background-only effect, off while the camera is inside

            bool CameraInShop()
            {
                float3 c = _WorldSpaceCameraPos;
                return _DSEWA_InteriorBoxMax.x > _DSEWA_InteriorBoxMin.x && all(c >= _DSEWA_InteriorBoxMin.xyz) && all(c <= _DSEWA_InteriorBoxMax.xyz);
            }

            half3 Tap(float2 uv) { return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb; }

            // one quadrant: 4 x 4 taps from the centre toward (sx, sy); returns mean (rgb) and variance (a)
            float4 Quadrant(float2 uv, float2 step, float sx, float sy)
            {
                float3 sum = 0, sum2 = 0;
                [unroll] for (int j = 0; j < 4; j++)
                [unroll] for (int i = 0; i < 4; i++)
                {
                    float3 c = Tap(uv + float2(i * sx, j * sy) * step);
                    c = c / (1 + c);                       // compress HDR so a single bright tap does not rule the variance
                    sum += c; sum2 += c * c;
                }
                float3 m = sum / 16.0;
                float3 v = abs(sum2 / 16.0 - m * m);
                return float4(m, v.r + v.g + v.b);
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
                float strength = lerp(_NearStrength, _FarStrength, smoothstep(_FadeStart, _FadeEnd, d));
                float lum = dot(col.rgb, float3(0.2126, 0.7152, 0.0722));
                strength *= 1 - smoothstep(_GlowKeep, _GlowKeep * 1.6, lum);
                if (strength <= 0.001) return col;

                float2 step = (_Radius / 3.0) * (_ScaledScreenParams.y / 1080.0) / _ScaledScreenParams.xy;
                float4 q0 = Quadrant(uv, step, -1, -1), q1 = Quadrant(uv, step, 1, -1);
                float4 q2 = Quadrant(uv, step, -1, 1), q3 = Quadrant(uv, step, 1, 1);
                float4 v = float4(q0.a, q1.a, q2.a, q3.a) + 1e-6;
                float4 w = pow(v / min(min(v.x, v.y), min(v.z, v.w)), -_Sharpness);
                float3 m = (q0.rgb * w.x + q1.rgb * w.y + q2.rgb * w.z + q3.rgb * w.w) / max(dot(w, 1), 1e-5);
                m = m / max(1 - m, 1e-3);                   // back to HDR
                col.rgb = lerp(col.rgb, m, strength);
                return col;
            }
            ENDHLSL
        }
    }
}
