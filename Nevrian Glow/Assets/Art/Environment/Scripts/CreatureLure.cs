using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A spot that draws the wandering creatures (2026-10-28, user: "creatures gather round the crops at night").
/// While enabled it sits in a shared list; CreatureWander with Visit Lures picks from it now and then and hovers
/// there for a while. CropReadyGlow switches one on when a crop is ready to harvest.
/// </summary>
public class CreatureLure : MonoBehaviour
{
    public static readonly List<CreatureLure> All = new List<CreatureLure>();

    [Tooltip("How strongly this spot draws creatures (relative to other lures)")]
    public float weight = 1f;

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() => All.Remove(this);
}
