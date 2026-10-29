using System;
using System.Collections.Generic;
using NevrianGlowFarm.Logic.Settings;
using UnityEngine;

namespace NevrianGlowFarm.Core
{
    /// <summary>
    /// 설정 값을 기기(PlayerPrefs)에 두고, 바뀌면 알린다 (TASKS 5-6). 진행 저장 파일과 별개 — 새 게임 · 불러오기에도 그대로.
    /// 소리는 AudioManager, 감도는 PlayerController, 그래픽 품질은 RenderBudget이 Changed를 받아 반영하고, 해상도 · 전체 화면은 여기서 바로 적용한다.
    /// </summary>
    public static class SettingsStore
    {
        private const string Prefix = "ngf_settings_";
        private static GameSettings current;

        /// <summary>지금 설정 (처음 읽을 때 기기에서 불러온다). 바꾸려면 Apply.</summary>
        public static GameSettings Current
        {
            get
            {
                if (current == null)
                    current = Load();
                return current;
            }
        }

        /// <summary>설정이 바뀐 뒤 (새 값).</summary>
        public static event Action<GameSettings> Changed;

        /// <summary>새 값을 적용하고 기기에 남긴다. 해상도 · 전체 화면이 달라졌을 때만 화면을 바꾼다.</summary>
        public static void Apply(GameSettings next)
        {
            if (next == null)
                return;
            next = next.Clone();
            next.Clamp();
            var before = Current;
            current = next;
            Save(next);
            if (before.Width != next.Width || before.Height != next.Height || before.Fullscreen != next.Fullscreen)
                ApplyDisplay(next);
            Changed?.Invoke(next);
        }

        /// <summary>고를 수 있는 해상도 (가로, 세로 — 작은 것부터, 주사율만 다른 것은 하나로).</summary>
        public static List<(int w, int h)> Resolutions()
        {
            var list = new List<(int w, int h)>();
            foreach (var r in Screen.resolutions)
                if (!list.Contains((r.width, r.height)))
                    list.Add((r.width, r.height));
            if (list.Count == 0)
                list.Add((Screen.width, Screen.height));
            list.Sort((a, b) => a.w != b.w ? a.w.CompareTo(b.w) : a.h.CompareTo(b.h));
            return list;
        }

        /// <summary>지금 화면 해상도 (설정이 0이면 실제 화면).</summary>
        public static (int w, int h) CurrentResolution(GameSettings s) =>
            s.Width > 0 ? (s.Width, s.Height) : (Screen.width, Screen.height);

        private static void ApplyDisplay(GameSettings s)
        {
            var (w, h) = CurrentResolution(s);
            Screen.SetResolution(w, h, s.Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }

        private static GameSettings Load()
        {
            var d = new GameSettings();
            var s = new GameSettings
            {
                MasterVolume = PlayerPrefs.GetFloat(Prefix + "master", d.MasterVolume),
                MusicVolume = PlayerPrefs.GetFloat(Prefix + "music", d.MusicVolume),
                SfxVolume = PlayerPrefs.GetFloat(Prefix + "sfx", d.SfxVolume),
                SensitivityScale = PlayerPrefs.GetFloat(Prefix + "sensitivity", d.SensitivityScale),
                Width = PlayerPrefs.GetInt(Prefix + "width", 0),
                Height = PlayerPrefs.GetInt(Prefix + "height", 0),
                Fullscreen = PlayerPrefs.GetInt(Prefix + "fullscreen", Screen.fullScreen ? 1 : 0) != 0,
                GraphicsQuality = PlayerPrefs.GetInt(Prefix + "graphics_quality", d.GraphicsQuality),
            };
            s.Clamp();
            return s;
        }

        private static void Save(GameSettings s)
        {
            PlayerPrefs.SetFloat(Prefix + "master", s.MasterVolume);
            PlayerPrefs.SetFloat(Prefix + "music", s.MusicVolume);
            PlayerPrefs.SetFloat(Prefix + "sfx", s.SfxVolume);
            PlayerPrefs.SetFloat(Prefix + "sensitivity", s.SensitivityScale);
            PlayerPrefs.SetInt(Prefix + "width", s.Width);
            PlayerPrefs.SetInt(Prefix + "height", s.Height);
            PlayerPrefs.SetInt(Prefix + "fullscreen", s.Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "graphics_quality", s.GraphicsQuality);
            PlayerPrefs.Save();
        }

        // 실행 시작 때 기기 값의 해상도를 한 번 적용한다 (빌드에서만 — 에디터 게임 창은 건드리지 않는다)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyOnStartup()
        {
            current = null;
            Changed = null;
#if !UNITY_EDITOR
            if (Current.Width > 0)
                ApplyDisplay(Current);
#endif
        }
    }
}
