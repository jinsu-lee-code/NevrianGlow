using UnityEngine;

// Put on the SD cat: grass using DSEWA/ToonFoliage bends away from this object within `radius`.
[ExecuteAlways]
public class FoliagePushSource : MonoBehaviour
{
    static readonly int CatPush = Shader.PropertyToID("_DSEWA_CatPush");
    public float radius = 1.2f;

    void LateUpdate()
    {
        var p = transform.position;
        Shader.SetGlobalVector(CatPush, new Vector4(p.x, p.y, p.z, radius));
    }

    void OnDisable()
    {
        Shader.SetGlobalVector(CatPush, Vector4.zero);
    }
}
