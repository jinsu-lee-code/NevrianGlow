#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Systems.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace NevrianGlowFarm.Capture
{
    /// <summary>
    /// 플레이 영상용 '사람처럼 움직이는' 조작 (11/03). 게임 입력 경로 그대로 가상 키보드 · 마우스를 쓴다 — 프롬프트 · 고리 · 소리가 실제 플레이와 같다.
    /// 시선: 목표까지 빠르게 갔다 살짝 넘치고 되돌아오는 움직임 + 멈춰 있어도 미세하게 흔들림, 걸을 때는 방향을 천천히 틀며 가끔 둘러봄.
    /// 조작 사이마다 반응 시간을 두고, 같은 동작도 간격이 조금씩 다르다. 시드가 같으면 같은 움직임.
    /// </summary>
    [DefaultExecutionOrder(-200)] // PlayerController보다 먼저 시점을 정한다
    public sealed class HumanPlayer : MonoBehaviour
    {
        private Keyboard keyboard;
        private Mouse mouse;
        private readonly List<InputDevice> disabled = new List<InputDevice>();
        private readonly HashSet<Key> held = new HashSet<Key>();
        private bool keysDirty, mouseDirty, leftButton, rightButton;
        private Vector2 cursor, lastCursorSent;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditorBehavior;
        private InputSettings.BackgroundBehavior oldBackground;

        private PlayerController player;
        private Transform pivot;
        private float yaw, pitch, lastYaw, lastPitch;
        private float noiseSeedA, noiseSeedB;
        private RectTransform cursorImage;
        private Canvas cursorCanvas;
        private System.Random random;

        /// <summary>시선 흔들림 크기 (도) — 멈춰 있을 때 기준.</summary>
        public float Sway { get; set; } = 0.12f;
        public PlayerController Player => player;
        public Vector2 Cursor => cursor;
        public Vector3 EyePosition => pivot.position;

        public static HumanPlayer Create(int seed = 7)
        {
            CaptureTweaks.Ensure();
            CaptureTweaks.SetGlowNear(3f, 10f);
            var go = new GameObject("HumanPlayer");
            DontDestroyOnLoad(go);
            var h = go.AddComponent<HumanPlayer>();
            h.random = new System.Random(seed);
            h.noiseSeedA = seed * 13.1f;
            h.noiseSeedB = seed * 47.7f;
            return h;
        }

        private void Awake()
        {
            // 실제 장치를 끄고 가상 장치만 — 촬영 중 진짜 마우스가 끼어들지 않게. 끝나면 되돌린다
            foreach (var d in InputSystem.devices)
                if (d is Keyboard || d is Pointer)
                    disabled.Add(d);
            foreach (var d in disabled)
                InputSystem.DisableDevice(d);
            keyboard = InputSystem.AddDevice<Keyboard>("HumanKeyboard");
            mouse = InputSystem.AddDevice<Mouse>("HumanMouse");
            oldEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            oldBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            cursor = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            BuildCursor();
            Rebind();
        }

        private void OnDestroy()
        {
            if (keyboard != null)
                InputSystem.RemoveDevice(keyboard);
            if (mouse != null)
                InputSystem.RemoveDevice(mouse);
            foreach (var d in disabled)
                InputSystem.EnableDevice(d);
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorBehavior;
            InputSystem.settings.backgroundBehavior = oldBackground;
        }

        /// <summary>플레이어를 다시 찾고 지금 시점을 받아 온다.</summary>
        public void Rebind()
        {
            player = FindFirstObjectByType<PlayerController>();
            if (player == null)
                return;
            // 위아래 시점은 카메라 자신이 돈다 (PlayerController.cameraPivot) — 눈높이 기준도 여기
            var cam = player.GetComponentInChildren<Camera>(true);
            pivot = cam != null ? cam.transform : player.transform;
            ReadLook();
        }

        private void ReadLook()
        {
            yaw = player.transform.eulerAngles.y;
            pitch = Mathf.DeltaAngle(0f, pivot.localEulerAngles.x);
            lastYaw = yaw;
            lastPitch = pitch;
        }

        // ── 난수 ──

        public float Rand(float min, float max) => min + (float)random.NextDouble() * (max - min);

        /// <summary>반응 시간 — 뭔가 하기 전 잠깐.</summary>
        public IEnumerator Beat(float min = 0.12f, float max = 0.3f)
        {
            yield return new WaitForSeconds(Rand(min, max));
        }

        // ── 매 프레임 ──

        private void Update()
        {
            if (player == null)
                return;
            // 순간이동 · 게임이 시점을 바꿨으면 그 값을 받는다
            float actualYaw = player.transform.eulerAngles.y;
            float actualPitch = Mathf.DeltaAngle(0f, pivot.localEulerAngles.x);
            if (Mathf.Abs(Mathf.DeltaAngle(actualYaw, lastYaw)) > 0.8f || Mathf.Abs(actualPitch - lastPitch) > 0.8f)
            {
                yaw = actualYaw;
                pitch = actualPitch;
            }
            float t = Time.time;
            float n1 = (Mathf.PerlinNoise(noiseSeedA, t * 0.35f) - 0.5f) * 2f + (Mathf.PerlinNoise(noiseSeedA + 9f, t * 1.3f) - 0.5f) * 0.6f;
            float n2 = (Mathf.PerlinNoise(noiseSeedB, t * 0.3f) - 0.5f) * 2f + (Mathf.PerlinNoise(noiseSeedB + 9f, t * 1.1f) - 0.5f) * 0.6f;
            lastYaw = yaw + n1 * Sway;
            lastPitch = pitch + n2 * Sway * 0.7f;
            if (!PlayerInputGate.IsBlocked)
                player.SetLook(lastYaw, lastPitch);
            else
            {
                lastYaw = actualYaw;
                lastPitch = actualPitch;
            }
        }

        private void LateUpdate()
        {
            if (keysDirty)
            {
                keysDirty = false;
                var keys = new Key[held.Count];
                held.CopyTo(keys);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            }
            if (mouseDirty || cursor != lastCursorSent)
            {
                mouseDirty = false;
                var state = new MouseState { position = cursor, delta = cursor - lastCursorSent };
                state = state.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left, leftButton).WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right, rightButton);
                InputSystem.QueueStateEvent(mouse, state);
                lastCursorSent = cursor;
            }
            if (cursorImage != null)
            {
                bool show = UnityEngine.Cursor.visible && UnityEngine.Cursor.lockState != CursorLockMode.Locked;
                if (cursorCanvas.enabled != show)
                    cursorCanvas.enabled = show;
                cursorImage.anchoredPosition = cursor;
            }
        }

        // ── 키 · 버튼 ──

        public void KeyDown(Key k)
        {
            if (held.Add(k))
                keysDirty = true;
        }

        public void KeyUp(Key k)
        {
            if (held.Remove(k))
                keysDirty = true;
        }

        public IEnumerator Tap(Key k, float hold = -1f)
        {
            KeyDown(k);
            yield return new WaitForSeconds(hold > 0f ? hold : Rand(0.06f, 0.11f));
            KeyUp(k);
            yield return new WaitForSeconds(Rand(0.05f, 0.1f));
        }

        public IEnumerator HoldKey(Key k, float seconds)
        {
            KeyDown(k);
            yield return new WaitForSeconds(seconds);
            KeyUp(k);
        }

        public void LeftDown()
        {
            leftButton = true;
            mouseDirty = true;
        }

        public void LeftUp()
        {
            leftButton = false;
            mouseDirty = true;
        }

        public IEnumerator Click(float hold = -1f)
        {
            LeftDown();
            yield return new WaitForSeconds(hold > 0f ? hold : Rand(0.06f, 0.1f));
            LeftUp();
            yield return new WaitForSeconds(Rand(0.03f, 0.07f));
        }

        // ── 시선 ──

        // 처음 빠르게 끝에서 느려지는 곡선 (최소 저크)
        private static float MinJerk(float x) => x * x * x * (10f + x * (-15f + 6f * x));

        /// <summary>그 지점을 본다. 큰 움직임은 살짝 넘쳤다 되돌아온다.</summary>
        public IEnumerator LookAt(Vector3 world, float durationScale = 1f)
        {
            Vector3 d = world - pivot.position;
            float targetYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float targetPitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            yield return LookTo(targetYaw, targetPitch, durationScale);
        }

        public IEnumerator LookTo(float targetYaw, float targetPitch, float durationScale = 1f)
        {
            float dy = Mathf.DeltaAngle(yaw, targetYaw);
            float dp = targetPitch - pitch;
            float amount = Mathf.Sqrt(dy * dy + dp * dp);
            if (amount < 0.05f)
                yield break;
            float duration = (0.16f + 0.0055f * amount) * durationScale * Rand(0.92f, 1.1f);
            float overshoot = amount > 12f ? Rand(0.015f, 0.05f) : 0f;
            float y0 = yaw, p0 = pitch;
            float first = duration * (overshoot > 0f ? 0.78f : 1f);
            for (float t = 0f; t < first; t += Time.deltaTime)
            {
                float k = MinJerk(Mathf.Clamp01(t / first)) * (1f + overshoot);
                yaw = y0 + dy * k;
                pitch = p0 + dp * k;
                yield return null;
            }
            if (overshoot > 0f)
            {
                float y1 = yaw, p1 = pitch, back = duration * 0.22f;
                for (float t = 0f; t < back; t += Time.deltaTime)
                {
                    float k = MinJerk(Mathf.Clamp01(t / back));
                    yaw = Mathf.Lerp(y1, y0 + dy, k);
                    pitch = Mathf.Lerp(p1, p0 + dp, k);
                    yield return null;
                }
            }
            yaw = y0 + dy;
            pitch = p0 + dp;
        }

        /// <summary>그 자리에서 잠깐 둘러본다 (시선 흔들림만).</summary>
        public IEnumerator Idle(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }

        // ── 걷기 ──

        /// <summary>
        /// 그 지점까지 걷는다: 먼저 대충 몸을 돌리고 W를 누른 채 방향을 조금씩 고친다. 막혀서 안 가면 시간 제한으로 끝난다.
        /// lookAhead = 걷는 동안 시선 높이 (수평에서 몇 도 아래), glance = 걷다가 가끔 다른 곳을 볼 지점.
        /// </summary>
        public IEnumerator WalkTo(Vector3 target, float stopDistance = 0.45f, float lookDown = 6f, bool sprint = false, float timeout = 25f, Vector3? glance = null)
        {
            Vector3 flat = Flat(target - player.transform.position);
            if (flat.magnitude <= stopDistance)
                yield break;
            float wantYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            if (Mathf.Abs(Mathf.DeltaAngle(yaw, wantYaw)) > 18f)
                yield return LookTo(wantYaw + Rand(-3f, 3f), lookDown + Rand(-2f, 2f), 1.15f);
            yield return Beat(0.05f, 0.16f);
            KeyDown(Key.W);
            if (sprint)
                KeyDown(Key.LeftShift);
            float start = Time.time;
            float yawVelocity = 0f, pitchVelocity = 0f;
            float glanceAt = glance.HasValue ? Time.time + Rand(0.8f, 1.6f) : float.MaxValue;
            float glanceEnd = 0f;
            float wobble = Rand(0f, 100f);
            float saved = Sway;
            Sway = 0.28f;
            while (Time.time - start < timeout)
            {
                flat = Flat(target - player.transform.position);
                float dist = flat.magnitude;
                if (dist <= stopDistance)
                    break;
                wantYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
                float wantPitch = lookDown + (Mathf.PerlinNoise(wobble, Time.time * 0.25f) - 0.5f) * 5f;
                wantYaw += (Mathf.PerlinNoise(wobble + 40f, Time.time * 0.2f) - 0.5f) * 5f * Mathf.Clamp01(dist / 4f);
                // 걷다가 잠깐 다른 곳을 봄 — 다리는 그대로 (몸이 곧 시선이라 살짝만)
                if (Time.time > glanceAt && glance.HasValue && dist > 2.5f)
                {
                    glanceEnd = Time.time + Rand(0.7f, 1.2f);
                    glanceAt = Time.time + Rand(3f, 6f);
                }
                if (Time.time < glanceEnd && glance.HasValue)
                {
                    Vector3 g = glance.Value - pivot.position;
                    float gy = Mathf.Atan2(g.x, g.z) * Mathf.Rad2Deg;
                    wantYaw += Mathf.Clamp(Mathf.DeltaAngle(wantYaw, gy), -14f, 14f);
                    wantPitch = Mathf.Lerp(wantPitch, -Mathf.Atan2(g.y, new Vector2(g.x, g.z).magnitude) * Mathf.Rad2Deg, 0.7f);
                }
                yaw = Mathf.SmoothDampAngle(yaw, wantYaw, ref yawVelocity, 0.32f);
                pitch = Mathf.SmoothDamp(pitch, wantPitch, ref pitchVelocity, 0.45f);
                yield return null;
            }
            KeyUp(Key.W);
            KeyUp(Key.LeftShift);
            Sway = saved;
            yield return new WaitForSeconds(Rand(0.1f, 0.22f));
        }

        /// <summary>가게처럼 NavMesh가 있는 곳: 가구를 돌아 걷는다. 길이 없으면 곧장.</summary>
        public IEnumerator WalkNav(Vector3 target, float stopDistance = 0.5f, float lookDown = 6f)
        {
            var path = new NavMeshPath();
            if (NavMesh.SamplePosition(player.transform.position, out var from, 3f, NavMesh.AllAreas)
                && NavMesh.SamplePosition(target, out var to, 3f, NavMesh.AllAreas)
                && NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) && path.corners.Length > 1)
            {
                for (int i = 1; i < path.corners.Length; i++)
                    yield return WalkTo(path.corners[i], i == path.corners.Length - 1 ? stopDistance : 0.55f, lookDown);
            }
            else
                yield return WalkTo(target, stopDistance, lookDown);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        // ── 커서 (화면이 열렸을 때) ──

        /// <summary>커서를 그 자리로 — 살짝 휜 길을 빠르게 갔다 끝에서 느려진다.</summary>
        public IEnumerator MoveCursor(Vector2 target, float durationScale = 1f)
        {
            Vector2 from = cursor;
            float dist = Vector2.Distance(from, target);
            if (dist < 1f)
                yield break;
            float duration = (0.2f + 0.00042f * dist) * durationScale * Rand(0.9f, 1.15f);
            Vector2 side = new Vector2(-(target - from).y, (target - from).x).normalized * dist * Rand(-0.09f, 0.09f);
            Vector2 mid = (from + target) * 0.5f + side;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = MinJerk(Mathf.Clamp01(t / duration));
                cursor = (1f - k) * (1f - k) * from + 2f * k * (1f - k) * mid + k * k * target;
                yield return null;
            }
            cursor = target;
        }

        /// <summary>화면 요소 위로 커서를 옮기고 잠깐 있다가 누른다 (가운데서 조금 벗어난 자리).</summary>
        public IEnumerator ClickUi(RectTransform rect, float linger = -1f)
        {
            Vector3[] c = new Vector3[4];
            rect.GetWorldCorners(c);
            Vector2 min = c[0], max = c[2];
            Vector2 p = new Vector2(Mathf.Lerp(min.x, max.x, Rand(0.35f, 0.65f)), Mathf.Lerp(min.y, max.y, Rand(0.35f, 0.65f)));
            yield return MoveCursor(p);
            yield return new WaitForSeconds(linger >= 0f ? linger : Rand(0.12f, 0.3f));
            yield return Click();
        }

        // 녹화에 커서가 안 찍혀서 그려 준다 — 가장 위 캔버스의 흰 화살표
        private void BuildCursor()
        {
            var go = new GameObject("HumanCursor", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            cursorCanvas = go.GetComponent<Canvas>();
            cursorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            cursorCanvas.sortingOrder = 32000;
            var image = new GameObject("Arrow", typeof(RectTransform), typeof(RawImage)).GetComponent<RectTransform>();
            image.SetParent(go.transform, false);
            image.anchorMin = image.anchorMax = Vector2.zero;
            image.pivot = new Vector2(0f, 1f);
            image.sizeDelta = new Vector2(30f, 44f);
            var raw = image.GetComponent<RawImage>();
            raw.texture = MakeArrow();
            raw.raycastTarget = false;
            cursorImage = image;
        }

        private static Texture2D MakeArrow()
        {
            const int w = 60, h = 88;
            Vector2[] poly = { new Vector2(4, 2), new Vector2(4, 70), new Vector2(20, 56), new Vector2(31, 82), new Vector2(42, 77), new Vector2(31, 52), new Vector2(52, 50) };
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float dist = float.MaxValue;
                    for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                    {
                        Vector2 a = poly[i], b = poly[j];
                        float k = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
                        dist = Mathf.Min(dist, Vector2.Distance(p, a + (b - a) * k));
                    }
                    bool inside = false;
                    for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                        if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                            inside = !inside;
                    Color c = Color.clear;
                    if (inside)
                        c = dist < 4f ? new Color(0.05f, 0.05f, 0.1f, 1f) : Color.white;
                    else if (dist < 2f)
                        c = new Color(0.05f, 0.05f, 0.1f, 1f - dist / 2f);
                    px[(h - 1 - y) * w + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
#endif
