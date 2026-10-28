#ifndef DSEWA_GROUND_FOG_INCLUDED
#define DSEWA_GROUND_FOG_INCLUDED

// Ground-only distance fog (GroundFog component): the terrain and grass melt into the sky's horizon colour with
// distance, while the sky / clouds stay untouched (Unity's global fog would also wash out the far cloud cards).
float4 _DSEWA_GroundFog;        // start (m), end (m), max amount, height falloff (1/m above the camera)
float4 _DSEWA_GroundFogColor;   // rgb, a = 1 when set

half3 ApplyGroundFog(half3 color, float3 positionWS)
{
    if (_DSEWA_GroundFogColor.a < 0.5) return color;
    float d = distance(positionWS, _WorldSpaceCameraPos);
    float f = saturate((d - _DSEWA_GroundFog.x) / max(_DSEWA_GroundFog.y - _DSEWA_GroundFog.x, 1e-3));
    f = f * f * (3 - 2 * f);
    // high ground (hill tops) keeps a little more of its own colour
    f *= exp(-max(positionWS.y - _WorldSpaceCameraPos.y, 0) * _DSEWA_GroundFog.w);
    return lerp(color, _DSEWA_GroundFogColor.rgb, f * _DSEWA_GroundFog.z);
}


// World waking (WorldAwakening, 2026-10-28, user: "grey outside the circle"): colour lives only near the farm and the
// lighthouse (a capsule from one to the other) and spreads outward as the lighthouse lights up. Outside it the
// background goes grey and its glows sleep; a faint light band rides the edge. Off (z = 0) = everything as authored.
float4 _DSEWA_WakeA;        // xy = farm (x, z), zw = lighthouse (x, z)
float4 _DSEWA_WakeParams;   // x = radius (m), y = edge softness (m), z = on (1), w = edge band strength

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

half3 ApplyWorldWake(half3 color, float3 positionWS)
{
    if (_DSEWA_WakeParams.z < 0.5) return color;
    float soft = max(_DSEWA_WakeParams.y, 0.01);
    float d = WorldWakeDistance(positionWS);
    float outside = smoothstep(_DSEWA_WakeParams.x - soft, _DSEWA_WakeParams.x + soft, d);
    half lum = dot(color, half3(0.299, 0.587, 0.114));
    half3 res = lerp(color, lum * half3(0.78, 0.82, 0.95) * 0.72, outside);
    float x = (d - _DSEWA_WakeParams.x) / soft;
    res += half3(0.45, 0.85, 1.0) * exp(-x * x * 1.5) * _DSEWA_WakeParams.w * (0.25 + lum);
    return res;
}

#endif
