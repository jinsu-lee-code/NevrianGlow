#ifndef DSEWA_GROUND_FOG_INCLUDED
#define DSEWA_GROUND_FOG_INCLUDED

// Ground-only distance fog (GroundFog component): the terrain and grass melt into the sky's horizon colour with
// distance, while the sky / clouds stay untouched (Unity's global fog would also wash out the far cloud cards).
float4 _DSEWA_GroundFog;        // start (m), end (m), max amount, height falloff (1/m above the camera)
float4 _DSEWA_GroundFogColor;   // rgb, a = 1 when set

// World waking (WorldAwakening, 2026-10-28, user: "grey outside the circle"): colour lives only near the farm and the
// lighthouse (a capsule from one to the other) and spreads outward as the lighthouse lights up. Outside it the
// background goes grey and its glows sleep; a faint light band rides the edge. Off (z = 0) = everything as authored.
float4 _DSEWA_WakeA;        // xy = farm (x, z), zw = lighthouse (x, z)
float4 _DSEWA_WakeParams;   // x = radius (m), y = edge softness (m), z = on (1), w = edge band strength
float4 _DSEWA_WakeBand;     // rgb = edge band colour (gold life light), a = band width x the edge (0 = old cyan band)

float WorldWakeDistance(float3 positionWS)
{
    float2 a = _DSEWA_WakeA.xy, ab = _DSEWA_WakeA.zw - _DSEWA_WakeA.xy, p = positionWS.xz;
    float t = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-4));
    float d = distance(p, a + ab * t);
    float ang = atan2(p.y - a.y, p.x - a.x);   // a ragged edge that drifts slowly
    d += (sin(ang * 5.0 + _Time.y * 0.15) * 0.5 + sin(ang * 11.0 - _Time.y * 0.1) * 0.3) * _DSEWA_WakeParams.y * 0.6;
    return d;
}

// 0 = inside the coloured area, 1 = outside (asleep); 0 everywhere while off
float WorldWakeOutside(float3 positionWS)
{
    if (_DSEWA_WakeParams.z < 0.5) return 0;
    float soft = max(_DSEWA_WakeParams.y, 0.01);
    return smoothstep(_DSEWA_WakeParams.x - soft, _DSEWA_WakeParams.x + soft, WorldWakeDistance(positionWS));
}

// The fog lifts with the grey (2026-10-30, user: "clear the fog together with the grey zone"): inside the coloured
// area the fog thins by _DSEWA_FogWakeClear (0 = no change), so the light wave pushes the fog back as it spreads.
float _DSEWA_FogWakeClear;
float WorldWakeFogScale(float3 positionWS)
{
    if (_DSEWA_WakeParams.z < 0.5) return 1;
    return 1 - saturate(_DSEWA_FogWakeClear) * (1 - WorldWakeOutside(positionWS));
}

half3 ApplyGroundFog(half3 color, float3 positionWS)
{
    if (_DSEWA_GroundFogColor.a < 0.5) return color;
    float d = distance(positionWS, _WorldSpaceCameraPos);
    float f = saturate((d - _DSEWA_GroundFog.x) / max(_DSEWA_GroundFog.y - _DSEWA_GroundFog.x, 1e-3));
    f = f * f * (3 - 2 * f);
    // high ground (hill tops) keeps a little more of its own colour
    f *= exp(-max(positionWS.y - _WorldSpaceCameraPos.y, 0) * _DSEWA_GroundFog.w);
    f *= WorldWakeFogScale(positionWS);
    return lerp(color, _DSEWA_GroundFogColor.rgb, f * _DSEWA_GroundFog.z);
}

// bandScale: how strongly this surface shows the wave band. Ground (terrain, grass) keeps it faint so the wave reads on
// the things standing round you (trees, bushes, props flash gold as it passes and keep a short afterglow) rather than
// as a ring painted on the ground (2026-10-30, user).
half3 ApplyWorldWakeBand(half3 color, float3 positionWS, half bandScale)
{
    if (_DSEWA_WakeParams.z < 0.5) return color;
    float soft = max(_DSEWA_WakeParams.y, 0.01);
    float d = WorldWakeDistance(positionWS);
    float outside = smoothstep(_DSEWA_WakeParams.x - soft, _DSEWA_WakeParams.x + soft, d);
    half lum = dot(color, half3(0.299, 0.587, 0.114));
    half3 res = lerp(color, lum * half3(0.78, 0.82, 0.95) * 0.72, outside);
    float bandW = _DSEWA_WakeBand.a > 0 ? _DSEWA_WakeBand.a : 1.0;
    float x = (d - _DSEWA_WakeParams.x) / (soft * bandW);
    half3 bandCol = _DSEWA_WakeBand.a > 0 ? _DSEWA_WakeBand.rgb : half3(0.45, 0.85, 1.0);
    // sharp ahead of the front, a longer afterglow behind it (inside the restored area)
    float g = x < 0 ? exp(-x * x * 0.35) : exp(-x * x * 3.0);
    float w = g * _DSEWA_WakeParams.w * bandScale;
    res += bandCol * w * (0.25 + lum);
    res += res * bandCol * w * 0.5;   // the surface itself flares a little brighter as it wakes
    return res;
}

half3 ApplyWorldWake(half3 color, float3 positionWS)
{
    return ApplyWorldWakeBand(color, positionWS, 1.0h);
}

// Dead in the grey (2026-10-30, user: "trees in the grey zone without leaves or fruit, like dead trees"): materials that
// opt in (leaves, fruit) vanish outside the coloured area. Vertices well outside collapse (every pass: colour, shadow,
// depth, outline - nothing is drawn there at all); across the soft edge the colour pass dissolves them in with a noise,
// so leaves and fruit grow back as the wave passes.
bool WorldWakeDeadVertex(float3 positionWS)
{
    return _DSEWA_WakeParams.z > 0.5 && WorldWakeOutside(positionWS) > 0.995;
}

// clip() argument for the colour pass: negative = cut
float WorldWakeDeadClip(float3 positionWS)
{
    if (_DSEWA_WakeParams.z < 0.5) return 1;
    float3 cell = floor(positionWS * 3.0);
    float h = frac(sin(dot(cell, float3(12.9898, 78.233, 37.719))) * 43758.5453);
    return h * 0.98 + 0.01 - WorldWakeOutside(positionWS);
}

// NaN position: the GPU drops every triangle touching it (a fixed point would stretch boundary triangles into streaks)
#define DSEWA_DEAD_VERT(cs, ws) if (_DeadInGrey > 0.5 && WorldWakeDeadVertex(ws)) cs = asfloat(0x7fc00000u).xxxx;

#endif
