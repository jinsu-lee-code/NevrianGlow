using System;
using System.Collections;
using NevrianGlowFarm.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 로딩 화면 (최적화 2026-10-30, 사용자 요청 "중간에 로딩창"). 두 때에 화면을 덮는다:
    /// · 실행 · 타이틀로 — Core가 구역 씬(농장 · 가게 · 배경)을 불러오는 동안 진행 막대, 다 올라온 뒤 첫 프레임들(풀 지우기 · 셰이더 준비)의 끊김까지 가리고 타이틀 위로 걷힌다.
    /// · 새 게임 · 이어하기 — 한 번 그려진 뒤에 시작 작업(칸 만들기 · 불러오기)을 하고, 잠깐 더 덮었다가 걷힌다.
    /// 덮는 동안 마우스 입력을 막는다. 장소 이동 · 다음 날은 지금처럼 페이드(ScreenFader).
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("구역 씬 불러오기 진행 (Core SceneBootstrapper)")]
        [SerializeField] private SceneBootstrapper bootstrapper;
        [Tooltip("보이기 · 숨기기 · 입력 막기")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("진행 막대 채움 (Image Type = Filled)")]
        [SerializeField] private Image progressFill;
        [Tooltip("안내 글자 (불러오는 중 · 농장으로 가는 중)")]
        [SerializeField] private TMP_Text messageLabel;
        [Tooltip("진행 % 글자")]
        [SerializeField] private TMP_Text percentLabel;

        [Header("조정값")]
        [Tooltip("구역 씬이 다 올라온 뒤에도 덮어 두는 시간 — 첫 프레임들의 끊김을 가린다 (초)")]
        [SerializeField, Min(0f)] private float holdAfterReady = 0.5f;
        [Tooltip("게임을 시작할 때 덮어 두는 최소 시간 (초)")]
        [SerializeField, Min(0f)] private float coverMinimum = 0.6f;
        [Tooltip("시작 작업 뒤에 더 덮어 두는 프레임 — 칸이 생긴 다음 프레임의 풀 지우기까지 (프레임)")]
        [SerializeField, Min(0)] private int settleFrames = 3;
        [Tooltip("걷힐 때 사라지는 시간 (초)")]
        [SerializeField, Min(0f)] private float fadeOut = 0.35f;
        [Tooltip("진행 막대가 따라가는 빠르기 — 1초에 채우는 비율 (배)")]
        [SerializeField, Min(0.1f)] private float fillSpeed = 1.5f;
        [Tooltip("안내 글자 뒤 점이 하나씩 늘어나는 간격 (초)")]
        [SerializeField, Min(0.05f)] private float dotInterval = 0.4f;
        [Tooltip("실행 · 타이틀로 때 안내 글자")]
        [SerializeField] private string bootMessage = "불러오는 중";
        [Tooltip("새 게임 · 이어하기 때 안내 글자")]
        [SerializeField] private string startMessage = "농장으로 가는 중";

        [Header("디버그")]
        [Tooltip("화면을 덮고 있는지 (읽기 전용)")]
        [SerializeField] private bool showing;

        private const float MaxStep = 1f / 15f;

        private enum Mode { Hidden, Boot, Cover, Fading }

        private Mode mode;
        private string message;
        private float shown;
        private float target;
        private float readyFor;
        private int lastDots = -1;
        private int lastPercent = -1;

        public static LoadingScreen Instance { get; private set; }

        /// <summary>화면을 덮고 있는 동안 (시작 버튼 두 번 누르기 막기 등).</summary>
        public bool IsShowing => showing;

        private void Awake()
        {
            if (bootstrapper == null || group == null || progressFill == null || messageLabel == null || percentLabel == null)
            {
                Debug.LogError("[LoadingScreen] 참조가 비어 있음 - 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            Instance = this;
            // 실행 첫 프레임부터 덮는다 (구역 씬이 이미 올라와 있어도 첫 프레임 끊김을 가리고 걷힌다)
            Show(bootMessage, Mode.Boot);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// 화면을 덮고 한 번 그려진 뒤 work(새 게임 · 이어하기 시작)를 한 다음, 최소 시간과 몇 프레임을 더 덮었다가 걷는다.
        /// 이미 덮고 있으면 바로 work만 한다.
        /// </summary>
        public void Cover(Action work)
        {
            if (!isActiveAndEnabled || mode == Mode.Cover)
            {
                work?.Invoke();
                return;
            }
            StopAllCoroutines();
            StartCoroutine(CoverRoutine(work));
        }

        private IEnumerator CoverRoutine(Action work)
        {
            Show(startMessage, Mode.Cover);
            yield return null;
            yield return null; // 로딩 화면이 그려진 뒤에 무거운 일을 한다 (누른 화면이 멈춰 보이지 않게)
            float start = Time.unscaledTime;
            work?.Invoke();
            for (int i = 0; i < settleFrames; i++)
                yield return null;
            while (Time.unscaledTime - start < coverMinimum)
            {
                target = Mathf.Clamp01((Time.unscaledTime - start) / Mathf.Max(0.01f, coverMinimum));
                yield return null;
            }
            target = 1f;
            mode = Mode.Fading;
        }

        private void Show(string text, Mode next)
        {
            mode = next;
            message = text;
            showing = true;
            shown = 0f;
            target = 0f;
            readyFor = 0f;
            lastDots = -1;
            lastPercent = -1;
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.gameObject.SetActive(true);
            transform.SetAsLastSibling();
            // 장소 이동 페이드는 늘 맨 위 (Create Meta Screens와 같은 규칙)
            var fader = transform.parent != null ? transform.parent.Find("ScreenFader") : null;
            if (fader != null)
                fader.SetAsLastSibling();
            Draw();
        }

        private void Update()
        {
            if (mode == Mode.Hidden)
                return;
            // 한 프레임이 길게 멈춰도(씬 켜기 · 풀 지우기) 그 시간을 덮어 둘 시간에서 한꺼번에 빼지 않는다
            float dt = Mathf.Min(Time.unscaledDeltaTime, MaxStep);
            if (mode == Mode.Boot)
            {
                if (!bootstrapper.IsReady)
                    target = bootstrapper.Progress * 0.9f; // 마지막 10%는 다 올라온 뒤 덮어 두는 시간
                else
                {
                    readyFor += dt;
                    target = 0.9f + 0.1f * Mathf.Clamp01(readyFor / Mathf.Max(0.01f, holdAfterReady));
                    if (readyFor >= holdAfterReady)
                        mode = Mode.Fading;
                }
            }
            shown = Mathf.MoveTowards(shown, target, fillSpeed * dt);
            if (mode == Mode.Fading)
            {
                shown = 1f;
                group.blocksRaycasts = false;
                group.alpha = fadeOut > 0f ? Mathf.MoveTowards(group.alpha, 0f, dt / fadeOut) : 0f;
                if (group.alpha <= 0f)
                {
                    mode = Mode.Hidden;
                    showing = false;
                    group.gameObject.SetActive(false);
                    return;
                }
            }
            Draw();
        }

        // 글자는 바뀔 때만 다시 쓴다 (매 프레임 문자열 만들지 않게)
        private void Draw()
        {
            progressFill.fillAmount = shown;
            int dots = 1 + (int)(Time.unscaledTime / dotInterval) % 3;
            if (dots != lastDots)
            {
                lastDots = dots;
                messageLabel.text = message + new string('.', dots);
            }
            int percent = Mathf.RoundToInt(shown * 100f);
            if (percent != lastPercent)
            {
                lastPercent = percent;
                percentLabel.text = percent + "%";
            }
        }
    }
}
