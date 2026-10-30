// Interact highlight outline (2026-10-30, user: "a bright outline on the things you can interact with"): InteractHighlight
// draws the aimed-at object's meshes once more with this material. Two passes:
//  1. Mask  - the object's whole silhouette, colour off, marked in the stencil
//  2. Line  - the mesh pushed out by a constant width on screen, drawn only where the mask is not - so only a ring
//             round the silhouette shows (an inverted hull alone also lit the gable and the door seams from inside)
// The push direction leans toward "away from the mesh centre" so hard-edged boxes do not split at the corners.
// Drawn in the transparent pass: this renderer primes depth, so an opaque-queue shader with no depth pass is rejected.
// Colour stays below the night bloom / star glare thresholds so the line is not smeared by post. All numbers are mine.
Shader "DSEWA/InteractOutline"
{
    Properties
    {
        _Color ("Colour (keep below 1: no bloom)", Color) = (0.92, 0.8, 0.5, 1)
        _Width ("Width (pixels at 1080p)", Range(0.5, 8)) = 3
        _CentreBias ("Push Away From The Centre (0 = along normals, 1 = from the centre)", Range(0, 1)) = 0.5
        _Pulse ("Breathing (0 = steady)", Range(0, 1)) = 0.2
        _PulseSpeed ("Breathing Speed", Float) = 3
        _MeshCentre ("Mesh Centre (object space, set by script)", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _Width, _CentreBias, _Pulse, _PulseSpeed;
            float4 _MeshCentre;
        CBUFFER_END
        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
        struct Varyings { float4 positionCS : SV_POSITION; };
        ENDHLSL

        // URP draws SRPDefaultUnlit before UniversalForward for the same object: mask first, line second
        Pass
        {
            Name "Mask"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            // whole silhouette, both sides, no depth test: one-sided panels (the gable) and depth wobble along the
            // plank seams must not leave holes the line shows through
            Cull Off
            ZWrite Off
            ZTest Always
            ColorMask 0
            Stencil { Ref 128 WriteMask 128 Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            Varyings vert(Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "Line"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Stencil { Ref 128 ReadMask 128 Comp NotEqual }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 fromCentre = v.positionOS.xyz - _MeshCentre.xyz;
                float3 dirOS = normalize(lerp(v.normalOS, normalize(fromCentre + 1e-5), _CentreBias));
                float4 cs = TransformObjectToHClip(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldDir(dirOS);
                float2 nCS = mul((float3x3)UNITY_MATRIX_VP, nWS).xy;
                float len = length(nCS);
                if (len > 1e-5)
                {
                    float px = _Width * (_ScreenParams.y / 1080.0);
                    cs.xy += nCS / len * px * 2.0 / _ScreenParams.xy * cs.w;
                }
                o.positionCS = cs;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half breath = 1.0 - _Pulse * (0.5 + 0.5 * sin(_Time.y * _PulseSpeed));
                return half4(_Color.rgb * breath, 1);
            }
            ENDHLSL
        }
    }
}
