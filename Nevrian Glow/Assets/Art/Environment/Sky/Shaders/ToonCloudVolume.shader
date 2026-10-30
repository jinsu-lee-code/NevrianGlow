// Toon cumulus (2026-10-30; user: "one Ghibli cloud in Unity", then "raise the detail, give me values to tune").
// After the three Ghibli cloud tutorials (Lightning Boy Studio, Kristof Dedene, Studio Aileron - story docs 13/14).
// No volume shader (no toon ramp there) and no painted textures (no paper grain). One camera-facing quad per cloud;
// the fragment sphere-traces a distance field and writes depth, so the aerial perspective, sun rays and the lens-flare
// occlusion all see the cloud. (A stack of alpha-cut slices was tried first: the slice cross-sections showed as
// contour rings - Blender hides them with Alpha Hashed accumulation, Unity has nothing like it.)
//  Shape   - big lumps (metaballs on a dome, Tower lifts them) smooth-unioned, flat underside (Base Flat), then a
//            second tier of small puffs on the surface (Puff Size / Amount), mostly on top (Puff Top Bias)
//  Shade   - toon ramp lit / shadow / deep; each small puff can shade on its own (Puff Shading); the band edges are
//            broken up by noise like a brush (Edge Break, Dedene's shadow distortion); dark underside (fake AO) with a
//            warm bounce colour
//  Colour  - the shadow sinks into the sky's violet, the lit side warms towards the sun's azimuth (same rules as
//            the painted-cloud shading in ToonSky)
//  Finish  - silver lining + backlight on the sun side, two "extra lights" (Lightning Boy: soft 3D brushes placed
//            by world position), an optional slow drift of the puffs
// Defaults are my own values (the design note lists them); presets live in ToonCloud.
Shader "DSEWA/ToonCloudVolume"
{
    Properties
    {
        [Header(Shape)]
        _Seed ("Seed", Range(0, 100)) = 0
        _LumpCount ("Lump Count", Range(5, 16)) = 12
        _Tower ("Tower (0 = wide and low, 1 = tall)", Range(0, 1)) = 0.5
        _BaseFlat ("Base Flat (height of the flat underside, 0 = low)", Range(0, 1)) = 0.3
        _BaseRound ("Base Round (0 = flat underside, 1 = lumpy all round)", Range(0, 1)) = 0
        _PuffSize ("Puff Size (of the radius)", Range(0.05, 0.3)) = 0.12
        _PuffAmount ("Puff Amount", Range(0, 1)) = 0.6
        _PuffTopBias ("Puff Top Bias (1 = puffs only on top)", Range(0, 1)) = 0.8
        [Header(Shading)]
        _LitEdge ("Lit Edge", Range(-1, 1)) = 0.1
        _DeepEdge ("Deep Shadow Edge", Range(-1, 1)) = -0.35
        _BandSoft ("Band Soft (0 = knife edge)", Range(0, 0.2)) = 0.02
        _EdgeBreak ("Edge Break (brushy shadow line)", Range(0, 1)) = 0.4
        _EdgeBreakScale ("Edge Break Scale", Range(1, 20)) = 8
        _PuffShading ("Puff Shading (each puff its own light)", Range(0, 1)) = 0.5
        _Underside ("Underside Darkening", Range(0, 1)) = 0.5
        _Wrap ("Light Wrap", Range(0, 1)) = 0.35
        [Header(Colour)]
        _LitSun ("Lit (towards the sun)", Color) = (0.96, 0.78, 0.68, 1)
        _LitFar ("Lit (away from the sun)", Color) = (0.88, 0.85, 0.97, 1)
        _Shadow ("Shadow (sky violet)", Color) = (0.47, 0.41, 0.76, 1)
        _Deep ("Deep Shadow", Color) = (0.32, 0.28, 0.6, 1)
        _Bounce ("Underside Bounce", Color) = (0.72, 0.5, 0.62, 1)
        _BounceAmount ("Underside Bounce Amount", Range(0, 1)) = 0.35
        [Header(Finish)]
        [HDR] _Rim ("Silver Lining", Color) = (0.9, 0.62, 0.48, 1)
        _RimWidth ("Silver Lining Width", Range(0, 0.3)) = 0.07
        _Backlight ("Backlight (thin parts glow against the sun)", Range(0, 1)) = 0.5
        _EdgeFade ("Edge Fade (dithered silhouette, 0 = off)", Range(0, 4)) = 0
        _Drift ("Drift (slow change of the puffs, live path only)", Range(0, 0.05)) = 0
        [Header(Wobble (the baked shape read through a slow 3D warp))]
        _WobbleAmount ("Wobble Amount (of the radius)", Range(0, 0.2)) = 0.05
        _WobbleScale ("Wobble Scale (waves per radius)", Range(0.5, 8)) = 2.5
        _WobbleSpeed ("Wobble Speed", Range(0, 0.5)) = 0.06
        [Header(Extra Lights (world position xyz, radius w))]
        _Extra1 ("Extra Light 1", Vector) = (0, 0, 0, 0)
        _Extra1Color ("Extra Light 1 Colour (a = amount)", Color) = (1, 0.8, 0.7, 0)
        _Extra2 ("Extra Light 2", Vector) = (0, 0, 0, 0)
        _Extra2Color ("Extra Light 2 Colour (a = amount)", Color) = (0.4, 0.36, 0.75, 0)
        [Header(Quality)]
        [KeywordEnum(Low, High)] _Quality ("Quality", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float _Seed, _LumpCount, _Tower, _BaseFlat, _BaseRound, _PuffSize, _PuffAmount, _PuffTopBias;
            float _LitEdge, _DeepEdge, _BandSoft, _EdgeBreak, _EdgeBreakScale, _PuffShading, _Underside, _Wrap;
            half4 _LitSun, _LitFar, _Shadow, _Deep, _Bounce;
            float _BounceAmount;
            half4 _Rim;
            float _RimWidth, _Backlight, _EdgeFade, _Drift;
            float _WobbleAmount, _WobbleScale, _WobbleSpeed;
            float4 _Extra1, _Extra2;
            half4 _Extra1Color, _Extra2Color;
            float _Quality;
        CBUFFER_END


        // baked shape (ToonCloud bakes the fields below into a 3D texture once per material and sets these per renderer):
        // r = cloud field (big lumps + puffs), g = big lumps only; unit space -1.4..1.4 on every axis
        TEXTURE3D(_BakedField); SAMPLER(sampler_BakedField);
        float _UseBaked;
        float _BakeSlice, _BakeRes;   // set while baking
        #define BAKE_EXTENT 1.4

        struct Attributes { float4 positionOS : POSITION; float3 slice : TEXCOORD0; };
        struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

        float3 CloudCentre() { return UNITY_MATRIX_M._m03_m13_m23; }
        float3 CloudRadii() { return float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22)); }

        // one camera-facing quad just in front of the cloud's bounding sphere
        Varyings CloudVert(Attributes v)
        {
            Varyings o;
            float3 c = CloudCentre();
            float3 r = CloudRadii();
            float R = max(r.x, max(r.y, r.z)) * 1.35;
            float3 fwd = normalize(c - _WorldSpaceCameraPos);
            float3 right = normalize(cross(float3(0, 1, 0), fwd) + float3(1e-4, 0, 0));
            float3 up = cross(fwd, right);
            float3 wp = c + (right * v.slice.x + up * v.slice.y - fwd) * R;
            o.positionWS = wp;
            o.positionCS = TransformWorldToHClip(wp);
            return o;
        }

        float Hash1(float n) { return frac(sin(n * 127.1 + 311.7) * 43758.5453); }
        float Hash3(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
        float Noise3(float3 x)
        {
            float3 i = floor(x), f = frac(x); f = f * f * (3 - 2 * f);
            return lerp(lerp(lerp(Hash3(i), Hash3(i + float3(1, 0, 0)), f.x), lerp(Hash3(i + float3(0, 1, 0)), Hash3(i + float3(1, 1, 0)), f.x), f.y),
                        lerp(lerp(Hash3(i + float3(0, 0, 1)), Hash3(i + float3(1, 0, 1)), f.x), lerp(Hash3(i + float3(0, 1, 1)), Hash3(i + float3(1, 1, 1)), f.x), f.y), f.z);
        }
        float3 Hash33(float3 p)
        {
            p = float3(dot(p, float3(127.1, 311.7, 74.7)), dot(p, float3(269.5, 183.3, 246.1)), dot(p, float3(113.5, 271.9, 124.6)));
            return frac(sin(p) * 43758.5453);
        }
        float SMax(float a, float b, float k) { float h = saturate(0.5 + 0.5 * (a - b) / k); return lerp(b, a, h) + k * h * (1 - h); }

        // big lumps: lump 0 is the core; the rest sit round a dome whose height follows Tower, getting smaller upwards
        float4 Lump(int b)
        {
            if (b == 0) return float4(0, 0.05 + 0.15 * _Tower, 0, 0.55);
            float s = _Seed * 13.37 + b * 7.1;
            float a = Hash1(s) * 6.2832;
            float h = Hash1(s + 1.3);                                   // 0 low ring .. 1 top
            float ring = lerp(0.78, 0.15, h) * lerp(1.0, 0.7, _Tower);
            float y = lerp(-0.05, lerp(0.45, 0.85, _Tower), h * h);
            float rad = lerp(0.42, 0.22, h) * lerp(0.9, 1.1, Hash1(s + 2.7));
            // Base Round: some lumps hang below the middle too, at uneven depths, so the underside billows instead of
            // lining up into one flat edge
            if (Hash1(s + 4.1) < 0.45 * _BaseRound)
            {
                y = -lerp(0.12, 0.38, Hash1(s + 5.3)) * _BaseRound;
                rad *= 0.85;
                ring *= 0.9;
            }
            return float4(cos(a) * ring, y, sin(a) * ring * 0.85, rad);
        }
        // flat underside; Base Round lowers it and melts it in, so the lumps (and puffs) show underneath too
        float BaseCut(float3 p) { return (p.y + lerp(0.4, 0.05, _BaseFlat) + _BaseRound * 0.45) * 2.0; }
        float SMin(float a, float b, float k) { return -SMax(-a, -b, k); }
        float ApplyBase(float d, float3 p) { return _BaseRound > 0.001 ? SMin(d, BaseCut(p), 0.04 + 0.3 * _BaseRound) : min(d, BaseCut(p)); }
        float BigField(float3 p)
        {
            float d = -1;
            [loop] for (int b = 0; b < 16; b++)
            {
                if (b >= (int)_LumpCount) break;
                float4 L = Lump(b);
                d = SMax(d, L.w - length(p - L.xyz), 0.085);
            }
            return ApplyBase(d, p);
        }
        // small puffs: a jittered grid of balls riding the big surface (Voronoi F1 turned into round bumps)
        float PuffField(float3 p, out float3 puffCentre)
        {
            float3 q = p / max(_PuffSize, 0.02) + _Seed * 3.1 + float3(0, _Time.y * _Drift, _Time.y * _Drift * 0.6);
            float3 i = floor(q), f = frac(q);
            // smooth nearest distance (soft minimum over the cells): where two puffs meet the surface dips in a round
            // valley instead of a sharp crease, so no spiky tips where the cells touch (2026-10-30, user)
            float best = 8, acc = 0; puffCentre = 0;
            const float k = 7.0;
            [unroll] for (int z = -1; z <= 1; z++)
            [unroll] for (int y = -1; y <= 1; y++)
            [unroll] for (int x = -1; x <= 1; x++)
            {
                float3 g = float3(x, y, z);
                float3 pt = g + 0.15 + Hash33(i + g) * 0.7;
                float d = length(pt - f);
                acc += exp(-k * d);
                if (d < best) { best = d; puffCentre = (i + pt); }
            }
            best = -log(max(acc, 1e-6)) / k;
            puffCentre = (puffCentre - _Seed * 3.1 - float3(0, _Time.y * _Drift, _Time.y * _Drift * 0.6)) * max(_PuffSize, 0.02);
            return (0.62 - best) * max(_PuffSize, 0.02);   // > 0 inside a puff, in radius units
        }
        float PuffWeight(float3 p) { return _PuffAmount * lerp(1.0, saturate(p.y * 1.6 + 0.6), _PuffTopBias * (1 - _BaseRound * 0.8)); }

        // the cloud: big lumps grown by the puffs near their surface
        float CloudField(float3 p, out float3 puffCentre)
        {
            float big = BigField(p);
            puffCentre = 0;
            float w = PuffWeight(p) * saturate(1 - abs(big) / max(_PuffSize * 1.5, 0.03));   // only near the surface
            if (w <= 0.001) return big;   // cost: the 27-cell Voronoi only runs in the thin shell round the surface
            float puff = PuffField(p, puffCentre);
            float d = big + SMax(puff, -_PuffSize * 0.5, _PuffSize * 0.3) * w * 1.6;
            return _BaseRound > 0.001 ? d : min(d, BaseCut(p));
        }

        // wobble (2026-10-30, user: "make it deform"): the baked shape is read through a slow 3D warp, so the lumps
        // billow and the edges churn without re-baking. Same in every pass (same time), so depth and colour agree.
        float3 Wobble(float3 p)
        {
            if (_WobbleAmount <= 0) return p;
            float3 q = p * _WobbleScale + _Seed * 1.7;
            float t = _Time.y * _WobbleSpeed;
            float3 w = float3(Noise3(q + float3(t, 0, 0)), Noise3(q + float3(5.2, t, 1.3)), Noise3(q + float3(2.9, 7.1, t))) - 0.5;
            return p + w * 2 * _WobbleAmount;
        }
        float2 BakedSample(float3 p)
        {
            p = Wobble(p);
            float3 uvw = p / (2 * BAKE_EXTENT) + 0.5;
            if (any(uvw < 0) || any(uvw > 1)) return float2(-1, -1);
            return SAMPLE_TEXTURE3D_LOD(_BakedField, sampler_BakedField, uvw, 0).rg;
        }
        // the field the renderer uses: baked when ready, else computed live (first frame / bake pass)
        float FieldD(float3 p, out float3 pc)
        {
            pc = 0;
            if (_UseBaked > 0.5) return BakedSample(p).r;
            return CloudField(p, pc);
        }
        float BigD(float3 p) { return _UseBaked > 0.5 ? BakedSample(p).g : BigField(p); }

        float3 FieldNormal(float3 p)
        {
            const float e = 0.03;
            const float2 k = float2(1, -1);
            float3 g = k.xyy * BigD(p + k.xyy * e) + k.yyx * BigD(p + k.yyx * e)
                     + k.yxy * BigD(p + k.yxy * e) + k.xxx * BigD(p + k.xxx * e);
            return normalize(TransformObjectToWorldNormal(-g));   // unit space = object space (yaw and scale included)
        }

        bool CloudMarch(float3 positionWS, out float3 hitWS, out float3 p, out float3 puffCentre, out float closest)
        {
            float3 r = CloudRadii();
            float3 c = CloudCentre();
            // unit space = the cloud's object space, so a turned (yaw) cloud marches correctly
            float3 ro = TransformWorldToObject(_WorldSpaceCameraPos);
            float3 rd = normalize(TransformWorldToObject(positionWS) - ro);
            float b = dot(ro, rd), cc = dot(ro, ro) - 1.9;
            float h = b * b - cc;
            hitWS = 0; p = 0; puffCentre = 0; closest = 1;
            if (h < 0) return false;
            float t = max(-b - sqrt(h), 0), tEnd = -b + sqrt(h);
            int steps = _Quality > 0.5 ? 56 : 32;
            [loop] for (int k = 0; k < 56; k++)
            {
                if (k >= steps) break;
                p = ro + rd * t;
                float d = FieldD(p, puffCentre);
                closest = min(closest, -d);
                if (d > -0.0015)
                {
                    float a = max(t - max(-d * 0.55, 0.003) * 2, 0), bb = t;
                    [unroll] for (int j = 0; j < 6; j++)
                    {
                        float m = (a + bb) * 0.5;
                        float3 pm; float dm = FieldD(ro + rd * m, pm);
                        if (dm > 0) bb = m; else a = m;
                    }
                    p = ro + rd * bb;
                    hitWS = TransformObjectToWorld(p);
                    return true;
                }
                t += max(-d * 0.7, 0.004);
                if (t > tEnd) break;
            }
            return false;
        }
        float DeviceDepth(float3 wp) { float4 cs = TransformWorldToHClip(wp); return cs.z / cs.w; }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex CloudVert
            #pragma fragment frag
            half4 frag(Varyings i, out float depth : SV_Depth) : SV_Target
            {
                float3 hit, p, pc; float closest;
                if (!CloudMarch(i.positionWS, hit, p, pc, closest)) discard;
                depth = DeviceDepth(hit);
                float3 r = CloudRadii();
                // edge fade: dither out the last pixels of the silhouette (closest approach of the ray in pixels)
                float3 nBig = FieldNormal(p);
                if (_EdgeFade > 0)
                {
                    float edgePx = (1 - saturate(dot(nBig, normalize(_WorldSpaceCameraPos - hit)) * 6)) * _EdgeFade;
                    float dither = frac(dot(floor(i.positionCS.xy), float2(0.7548, 0.5698)));
                    clip(1 - edgePx * 0.35 - dither * saturate(edgePx));
                }

                // puff normal from the surface slope itself (the nearest-puff-centre direction jumped between cells
                // pixel by pixel at puff borders and sprinkled dark specks over the lit side)
                float3 nPuff = nBig;
                if (_PuffShading > 0.01)
                {
                    float3 pd; float pe = _UseBaked > 0.5 ? 2.8 / 96.0 : 0.006;
                    const float2 k = float2(1, -1);
                    float3 gP = k.xyy * FieldD(p + k.xyy * pe, pd) + k.yyx * FieldD(p + k.yyx * pe, pd)
                              + k.yxy * FieldD(p + k.yxy * pe, pd) + k.xxx * FieldD(p + k.xxx * pe, pd);
                    nPuff = normalize(TransformObjectToWorldNormal(-gP));
                }
                float puffW = PuffWeight(p) * _PuffShading;
                float3 N = normalize(lerp(nBig, nPuff, puffW));
                Light light = GetMainLight();
                float3 L = light.direction;
                float ndl = (dot(N, L) + _Wrap) / (1 + _Wrap) * 2 - 1;
                // brushy band edges: the split is pushed by noise, strongest right at the edges
                float brush = (Noise3(p * _EdgeBreakScale + _Seed) - 0.5) + (Noise3(p * _EdgeBreakScale * 2.3 + 5.1) - 0.5) * 0.5;
                ndl += brush * _EdgeBreak * 0.6;
                float under = saturate(-p.y * 1.2 + 0.1) * _Underside;
                ndl -= under;
                half lit = smoothstep(_LitEdge - _BandSoft, _LitEdge + _BandSoft + 1e-4, ndl);
                half deep = 1 - smoothstep(_DeepEdge - _BandSoft, _DeepEdge + _BandSoft + 1e-4, ndl);
                float3 c = CloudCentre();
                float az = saturate(dot(normalize(c.xz - _WorldSpaceCameraPos.xz + 1e-5), normalize(L.xz + 1e-5)) * 0.5 + 0.5);
                half3 litCol = lerp(_LitFar.rgb, _LitSun.rgb, az * az);
                half3 col = lerp(_Shadow.rgb, litCol, lit);
                col = lerp(col, _Deep.rgb, deep * (1 - lit));
                // warm bounce on the underside (light from the ground / sunset below)
                col = lerp(col, _Bounce.rgb, saturate(-N.y) * saturate(-p.y * 2 + 0.2) * _BounceAmount * (1 - lit));
                // extra lights: soft 3D brushes
                if (_Extra1.w > 0) col = lerp(col, _Extra1Color.rgb, saturate(1 - distance(hit, _Extra1.xyz) / _Extra1.w) * _Extra1Color.a);
                if (_Extra2.w > 0) col = lerp(col, _Extra2Color.rgb, saturate(1 - distance(hit, _Extra2.xyz) / _Extra2.w) * _Extra2Color.a);
                // silver lining + backlight: when the sun is behind the cloud, the thin silhouette glows
                float3 V = normalize(hit - _WorldSpaceCameraPos);
                half facing = saturate(dot(nBig, -V));
                half rimMask = 1 - smoothstep(0, max(_RimWidth, 1e-3) * 4, facing);
                half sunBehind = saturate(dot(V, L) * 0.5 + 0.5);
                col += _Rim.rgb * rimMask * lerp(0.35, 1, sunBehind) * saturate(dot(N, L) + 0.4);
                col += _Rim.rgb * _Backlight * pow(sunBehind, 4) * rimMask * 0.6;
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex CloudVert
            #pragma fragment frag
            half frag(Varyings i, out float depth : SV_Depth) : SV_Target
            {
                float3 hit, p, pc; float cl;
                if (!CloudMarch(i.positionWS, hit, p, pc, cl)) discard;
                depth = DeviceDepth(hit);
                return 0;
            }
            ENDHLSL
        }

        // the renderer primes depth from the depth-normals prepass (screen outline)
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex CloudVert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 frag(Varyings i, out float depth : SV_Depth) : SV_Target
            {
                float3 hit, p, pc; float cl;
                if (!CloudMarch(i.positionWS, hit, p, pc, cl)) discard;
                depth = DeviceDepth(hit);
                // the outline reads these: the smooth big-lump normal (the puffs would draw a line round every puff),
                // packed the way URP expects (octahedral when the renderer asks for it - writing it raw made the
                // outline read garbage and crawl)
                float3 n = FieldNormal(p);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = PackNormalOctQuadEncode(n);
                return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
            #else
                return half4(n, 0);
            #endif
            }
            ENDHLSL
        }

        // bake: one slice of the 3D texture per draw (ToonCloud). Pixel position = voxel, so no flip issues.
        Pass
        {
            Name "Bake"
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex BakeVert
            #pragma fragment BakeFrag
            float4 BakeVert(float4 positionOS : POSITION) : SV_POSITION { return float4(positionOS.xy * 2 - 1, 0.5, 1); }
            float2 BakeFrag(float4 positionCS : SV_POSITION) : SV_Target
            {
                float3 uvw = float3(positionCS.xy / _BakeRes, (_BakeSlice + 0.5) / _BakeRes);
                float3 p = (uvw - 0.5) * 2 * BAKE_EXTENT;
                float3 pc;
                return float2(CloudField(p, pc), BigField(p));
            }
            ENDHLSL
        }

        // shadow bake (2026-10-30, user: cloud shadows option B): a 2D map in the sun's view. Each texel is a ray along
        // the light through the baked cloud; r = how much cloud it crossed (0..1). ToonCloud bakes it once (and again
        // when the sun or the shape changes); CloudShadow.hlsl projects it onto the ground.
        Pass
        {
            Name "BakeShadow"
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex BakeVert
            #pragma fragment ShadowFrag
            float4 _ShadowCentre;          // xyz centre, w half size of the map (m)
            float4 _ShadowU, _ShadowV, _ShadowL, _ShadowRadii;
            float4x4 _ShadowWorldToObject;
            float _ShadowRes;
            float4 BakeVert(float4 positionOS : POSITION) : SV_POSITION { return float4(positionOS.xy * 2 - 1, 0.5, 1); }
            float ShadowFrag(float4 positionCS : SV_POSITION) : SV_Target
            {
                float2 uv = positionCS.xy / _ShadowRes * 2 - 1;
                float3 q = _ShadowCentre.xyz + (_ShadowU.xyz * uv.x + _ShadowV.xyz * uv.y) * _ShadowCentre.w;
                float3 r = _ShadowRadii.xyz;
                float best = -1;
                [loop] for (int k = 0; k < 40; k++)
                {
                    float tt = lerp(-1, 1, (k + 0.5) / 40) * _ShadowCentre.w;
                    float3 w = q + _ShadowL.xyz * tt;
                    float3 pc;
                    best = max(best, FieldD(mul(_ShadowWorldToObject, float4(w, 1)).xyz, pc));
                }
                return smoothstep(-0.04, 0.03, best);
            }
            ENDHLSL
        }
    }
}
