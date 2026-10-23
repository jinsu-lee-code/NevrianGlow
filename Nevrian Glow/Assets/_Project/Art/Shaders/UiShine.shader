// 타이틀 로고 빛 쓸기 (5-4a, 2026-10-23): 로고 그림 위에 같은 그림으로 겹쳐 그리는 더하기(Additive) 층.
// 비스듬한 빛 띠가 _ShinePos를 따라 지나가고, 로고의 불투명 · 밝은 곳(글자 · 달 · 별)에만 빛이 얹힌다.
// _ShinePos는 UiShineSweep이 움직인다 (0 = 왼쪽 끝, 1 = 오른쪽 끝, 띠가 화면 밖일 때는 -1 · 2 근처).
Shader "NevrianGlowFarm/UI/Shine"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _ShineColor ("빛 색", Color) = (0.85, 0.97, 1, 1)
        _ShinePos ("빛 위치 (0 ~ 1)", Float) = -1
        _ShineWidth ("빛 띠 반폭 (UV)", Float) = 0.07
        _ShineSlant ("기울기 (UV y당 x)", Float) = 0.35
        _Intensity ("세기", Float) = 0.9
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _ShineColor;
            float _ShinePos, _ShineWidth, _ShineSlant, _Intensity;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                // 비스듬한 띠: 위로 갈수록 오른쪽으로 기운다
                float x = i.uv.x + (i.uv.y - 0.5) * _ShineSlant;
                float band = saturate(1 - abs(x - _ShinePos) / _ShineWidth);
                band = band * band * (3 - 2 * band);
                // 밝은 곳(글자 · 달 · 별)일수록 강하게 — 어두운 테두리 · 잎에는 거의 안 얹힘
                float lum = dot(tex.rgb, float3(0.3, 0.59, 0.11));
                float weight = tex.a * smoothstep(0.15, 0.7, lum);
                float3 c = _ShineColor.rgb * band * weight * _Intensity * i.color.a;
                return fixed4(c, 0);
            }
            ENDCG
        }
    }
}
