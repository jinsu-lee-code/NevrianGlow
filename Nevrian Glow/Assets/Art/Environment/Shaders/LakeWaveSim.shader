// Height-field wave simulation step for the lake (2026-10-26, user: "high quality waves from the falls, not bands").
// Hugo Elias' 2D water: new = (left + right + up + down) / 2 - previous, times damping (ping-pong between two RG
// textures: R = height now, G = height one step ago). The water mask (baked from the terrain) pins land cells to 0, so
// waves reflect off the shore and bend round the stones. Impacts (the falling water at the foot of the falls, small
// drops) are added as soft discs. Used by LakeWaveSim.cs through Graphics.Blit.
Shader "Hidden/DSEWA/LakeWaveSim"
{
    Properties { _MainTex ("Previous State (R now, G before)", 2D) = "black" {} }
    SubShader
    {
        ZTest Always ZWrite Off Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            float4x4 unity_ObjectToWorld;
            float4x4 unity_MatrixVP;
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_WaterMask); SAMPLER(sampler_WaterMask);
            float4 _SimTexel;               // 1 / resolution (set by LakeWaveSim.cs; Blit does not fill _TexelSize here)
            float _Damping;
            float _SimSize;                 // metres covered by the texture
            float4 _Drops[8];               // xy = uv, z = radius (m), w = strength
            int _DropCount;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = mul(unity_MatrixVP, mul(unity_ObjectToWorld, float4(v.positionOS.xyz, 1)));
                o.uv = v.uv;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float2 t = _SimTexel.xy;
                float2 c = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, i.uv, 0).rg;
                float sum = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, i.uv + float2(t.x, 0), 0).r
                          + SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, i.uv - float2(t.x, 0), 0).r
                          + SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, i.uv + float2(0, t.y), 0).r
                          + SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, i.uv - float2(0, t.y), 0).r;
                float h = (sum * 0.5 - c.g) * _Damping;
                for (int k = 0; k < _DropCount; k++)
                {
                    float d = length((i.uv - _Drops[k].xy) * _SimSize) / max(_Drops[k].z, 1e-3);
                    h += _Drops[k].w * exp(-d * d * 3);
                }
                h *= SAMPLE_TEXTURE2D_LOD(_WaterMask, sampler_WaterMask, i.uv, 0).r;       // land = 0 (reflects)
                return float4(clamp(h, -4, 4), c.r, 0, 0);
            }
            ENDHLSL
        }
    }
}
