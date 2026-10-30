using System.Collections.Generic;
using NevrianGlowFarm.Systems.Interaction;
using NevrianGlowFarm.UI;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Bright outline on what the player can interact with (2026-10-30, user: "a bright outline on interactable things is
/// important"). Reads the game's InteractionRaycaster (the crosshair target it already picks: E / left / right click
/// targets) and draws that object's meshes once more with DSEWA/InteractOutline - nothing in the game code changes and
/// the objects' own materials are left alone. Off while a screen (shop, storage, cutscene ...) is open.
/// Mesh renderers are drawn per submesh with Graphics.RenderMesh; skinned ones are baked once per frame.
/// Farm cells: when a crop grows on the cell only the crop is outlined (the flat soil slab gave broken lines); a bare
/// cell outlines its soil. The list is refreshed a few times a second, so a crop planted while aiming is picked up.
/// </summary>
public class InteractHighlight : MonoBehaviour
{
    [Tooltip("DSEWA/InteractOutline material")] public Material outlineMaterial;
    [Tooltip("Skip renderers bigger than this (m) - a whole field or building is not outlined")] public float maxRendererSize = 6f;

    static readonly int CentreId = Shader.PropertyToID("_MeshCentre");

    InteractionRaycaster raycaster;
    Component current;
    readonly List<Renderer> renderers = new List<Renderer>();
    readonly List<MaterialPropertyBlock> blocks = new List<MaterialPropertyBlock>();
    Mesh baked;
    float nextRefresh;

    void LateUpdate()
    {
        if (outlineMaterial == null) return;
        if (raycaster == null) { raycaster = FindFirstObjectByType<InteractionRaycaster>(); if (raycaster == null) return; }

        Component target = null;
        if (GameScreen.Current == null)
            target = (raycaster.HandTarget as Component) ?? (raycaster.UseTarget as Component) ?? (raycaster.SecondaryTarget as Component);
        if (target != current || (target != null && Time.unscaledTime >= nextRefresh)) Collect(target);
        if (current == null) return;

        for (int r = 0; r < renderers.Count; r++)
        {
            var rend = renderers[r];
            if (rend == null || !rend.enabled || !rend.gameObject.activeInHierarchy) continue;
            Mesh mesh; Matrix4x4 m;
            if (rend is SkinnedMeshRenderer smr)
            {
                if (baked == null) baked = new Mesh { name = "InteractHighlightBake" };
                smr.BakeMesh(baked, true);
                mesh = baked; m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);   // the bake already holds the scale
            }
            else
            {
                var mf = rend.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                mesh = mf.sharedMesh; m = rend.localToWorldMatrix;
            }
            blocks[r].SetVector(CentreId, mesh.bounds.center);
            var rp = new RenderParams(outlineMaterial)
            {
                layer = rend.gameObject.layer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                matProps = blocks[r],
            };
            for (int s = 0; s < mesh.subMeshCount; s++)
                Graphics.RenderMesh(rp, mesh, s, m);
        }
    }

    void Collect(Component target)
    {
        current = target;
        nextRefresh = Time.unscaledTime + 0.25f;
        renderers.Clear();
        if (target == null) { blocks.Clear(); return; }
        bool hasNonSoil = false;
        foreach (var rend in target.GetComponentsInChildren<Renderer>())
        {
            if (!(rend is MeshRenderer) && !(rend is SkinnedMeshRenderer)) continue;
            if (rend.bounds.size.magnitude > maxRendererSize) continue;
            var mat = rend.sharedMaterial;
            if (mat != null && mat.renderQueue >= 2500) continue;   // glow halos / FX quads: no outline
            renderers.Add(rend);
            if (rend.GetComponent<SoilFX>() == null) hasNonSoil = true;
        }
        if (hasNonSoil) renderers.RemoveAll(r => r.GetComponent<SoilFX>() != null);   // crop on the cell: outline the crop only
        while (blocks.Count < renderers.Count) blocks.Add(new MaterialPropertyBlock());
    }

    void OnDestroy() { if (baked != null) Destroy(baked); }
}
