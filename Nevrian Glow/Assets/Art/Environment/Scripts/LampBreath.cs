using UnityEngine;

/// <summary>
/// A light that slowly breathes. Put it on a light; give lights different phases so they rise and fall in turn.
/// Runs in Play mode only, so the intensity in the prefab stays the authored one.
/// </summary>
[RequireComponent(typeof(Light))]
public class LampBreath : MonoBehaviour
{
    [Tooltip("Seconds for one breath")]
    [Min(0.1f)] public float period = 7f;
    [Tooltip("Lowest brightness, share of the authored intensity")]
    [Range(0f, 1f)] public float low = 0.45f;
    [Tooltip("Where in the breath it starts, 0..1")]
    [Range(0f, 1f)] public float phase;

    Light lamp;
    float authored;

    void Awake()
    {
        lamp = GetComponent<Light>();
        authored = lamp.intensity;
    }

    void Update()
    {
        float breath = Mathf.Sin((Time.time / period + phase) * Mathf.PI * 2f) * 0.5f + 0.5f;
        lamp.intensity = authored * Mathf.Lerp(low, 1f, breath);
    }
}
