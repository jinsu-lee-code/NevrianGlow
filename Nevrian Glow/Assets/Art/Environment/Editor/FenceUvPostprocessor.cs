using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// tex_env_fence_* was painted on another UV layout than env_post.fbx / env_rail.fbx (2026-11-02: overlaying the FBX UVs
// showed nothing lines up, and no matching model exists). On import each face is laid onto the painted plank of its
// side, so the FBX and the scenes stay as they are. Rectangles are pixels of the 1024 texture, read off the painting;
// which side takes which plank is my choice.
public class FenceUvPostprocessor : AssetPostprocessor
{
    const string Folder = "Environment/fence/Models/";
    static readonly RectInt RailFront = new RectInt(214, 98, 602, 138);
    static readonly RectInt RailBack = new RectInt(8, 252, 600, 130);
    static readonly RectInt RailTop = new RectInt(60, 524, 680, 104);
    static readonly RectInt RailBottom = new RectInt(290, 404, 606, 102);
    static readonly RectInt EndGrain = new RectInt(624, 274, 136, 104);
    static readonly RectInt PostA = new RectInt(8, 644, 860, 114);    // the tip is at the left of these three
    static readonly RectInt PostB = new RectInt(8, 772, 870, 114);
    static readonly RectInt PostC = new RectInt(8, 902, 790, 104);

    public override uint GetVersion() => 2;

    bool IsFence => assetPath.EndsWith(Folder + "env_post.fbx") || assetPath.EndsWith(Folder + "env_rail.fbx");

    // the mesh has no CPU data to read during import unless it is readable
    void OnPreprocessModel()
    {
        if (IsFence) ((ModelImporter)assetImporter).isReadable = true;
    }

    void OnPostprocessModel(GameObject root)
    {
        if (!IsFence) return;
        bool post = assetPath.EndsWith(Folder + "env_post.fbx");
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            Lay(filter.sharedMesh, post);
    }

    static void Lay(Mesh mesh, bool post)
    {
        if (mesh.vertexCount == 0) return;
        Vector3[] positions = mesh.vertices, normals = mesh.normals;
        int[] triangles = mesh.triangles;
        Bounds box = mesh.bounds;
        var outPositions = new List<Vector3>();
        var outNormals = new List<Vector3>();
        var outUvs = new List<Vector2>();
        var outTriangles = new int[triangles.Length];
        var welded = new Dictionary<(int, int), int>();   // a vertex on an edge between two planks is split

        for (int t = 0; t < triangles.Length; t += 3)
        {
            Vector3 n = (normals[triangles[t]] + normals[triangles[t + 1]] + normals[triangles[t + 2]]).normalized;
            int side = post ? PostSide(n) : RailSide(n);
            for (int k = 0; k < 3; k++)
            {
                int index = triangles[t + k];
                if (!welded.TryGetValue((index, side), out int mapped))
                {
                    mapped = outPositions.Count;
                    welded[(index, side)] = mapped;
                    outPositions.Add(positions[index]);
                    outNormals.Add(normals[index]);
                    outUvs.Add(post ? PostUv(positions[index], box, side) : RailUv(positions[index], box, side));
                }
                outTriangles[t + k] = mapped;
            }
        }
        mesh.Clear();
        mesh.SetVertices(outPositions);
        mesh.SetNormals(outNormals);
        mesh.SetUVs(0, outUvs);
        mesh.SetTriangles(outTriangles, 0);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
    }

    // post: stands along Y. 0-3 the four sides, 4 the foot
    static int PostSide(Vector3 n)
    {
        if (n.y < -0.85f) return 4;
        if (Mathf.Abs(n.x) > Mathf.Abs(n.z)) return n.x > 0 ? 0 : 1;
        return n.z > 0 ? 2 : 3;
    }

    static Vector2 PostUv(Vector3 p, Bounds box, int side)
    {
        float along = 1f - Across(p.y, box.min.y, box.size.y);      // top of the post = left of the plank
        float x = Across(p.x, box.min.x, box.size.x), z = Across(p.z, box.min.z, box.size.z);
        switch (side)
        {
            case 0: return Pixel(PostA, along, z);
            case 1: return Pixel(PostC, along, 1f - z);
            case 2: return Pixel(PostB, along, x);
            case 3: return Pixel(PostB, along, 1f - x);
            default: return Pixel(EndGrain, x, z);
        }
    }

    // rail: lies along X. 0 front, 1 back, 2 top, 3 bottom, 4 the ends
    static int RailSide(Vector3 n)
    {
        float x = Mathf.Abs(n.x), y = Mathf.Abs(n.y), z = Mathf.Abs(n.z);
        if (x > y && x > z) return 4;
        if (y > z) return n.y > 0 ? 2 : 3;
        return n.z > 0 ? 0 : 1;
    }

    static Vector2 RailUv(Vector3 p, Bounds box, int side)
    {
        float along = Across(p.x, box.min.x, box.size.x);
        float y = Across(p.y, box.min.y, box.size.y), z = Across(p.z, box.min.z, box.size.z);
        switch (side)
        {
            case 0: return Pixel(RailFront, along, y);
            case 1: return Pixel(RailBack, 1f - along, y);
            case 2: return Pixel(RailTop, along, z);
            case 3: return Pixel(RailBottom, along, z);
            default: return Pixel(EndGrain, z, y);
        }
    }

    static float Across(float value, float min, float size) => size > 1e-6f ? (value - min) / size : 0.5f;

    // u, v 0..1 inside a rectangle given in image pixels (y down)
    static Vector2 Pixel(RectInt r, float u, float v) =>
        new Vector2((r.x + u * r.width) / 1024f, 1f - (r.y + (1f - v) * r.height) / 1024f);
}
