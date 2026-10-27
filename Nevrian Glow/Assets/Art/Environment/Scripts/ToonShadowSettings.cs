using UnityEngine;

/// <summary>
/// Scene-wide soft shadow controls for every DSEWA toon shader that uses ToonMainLightShadow (2026-10-27, user:
/// "add soft shadows and raise the resolution"). Publishes _DSEWA_ToonShadowSoft:
///   x = edge softness: 0 keeps the crisp toon line, higher widens the light / shadow transition into a soft band
///   y = blur multiplier on each material's Poisson blur radius (rounder, softer shapes)
/// Also the scene shadow colour (2026-10-27, the shop shading pass carried to the background): _DSEWA_SceneShadowTint
/// pulls every toon shader's shadow multiplier (ToonProp, ToonCrystal, ToonTerrain, ToonCliff, ToonLeaf, ToonFoliage)
/// toward one tint so the whole scene's shadows share a colour family. Amount 0 = each material keeps its own.
/// All numbers are my own values.
/// </summary>
[ExecuteAlways]
public class ToonShadowSettings : MonoBehaviour
{
    [Range(0f, 0.5f)] public float edgeSoftness = 0.18f;
    [Range(0.5f, 4f)] public float blurScale = 1.8f;

    [Header("Scene Shadow Colour")]
    public Color sceneShadowTint = new Color(0.52f, 0.5f, 0.86f);
    [Range(0f, 1f)] public float sceneShadowAmount = 0f;

    static readonly int Id = Shader.PropertyToID("_DSEWA_ToonShadowSoft");
    static readonly int TintId = Shader.PropertyToID("_DSEWA_SceneShadowTint");

    void OnEnable() => Publish();
    void OnValidate() => Publish();
    void Update() => Publish();
    void OnDisable()
    {
        Shader.SetGlobalVector(Id, Vector4.zero);
        Shader.SetGlobalVector(TintId, Vector4.zero);
    }

    void Publish()
    {
        Shader.SetGlobalVector(Id, new Vector4(edgeSoftness, blurScale, 0, 0));
        Shader.SetGlobalVector(TintId, new Vector4(sceneShadowTint.r, sceneShadowTint.g, sceneShadowTint.b, sceneShadowAmount));
    }
}
