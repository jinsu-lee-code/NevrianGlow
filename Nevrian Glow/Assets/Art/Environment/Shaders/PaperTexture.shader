// Paper texture (2026-10-29, background post step 5): a Full Screen Pass renderer feature runs this after
// post-processing (display colours, so the grade and tonemapping do not change its strength). A procedural paper
// fixed to the screen like the canvas under a painting: soft cloudy tooth at two scales plus faint horizontal
// fibres, multiplied in very lightly so the 3D parts pick up the same surface as the painted sky. The film grain
// comes from the URP Film Grain override on the background volumes. Off while the camera is in the shop box
// (InteriorFill). All numbers are my own values.
Shader "Hidden/DSEWA/PaperTexture"
{
    Properties
    {
        _Strength ("Strength", Range(0, 0.3)) = 0.06
        _Scale ("Tooth Size (px at 1080p)", Range(1, 16)) = 5
        _FibreStrength ("Fibres", Range(0, 1)) = 0.35
        _ShadowKeep ("Lighter In The Darks", Range(0, 1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off
        Pass
        {
            Name "PaperTexture"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Strength, _Scale, _FibreStrength, _ShadowKeep;
            float4 _DSEWA_InteriorBoxMin, _DSEWA_InteriorBoxMax;

            bool CameraInShop()
            {
                float3 c = _WorldSpaceCameraPos;
                return _DSEWA_InteriorBoxMax.x > _DSEWA_InteriorBoxMin.x && all(c >= _DSEWA_InteriorBoxMin.xyz) && all(c <= _DSEWA_InteriorBoxMax.xyz);
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (CameraInShop()) return col;
                float2 px = uv * _ScaledScreenParams.xy * (1080.0 / _ScaledScreenParams.y);   // resolution-independent
                float tooth = Noise(px / _Scale) * 0.55 + Noise(px / (_Scale * 4.3) + 17.0) * 0.45;
                float fibre = Noise(float2(px.x / (_Scale * 18.0), px.y / (_Scale * 0.9)) + 41.0);
                float paper = lerp(tooth, fibre, _FibreStrength * 0.5);        // 0..1 around 0.5
                float lum = dot(col.rgb, float3(0.2126, 0.7152, 0.0722));
                float k = _Strength * lerp(1 - _ShadowKeep, 1, saturate(lum * 2));   // softer in the darks so they do not go grey
                col.rgb *= 1 + (paper - 0.5) * 2 * k;
                return col;
            }
            ENDHLSL
        }
    }
}
