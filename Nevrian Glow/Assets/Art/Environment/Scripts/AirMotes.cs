using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the mesh for DSEWA/AirMotes and DSEWA/ShootingStar: one quad per speck, each with a random seed. The shaders do
/// all the moving, so this only has to hand them the share of specks awake, which grows with the lighthouse: the grey
/// world of a new game has little in the air, the woken world has it all. Put it on an empty object, the Glow Art/Air
/// Particles menu does this.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class AirMotes : MonoBehaviour
{
    [Tooltip("Specks in the box round the camera")]
    [Min(1)] public int count = 300;
    [Tooltip("Share of the specks awake at lighthouse progress 0; progress 1 wakes them all")]
    [Range(0f, 1f)] public float wakeAtStart = 0.2f;
    [Tooltip("Box it can be seen in (m): huge for the air that follows the camera, the room for dust that stays in one")]
    public Vector3 boundsSize = Vector3.one * 100000f;

    static readonly int Wake = Shader.PropertyToID("_Wake");
    Mesh mesh;
    MeshRenderer view;
    MaterialPropertyBlock block;
    LighthouseGoal lighthouse;
    float written = -1f;

    void OnEnable()
    {
        view = GetComponent<MeshRenderer>();
        block = new MaterialPropertyBlock();
        lighthouse = FindAnyObjectByType<LighthouseGoal>();
        Build();
        written = -1f;
    }

    void OnDisable()
    {
        if (mesh == null) return;
        if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
    }

    void OnValidate()
    {
        if (isActiveAndEnabled && mesh != null && mesh.vertexCount != count * 4) Build();
    }

    void Build()
    {
        if (mesh == null) mesh = new Mesh { name = "air_motes", hideFlags = HideFlags.HideAndDontSave };
        mesh.Clear();
        var random = new System.Random(count);   // fixed: the air is the same on every run
        var vertices = new List<Vector3>(count * 4);
        var corners = new List<Vector2>(count * 4);
        var seeds = new List<Vector4>(count * 4);
        var quads = new List<int>(count * 6);
        for (int i = 0; i < count; i++)
        {
            var seed = new Vector4((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
            int first = vertices.Count;
            for (int c = 0; c < 4; c++)
            {
                vertices.Add(Vector3.zero);
                corners.Add(new Vector2(c & 1, c >> 1));
                seeds.Add(seed);
            }
            quads.Add(first); quads.Add(first + 2); quads.Add(first + 1);
            quads.Add(first + 1); quads.Add(first + 2); quads.Add(first + 3);
        }
        mesh.indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, corners);
        mesh.SetUVs(1, seeds);
        mesh.SetTriangles(quads, 0);
        mesh.bounds = new Bounds(Vector3.zero, boundsSize);   // the shader does the placing, the mesh itself has no size
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    void Update()
    {
        float wake = Application.isPlaying && lighthouse != null ? Mathf.Lerp(wakeAtStart, 1f, lighthouse.progress) : 1f;
        if (Mathf.Approximately(wake, written)) return;
        written = wake;
        view.GetPropertyBlock(block);
        block.SetFloat(Wake, wake);
        view.SetPropertyBlock(block);
    }
}
