using System;
using System.Globalization;
using System.IO;
using System.Linq;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Systems.Cleaning;
using NevrianGlowFarm.Systems.Farm;
using UnityEngine;

namespace NevrianGlowFarm.Core
{
    /// <summary>
    /// 직접 플레이 기록 (TASKS 4-2): 단계마다 걸린 실제 시간과 그날 정산을 모아, 정산이 끝날 때마다 파일로 다시 쓴다.
    /// 에디터 = 저장소 Reports/playtest_시각.md, 빌드 = persistentDataPath/Playtest/. 측정 봇 리포트와 같은 숫자를 비교한다.
    /// 규칙 · 형식은 Logic PlaySessionLog, 여기는 단계 전환 수신과 파일 쓰기만.
    /// </summary>
    public class PlaytestRecorder : MonoBehaviour
    {
        [Header("조정값")]
        [Tooltip("기록할지 — 끄면 파일을 만들지 않는다")]
        [SerializeField] private bool record = true;

        [Header("디버그")]
        [Tooltip("이번 세션 기록 파일 (읽기 전용)")]
        [SerializeField] private string filePath;

        private readonly PlaySessionLog log = new PlaySessionLog();
        private bool hasPhase;
        private int day;
        private DayPhase phase;
        private double phaseStart;
        private FarmGrid farm;
        private string title;

        public PlaySessionLog Log => log;
        public string FilePath => filePath;

        private void Awake()
        {
            var now = DateTime.Now;
            string stamp = now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
#if UNITY_EDITOR
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Reports"));
#else
            string folder = Path.Combine(Application.persistentDataPath, "Playtest");
#endif
            filePath = Path.Combine(folder, $"playtest_{stamp}.md");
            title = $"직접 플레이 기록 {now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} (v{Application.version})";
        }

        private void OnEnable() => GameEvents.PhaseChanged += OnPhaseChanged;
        private void OnDisable() => GameEvents.PhaseChanged -= OnPhaseChanged;

        private void OnPhaseChanged(PhaseChange change)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (hasPhase)
            {
                log.AddTime(day, phase, now - phaseStart);
                if (phase == DayPhase.Sales && CleaningSystem.Instance != null)
                    log.DayOf(day).Cleanliness = CleaningSystem.Instance.LockedCleanliness;
                if (phase == DayPhase.Settlement)
                {
                    CaptureSettlement(day);
                    Write();
                }
            }
            hasPhase = true;
            day = change.Day;
            phase = change.Phase;
            phaseStart = now;
        }

        private void CaptureSettlement(int settledDay)
        {
            var game = GameManager.Instance;
            if (game == null)
                return;
            var d = log.DayOf(settledDay);
            foreach (var r in game.Run.Days)
                if (r.Day == settledDay)
                    d.Settlement = r;
            d.Money = game.Money;
            d.SkillPoints = game.SkillPoints;
            if (farm == null)
                farm = FindFirstObjectByType<FarmGrid>(); // 정산 때 한 번 (매 프레임 아님)
            if (farm != null && farm.State != null)
            {
                d.OpenCells = farm.State.Cells.Count;
                d.CropCells = farm.State.Cells.Count(c => c.HasCrop);
            }
        }

        private void OnDestroy()
        {
            // 플레이를 멈춘 시점까지의 시간도 남긴다
            if (hasPhase)
            {
                log.AddTime(day, phase, Time.realtimeSinceStartupAsDouble - phaseStart);
                hasPhase = false;
                Write();
            }
        }

        private void Write()
        {
            if (!record || !log.Days.Any())
                return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                string temp = filePath + ".tmp";
                File.WriteAllText(temp, log.Markdown(title), new System.Text.UTF8Encoding(false));
                if (File.Exists(filePath))
                    File.Delete(filePath);
                File.Move(temp, filePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlaytestRecorder] 기록 파일을 쓰지 못함: {e.Message}", this);
            }
        }
    }
}
