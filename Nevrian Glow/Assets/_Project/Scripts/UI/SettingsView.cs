using System;
using System.Collections.Generic;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 설정 패널 (TASKS 5-6): 전체 · 음악 · 효과음 볼륨, 마우스 감도, 해상도, 전체 화면, 그래픽 품질. 바꾸는 즉시 적용 · 기기에 남김(SettingsStore).
    /// 타이틀 · 일시정지 화면이 자기 메뉴 대신 이 패널을 띄우고, '뒤로'를 누르면 돌려준다.
    /// </summary>
    public class SettingsView : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("전체 볼륨 슬라이더 (0~1)")]
        [SerializeField] private Slider masterSlider;
        [Tooltip("음악 볼륨 슬라이더 (배경음 · 환경음, 0~1)")]
        [SerializeField] private Slider musicSlider;
        [Tooltip("효과음 볼륨 슬라이더 (효과음 · UI, 0~1)")]
        [SerializeField] private Slider sfxSlider;
        [Tooltip("마우스 감도 배율 슬라이더")]
        [SerializeField] private Slider sensitivitySlider;
        [Tooltip("슬라이더 값 글자 (전체 · 음악 · 효과음 · 감도 순)")]
        [SerializeField] private TMP_Text[] valueLabels = new TMP_Text[4];
        [Tooltip("해상도 글자")]
        [SerializeField] private TMP_Text resolutionLabel;
        [Tooltip("해상도 이전(작게) 버튼")]
        [SerializeField] private Button resolutionPrev;
        [Tooltip("해상도 다음(크게) 버튼")]
        [SerializeField] private Button resolutionNext;
        [Tooltip("전체 화면 켜기 · 끄기 버튼")]
        [SerializeField] private Button fullscreenButton;
        [Tooltip("전체 화면 버튼 글자")]
        [SerializeField] private TMP_Text fullscreenLabel;
        [Tooltip("'뒤로' 버튼")]
        [SerializeField] private Button backButton;

        [Header("그래픽 품질 (최적화 2026-10-30 — 비우면 줄을 숨김)")]
        [Tooltip("품질 단계 이름 · 수 (Data/Stats/GraphicsQuality)")]
        [SerializeField] private GraphicsQualityData quality;
        [Tooltip("그래픽 품질 글자")]
        [SerializeField] private TMP_Text qualityLabel;
        [Tooltip("그래픽 품질 이전(가볍게) 버튼")]
        [SerializeField] private Button qualityPrev;
        [Tooltip("그래픽 품질 다음(곱게) 버튼")]
        [SerializeField] private Button qualityNext;

        private List<(int w, int h)> resolutions;
        private Action onBack;
        private bool refreshing;
        private bool wired;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            if (masterSlider == null || musicSlider == null || sfxSlider == null || sensitivitySlider == null || valueLabels == null || valueLabels.Length < 4
                || resolutionLabel == null || resolutionPrev == null || resolutionNext == null || fullscreenButton == null || fullscreenLabel == null || backButton == null)
            {
                Debug.LogError("[SettingsView] 참조가 비어 있음 - 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            Wire();
        }

        private void Wire()
        {
            if (wired)
                return;
            wired = true;
            SetupSlider(masterSlider, 0f, 1f, v => Change(s => s.MasterVolume = v));
            SetupSlider(musicSlider, 0f, 1f, v => Change(s => s.MusicVolume = v));
            SetupSlider(sfxSlider, 0f, 1f, v => Change(s => s.SfxVolume = v));
            SetupSlider(sensitivitySlider, GameSettings.MinSensitivity, GameSettings.MaxSensitivity, v => Change(s => s.SensitivityScale = v));
            resolutionPrev.onClick.AddListener(() => StepResolution(-1));
            resolutionNext.onClick.AddListener(() => StepResolution(1));
            fullscreenButton.onClick.AddListener(() => Change(s => s.Fullscreen = !s.Fullscreen));
            backButton.onClick.AddListener(Back);
            if (HasQualityRow)
            {
                qualityPrev.onClick.AddListener(() => StepQuality(-1));
                qualityNext.onClick.AddListener(() => StepQuality(1));
            }
        }

        private bool HasQualityRow => quality != null && quality.Tiers.Count > 0 && qualityLabel != null && qualityPrev != null && qualityNext != null;

        private void SetupSlider(Slider slider, float min, float max, Action<float> changed)
        {
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = false;
            slider.onValueChanged.AddListener(v =>
            {
                if (!refreshing)
                    changed(v);
            });
        }

        /// <summary>패널을 띄운다. '뒤로'(또는 Back)를 누르면 닫고 onClosed를 부른다.</summary>
        public void Open(Action onClosed)
        {
            onBack = onClosed;
            resolutions = SettingsStore.Resolutions();
            gameObject.SetActive(true);
            Refresh();
        }

        /// <summary>닫고 띄운 쪽으로 돌려준다 (Esc도 띄운 화면이 이걸 부른다).</summary>
        public void Back()
        {
            if (!IsOpen)
                return;
            gameObject.SetActive(false);
            var callback = onBack;
            onBack = null;
            callback?.Invoke();
        }

        /// <summary>띄운 쪽에 돌려주지 않고 닫는다 (화면이 통째로 닫힐 때).</summary>
        public void Hide()
        {
            onBack = null;
            gameObject.SetActive(false);
        }

        private void Change(Action<GameSettings> edit)
        {
            var next = SettingsStore.Current.Clone();
            edit(next);
            SettingsStore.Apply(next);
            Refresh();
        }

        private void StepResolution(int step)
        {
            if (resolutions == null || resolutions.Count == 0)
                return;
            var now = SettingsStore.CurrentResolution(SettingsStore.Current);
            int index = GameSettings.NearestResolution(resolutions, now.w, now.h);
            index = Mathf.Clamp(index + step, 0, resolutions.Count - 1);
            var pick = resolutions[index];
            Change(s =>
            {
                s.Width = pick.w;
                s.Height = pick.h;
            });
        }

        private void StepQuality(int step)
        {
            int index = SettingsStore.Current.QualityIndex(quality.Tiers.Count, quality.DefaultTier);
            int next = Mathf.Clamp(index + step, 0, quality.Tiers.Count - 1);
            if (next != index)
                Change(s => s.GraphicsQuality = next);
        }

        private void Refresh()
        {
            var s = SettingsStore.Current;
            refreshing = true;
            masterSlider.value = s.MasterVolume;
            musicSlider.value = s.MusicVolume;
            sfxSlider.value = s.SfxVolume;
            sensitivitySlider.value = s.SensitivityScale;
            refreshing = false;

            valueLabels[0].text = Percent(s.MasterVolume);
            valueLabels[1].text = Percent(s.MusicVolume);
            valueLabels[2].text = Percent(s.SfxVolume);
            valueLabels[3].text = $"×{s.SensitivityScale:0.0}";

            var res = SettingsStore.CurrentResolution(s);
            resolutionLabel.text = $"{res.w} × {res.h}";
            if (resolutions != null)
            {
                int index = GameSettings.NearestResolution(resolutions, res.w, res.h);
                resolutionPrev.interactable = index > 0;
                resolutionNext.interactable = index >= 0 && index < resolutions.Count - 1;
            }
            fullscreenLabel.text = s.Fullscreen ? "전체 화면: 켜짐" : "전체 화면: 꺼짐 (창 모드)";

            if (HasQualityRow)
            {
                int q = s.QualityIndex(quality.Tiers.Count, quality.DefaultTier);
                qualityLabel.text = quality.Get(q).DisplayName;
                qualityPrev.interactable = q > 0;
                qualityNext.interactable = q < quality.Tiers.Count - 1;
            }
        }

        private static string Percent(float v) => $"{Mathf.RoundToInt(v * 100f)}%";
    }
}
