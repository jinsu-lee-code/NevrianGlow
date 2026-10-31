// Light shafts under a toon cloud (2026-10-30, user: "light between the clouds", option C = screen rays + these).
// "Angel's ladder": a few long strips hang from the cloud's underside, leaning away from the sun, each turned round its
// own axis to face the camera. Stripes across the width (gaps where the cloud would block), a soft head under the
// cloud, a fade before the ground, soft where they cut into terrain (depth), a slow sway. Additive and faint, in the
// dreamy lavender-pink of the clouds. ToonCloud builds the strips (uv.x across 0..1, uv.y 0 at the cloud .. 1 at the
// far end, uv2.x = strip index). Numbers are mine.
Shader "DSEWA/CloudLightShafts"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (0.95, 0.8, 0.95, 1)
        _Intensity ("Intensity", Range(0, 1)) = 0.25
        _Stripes ("Stripes Across", Range(1, 12)) = 5
        _StripeSharp ("Stripe Sharpness", Range(0, 1)) = 0.5
        _Sway ("Sway Speed", Range(0, 0.3)) = 0.04
        _HeadFade ("Fade In Under The Cloud (0-1 of length)", Range(0.01, 0.5)) = 0.12
        _TailFade ("Fade Out Towards The End (0-1 of length)", Range(0.1, 1)) = 0.6
        _SoftDepth ("Soft Into Terrain (m)", Range(0.5, 40)) = 12
        _NearFade ("Fade When The Camera Is Closer Than (m)", Range(0, 200)) = 40
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+5" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Shafts"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity, _Stripes, _StripeSharp, _Sway, _HeadFade, _TailFade, _SoftDepth, _NearFade;
            CBUFFER_END
            // per strip, in world space, from ToonCloud: head position (xyz) + width (w), axis direction * length
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 head : TEXCOORD1; float4 axis : TEXCOORD2; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 screenPos : TEXCOORD1; float eyeDepth : TEXCOORD2; float seed : TEXCOORD3; float camDist : TEXCOORD4; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 axis = v.axis.xyz;
                float3 mid = v.head.xyz + axis * 0.5;
                float3 view = normalize(_WorldSpaceCameraPos - mid);
                float3 side = normalize(cross(normalize(axis), view) + 1e-5);
                // widen a little towards the far end (light spreads)
                float w = v.head.w * lerp(1.0, 1.6, v.uv.y);
                float3 wp = v.head.xyz + axis * v.uv.y + side * (v.uv.x - 0.5) * w;
                o.positionCS = TransformWorldToHClip(wp);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.eyeDepth = -TransformWorldToView(wp).z;
                o.seed = v.axis.w;
                o.camDist = distance(_WorldSpaceCameraPos, wp);
                return o;
            }

            float Hash(float n) { return frac(sin(n * 91.3458) * 47453.5453); }
            float Noise1(float x) { float i = floor(x), f = frac(x); f = f * f * (3 - 2 * f); return lerp(Hash(i), Hash(i + 1), f); }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _Sway;
                // stripes: two layers of 1D noise across the width, drifting slowly
                float x = i.uv.x * _Stripes + i.seed * 13.1;
                float s = Noise1(x + t) * 0.65 + Noise1(x * 2.3 - t * 1.3 + 4.7) * 0.35;
                s = smoothstep(0.5 - 0.5 * (1 - _StripeSharp), 0.5 + 0.5 * (1 - _StripeSharp) * 0.6, s);
                float across = sin(i.uv.x * PI);                       // soft sides
                float along = smoothstep(0, _HeadFade, i.uv.y) * (1 - smoothstep(1 - _TailFade, 1, i.uv.y));
                float a = s * across * across * along;
                // soft where the strip goes into the ground or behind a hill
                float2 suv = i.screenPos.xy / i.screenPos.w;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                a *= saturate((sceneEye - i.eyeDepth) / _SoftDepth);
                a *= saturate(i.camDist / max(_NearFade, 0.01));
                return half4(_Color.rgb * a * _Intensity, 0);
            }
            ENDHLSL
        }
    }
}
