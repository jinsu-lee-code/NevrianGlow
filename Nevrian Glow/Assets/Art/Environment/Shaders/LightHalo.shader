// Light scattered in the air round a lamp (2026-10-30, user: "add light scattering" to the shop orb lamps). A quad that
// always faces the camera, centred on the object, with a soft falloff: a bright core fading out like light through
// haze (exp + a long 1/(1+k d^2) tail). Additive, no depth write. Where the halo cuts into a wall or the ceiling it
// fades out (soft particles from the depth texture), and it thins when the camera gets inside it.
// Colour stays well under the bloom / star glare thresholds so post does not turn it into rays. All numbers are mine.
Shader "DSEWA/LightHalo"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.55, 0.25, 1)
        _Intensity ("Intensity", Range(0, 2)) = 0.35
        _Size ("Size (m, whole quad)", Float) = 2.5
        _Core ("Core Tightness", Range(1, 30)) = 9
        _Tail ("Tail Amount", Range(0, 1)) = 0.35
        _SoftDepth ("Fade Into Walls (m)", Range(0.01, 2)) = 0.4
        _NearFade ("Fade When The Camera Is Closer Than (m)", Range(0, 3)) = 0.8
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Halo"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity, _Size, _Core, _Tail, _SoftDepth, _NearFade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                float camDist : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 centreWS = TransformObjectToWorld(float3(0, 0, 0));
                // billboard: quad corners laid out along the camera's right / up
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float2 corner = (v.uv - 0.5) * _Size;
                float3 posWS = centreWS + right * corner.x + up * corner.y;
                o.positionCS = TransformWorldToHClip(posWS);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.eyeDepth = -TransformWorldToView(posWS).z;
                o.camDist = distance(_WorldSpaceCameraPos, centreWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float d = length(i.uv - 0.5) * 2.0;                     // 0 centre .. 1 quad edge
                float core = exp(-d * d * _Core);
                float tail = _Tail / (1.0 + 40.0 * d * d);
                float h = (core + tail) * saturate(1.0 - d);              // ends at the quad edge
                float2 suv = i.screenPos.xy / i.screenPos.w;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                h *= saturate((sceneEye - i.eyeDepth) / _SoftDepth);
                h *= saturate(i.camDist / max(_NearFade, 0.01));
                return half4(_Color.rgb * h * _Intensity, 0);
            }
            ENDHLSL
        }
    }
}
