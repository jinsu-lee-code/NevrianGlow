using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// A second light for the sky clouds only (DSEWA/CloudPuff): the night side of the sky lights them while the sun does,
/// without being a real Light, so the ground and the light count stay as they are. Rotate this object to move it;
/// the light comes from the direction opposite its forward, like a directional light.
/// </summary>
[ExecuteAlways]
public class CloudNightLight : MonoBehaviour
{
    [ColorUsage(false, true)] public Color color = new Color(0.42f, 0.5f, 1f);
    [Tooltip("0 = off")]
    [Range(0f, 2f)] public float intensity = 0.7f;

    static readonly int DirId = Shader.PropertyToID("_DSEWA_CloudNightDir");
    static readonly int ColorId = Shader.PropertyToID("_DSEWA_CloudNightColor");

    void OnEnable()
    {
        Apply();
#if UNITY_EDITOR
        EditorApplication.update += EditorTick;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
#endif
        Shader.SetGlobalVector(ColorId, Vector4.zero);
    }

    void Apply()
    {
        Shader.SetGlobalVector(DirId, -transform.forward);
        Shader.SetGlobalVector(ColorId, new Vector4(color.r, color.g, color.b, intensity));
    }

    void LateUpdate()
    {
        if (Application.isPlaying) Apply();
    }

#if UNITY_EDITOR
    void EditorTick()
    {
        if (Application.isPlaying || this == null) return;
        Apply();
    }
#endif
}
