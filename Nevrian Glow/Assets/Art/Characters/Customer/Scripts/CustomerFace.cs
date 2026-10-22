using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Customer face expression (pipi / or). The mouth is painted in the face texture (_BaseMap of the skin material),
/// so an expression is a texture swap:
///   SetHappy(true)  - open smile texture, antenna / ear glow a little brighter
///   SetHappy(false) - back to the soft smile
/// Writes only through MaterialPropertyBlocks (per material slot), so the shared materials are not changed and one
/// material serves every customer instance. The glow factor is my own value.
/// </summary>
public class CustomerFace : MonoBehaviour
{
    [Tooltip("Face mesh (skin material)")] public Renderer Face;
    public Texture2D NeutralMouth;
    public Texture2D HappyMouth;
    [Tooltip("Glow material of the antenna / ears; every slot using it under this object gets brighter while happy")]
    public Material GlowMaterial;
    [Tooltip("Glow colour multiplier while happy")] public float HappyGlow = 1.15f;

    static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    MaterialPropertyBlock _mpb;
    readonly List<KeyValuePair<Renderer, int>> _glowSlots = new List<KeyValuePair<Renderer, int>>();
    Color _glowBase = Color.white;

    public bool IsHappy { get; private set; }

    void Awake()
    {
        if (_mpb != null) return;
        _mpb = new MaterialPropertyBlock();
        if (!GlowMaterial) return;
        _glowBase = GlowMaterial.GetColor(BaseColor);
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] == GlowMaterial) _glowSlots.Add(new KeyValuePair<Renderer, int>(r, i));
        }
    }

    public void SetHappy(bool happy)
    {
        Awake();
        IsHappy = happy;
        if (Face)
        {
            var tex = happy ? HappyMouth : NeutralMouth;
            Face.GetPropertyBlock(_mpb);
            if (tex) _mpb.SetTexture(BaseMap, tex);
            Face.SetPropertyBlock(_mpb);
        }
        foreach (var s in _glowSlots)
        {
            s.Key.GetPropertyBlock(_mpb, s.Value);
            _mpb.SetColor(BaseColor, happy ? _glowBase * HappyGlow : _glowBase);
            s.Key.SetPropertyBlock(_mpb, s.Value);
        }
    }
}
