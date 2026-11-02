// A shaft of moonlight falling on one place, seen from the side rather than shining at the camera. Drawn on the inside of
// a box round the shaft. Each pixel solves its view ray against a cylinder along the moon's direction, so there is no ray
// marching: the length of the ray inside the cylinder is the light. Solid objects cut the ray and whatever stands inside
// the cylinder takes a pool of light. A slow noise across the shaft stands for the gaps in the clouds, so it breaks into
// strands that drift, and MoonBeam.cs dims it all when a cloud crosses the moon. Numbers are mine.
Shader "DSEWA/MoonBeam"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (0.55, 0.68, 1, 1)
        _Density ("Air Density", Range(0, 0.1)) = 0.012
        _Pool ("Pool Of Light Where It Lands", Range(0, 2)) = 0.35
        _CoreSoft ("Soft Edge (0 = hard, 1 = all soft)", Range(0.05, 1)) = 0.7
        _FadeTop ("Fades Out Between These Heights (m, from, to)", Vector) = (30, 140, 0, 0)
        _GapScale ("Cloud Gaps Across The Shaft (m)", Float) = 9
        _GapAmount ("Cloud Gaps Amount", Range(0, 1)) = 0.75
        _GapSpeed ("Cloud Gaps Drift (m per s)", Float) = 0.6
        _MaxGlow ("Brightest It Gets (stays under the bloom)", Range(0, 2)) = 0.4
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+6" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Beam"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _FadeTop;
                float _Density, _Pool, _CoreSoft, _GapScale, _GapAmount, _GapSpeed, _MaxGlow;
                float4 _BeamTarget;   // from MoonBeam.cs: xyz where it lands, w radius
                float4 _BeamAxis;     // xyz towards the moon, w strength (cloud cover and night)
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            // gaps in the clouds: the same pattern all along the shaft, because light travels straight
            float Gaps(float3 rel, float3 axis)
            {
                float3 e1 = normalize(cross(axis, float3(0, 1, 0)));
                float3 e2 = cross(axis, e1);
                float2 q = float2(dot(rel, e1), dot(rel, e2)) / max(_GapScale, 0.01);
                float2 drift = float2(_Time.y * _GapSpeed / max(_GapScale, 0.01), 0);
                float n = Noise(q + drift) * 0.65 + Noise(q * 2.3 - drift * 1.4 + 4.7) * 0.35;
                return lerp(1, smoothstep(0.35, 0.65, n), _GapAmount);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 eye = _WorldSpaceCameraPos;
                float3 toExit = i.positionWS - eye;
                float3 dir = normalize(toExit);
                float2 uv = i.positionCS.xy / _ScaledScreenParams.xy;
                float raw = SampleSceneDepth(uv);
                float3 scene = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                float sceneDistance = distance(scene, eye);
            #if UNITY_REVERSED_Z
                bool sky = raw <= 0.0000001;
            #else
                bool sky = raw >= 0.9999999;
            #endif

                float3 axis = _BeamAxis.xyz;
                float radius = _BeamTarget.w;
                float3 origin = eye - _BeamTarget.xyz;
                float3 dp = dir - axis * dot(dir, axis);
                float3 op = origin - axis * dot(origin, axis);
                float a = dot(dp, dp), b = dot(op, dp), c = dot(op, op) - radius * radius;
                float disc = b * b - a * c;
                float3 glow = 0;
                if (disc > 0 && a > 1e-6)
                {
                    float root = sqrt(disc);
                    float t0 = max((-b - root) / a, 0);
                    float t1 = min((-b + root) / a, sceneDistance);
                    if (t1 > t0)
                    {
                        // brightest near the axis, nothing at the wall: no hard tube
                        float tm = clamp(-b / a, t0, t1);
                        float3 rel = origin + dir * tm;
                        float3 perp = rel - axis * dot(rel, axis);
                        float edge = saturate(1 - length(perp) / radius);
                        float profile = smoothstep(0, _CoreSoft, edge);
                        float worldY = rel.y + _BeamTarget.y;
                        float top = 1 - smoothstep(_FadeTop.x, _FadeTop.y, worldY);
                        glow = _Color.rgb * _Density * (t1 - t0) * profile * top * Gaps(rel, axis);
                    }
                }
                // the pool: whatever stands inside the shaft is lit
                if (!sky)
                {
                    float3 rel = scene - _BeamTarget.xyz;
                    float3 perp = rel - axis * dot(rel, axis);
                    float edge = saturate(1 - length(perp) / radius);
                    if (edge > 0 && scene.y < _FadeTop.y)
                        glow += _Color.rgb * _Pool * smoothstep(0, _CoreSoft, edge) * Gaps(rel, axis);
                }
                return half4(min(glow * _BeamAxis.w, _MaxGlow), 0);
            }
            ENDHLSL
        }
    }
}
