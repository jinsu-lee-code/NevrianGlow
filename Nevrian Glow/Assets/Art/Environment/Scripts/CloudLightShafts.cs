using UnityEngine;

/// <summary>
/// Light shafts hanging under a ToonCloud (2026-10-30, user: "light between the clouds", option C). Builds a few long
/// strips from the cloud's underside, leaning away from the sun, and draws them with DSEWA/CloudLightShafts (each
/// strip turns to face the camera in the shader). The strips follow the cloud and the sun every frame; nothing is
/// saved but these numbers (a hidden child holds the mesh). Numbers are mine.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(ToonCloud))]
public class CloudLightShafts : MonoBehaviour
{
    public Material material;
    [Tooltip("Strips (bundles of shafts)")] [Range(1, 8)] public int strips = 4;
    [Tooltip("Length as a multiple of the cloud's height radius")] public float length = 3f;
    [Tooltip("Width of each strip as a fraction of the cloud's width radius")] [Range(0.05f, 1f)] public float width = 0.45f;
    [Tooltip("How far the shafts lean away from the sun (0 = straight down, 1 = along the light)")] [Range(0f, 1f)] public float lean = 0.35f;
    [Tooltip("Where they start under the cloud (fraction of the height radius below the centre)")] [Range(0f, 1f)] public float start = 0.45f;
    public Light sun;

    Mesh mesh;
    GameObject holder;

    void OnEnable() => Ensure();
    void OnDisable() { if (holder != null) holder.SetActive(false); }
    void OnDestroy() { if (holder != null) { if (Application.isPlaying) Destroy(holder); else DestroyImmediate(holder); } }

    void Ensure()
    {
        if (holder == null)
        {
            var t = transform.Find("_Shafts");
            holder = t != null ? t.gameObject : new GameObject("_Shafts");
            holder.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            holder.transform.SetParent(transform, false);
        }
        holder.SetActive(true);
        var mf = holder.GetComponent<MeshFilter>(); if (mf == null) mf = holder.AddComponent<MeshFilter>();
        var mr = holder.GetComponent<MeshRenderer>(); if (mr == null) mr = holder.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        if (mesh == null) mesh = new Mesh { name = "CloudShafts", hideFlags = HideFlags.DontSave };
        mf.sharedMesh = mesh;
        Rebuild();
    }

    void LateUpdate() { if (holder == null || !holder.activeSelf) Ensure(); else Rebuild(); }

    void Rebuild()
    {
        if (mesh == null) return;
        var sunLight = sun != null ? sun : RenderSettings.sun;
        Vector3 toSun = sunLight != null ? -sunLight.transform.forward : Vector3.up;
        Vector3 light = -toSun;                                   // the way the light travels
        Vector3 dir = Vector3.Slerp(Vector3.down, light.y < -0.05f ? light : new Vector3(light.x, -0.6f, light.z), lean).normalized;
        Vector3 r = transform.lossyScale;
        Vector3 c = transform.position;
        float len = r.y * length;
        // spread the heads across the cloud's underside, perpendicular to the lean
        Vector3 across = Vector3.Cross(dir, Vector3.up); if (across.sqrMagnitude < 1e-4f) across = Vector3.right; across.Normalize();
        int n = Mathf.Max(1, strips);
        var v = new Vector3[n * 4]; var uv = new Vector2[n * 4]; var head = new Vector4[n * 4]; var axis = new Vector4[n * 4];
        var tri = new int[n * 6];
        for (int s = 0; s < n; s++)
        {
            float u = n == 1 ? 0f : Mathf.Lerp(-0.6f, 0.6f, s / (float)(n - 1));
            float jitter = Mathf.Sin((s + 1) * 12.9898f) * 0.15f;
            Vector3 h = c + across * (u + jitter) * r.x + Vector3.down * start * r.y + Vector3.forward * jitter * r.z * 0.5f;
            float w = width * r.x * (0.7f + 0.3f * Mathf.Abs(Mathf.Cos(s * 2.3f)));
            float l = len * (0.75f + 0.25f * Mathf.Abs(Mathf.Sin(s * 1.7f)));
            for (int k = 0; k < 4; k++)
            {
                int i = s * 4 + k;
                v[i] = Vector3.zero;
                uv[i] = new Vector2(k == 1 || k == 2 ? 1 : 0, k >= 2 ? 1 : 0);
                head[i] = new Vector4(h.x, h.y, h.z, w);
                axis[i] = new Vector4(dir.x * l, dir.y * l, dir.z * l, s + 0.37f);
            }
            int b = s * 4, t = s * 6;
            tri[t] = b; tri[t + 1] = b + 2; tri[t + 2] = b + 1; tri[t + 3] = b; tri[t + 4] = b + 3; tri[t + 5] = b + 2;
        }
        mesh.Clear();
        mesh.vertices = v;
        mesh.SetUVs(0, uv);
        mesh.SetUVs(1, head);
        mesh.SetUVs(2, axis);
        mesh.triangles = tri;
        // positions are built in the shader from world data: bounds in the holder's space, big enough for the strips
        float reach = (len + Mathf.Max(r.x, r.z) * 2f) / Mathf.Max(Mathf.Min(r.x, Mathf.Min(r.y, r.z)), 0.01f);
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * reach * 2f);
    }
}
