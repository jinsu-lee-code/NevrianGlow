using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 타이틀 배경 카메라 투어 (2026-10-23, 사용자: "메인화면이 정적 — 맵 곳곳을 자연스럽게", "천천히 둘러보다 다음 풍경" — 장면 10초).
    /// 타이틀이 열려 있는 동안 장면 목록을 차례로(끝나면 처음부터) 보여 준다. 한 장면 = 자리에서 조금 밀려가며(drift) 천천히 고개를 돌림(pan),
    /// 장면 사이는 화면이 잠깐 어두워졌다 밝아진다. 타이틀이 닫히면 카메라를 원래 자리(플레이어 머리)로 되돌린다 — 게임 상태는 건드리지 않는다.
    /// 장면 값은 조정값(월드 좌표) — 배경 · 농장 배치가 바뀌면 기즈모를 보며 옮긴다.
    /// </summary>
    [DefaultExecutionOrder(1000)] // 플레이어 시점 코드가 카메라를 놓은 뒤에 덮어쓴다
    public class TitleCameraTour : MonoBehaviour
    {
        [System.Serializable]
        public struct Shot
        {
            [Tooltip("시작 카메라 위치 (m, 월드)")]
            public Vector3 position;
            [Tooltip("보는 곳 (m, 월드)")]
            public Vector3 lookAt;
            [Tooltip("장면 동안 카메라가 밀려가는 거리 · 방향 (m, 월드)")]
            public Vector3 drift;
            [Tooltip("장면 동안 고개를 돌리는 각도 — +는 오른쪽 (°)")]
            public float pan;

            public Shot(Vector3 position, Vector3 lookAt, Vector3 drift, float pan)
            {
                this.position = position;
                this.lookAt = lookAt;
                this.drift = drift;
                this.pan = pan;
            }
        }

        [Header("참조")]
        [Tooltip("타이틀 화면 — 열려 있는 동안만 움직인다")]
        [SerializeField] private TitleScreen title;

        [Header("조정값")]
        [Tooltip("보여 줄 장면 (순서대로, 끝나면 처음부터)")]
        [SerializeField] private List<Shot> shots = new List<Shot>
        {
            new Shot(new Vector3(22f, 1.0f, -14f), new Vector3(20f, 3f, 0f), new Vector3(-0.8f, 0.1f, 0.6f), 8f),    // 포자 빛 · 달 (아트 미리보기 시점)
            new Shot(new Vector3(12f, 1.6f, -6f), new Vector3(3f, 0.8f, 3f), new Vector3(-0.6f, 0f, 0.6f), -6f),     // 가게 문 · 밭
            new Shot(new Vector3(-4f, 5.5f, -12f), new Vector3(1f, 0f, 5f), new Vector3(1.2f, 0.3f, 0f), 5f),        // 밭 위
            new Shot(new Vector3(-16f, 2.5f, 4f), new Vector3(-40f, 10f, 12f), new Vector3(0f, 0.3f, 0.8f), 10f),   // 구름 · 고리 행성
            new Shot(new Vector3(-8f, 1.4f, 18f), new Vector3(4f, 1f, 4f), new Vector3(0.8f, 0f, -0.4f), -5f),      // 밭 너머 문
            new Shot(new Vector3(14f, 2.2f, 16f), new Vector3(20f, 4f, -10f), new Vector3(-0.6f, 0f, -0.6f), 7f),   // 풀밭 너머 포자 빛
        };
        [Tooltip("한 장면을 보여 주는 시간 (초)")]
        [SerializeField, Range(2f, 20f)] private float shotSeconds = 10f; // 5초는 너무 자주 바뀜 (2026-10-23, 사용자)
        [Tooltip("장면 사이 어두워졌다 밝아지는 시간 — 각각 (초)")]
        [SerializeField, Range(0f, 2f)] private float fadeSeconds = 0.8f;
        [Tooltip("지형 위로 최소 높이 — 땅에 묻히지 않게 (m)")]
        [SerializeField, Min(0f)] private float minHeight = 0.7f;

        [Header("기즈모")]
        [Tooltip("장면 위치 · 밀려가는 방향 · 보는 곳 표시")]
        [SerializeField] private bool drawShots = true;

        private Transform cam;
        private Vector3 savedLocalPosition;
        private Quaternion savedLocalRotation;
        private bool touring;
        private int shot;
        private float shotTime;
        private Image fade;   // 장면 사이 검은 막 (타이틀 화면 맨 뒤 — 메뉴는 그 위에)

        private void LateUpdate()
        {
            bool open = title != null && title.IsOpen && shots.Count > 0;
            if (open && !touring)
                Begin();
            else if (!open && touring)
                End();
            if (!touring)
                return;

            float dt = Time.unscaledDeltaTime;
            shotTime += dt;
            if (shotTime >= shotSeconds)
            {
                shotTime -= shotSeconds;
                shot = (shot + 1) % shots.Count;
            }
            Apply(shots[shot], shotTime / shotSeconds);

            // 장면 끝 fadeSeconds 동안 어두워지고, 다음 장면 처음 fadeSeconds 동안 밝아짐
            float black = 0f;
            if (fadeSeconds > 0f && shots.Count > 1)
            {
                if (shotTime < fadeSeconds)
                    black = 1f - shotTime / fadeSeconds;
                else if (shotTime > shotSeconds - fadeSeconds)
                    black = (shotTime - (shotSeconds - fadeSeconds)) / fadeSeconds;
            }
            SetFade(Mathf.SmoothStep(0f, 1f, black));
        }

        // 장면 하나의 t(0~1) 지점 — 천천히 출발해 천천히 멈추게(가속 · 감속) 밀려가며 고개를 돌린다
        private void Apply(Shot s, float t)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            Vector3 pos = s.position + s.drift * k;
            var terrain = Terrain.activeTerrain;
            if (terrain != null)
                pos.y = Mathf.Max(pos.y, terrain.SampleHeight(pos) + terrain.GetPosition().y + minHeight);
            var look = Quaternion.LookRotation(s.lookAt - s.position, Vector3.up);
            var rotation = Quaternion.AngleAxis(s.pan * (k - 0.5f), Vector3.up) * look;
            cam.SetPositionAndRotation(pos, rotation);
        }

        private void Begin()
        {
            var main = Camera.main;
            if (main == null)
                return;
            cam = main.transform;
            savedLocalPosition = cam.localPosition;
            savedLocalRotation = cam.localRotation;
            touring = true;
            shot = 0;
            shotTime = fadeSeconds; // 첫 장면은 바로 밝게
            EnsureFade();
            Apply(shots[0], shotTime / shotSeconds);
        }

        private void End()
        {
            touring = false;
            SetFade(0f);
            if (cam == null)
                return;
            cam.localPosition = savedLocalPosition;
            cam.localRotation = savedLocalRotation;
        }

        private void OnDisable()
        {
            if (touring)
                End();
        }

        private void EnsureFade()
        {
            if (fade != null || title == null)
                return;
            var go = new GameObject("TourFade", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(title.transform, false);
            rt.SetAsFirstSibling(); // 메뉴 · 제목 뒤
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            fade = go.GetComponent<Image>();
            fade.raycastTarget = false;
            fade.color = new Color(0f, 0f, 0f, 0f);
        }

        private void SetFade(float alpha)
        {
            if (fade == null)
                return;
            fade.color = new Color(0f, 0f, 0f, alpha);
            fade.enabled = alpha > 0.001f;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawShots)
                return;
            foreach (var s in shots)
            {
                Core.GizmoKit.Point(s.position, 0.3f, Core.GizmoKit.Spawn);
                Core.GizmoKit.Line(s.position, s.position + s.drift, Core.GizmoKit.Spawn);
                Core.GizmoKit.Line(s.position, s.lookAt, Core.GizmoKit.Interaction);
            }
        }
    }
}
