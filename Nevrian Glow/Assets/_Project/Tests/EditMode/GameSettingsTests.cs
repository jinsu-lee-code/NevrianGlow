using System.Collections.Generic;
using NevrianGlowFarm.Logic.Settings;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    /// <summary>설정 값 범위 · 해상도 고르기 (TASKS 5-6).</summary>
    public class GameSettingsTests
    {
        [Test]
        public void Defaults_AreFullVolumeAndBaseSensitivity()
        {
            var s = new GameSettings();
            Assert.AreEqual(1f, s.MasterVolume);
            Assert.AreEqual(1f, s.MusicVolume);
            Assert.AreEqual(1f, s.SfxVolume);
            Assert.AreEqual(1f, s.SensitivityScale);
            Assert.AreEqual(0, s.Width);
        }

        [Test]
        public void Clamp_PutsValuesInRange()
        {
            var s = new GameSettings { MasterVolume = 1.5f, MusicVolume = -0.2f, SfxVolume = float.NaN, SensitivityScale = 10f, Width = 1920, Height = 0 };
            s.Clamp();
            Assert.AreEqual(1f, s.MasterVolume);
            Assert.AreEqual(0f, s.MusicVolume);
            Assert.AreEqual(1f, s.SfxVolume);
            Assert.AreEqual(GameSettings.MaxSensitivity, s.SensitivityScale);
            Assert.AreEqual(0, s.Width, "가로 · 세로 중 하나라도 0이면 '지금 화면 그대로'");
            Assert.AreEqual(0, s.Height);

            s.SensitivityScale = 0f;
            s.Clamp();
            Assert.AreEqual(GameSettings.MinSensitivity, s.SensitivityScale);
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var a = new GameSettings();
            var b = a.Clone();
            b.MasterVolume = 0.3f;
            Assert.AreEqual(1f, a.MasterVolume);
        }

        [Test]
        public void NearestResolution_ExactThenClosestArea()
        {
            var list = new List<(int w, int h)> { (1280, 720), (1600, 900), (1920, 1080), (2560, 1440) };
            Assert.AreEqual(2, GameSettings.NearestResolution(list, 1920, 1080));
            Assert.AreEqual(1, GameSettings.NearestResolution(list, 1680, 945), "목록에 없는 창 크기 → 넓이가 가장 가까운 칸");
            Assert.AreEqual(0, GameSettings.NearestResolution(list, 800, 600));
            Assert.AreEqual(-1, GameSettings.NearestResolution(new List<(int w, int h)>(), 1920, 1080));
        }
    }
}
