using UnityEngine;

/// <summary>
/// Hands DSEWA/RoomDust the lamps of the room it stands in. The shop lives in a team scene loaded next to the background
/// scene, so nothing is wired by hand: every few seconds it looks for point and spot lights inside the room box. A lamp
/// added or moved there is picked up by itself. Goes next to AirMotes, the Glow Art/Air Particles menu does this.
/// Crystal clusters under the same root are handed over too, so the dust round them takes their soft glow.
/// </summary>
[ExecuteAlways]
public class RoomDust : MonoBehaviour
{
    const int MaxLights = 6;   // the shader's array size
    const int MaxCrystals = 4;
    const string CrystalPrefix = "Crystal_Cluster";

    [Tooltip("Room size (m), centred on this object")]
    public Vector3 roomSize = new Vector3(12f, 3.6f, 10f);
    [Tooltip("Seconds between looks for lamps")]
    [Min(0.1f)] public float rescanSeconds = 2f;
    [Tooltip("How far a crystal lights the dust round it (m, from its middle)")]
    [Min(0.1f)] public float crystalReach = 2f;

    static readonly int CountId = Shader.PropertyToID("_DSEWA_RoomLightCount");
    static readonly int PositionId = Shader.PropertyToID("_DSEWA_RoomLightPos");
    static readonly int ColorId = Shader.PropertyToID("_DSEWA_RoomLightColor");
    static readonly int CrystalCountId = Shader.PropertyToID("_DSEWA_RoomCrystalCount");
    static readonly int CrystalPosId = Shader.PropertyToID("_DSEWA_RoomCrystalPos");
    static readonly int CrystalColorId = Shader.PropertyToID("_DSEWA_RoomCrystalColor");
    static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");

    readonly Light[] lamps = new Light[MaxLights];
    readonly Vector4[] positions = new Vector4[MaxLights];
    readonly Vector4[] colors = new Vector4[MaxLights];
    readonly Vector4[] crystalPositions = new Vector4[MaxCrystals];
    readonly Vector4[] crystalColors = new Vector4[MaxCrystals];
    int count, crystals;
    float nextScan;

    void OnEnable() => ScanCrystals();

    // they never move, so once is enough
    void ScanCrystals()
    {
        crystals = 0;
        foreach (var view in transform.root.GetComponentsInChildren<Renderer>(true))
        {
            if (!view.name.StartsWith(CrystalPrefix) || view.sharedMaterial == null || !view.sharedMaterial.HasProperty(CoreColorId)) continue;
            Vector3 p = view.bounds.center;
            crystalPositions[crystals] = new Vector4(p.x, p.y, p.z, crystalReach);
            crystalColors[crystals] = view.sharedMaterial.GetColor(CoreColorId);
            if (++crystals == MaxCrystals) break;
        }
    }

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
        Shader.SetGlobalFloat(CrystalCountId, crystals);
        Shader.SetGlobalVectorArray(CrystalPosId, crystalPositions);
        Shader.SetGlobalVectorArray(CrystalColorId, crystalColors);
    }

    void OnDisable()
    {
        Shader.SetGlobalFloat(CountId, 0f);
        Shader.SetGlobalFloat(CrystalCountId, 0f);
    }
}
