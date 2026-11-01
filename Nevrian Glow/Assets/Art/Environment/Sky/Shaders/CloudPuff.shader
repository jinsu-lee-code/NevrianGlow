// Soft CG clouds and sky wisps. Shape and shade data come from Blender, the look is built here. Clouds carry a shape map
// baked from the high-res mesh (bake_maps.py): RG = normal bent towards the open sky in the cloud's frame (right, up),
// B = distance inside the outline, A = ambient occlusion, projected from the front, so a cloud has to keep its front to
// the player. The silhouette is cut from B, not from the mesh: the low-poly shell is pushed out and never shows.
// Scattering, from B as thickness: thin parts let light through (forward lobe towards the sun), the mass gets sky light
// from above and warm bounce from below, and dense folds stay a touch darker.
// Wisps (make_bands.py) have no map and use mesh normals. One dot(N, L) runs a continuous colour ramp, cream -> peach ->
// pink -> mauve -> deep, with no toon steps. Clouds away from the sun use a dim indigo ramp. Vertex colour: G = height
// 0..1, A = ambient occlusion without a map. The underside and the rim dissolve into the sky.
// Brush and wind: a long stroke pattern laid along the wind smears the shape map, the shade and the dissolve so no hard
// facet survives, and slides slowly through the cloud. The mesh leans with the wind and its downwind end trails out.
// Draws in the transparent queue like DSEWA/BrushCloud: a depth-only pass first, so each cloud shows its front surface
// only. Distance: same camera shrink as DSEWA/ToonCloudMesh; stay beyond AerialPerspective's 520 m after the shrink.
Shader "DSEWA/CloudPuff"
{
    Properties
    {
        [Header(Shape)]
        [NoScaleOffset] _Map ("Shape Map (RGB normal in cloud frame, A AO)", 2D) = "white" {}
        [Toggle] _UseMap ("Use Shape Map", Float) = 1
        _MapStrength ("Shape Map Strength", Range(0, 1)) = 0.42
        _MapBlur ("Shape Map Blur (mip levels)", Range(0, 5)) = 2.6
        _Inflate ("Shell Push (object units)", Float) = 0.2
        _FrameSize ("Shape Map Frame (object units)", Vector) = (20.48, 7.08, 0, 0)
        _EdgeFade ("Outline Cut (B from, to)", Vector) = (0.02, 0.16, 0, 0)

        [Header(Brush)]
        _StrokeScale ("Stroke Scale (1/m)", Float) = 0.006
        _StrokeStretch ("Stroke Length Along The Wind", Range(1, 12)) = 5
        _StrokeWarp ("Stroke Smear Of The Shape Map", Range(0, 0.08)) = 0.025
        _StrokeShade ("Stroke In The Shade", Range(0, 0.25)) = 0.07
        _StrokeFlow ("Stroke Flow Speed", Float) = 0.06

        [Header(Wind)]
        _WindLean ("Lean (metres per metre of height)", Range(0, 1.5)) = 0.5
        _TailStretch ("Tail Stretch (downwind underside)", Range(0, 1.5)) = 0.2
        _TailFade ("Tail Fade (downwind end)", Range(0, 1)) = 0.6

        [Header(Streaks (the outline dragged out downwind in strands))]
        _StreakLen ("Streak Length (share of the cloud's width)", Range(0, 0.6)) = 0.05
        _StreakRows ("Strands Per Cloud Height", Range(2, 60)) = 22
        _StreakVary ("Strand Length Variation", Range(0, 1)) = 0.7

        [Header(Cotton (the outline keeps swelling and fraying, like a timelapse))]
        _MorphAmount ("Morph Amount (share of the shape map)", Range(0, 0.08)) = 0.034
        _MorphSpeed ("Morph Speed", Float) = 0.08
        _HoleCut ("Hole Threshold (higher = fewer, 1 = none)", Range(0.3, 1)) = 0.84
        _HoleSoft ("Hole Rim Width", Range(0.01, 0.4)) = 0.12
        _HoleScale ("Hole Frequency", Range(0.2, 6)) = 1.5
        _FluffAmount ("Fluff In The Outline", Range(0, 0.8)) = 0.16
        _FluffScale ("Fluff Size (higher = finer)", Range(1, 12)) = 4
        _TopLight ("Bright Top, Flat Dim Underside", Range(0, 1)) = 0.35

        [Header(Light from the world)]
        _GroundGlowColor ("Ground Glow (crystals and lake on the underside)", Color) = (0.25, 0.85, 1, 1)
        _GroundGlowAmount ("Ground Glow Amount", Range(0, 1)) = 0.4
        _AuroraDir ("Aurora Direction (from the sky material)", Vector) = (0.005, 0.319, 0.948, 0)
        _AuroraTintA ("Aurora Tint A", Color) = (0.35, 0.92, 1, 1)
        _AuroraTintB ("Aurora Tint B", Color) = (0.6, 0.35, 1, 1)
        _AuroraTint ("Aurora Tint Amount", Range(0, 1)) = 0.75
        _AuroraSpread ("Aurora Tight Around Its Direction", Range(1, 32)) = 8
        _StarThrough ("Stars Through Thin Parts (night side)", Range(0, 1)) = 0.9

        [Header(Scattering)]
        _ScatterColor ("Light Through Thin Parts", Color) = (1, 0.84, 0.64, 1)
        _ScatterAmount ("Scatter Amount", Range(0, 2)) = 0.22
        _ScatterG ("Forward Lobe (0 even .. 0.95 tight round the sun)", Range(0, 0.95)) = 0.7
        _ScatterDepth ("Thickness That Stops The Light", Range(0.5, 12)) = 5
        _SkyFill ("Sky Light From Above", Color) = (0.62, 0.68, 1, 1)
        _Bounce ("Warm Bounce From Below", Color) = (1, 0.68, 0.58, 1)
        _AmbientAmount ("Ambient Amount", Range(0, 0.5)) = 0.1
        _FoldDark ("Dense Folds Darker", Range(0, 0.6)) = 0.15

        [Header(Sun side ramp)]
        _Cream ("Cream (facing the sun)", Color) = (1, 0.96, 0.84, 1)
        _Peach ("Peach", Color) = (0.98, 0.76, 0.68, 1)
        _Pink ("Pink", Color) = (0.89, 0.64, 0.68, 1)
        _Mauve ("Mauve", Color) = (0.68, 0.58, 0.8, 1)
        _Deep ("Deep (away from the sun)", Color) = (0.56, 0.58, 0.84, 1)

        [Header(Far side ramp)]
        _FarLit ("Far Lit", Color) = (0.48, 0.44, 0.72, 1)
        _FarShadow ("Far Shadow", Color) = (0.28, 0.29, 0.6, 1)
        _SideRange ("Side Blend (dot sun azimuth, from, to)", Vector) = (-0.35, 0.25, 0, 0)

        [Header(Light)]
        _PaintLight ("Paint Light (x sideways towards the source, y up, z towards the viewer)", Vector) = (0.85, 0.6, 0.45, 0)
        _NightOnDay ("Night Light On The Day Side", Range(0, 1)) = 0.3
        _SunOnNight ("Sun On The Night Side", Range(0, 1)) = 0.25
        _AirSun ("Air Towards The Sun (same as AerialPerspective's far air)", Color) = (0.84, 0.56, 0.5, 1)
        _AirFar ("Air Away From The Sun", Color) = (0.38, 0.5, 0.78, 1)
        _AirAmount ("Air On Low Clouds", Range(0, 1)) = 0.7
        _AirHeight ("Air Height (view elevation sin)", Range(0.01, 0.6)) = 0.24
        _RampLow ("Ramp Low", Range(0, 1)) = 0.34
        _RampHigh ("Ramp High", Range(0, 1.5)) = 1.08
        _Exposure ("Exposure (the night grade darkens the screen)", Range(0.5, 3)) = 1.02
        _Spherize ("Big Form (whole cloud lit as one mass)", Range(0, 1)) = 0.4
        _AOAmount ("AO Into Ramp", Range(0, 1)) = 0.5
        _Glow ("Sun Glow Through Thin Edges", Range(0, 2)) = 0.25
        _GlowFocus ("Sun Glow Focus", Range(1, 32)) = 4

        [Header(Dissolve)]
        _Opacity ("Opacity", Range(0, 1)) = 1
        _BaseFade ("Base Fade (height from, to)", Vector) = (0, 0.14, 0, 0)
        _RimFade ("Rim Fade Width", Range(0.001, 1)) = 0.26
        _NoiseAmount ("Stroke In The Dissolve", Range(0, 0.8)) = 0.34

        [Header(Distance)]
        _CameraShrink ("Camera Shrink (fit inside the far plane)", Range(0.01, 1)) = 0.7
        _DepthClip ("Depth Proxy Cut (solid body only)", Range(0, 1)) = 0.97
        _MinLight ("Min Light", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _Cream, _Peach, _Pink, _Mauve, _Deep, _FarLit, _FarShadow;
            float4 _SideRange, _BaseFade, _PaintLight;
            half _RampLow, _RampHigh, _Spherize, _AOAmount, _Glow, _GlowFocus, _NightOnDay, _SunOnNight;
            half _Opacity, _RimFade, _NoiseAmount, _MinLight, _UseMap, _MapStrength, _MapBlur, _Exposure;
            half _StrokeStretch, _StrokeWarp, _StrokeShade, _WindLean, _TailStretch, _TailFade, _StreakVary;
            float _StreakLen, _StreakRows;
            half _MorphAmount, _FluffAmount, _FluffScale, _TopLight;
            float _MorphSpeed, _HoleCut, _HoleSoft, _HoleScale;
            half4 _ScatterColor, _SkyFill, _Bounce, _AirSun, _AirFar;
            half _AirAmount, _AirHeight, _DepthClip;
            half4 _GroundGlowColor, _AuroraTintA, _AuroraTintB;
            float4 _AuroraDir;
            half _GroundGlowAmount, _AuroraTint, _AuroraSpread, _StarThrough;
            half _ScatterAmount, _ScatterG, _ScatterDepth, _AmbientAmount, _FoldDark;
            float4 _FrameSize, _EdgeFade;
            float _CameraShrink, _StrokeScale, _StrokeFlow, _WindSwayRate, _Inflate;
        CBUFFER_END
        TEXTURE2D(_Map);
        SAMPLER(sampler_Map);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;   // real (unshrunk) world position
            float3 normalWS : TEXCOORD1;
            float3 centreWS : TEXCOORD2;
            float2 uv : TEXCOORD3;
            float3 strokeCoord : TEXCOORD4;
            float tail : TEXCOORD5;          // 0 upwind end .. 1 downwind end
            float windU : TEXCOORD6;         // sign of u going downwind
            half4 color : COLOR;
        };

        Varyings vert(Attributes input)
        {
            Varyings o;
            float3 os = input.positionOS.xyz;
            float2 uv = input.uv;
            float3 eye = _WorldSpaceCameraPos;
            float3 centre = TransformObjectToWorld(float3(0, 0, 0));

            // the ring turns clockwise from above (Cloud3DSetup), so the wind runs along the tangent at each cloud
            float3 wind = normalize(cross(float3(0, 1, 0), normalize(float3(centre.x, 0, centre.z) + 1e-5)));
            float objectX = dot(TransformObjectToWorldDir(float3(1, 0, 0)), wind);
            float downwind = objectX > 0 ? 1 : -1;
            o.windU = -downwind;                                                  // u runs against object x
            if (_UseMap > 0.5)
            {
                // the shell grows past the outline and takes its uv along, so B alone draws the silhouette; downwind
                // it grows further, to make room for the strands the fragment stage drags out
                float3 push = input.normalOS * _Inflate;
                float lee = objectX > 0 ? 1 - uv.x : uv.x;
                push.x += downwind * lee * lee * _StreakLen * _FrameSize.x;
                os += push;
                uv += float2(-push.x / _FrameSize.x, push.y / _FrameSize.y);
            }
            float3 ws = TransformObjectToWorld(os);
            float3 rel = ws - centre;
            float along = dot(rel, wind);
            o.strokeCoord = float3(along / _StrokeStretch - _Time.y * _StrokeFlow / _StrokeScale, rel.y, dot(rel, cross(wind, float3(0, 1, 0))) * 0.5) * _StrokeScale;
            o.strokeCoord.z += _Time.y * _MorphSpeed;                             // the pattern itself evolves, so nothing repeats
            ws += wind * rel.y * _WindLean;                                       // tops lean downwind
            ws += wind * max(along, 0) * _TailStretch * (1 - input.color.g);      // the low downwind end trails out

            o.tail = _UseMap > 0.5 ? (objectX > 0 ? 1 - uv.x : uv.x) : 0;
            o.positionWS = ws;
            o.centreWS = centre;
            o.normalWS = TransformObjectToWorldNormal(input.normalOS);
            o.color = input.color;
            o.uv = uv;
            o.positionCS = TransformWorldToHClip(eye + (ws - eye) * _CameraShrink);
            return o;
        }

        float Hash31(float3 p) { p = frac(p * 0.1031); p += dot(p, p.zyx + 31.32); return frac((p.x + p.y) * p.z); }
        float ValueNoise3(float3 p)
        {
            float3 i = floor(p), f = frac(p);
            f = f * f * (3 - 2 * f);
            float a = lerp(Hash31(i), Hash31(i + float3(1, 0, 0)), f.x);
            float b = lerp(Hash31(i + float3(0, 1, 0)), Hash31(i + float3(1, 1, 0)), f.x);
            float c = lerp(Hash31(i + float3(0, 0, 1)), Hash31(i + float3(1, 0, 1)), f.x);
            float d = lerp(Hash31(i + float3(0, 1, 1)), Hash31(i + float3(1, 1, 1)), f.x);
            return lerp(lerp(a, b, f.y), lerp(c, d, f.y), f.z);
        }

        // long soft strokes, -0.5..0.5
        float Stroke(float3 c)
        {
            return ValueNoise3(c) * 0.6 + ValueNoise3(c * 2.3 + 7.1) * 0.3 + ValueNoise3(c * 5.1 + 3.7) * 0.1 - 0.5;
        }

        void Surface(Varyings i, float stroke, out float3 N, out float ao, out float thick)
        {
            if (_UseMap > 0.5)
            {
                // morph: two slow fields push the whole shape around, outline and shade together
                float2 morph = float2(stroke, Stroke(i.strokeCoord * 0.55 + 17.3));
                float2 uv = i.uv + float2(1, 0.3) * stroke * _StrokeWarp + morph * _MorphAmount * float2(1, 3);
                // fixed mip levels: the shape must not change with the cloud's size on screen
                half4 map = SAMPLE_TEXTURE2D_LOD(_Map, sampler_Map, uv, _MapBlur);
                float2 xy = (map.rg * 2 - 1) * _MapStrength;
                float z = sqrt(saturate(1 - dot(xy, xy)));
                N = normalize(TransformObjectToWorldNormal(float3(-xy.x, xy.y, z)));   // Blender x comes in as -x in Unity
                ao = map.a;
                // the cut stays sharp enough to hold the shape: the outline is read from a lightly smeared, finer mip
                thick = SAMPLE_TEXTURE2D_LOD(_Map, sampler_Map, uv, 0.8).b;
                // strands: each row of the cloud looks back upwind along its own length and carries the outline out,
                // thinning with distance
                float row = ValueNoise3(float3(0, i.uv.y * _StreakRows, 3.7)) * 0.7 + ValueNoise3(float3(0, i.uv.y * _StreakRows * 2.7, 9.1)) * 0.3;
                float reach = _StreakLen * lerp(1, smoothstep(0.25, 0.75, row), _StreakVary);
                for (int s = 1; s <= 5; s++)
                {
                    float k = s / 5.0;
                    float b = SAMPLE_TEXTURE2D_LOD(_Map, sampler_Map, uv - float2(i.windU * reach * k, 0), 1.2).b;
                    thick = max(thick, b * (1 - k) * (1 - k));
                }
                // holes: thinning B here opens the alpha, the depth proxy and the light through it all at once, so the
                // sun leaks out through the gap and its rim glows
                float3 hc = i.strokeCoord * float3(_StrokeStretch * 0.7, 1.2, 0.6) * _HoleScale;
                float holes = ValueNoise3(hc) * 0.7 + ValueNoise3(hc * 2.4 + 3.1) * 0.3;
                thick *= 1 - smoothstep(_HoleCut - _HoleSoft, _HoleCut, holes);
            }
            else
            {
                N = normalize(i.normalWS);
                ao = i.color.a;
                thick = i.color.g * 0.5;
            }
        }

        float CloudAlpha(Varyings i, float stroke, float thick)
        {
            float3 V = normalize(_WorldSpaceCameraPos - i.positionWS);
            float base = smoothstep(_BaseFade.x, _BaseFade.y, i.color.g + stroke * _NoiseAmount);
            // cotton: small round fluff eats into the rim only, the body stays closed
            float3 fine = i.strokeCoord * float3(_StrokeStretch, 1, 1) * _FluffScale;
            float fluff = ValueNoise3(fine) * 0.6 + ValueNoise3(fine * 2.6 + 5.3) * 0.4 - 0.5;
            float rim = _UseMap > 0.5
                ? smoothstep(_EdgeFade.x, _EdgeFade.y, thick + stroke * _NoiseAmount * 0.35 + fluff * _FluffAmount * (1 - saturate(thick * 4)))
                : smoothstep(0, _RimFade, saturate(dot(normalize(i.normalWS), V)) + stroke * _NoiseAmount);
            float tail = 1 - _TailFade * smoothstep(0.8, 1.1, i.tail + stroke * 0.3);
            // night side: thin parts let the real stars behind them through
            float dayCos = dot(normalize(-V.xz + 1e-5), normalize(_MainLightPosition.xz + 1e-5));
            float night = 1 - smoothstep(_SideRange.x, _SideRange.y, dayCos);
            float stars = lerp(1, smoothstep(0, 0.5, thick), night * _StarThrough);
            return base * rim * tail * stars * _Opacity;
        }
        ENDHLSL

        // depth first, so only the front surface of a cloud shows; the dissolved rim stays out of it
        Pass
        {
            Name "DepthPrepass"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Back ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(Varyings i) : SV_Target
            {
                float stroke = Stroke(i.strokeCoord);
                float3 N; float ao, thick;
                Surface(i, stroke, N, ao, thick);
                clip(CloudAlpha(i, stroke, thick) - 0.5 * _Opacity);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back ZWrite Off ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float4 _DSEWA_CloudTint, _DSEWA_CloudParams;   // from CloudSunLight: tint rgb + intensity factor ; (falloff, set, 0, 0)
            float4 _DSEWA_CloudNightDir, _DSEWA_CloudNightColor;   // from CloudNightLight: direction ; rgb + intensity

            // The clouds show the player their front, so a real light is usually behind or edge-on. Each source is turned
            // into a light in the cloud's own frame: it comes from the source's side, from above, and from the viewer's side.
            float3 PaintDir(float3 source, float3 toViewer)
            {
                float3 right = cross(float3(0, 1, 0), toViewer);
                float3 flat = normalize(float3(source.x, 0, source.z) + 1e-5);
                return normalize(right * dot(flat, right) * _PaintLight.x + float3(0, _PaintLight.y, 0) + toViewer * _PaintLight.z);
            }

            // Henyey-Greenstein lobe, scaled so a side view reads about 1
            float Lobe(float cosAngle, float g)
            {
                float d = 1 + g * g - 2 * g * cosAngle;
                return (1 - g * g) / max(d * sqrt(d), 1e-3) * 0.4;
            }

            half3 SunRamp(float t)
            {
                half3 c = lerp(_Deep.rgb, _Mauve.rgb, smoothstep(0, 0.3, t));
                c = lerp(c, _Pink.rgb, smoothstep(0.25, 0.55, t));
                c = lerp(c, _Peach.rgb, smoothstep(0.5, 0.78, t));
                return lerp(c, _Cream.rgb, smoothstep(0.72, 1, t));
            }

            half4 frag(Varyings i) : SV_Target
            {
                float stroke = Stroke(i.strokeCoord);
                float3 surface; float ao, thick;
                Surface(i, stroke, surface, ao, thick);
                float3 L = GetMainLight().direction;
                float3 V = normalize(_WorldSpaceCameraPos - i.positionWS);
                float3 toViewer = _WorldSpaceCameraPos - i.centreWS;
                toViewer = normalize(float3(toViewer.x, 0, toViewer.z) + 1e-5);
                float3 sunPaint = PaintDir(L, toViewer);
                float3 nightPaint = PaintDir(_DSEWA_CloudNightDir.xyz, toViewer);
                float3 mass = i.positionWS - i.centreWS;
                mass.y *= 2;                                   // clouds are wide: flatten the big form
                float3 N = normalize(lerp(surface, normalize(mass), _Spherize));

                float occlusion = lerp(1, ao, _AOAmount);
                float range = max(_RampHigh - _RampLow, 1e-3);
                float lift = stroke * _StrokeShade + (i.color.g - 0.5) * _TopLight;   // tops catch the light, the underside stays flat and dim
                float tSun = saturate(((dot(N, sunPaint) * 0.5 + 0.5) * occlusion + lift - _RampLow) / range);
                float tNight = saturate(((dot(N, nightPaint) * 0.5 + 0.5) * occlusion + lift - _RampLow) / range);

                float2 sunFlat = normalize(L.xz + 1e-5);
                float side = smoothstep(_SideRange.x, _SideRange.y, dot(normalize(-V.xz + 1e-5), sunFlat));
                half3 col = lerp(lerp(_FarShadow.rgb, _FarLit.rgb, tNight), SunRamp(tSun), side);

                // sun through thin edges: the side of the puff that faces the sun blooms towards cream
                float fres = 1 - saturate(dot(normalize(i.normalWS), V));
                float toward = pow(saturate(dot(surface, sunPaint) * 0.5 + 0.5), _GlowFocus);
                col = lerp(col, _Cream.rgb, saturate(fres * fres * toward * _Glow * side));

                // both lights at once: each side of the sky also catches the other side's light
                float nightHit = pow(saturate(dot(N, nightPaint)), 2) * occlusion;
                float sunHit = pow(saturate(dot(N, sunPaint)), 3) * occlusion;
                col += _DSEWA_CloudNightColor.rgb * _DSEWA_CloudNightColor.a * nightHit * lerp(1, _NightOnDay, side);
                col += _Peach.rgb * sunHit * _SunOnNight * (1 - side);

                // scattering: thickness (B) decides how much light gets through
                float through = exp(-thick * _ScatterDepth);
                float sunLobe = Lobe(dot(-V, L), _ScatterG);
                float nightLobe = Lobe(dot(-V, _DSEWA_CloudNightDir.xyz), _ScatterG);
                col += _ScatterColor.rgb * through * saturate(0.4 + sunLobe) * side * _ScatterAmount;
                col += _DSEWA_CloudNightColor.rgb * _DSEWA_CloudNightColor.a * through * saturate(0.4 + nightLobe) * (1 - side) * _ScatterAmount;
                col += lerp(_Bounce.rgb, _SkyFill.rgb, N.y * 0.5 + 0.5) * _AmbientAmount * lerp(0.4, 1, side);
                col *= 1 - _FoldDark * (1 - ao) * saturate(thick * 2);

                // light from the world: the glowing ground on the underside, the aurora on clouds near it
                float under = 1 - smoothstep(0, 0.5, i.color.g);
                col += _GroundGlowColor.rgb * under * under * _GroundGlowAmount * lerp(1, 0.45, side);
                float aurora = pow(saturate(dot(-V, normalize(_AuroraDir.xyz))), _AuroraSpread);
                col += lerp(_AuroraTintB.rgb, _AuroraTintA.rgb, saturate(0.5 + stroke * 2.5)) * aurora * _AuroraTint * saturate(through + 0.35);

                if (_DSEWA_CloudParams.y > 0.5)
                    col *= lerp(half3(1, 1, 1), _DSEWA_CloudTint.rgb, 0.5) * max(saturate(_DSEWA_CloudTint.a), _MinLight);
                // air: low clouds sink into the same far air as the mountains in front of them
                float air = (1 - smoothstep(0, _AirHeight, -V.y)) * _AirAmount;
                float towardsSun = saturate(dot(normalize(-V.xz + 1e-5), sunFlat) * 0.5 + 0.5);
                col = lerp(col * _Exposure, lerp(_AirFar.rgb, _AirSun.rgb, towardsSun), air);
                return half4(col, CloudAlpha(i, stroke, thick));
            }
            ENDHLSL
        }

        // the depth proxy: a second copy of the material in the opaque queue with the two passes above switched off
        // (Cloud3DSetup) writes the solid body into the camera depth, so sun rays and lens flares see the cloud as a blocker
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Back ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half frag(Varyings i) : SV_Target
            {
                float stroke = Stroke(i.strokeCoord);
                float3 N; float ao, thick;
                Surface(i, stroke, N, ao, thick);
                clip(CloudAlpha(i, stroke, thick) - _DepthClip);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 frag(Varyings i) : SV_Target
            {
                float stroke = Stroke(i.strokeCoord);
                float3 N; float ao, thick;
                Surface(i, stroke, N, ao, thick);
                clip(CloudAlpha(i, stroke, thick) - _DepthClip);
            #if defined(_GBUFFER_NORMALS_OCT)
                return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(N) * 0.5 + 0.5)), 0);
            #else
                return half4(N, 0);
            #endif
            }
            ENDHLSL
        }
    }
}
