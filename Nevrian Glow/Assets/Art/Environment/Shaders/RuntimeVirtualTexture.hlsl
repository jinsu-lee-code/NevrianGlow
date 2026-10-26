// Runtime virtual texture lookup (2026-10-27): RuntimeVirtualTexture.cs renders the terrain colour into pages of a
// tile atlas on a quadtree around the camera; one indirection texel per finest page says which atlas slot covers it.
// Pages hold the ground albedo; callers apply the ground's lit colour (_DSEWA_GroundLitColor).
#ifndef DSEWA_RVT_INCLUDED
#define DSEWA_RVT_INCLUDED
TEXTURE2D(_DSEWA_RVT_Atlas); SAMPLER(sampler_DSEWA_RVT_Atlas);
TEXTURE2D(_DSEWA_RVT_Indirection);
float4 _DSEWA_RVT_Rect;      // (minX, minZ, 1/sizeX, 1/sizeZ); z = 0 -> off
float4 _DSEWA_RVT_Params;    // (indirection size, slot uv size, 0, 0)

// true when a page covers xz; c = albedo
bool SampleRVT(float2 xz, out half4 c)
{
    c = 0;
    if (_DSEWA_RVT_Rect.z == 0) return false;
    float2 uv = (xz - _DSEWA_RVT_Rect.xy) * _DSEWA_RVT_Rect.zw;
    if (any(uv != saturate(uv))) return false;
    int n = (int)_DSEWA_RVT_Params.x;
    int2 cell = min((int2)(uv * n), n - 1);
    float4 e = LOAD_TEXTURE2D_LOD(_DSEWA_RVT_Indirection, cell, 0);
    if (e.z <= 0) return false;
    float2 local = frac(uv * e.z);                              // position inside the page at that level
    c = SAMPLE_TEXTURE2D_LOD(_DSEWA_RVT_Atlas, sampler_DSEWA_RVT_Atlas, e.xy + local * _DSEWA_RVT_Params.y, 0);
    return true;
}
#endif
