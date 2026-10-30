// Cloud shadow — port of SurvivalKit's sun Light Function MM_CloudShadow / MI_CloudShadow
// (stylized-shading-research, SurvivalKit_Shader_Analysis_KO "하늘 구체와 구름").
// World XZ sampled at two sizes, panned, mixed, SmoothStep(Levels Min, Max), then Lerp(1, mask, Opacity).
// Research values: tiling 830.007629 / 1675.230957 UE units (cm) -> 8.300 / 16.752 m, Speed -0.005,
// Levels 0.292 / 0.762758, Opacity 1.675799 (> 1 extrapolates, clamped to 0..1 like a light value).
// The original noise texture is not in the clone: value noise stands in, and the two samples are averaged.
// Multiply it into the main light's DIRECT term only — never BaseColor or ambient (research rule).
// Globals come from CloudShadowSettings.cs; _DSEWA_CloudShadowTiling.x == 0 means off (returns 1).
#ifndef DSEWA_CLOUD_SHADOW_INCLUDED
#define DSEWA_CLOUD_SHADOW_INCLUDED

float4 _DSEWA_CloudShadowTiling;   // x = size A (m), y = size B (m), z = pan speed (UV per second), w = opacity
float4 _DSEWA_CloudShadowLevels;   // x = SmoothStep min, y = max
float4 _DSEWA_CloudShadowTweak;    // my controls on top of the research values: x = coverage offset (+ = fewer shadows),
                                   // y = strength (0 = no shadow, 1 = research result). Unset (0, 0) = research as-is

float CloudShadowHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float CloudShadowNoise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    f = f * f * (3 - 2 * f);
    return lerp(lerp(CloudShadowHash(i), CloudShadowHash(i + float2(1, 0)), f.x),
                lerp(CloudShadowHash(i + float2(0, 1)), CloudShadowHash(i + float2(1, 1)), f.x), f.y);
}

// 3D toon cloud shadows (2026-10-30, option B): each ToonCloud bakes a map of itself seen from the sun; the ground
// point walks along the light to the cloud's plane and reads it. Up to 4 clouds.
TEXTURE2D(_DSEWA_CloudShadowMap0); TEXTURE2D(_DSEWA_CloudShadowMap1); TEXTURE2D(_DSEWA_CloudShadowMap2); TEXTURE2D(_DSEWA_CloudShadowMap3);
SAMPLER(sampler_DSEWA_CloudShadowMap0);
float4 _DSEWA_CloudShadowCentre[4];   // xyz centre, w half size (m)
float4 _DSEWA_CloudShadowU, _DSEWA_CloudShadowV, _DSEWA_CloudShadowL;   // basis of the maps, L = towards the sun
float4 _DSEWA_CloudShadowParams;   // x = count, y = strength, z = edge softness (texels as uv), w = unused

half ToonCloudShadowOne(float3 positionWS, int idx)
{
    float4 c = _DSEWA_CloudShadowCentre[idx];
    float t = dot(c.xyz - positionWS, _DSEWA_CloudShadowL.xyz);
    if (t <= 0) return 0;                                   // cloud below / behind the point
    float3 o = positionWS + _DSEWA_CloudShadowL.xyz * t - c.xyz;
    float2 uv = float2(dot(o, _DSEWA_CloudShadowU.xyz), dot(o, _DSEWA_CloudShadowV.xyz)) / c.w * 0.5 + 0.5;
    if (any(uv < 0) || any(uv > 1)) return 0;
    float s = 0;
    if (idx == 0) s = SAMPLE_TEXTURE2D_LOD(_DSEWA_CloudShadowMap0, sampler_DSEWA_CloudShadowMap0, uv, 0).r;
    else if (idx == 1) s = SAMPLE_TEXTURE2D_LOD(_DSEWA_CloudShadowMap1, sampler_DSEWA_CloudShadowMap0, uv, 0).r;
    else if (idx == 2) s = SAMPLE_TEXTURE2D_LOD(_DSEWA_CloudShadowMap2, sampler_DSEWA_CloudShadowMap0, uv, 0).r;
    else s = SAMPLE_TEXTURE2D_LOD(_DSEWA_CloudShadowMap3, sampler_DSEWA_CloudShadowMap0, uv, 0).r;
    return s;
}

half ToonCloudShadow(float3 positionWS)
{
    int n = (int)_DSEWA_CloudShadowParams.x;
    if (n <= 0) return 1;
    half occ = 0;
    [unroll] for (int i = 0; i < 4; i++) if (i < n) occ = max(occ, ToonCloudShadowOne(positionWS, i));
    return 1 - occ * _DSEWA_CloudShadowParams.y;
}

half EvaluateCloudShadow(float3 positionWS)
{
    half toon = ToonCloudShadow(positionWS);
    if (_DSEWA_CloudShadowTiling.x <= 0) return toon;
    float pan = _Time.y * _DSEWA_CloudShadowTiling.z;
    float a = CloudShadowNoise(positionWS.xz / _DSEWA_CloudShadowTiling.x + pan);
    float b = CloudShadowNoise(positionWS.xz / _DSEWA_CloudShadowTiling.y + pan);
    float mask = smoothstep(_DSEWA_CloudShadowLevels.x, _DSEWA_CloudShadowLevels.y, (a + b) * 0.5 + _DSEWA_CloudShadowTweak.x);
    half light = saturate(lerp(1, mask, _DSEWA_CloudShadowTiling.w));
    half strength = _DSEWA_CloudShadowTweak.y > 0 ? _DSEWA_CloudShadowTweak.y : 1;
    return lerp(1, light, strength) * toon;
}
#endif
