using NevrianGlowFarm.Core;
using NevrianGlowFarm.Logic.Game;
using UnityEngine;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 정산 시점 (김봉준 요청 11/02): 정산 단계 동안 카메라를 가게 위쪽 모서리(이 오브젝트 자리 · 방향)로 옮겨 가게 전체를 보여 준다.
    /// 들어갈 때 부드럽게 옮기고, 정산이 끝나면 카메라를 원래 자리(플레이어 머리)로 되돌린다 — 게임 상태는 건드리지 않는다.
    /// 자리 · 방향은 Build Shop Layout이 배치도 크기로 놓는다 (가게 앞 왼쪽 위 모서리 → 가게 가운데).
    /// </summary>
    [DefaultExecutionOrder(1000)] // 플레이어 시점 코드가 카메라를 놓은 뒤에 덮어쓴다
    public class SettlementCameraView : MonoBehaviour
    {
        [Header("조정값")]
        [Tooltip("카메라를 옮기는 단계")]
        [SerializeField] private DayPhase phase = DayPhase.Settlement;
        [Tooltip("원래 시점에서 이 자리로 옮겨 가는 시간 (초, 0이면 바로)")]
        [SerializeField, Range(0f, 3f)] private float blendSeconds = 0.8f;

        [Header("기즈모")]
        [Tooltip("카메라 자리 · 보는 방향 표시")]
        [SerializeField] private bool drawView = true;

        private Transform cam;
        private Vector3 savedLocalPosition;
        private Quaternion savedLocalRotation;
        private Vector3 fromPosition;
        private Quaternion fromRotation;
        private bool viewing;
        private float blend;

        private void LateUpdate()
        {
            var game = GameManager.Instance;
            bool want = game != null && game.IsStarted && game.Phase == phase;
            if (want && !viewing)
                Begin();
            else if (!want && viewing)
                End();
            if (!viewing)
                return;

            blend = blendSeconds > 0f ? Mathf.Min(1f, blend + Time.unscaledDeltaTime / blendSeconds) : 1f;
            float k = Mathf.SmoothStep(0f, 1f, blend);
            cam.SetPositionAndRotation(Vector3.Lerp(fromPosition, transform.position, k), Quaternion.Slerp(fromRotation, transform.rotation, k));
        }

        private void Begin()
        {
            var main = Camera.main;
            if (main == null)
                return;
            cam = main.transform;
            savedLocalPosition = cam.localPosition;
            savedLocalRotation = cam.localRotation;
            fromPosition = cam.position;
            fromRotation = cam.rotation;
            blend = 0f;
            viewing = true;
        }

        private void End()
        {
            viewing = false;
            if (cam == null)
                return;
            cam.localPosition = savedLocalPosition;
            cam.localRotation = savedLocalRotation;
        }

        private void OnDisable()
        {
            if (viewing)
                End();
        }

        private void OnDrawGizmos()
        {
            if (!drawView)
                return;
            GizmoKit.Point(transform.position, 0.3f, GizmoKit.Interaction);
            GizmoKit.Arrow(transform.position, transform.position + transform.forward * 3f, GizmoKit.Interaction);
            GizmoKit.Label(transform.position + Vector3.up * 0.4f, "정산 시점", GizmoKit.Interaction);
        }
    }
}
