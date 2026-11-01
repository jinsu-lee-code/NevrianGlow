using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Lets BackGroundScene be played alone and walked through with the game's own first-person view. Camera values and the
/// numbers below are copied from Core's Main Camera and Data/Stats/PlayerSettings. WASD walks, Shift sprints, the mouse
/// looks, and the eye stays above whatever collider is underneath. It turns itself off as soon as the game's camera is
/// around, so it never competes with the player in the real game. A scene that needs more to look like the game (the shop
/// alone has no bloom or room light) names it below: played alone, the background scene and the room light come along.
/// </summary>
[RequireComponent(typeof(Camera))]
public class BackgroundCheckCamera : MonoBehaviour
{
    [Tooltip("Walking speed (m/s)")]
    [Min(0f)] public float moveSpeed = 4.6f;
    [Tooltip("Speed multiplier while Shift is held")]
    [Min(1f)] public float sprintMultiplier = 1.6f;
    [Tooltip("Degrees per mouse pixel")]
    [Min(0f)] public float lookSensitivity = 0.14f;
    [Tooltip("Furthest the view tilts up or down (degrees)")]
    [Range(0f, 90f)] public float pitchLimit = 83f;
    [Tooltip("Eye height above the ground (m)")]
    [Min(0f)] public float eyeHeight = 1.7f;
    [Tooltip("Lighthouse progress to start from: 1 = the fully woken world, 0 = the grey world of a new game")]
    [Range(0f, 1f)] public float worldProgress = 1f;
    [Tooltip("Scene to load next to this one when played alone (empty = none)")]
    public string alsoLoad = "";
    [Tooltip("Flat room light to use when played alone (alpha 0 = keep the scene's)")]
    public Color roomAmbient = Color.clear;

    float yaw, pitch;

    void Awake()
    {
        if (gameObject.scene != SceneManager.GetActiveScene() || GameCameraExists()) { gameObject.SetActive(false); return; }
        if (roomAmbient.a > 0f)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = roomAmbient;
        }
        if (!string.IsNullOrEmpty(alsoLoad) && !SceneManager.GetSceneByName(alsoLoad).isLoaded)
            SceneManager.LoadSceneAsync(alsoLoad, LoadSceneMode.Additive);
        yaw = transform.eulerAngles.y;
        pitch = Mathf.DeltaAngle(0f, transform.eulerAngles.x);
    }

    void Start()
    {
        var lighthouse = FindAnyObjectByType<LighthouseGoal>();
        if (lighthouse != null) lighthouse.SetProgressImmediate(worldProgress);
    }

    bool GameCameraExists()
    {
        foreach (var other in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (other.gameObject != gameObject && other.CompareTag("MainCamera")) return true;
        return false;
    }

    void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (Mouse.current != null)
        {
            Vector2 look = Mouse.current.delta.ReadValue() * lookSensitivity;
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, -pitchLimit, pitchLimit);
        }
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        var keys = Keyboard.current;
        if (keys == null) return;
        var input = new Vector2((keys.dKey.isPressed ? 1f : 0f) - (keys.aKey.isPressed ? 1f : 0f),
                                (keys.wKey.isPressed ? 1f : 0f) - (keys.sKey.isPressed ? 1f : 0f));
        float speed = moveSpeed * (keys.leftShiftKey.isPressed ? sprintMultiplier : 1f);
        var flat = Quaternion.Euler(0f, yaw, 0f);
        var position = transform.position + flat * new Vector3(input.x, 0f, input.y).normalized * speed * Time.deltaTime;
        if (Physics.Raycast(position + Vector3.up * 3f, Vector3.down, out var hit, 50f, ~0, QueryTriggerInteraction.Ignore))
            position.y = hit.point.y + eyeHeight;
        transform.position = position;
    }
}
