using UnityEngine;

/// <summary>
/// Dims the sun while a sky cloud stands in front of it, so the land darkens and brightens as clouds pass. Each cloud is
/// a plane facing the map, its outline taken as an ellipse: close enough to a soft shadow, and cheap. Light intensity
/// belongs to DayNightBlend, which writes it only when the hour changes, so the base is re-read whenever the value is
/// not the one written here. Runs in Play mode only: a dimmed value must never be saved into the scene.
/// Put it on Clouds3D, next to CloudDrift.
/// </summary>
public class CloudSunCover : MonoBehaviour
{
    [Tooltip("Defaults to the first directional light")]
    public Light sun;
    [Tooltip("Light left when a cloud fully covers the sun, fraction of the normal light")]
    [Range(0f, 1f)] public float coveredLight = 0.55f;
    [Tooltip("Share of the cloud's half size where the shadow starts to fade towards its edge")]
    [Range(0f, 1f)] public float softFrom = 0.5f;
    [Tooltip("Share of the mesh bounds the visible body fills: the rest is dissolved rim and leaves the sun clear")]
    [Range(0.1f, 1f)] public float bodyShare = 0.6f;
    [Tooltip("Seconds for the light to follow the cloud")]
    [Min(0.01f)] public float smoothing = 1.5f;

    // not serialized: a script reload would bring a null array back as an empty one and the cache would never fill
    [System.NonSerialized] Transform[] clouds;
    [System.NonSerialized] Bounds[] shapes;
    float cover, baseIntensity, written = -1f;

    /// <summary>0 = the sun is clear, 1 = behind a cloud, as seen from the given point.</summary>
    public float CoverFrom(Vector3 eye, Vector3 toSun)
    {
        if (clouds == null) Cache();
        float best = 0f;
        for (int i = 0; i < clouds.Length; i++)
        {
            Vector3 e = clouds[i].InverseTransformPoint(eye);
            Vector3 d = clouds[i].InverseTransformVector(toSun);   // keeps the sign on mirrored clouds
            if (Mathf.Abs(d.z) < 1e-7f) continue;
            float t = -e.z / d.z;
            if (t <= 0f) continue;
            Vector3 hit = e + d * t;
            Vector3 c = shapes[i].center, half = shapes[i].extents;
            float r = new Vector2((hit.x - c.x) / half.x, (hit.y - c.y) / half.y).magnitude / bodyShare;
            best = Mathf.Max(best, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(softFrom, 1f, r)));
        }
        return best;
    }

    void Cache()
    {
        var filters = GetComponentsInChildren<MeshFilter>();
        var list = new System.Collections.Generic.List<MeshFilter>();
        foreach (var f in filters) if (f.sharedMesh != null && f.sharedMesh.name.Contains("cloudlow")) list.Add(f);
        clouds = new Transform[list.Count]; shapes = new Bounds[list.Count];
        for (int i = 0; i < list.Count; i++) { clouds[i] = list[i].transform; shapes[i] = list[i].sharedMesh.bounds; }
    }

    Light FindSun()
    {
        if (sun != null) return sun;
        if (RenderSettings.sun != null) return sun = RenderSettings.sun;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) return sun = l;
        return null;
    }

    void LateUpdate()
    {
        var light = FindSun();
        var cam = Camera.main;
        if (light == null || cam == null) return;
        if (!Mathf.Approximately(light.intensity, written)) baseIntensity = light.intensity;
        float target = CoverFrom(cam.transform.position, -light.transform.forward);
        cover = Mathf.MoveTowards(cover, target, Time.deltaTime / smoothing);
        written = baseIntensity * Mathf.Lerp(1f, coveredLight, cover);
        light.intensity = written;
    }

    void OnDisable()
    {
        if (sun != null && written >= 0f && Mathf.Approximately(sun.intensity, written)) sun.intensity = baseIntensity;
        written = -1f;
        cover = 0f;
    }
}
