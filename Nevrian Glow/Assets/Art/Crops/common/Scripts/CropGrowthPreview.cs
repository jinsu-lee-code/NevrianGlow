using System.Collections;
using UnityEngine;

/// <summary>
/// Preview scene helper (2026-10-28): plays every crop's stage prefabs in order, the way the farm swaps them
/// (new stage prefab instantiated -> its Animator plays GrowIn / GrowUp by itself), then loops.
/// Space = restart. Art-only, not used by the game.
/// </summary>
public class CropGrowthPreview : MonoBehaviour
{
    [System.Serializable]
    public class Column
    {
        public Transform anchor;
        public GameObject[] stages = new GameObject[0];
    }

    public Column[] columns = new Column[0];
    [Tooltip("Seconds each stage stays before the next one replaces it")]
    public float stageSeconds = 2.5f;
    [Tooltip("Seconds the last stage stays before the loop restarts")]
    public float endHold = 3f;

    GameObject[] shown;
    Coroutine run;

    void Start() => Restart();

    void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;   // project uses the new Input System only
        if (kb != null && kb.spaceKey.wasPressedThisFrame) Restart();
    }

    void Restart()
    {
        if (run != null) StopCoroutine(run);
        run = StartCoroutine(Loop());
    }

    IEnumerator Loop()
    {
        shown ??= new GameObject[columns.Length];
        int most = 0;
        foreach (var c in columns) most = Mathf.Max(most, c.stages.Length);
        while (true)
        {
            for (int s = 0; s < most; s++)
            {
                for (int i = 0; i < columns.Length; i++)
                {
                    var c = columns[i];
                    if (s >= c.stages.Length || c.stages[s] == null) continue;   // this crop is done: keep its last stage
                    if (shown[i] != null) Destroy(shown[i]);
                    shown[i] = Instantiate(c.stages[s], c.anchor);
                    shown[i].transform.localPosition = Vector3.zero;
                    shown[i].transform.localRotation = Quaternion.identity;
                }
                yield return new WaitForSeconds(stageSeconds);
            }
            yield return new WaitForSeconds(endHold);
            for (int i = 0; i < shown.Length; i++) if (shown[i] != null) Destroy(shown[i]);
            yield return new WaitForSeconds(0.5f);
        }
    }
}
