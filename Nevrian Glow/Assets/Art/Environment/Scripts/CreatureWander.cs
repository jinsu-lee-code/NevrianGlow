using UnityEngine;

/// <summary>
/// Creature wander (2026-10-27, user: "creatures made only from effects"). Moves an effect-only creature (a VFX Graph
/// whose particles follow this transform) slowly round an area: it picks a new point inside the box now and then and
/// glides towards it with a gentle bob, keeping a height above the terrain. The particles lag behind on their own, so
/// the creature trails and stretches as it moves. Optional facing along the path (birds, fish).
/// All numbers are my own values.
/// </summary>
public class CreatureWander : MonoBehaviour
{
    [Tooltip("World centre of the area the creature roams")]
    public Vector3 areaCenter;
    [Tooltip("Size of the area (x, z used; y ignored)")]
    public Vector3 areaSize = new Vector3(20, 0, 20);
    [Tooltip("Height above the ground (min, max)")]
    public Vector2 heightRange = new Vector2(0.8f, 2.2f);
    [Tooltip("Travel speed (m/s)")]
    public float speed = 0.8f;
    [Tooltip("How quickly the heading turns towards the target")]
    public float turnRate = 0.8f;
    [Tooltip("Vertical bob (m) and its speed")]
    public Vector2 bob = new Vector2(0.25f, 0.6f);
    [Tooltip("Rotate the transform to face the travel direction")]
    public bool faceTravel;
    [Tooltip("Optional: stay over these heights only (e.g. the water surface for fish); 0 = use the terrain")]
    public float fixedGroundY;
    public bool useFixedGround;

    [Header("Gather round points of interest (e.g. butterflies round flowers)")]
    [Tooltip("Children of this root are the spots the creature visits (e.g. FlowersBG); empty = roam the area box")]
    public Transform attractRoot;
    [Tooltip("Only spots within this distance of the area centre (m)")]
    public float attractRange = 25f;
    [Tooltip("Seconds spent hovering at a spot before moving on (min, max)")]
    public Vector2 dwell = new Vector2(5f, 11f);
    [Tooltip("Slow down within this distance of the spot (m)")]
    public float arriveRadius = 2.5f;

    Vector3 target, velocity;
    float seed, nextPick;
    readonly System.Collections.Generic.List<Vector3> spots = new System.Collections.Generic.List<Vector3>();
    int spotIndex = -1;

    void OnEnable()
    {
        seed = Random.value * 100f;
        if (areaCenter == Vector3.zero) areaCenter = transform.position;
        spots.Clear();
        if (attractRoot != null)
            foreach (Transform c in attractRoot)
                if (c.gameObject.activeInHierarchy && Vector2.Distance(new Vector2(c.position.x, c.position.z), new Vector2(areaCenter.x, areaCenter.z)) < attractRange)
                    spots.Add(c.position);
        PickTarget();
        velocity = (target - transform.position).normalized * speed;
    }

    void PickTarget()
    {
        if (spots.Count > 0)
        {
            // next spot: a random one, preferring nearer spots so the flock hops between neighbouring flowers
            int best = -1; float bestScore = float.MaxValue;
            for (int k = 0; k < 4; k++)
            {
                int i = Random.Range(0, spots.Count);
                if (i == spotIndex && spots.Count > 1) continue;
                float d = Vector3.Distance(spots[i], transform.position) * Random.Range(0.6f, 1.4f);
                if (d < bestScore) { bestScore = d; best = i; }
            }
            if (best < 0) best = (spotIndex + 1) % spots.Count;
            spotIndex = best;
            target = spots[best];
            float travel = Vector3.Distance(target, transform.position) / Mathf.Max(speed, 0.05f);
            nextPick = Time.time + travel + Random.Range(dwell.x, dwell.y);
            return;
        }
        var r = new Vector3(Random.Range(-0.5f, 0.5f) * areaSize.x, 0, Random.Range(-0.5f, 0.5f) * areaSize.z);
        target = areaCenter + r;
        nextPick = Time.time + Random.Range(6f, 14f);
    }

    float Ground(Vector3 p)
    {
        if (useFixedGround) return fixedGroundY;
        var t = Terrain.activeTerrain;
        return t != null ? t.SampleHeight(p) + t.GetPosition().y : 0f;
    }

    void Update()
    {
        var p = transform.position;
        var flat = new Vector3(target.x - p.x, 0, target.z - p.z);
        if (spots.Count > 0) { if (Time.time > nextPick) PickTarget(); }     // hover at a spot until the dwell ends
        else if (flat.magnitude < 1.5f || Time.time > nextPick) PickTarget();
        float arrive = spots.Count > 0 ? Mathf.Clamp01(flat.magnitude / Mathf.Max(arriveRadius, 0.1f)) : 1f;
        var want = flat.normalized * speed * arrive;
        velocity = Vector3.Lerp(velocity, want, 1f - Mathf.Exp(-turnRate * Time.deltaTime));
        p += velocity * Time.deltaTime;
        float h = Mathf.Lerp(heightRange.x, heightRange.y, Mathf.PerlinNoise(seed, Time.time * 0.05f));
        float y = Ground(p) + h + Mathf.Sin(Time.time * bob.y * 6.2832f + seed) * bob.x;
        p.y = Mathf.Lerp(p.y, y, 1f - Mathf.Exp(-2f * Time.deltaTime));
        if (faceTravel && velocity.sqrMagnitude > 1e-4f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(velocity.normalized + Vector3.up * (y - transform.position.y) * 0.3f), 1f - Mathf.Exp(-3f * Time.deltaTime));
        transform.position = p;
    }
}
