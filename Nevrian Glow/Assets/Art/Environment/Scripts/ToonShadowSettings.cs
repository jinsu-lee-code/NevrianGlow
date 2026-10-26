using UnityEngine;

/// <summary>
/// Scene-wide soft shadow controls for every DSEWA toon shader that uses ToonMainLightShadow (2026-10-27, user:
/// "add soft shadows and raise the resolution"). Publishes _DSEWA_ToonShadowSoft:
///   x = edge softness: 0 keeps the crisp toon line, higher widens the light / shadow transition into a soft band
///   y = blur multiplier on each material's Poisson blur radius (rounder, softer shapes)
/// All numbers are my own values.
/// </summary>
[ExecuteAlways]
public class ToonShadowSettings : MonoBehaviour
{
    [Range(0f, 0.5f)] public float edgeSoftness = 0.18f;
    [Range(0.5f, 4f)] public float blurScale = 1.8f;

    static readonly int Id = Shader.PropertyToID("_DSEWA_ToonShadowSoft");

    void OnEnable() => Publish();
    void OnValidate() => Publish();
    void Update() => Publish();
    void OnDisable() => Shader.SetGlobalVector(Id, Vector4.zero);

    void Publish() => Shader.SetGlobalVector(Id, new Vector4(edgeSoftness, blurScale, 0, 0));
}
