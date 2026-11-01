using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Removes objects of a team scene that art cannot edit, by name. Used by the shop chandelier: two old bulbs still stand in
/// Shop.unity. In Play mode they are switched off; in the editor they are only kept from drawing and lighting, so the scene
/// file stays as it was. Delete this once the bulbs are removed there (Assets/Art/mapping.md).
/// </summary>
[ExecuteAlways]
public class HideSceneLamps : MonoBehaviour
{
    [Tooltip("Exact object names to remove in this object's scene")]
    public string[] names = new string[0];

    readonly List<Renderer> hiddenViews = new List<Renderer>();
    readonly List<Light> hiddenLights = new List<Light>();

    // a scene that is still loading has no roots to look through, Start comes after the whole scene is in
    void OnEnable() => Hide();
    void Start() => Hide();

    void Hide()
    {
        if (!gameObject.scene.isLoaded) return;
        Show();
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (System.Array.IndexOf(names, t.name) < 0) continue;
                if (Application.isPlaying) { t.gameObject.SetActive(false); continue; }
                foreach (var view in t.GetComponentsInChildren<Renderer>(true)) { view.forceRenderingOff = true; hiddenViews.Add(view); }
                foreach (var lamp in t.GetComponentsInChildren<Light>(true)) { lamp.enabled = false; hiddenLights.Add(lamp); }
            }
    }

    void OnDisable() => Show();

    void Show()
    {
        foreach (var view in hiddenViews) if (view != null) view.forceRenderingOff = false;
        foreach (var lamp in hiddenLights) if (lamp != null) lamp.enabled = true;
        hiddenViews.Clear();
        hiddenLights.Clear();
    }
}
