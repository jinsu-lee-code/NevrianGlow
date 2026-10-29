// Window pane that shows the live outside view rendered by ShopWindowView.cs (2026-10-27).
// The texture is the virtual-room camera's full screen, so the pane samples it at its own screen position:
// whatever pixel of the window you look through shows exactly what the outside camera sees there.
// Without the view (player outside the room, editor) it falls back to a flat night-sky colour.
Shader "DSEWA/ShopWindowView"
{
    Properties
    {
        _Fallback ("Fallback Colour (view off)", Color) = (0.10, 0.13, 0.30, 1)
        _Exposure ("Exposure", Range(0, 3)) = 1
        [Toggle] _FlipY ("Flip Y", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Fallback;
                half _Exposure;
                half _FlipY;
            CBUFFER_END
            TEXTURE2D(_DSEWA_ShopWindowRT); SAMPLER(sampler_DSEWA_ShopWindowRT);
            float _DSEWA_ShopWindowOn;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                if (_DSEWA_ShopWindowOn < 0.5) return half4(_Fallback.rgb, 1);
                float2 uv = GetNormalizedScreenSpaceUV(i.positionCS);
                if (_FlipY > 0.5) uv.y = 1.0 - uv.y;
                half3 c = SAMPLE_TEXTURE2D(_DSEWA_ShopWindowRT, sampler_DSEWA_ShopWindowRT, uv).rgb;
                return half4(c * _Exposure, 1);
            }
            ENDHLSL
        }
        // depth priming is forced on the renderer: an opaque pass is only drawn where the depth prepass wrote it,
        // so the pane needs its own DepthOnly / DepthNormals passes (same as ToonMoon)
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS : POSITION; };
            float4 vert(A v) : SV_POSITION { return TransformObjectToHClip(v.positionOS.xyz); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; float3 n : TEXCOORD0; };
            V vert(A v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.n = TransformObjectToWorldNormal(v.normalOS); return o; }
            half4 frag(V i) : SV_Target { return half4(NormalizeNormalPerPixel(i.n), 0); }
            ENDHLSL
        }
    }
}
