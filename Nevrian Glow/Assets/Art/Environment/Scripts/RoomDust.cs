using UnityEngine;

/// <summary>
/// Hands DSEWA/RoomDust the lamps of the room it stands in. The shop lives in a team scene loaded next to the background
/// scene, so nothing is wired by hand: every few seconds it looks for point and spot lights inside the room box. A lamp
/// added or moved there is picked up by itself. Goes next to AirMotes, the Glow Art/Air Particles menu does this.
/// </summary>
[ExecuteAlways]
public class RoomDust : MonoBehaviour
{
    const int MaxLights = 6;   // the shader's array size

    [Tooltip("Room size (m), centred on this object")]
    public Vector3 roomSize = new Vector3(12f, 3.6f, 10f);
    [Tooltip("Seconds between looks for lamps")]
    [Min(0.1f)] public float rescanSeconds = 2f;

    static readonly int CountId = Shader.PropertyToID("_DSEWA_RoomLightCount");
    static readonly int PositionId = Shader.PropertyToID("_DSEWA_RoomLightPos");
    static readonly int ColorId = Shader.PropertyToID("_DSEWA_RoomLightColor");

    readonly Light[] lamps = new Light[MaxLights];
    readonly Vector4[] positions = new Vector4[MaxLights];
    readonly Vector4[] colors = new Vector4[MaxLights];
    int count;
    float nextScan;

    /// <summary>Finds the lamps inside the room now; returns how many.</summary>
    public int Scan()
    {
        var room = new Bounds(transform.position, roomSize + Vector3.one);
        count = 0;
        foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type == LightType.Directional || !room.Contains(light.transform.position)) continue;
            lamps[count++] = light;
            if (count == MaxLights) break;
        }
        return count;
    }

    void LateUpdate()
    {
        if (Time.realtimeSinceStartup >= nextScan || Time.realtimeSinceStartup < nextScan - rescanSeconds)
        {
            nextScan = Time.realtimeSinceStartup + rescanSeconds;
            Scan();
        }
        int live = 0;
        for (int i = 0; i < count; i++)
        {
            var lamp = lamps[i];
            if (lamp == null || !lamp.isActiveAndEnabled) continue;
            Vector3 p = lamp.transform.position;
            positions[live] = new Vector4(p.x, p.y, p.z, lamp.range);
            colors[live] = (Vector4)(lamp.color * lamp.intensity);
            live++;
        }
        Shader.SetGlobalFloat(CountId, live);
        Shader.SetGlobalVectorArray(PositionId, positions);
        Shader.SetGlobalVectorArray(ColorId, colors);
    }

    void OnDisable() => Shader.SetGlobalFloat(CountId, 0f);
}
