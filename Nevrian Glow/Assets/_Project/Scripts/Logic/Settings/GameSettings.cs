using System;
using System.Collections.Generic;

namespace NevrianGlowFarm.Logic.Settings
{
    /// <summary>
    /// 설정 화면 값 (TASKS 5-6): 볼륨 3종 · 마우스 감도 배율 · 해상도 · 전체 화면 · 그래픽 품질. 저장 파일(진행)과 별개로 기기에 남는다.
    /// 볼륨은 사운드 담당이 맞춘 기본 믹스(AudioManager 조정값)에 곱하는 배율, 감도는 PlayerSettings 감도에 곱하는 배율.
    /// </summary>
    [Serializable]
    public class GameSettings
    {
        public const float MinSensitivity = 0.2f;
        public const float MaxSensitivity = 3f;

        public float MasterVolume = 1f;
        public float MusicVolume = 1f;
        public float SfxVolume = 1f;
        public float SensitivityScale = 1f;
        /// <summary>해상도 가로 · 세로 (0 = 지금 화면 그대로).</summary>
        public int Width;
        public int Height;
        public bool Fullscreen = true;
        /// <summary>그래픽 품질 단계 번호 (GraphicsQualityData 목록, 가벼운 것부터 0). -1 = 아직 안 고름(데이터 기본 단계).</summary>
        public int GraphicsQuality = -1;

        public GameSettings Clone() => (GameSettings)MemberwiseClone();

        /// <summary>고른 품질 단계 번호 — 안 골랐으면 기본 단계, 단계 수를 넘으면 마지막 단계. 단계가 없으면 0.</summary>
        public int QualityIndex(int tierCount, int defaultTier)
        {
            if (tierCount <= 0)
                return 0;
            int index = GraphicsQuality < 0 ? defaultTier : GraphicsQuality;
            return Math.Max(0, Math.Min(tierCount - 1, index));
        }

        /// <summary>범위 밖 값(손으로 고친 기록 · 이전 버전)을 범위 안으로.</summary>
        public void Clamp()
        {
            MasterVolume = Clamp01(MasterVolume);
            MusicVolume = Clamp01(MusicVolume);
            SfxVolume = Clamp01(SfxVolume);
            SensitivityScale = float.IsNaN(SensitivityScale) ? 1f : Math.Max(MinSensitivity, Math.Min(MaxSensitivity, SensitivityScale));
            if (Width <= 0 || Height <= 0)
                Width = Height = 0;
            if (GraphicsQuality < -1)
                GraphicsQuality = -1;
        }

        private static float Clamp01(float v) => float.IsNaN(v) ? 1f : Math.Max(0f, Math.Min(1f, v));

        /// <summary>
        /// 해상도 목록(가로, 세로 — 작은 것부터)에서 가로 · 세로가 같은 칸, 없으면 넓이가 가장 가까운 칸. 목록이 비면 -1.
        /// </summary>
        public static int NearestResolution(IReadOnlyList<(int w, int h)> list, int width, int height)
        {
            int best = -1;
            long bestDiff = long.MaxValue;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].w == width && list[i].h == height)
                    return i;
                long diff = Math.Abs((long)list[i].w * list[i].h - (long)width * height);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    best = i;
                }
            }
            return best;
        }
    }
}
