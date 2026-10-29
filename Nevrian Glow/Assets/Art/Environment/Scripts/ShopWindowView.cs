using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Live view through the shop windows (2026-10-27, user picked "real-time render" for the empty window outside).
/// The shop room sits alone at world (500, 0, 0), far from the background terrain, so its windows only show sky.
/// This places a hidden camera at a virtual shop position on the terrain (anchor) and moves it with the player
/// camera (same offset from the room origin, same rotation, same lens), renders it into a screen-sized texture and
/// publishes it as _DSEWA_ShopWindowRT. The window panes (DSEWA/ShopWindowView) sample it in screen space, so the
/// outside has true parallax. It only renders while the player camera is inside the room bounds.
/// All numbers are my own values.
/// </summary>
[ExecuteAlways]
public class ShopWindowView : MonoBehaviour
{
    [Tooltip("Room bounds; the view only renders while the main camera is inside")]
    public BoxCollider room;
    [Tooltip("Where the shop room would stand on the background terrain (world position of the room origin)")]
    public Vector3 anchor = new Vector3(-28f, 4f, -14f);
    [Tooltip("Yaw of the virtual room on the terrain (degrees)")]
    public float anchorYaw = 0f;
    [Range(0.25f, 1f)] public float resolutionScale = 0.5f;
    public LayerMask cullingMask = ~(1 << 8);   // everything but Viewmodel
    [Tooltip("Optional: follow this camera instead of Camera.main (capture / testing)")]
    public Camera viewerOverride;

    static readonly int TexId = Shader.PropertyToID("_DSEWA_ShopWindowRT");
    static readonly int OnId = Shader.PropertyToID("_DSEWA_ShopWindowOn");

    Camera view;
    RenderTexture rt;

    void OnEnable() { Shader.SetGlobalFloat(OnId, 0); }

    void OnDisable()
    {
        Shader.SetGlobalFloat(OnId, 0);
        // destroy the hidden camera outright: after a script reload a surviving camera would lose its texture and
        // render straight to the back buffer (BlitFinalToBackBuffer size mismatch errors)
        if (view != null) { view.targetTexture = null; DestroyImmediate(view.gameObject); view = null; }
        if (rt != null) { rt.Release(); DestroyImmediate(rt); rt = null; }
    }

    Camera Viewer()
    {
        if (viewerOverride != null) return viewerOverride;
        var main = Camera.main;
#if UNITY_EDITOR
        if (!Application.isPlaying && UnityEditor.SceneView.lastActiveSceneView != null)
            main = UnityEditor.SceneView.lastActiveSceneView.camera;
#endif
        return main;
    }

    void LateUpdate() => Tick();

    /// <summary>Moves the outside camera for the current viewer; call before rendering the viewer manually.</summary>
    public void Tick()
    {
        var src = Viewer();
        bool inside = src != null && room != null && room.bounds.Contains(src.transform.position);
        // nothing to look at without the background terrain (Shop scene opened alone in the editor): the camera
        // would only see Unity's default daylight sky, which glows yellow and blooms in the windows
        if (Terrain.activeTerrain == null) inside = false;
        if (!inside)
        {
            Shader.SetGlobalFloat(OnId, 0);
            if (view != null) view.enabled = false;
            return;
        }
        EnsureCamera();
        int w = Mathf.Max(64, Mathf.RoundToInt(src.pixelWidth * resolutionScale));
        int h = Mathf.Max(64, Mathf.RoundToInt(src.pixelHeight * resolutionScale));
        if (rt == null || rt.width != w || rt.height != h)
        {
            view.targetTexture = null;
            if (rt != null) { rt.Release(); DestroyImmediate(rt); }
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR) { name = "ShopWindowView", hideFlags = HideFlags.DontSave };
            view.targetTexture = rt;
        }

        // room origin = this object's position (the wall prefab sits at the room origin)
        Quaternion yaw = Quaternion.Euler(0f, anchorYaw, 0f);
        Vector3 local = Quaternion.Inverse(transform.rotation) * (src.transform.position - transform.position);
        view.transform.SetPositionAndRotation(anchor + yaw * local,
            yaw * Quaternion.Inverse(transform.rotation) * src.transform.rotation);
        view.fieldOfView = src.fieldOfView;
        view.nearClipPlane = src.nearClipPlane;
        view.farClipPlane = Mathf.Max(src.farClipPlane, 1500f);
        view.aspect = src.aspect;
        view.cullingMask = cullingMask;
        view.enabled = true;

        Shader.SetGlobalTexture(TexId, rt);
        Shader.SetGlobalFloat(OnId, 1);
    }

    void EnsureCamera()
    {
        if (view != null) return;
        foreach (var c in Resources.FindObjectsOfTypeAll<Camera>())   // orphans from an earlier script reload
            if (c != null && c.name == "ShopWindowView_Camera") DestroyImmediate(c.gameObject);
        var go = new GameObject("ShopWindowView_Camera") { hideFlags = HideFlags.HideAndDontSave };
        view = go.AddComponent<Camera>();
        view.enabled = false;   // only ever renders into its texture (enabled in Tick after the texture is set)
        view.depth = -50;   // render before the player camera (the lake reflection follows the first game camera)
        view.allowMSAA = false;
        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = false;
        data.renderShadows = true;
        data.requiresDepthOption = CameraOverrideOption.Off;
        data.requiresColorOption = CameraOverrideOption.Off;
    }
}
