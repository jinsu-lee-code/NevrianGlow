using System;
using System.Globalization;
using System.IO;
using System.Text;
using NevrianGlowFarm.Logic.Logging;
using UnityEngine;

namespace NevrianGlowFarm.Core
{
    /// <summary>
    /// 콘솔의 Error · Exception · Assert · Warning을 세션 로그 파일로 모은다 (노션 「버그 트래킹」 2번).
    /// 묶기 · 형식은 Logic의 LogAggregator · LogLineFormatter가 맡고, 여기서는 수신과 파일 쓰기만 한다.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class LogCollector : MonoBehaviour
    {
        private const string LogFolderName = "Logs";

        [Header("조정값")]
        [Tooltip("횟수만 늘어난 경우 파일을 다시 쓰는 최소 간격 (초)")]
        [SerializeField, Min(0.1f)] private float flushInterval = 1f;
        [Tooltip("세션 파일에 남기는 서로 다른 로그 최대 개수 (개)")]
        [SerializeField, Min(1)] private int maxEntries = 500;

        [Header("디버그")]
        [Tooltip("이번 세션 로그 파일 경로 (첫 로그가 들어오면 파일이 생긴다)")]
        [SerializeField] private string sessionFilePath;

        private static LogCollector instance;

        [ThreadStatic] private static bool isHandling;

        private readonly object gate = new object();
        private LogAggregator aggregator;
        private string header;
        private string buildVersion;
        private int day;
        private string phase;
        private string zone;
        private bool dirty;
        private float nextFlushTime;

        /// <summary>실행 중인 수집기. Core 씬이 없으면 null.</summary>
        public static LogCollector Instance => instance;

        /// <summary>이번 세션 로그 파일 경로.</summary>
        public string SessionFilePath => sessionFilePath;

        /// <summary>세션 로그 폴더. 메인 스레드에서만 호출한다.</summary>
        public static string GetLogFolder() => Path.Combine(Application.persistentDataPath, LogFolderName);

        /// <summary>일차 · 단계가 바뀔 때 호출한다 (1-3 GameManager 연결 예정).</summary>
        public void SetDayAndPhase(int newDay, string newPhase)
        {
            lock (gate)
            {
                day = newDay;
                phase = newPhase;
            }
        }

        /// <summary>구역이 바뀔 때 호출한다 (2-3 ZoneTransition 연결 예정).</summary>
        public void SetZone(string newZone)
        {
            lock (gate)
                zone = newZone;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogError("[LogCollector] 수집기가 두 개 있음 — Core 씬에 하나만 둔다.", this);
                enabled = false;
                return;
            }
            instance = this;

            var now = DateTime.Now;
            buildVersion = Application.version;
            aggregator = new LogAggregator(maxEntries);
            sessionFilePath = Path.Combine(GetLogFolder(),
                "session_" + now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".log");
            header = $"{Application.productName} 세션 로그 · 시작 {now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}"
                   + $" · v{buildVersion} · Unity {Application.unityVersion} · {Application.platform}";

            Application.logMessageReceivedThreaded += OnLogReceived;
        }

        private void Update()
        {
            if (!dirty || Time.unscaledTime < nextFlushTime)
                return;

            lock (gate)
                WriteFile();
            nextFlushTime = Time.unscaledTime + flushInterval;
        }

        private void OnDestroy()
        {
            if (instance != this)
                return;

            Application.logMessageReceivedThreaded -= OnLogReceived;
            lock (gate)
            {
                if (dirty)
                    WriteFile();
            }
            instance = null;
        }

        private void OnLogReceived(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Log || isHandling)
                return;

            isHandling = true;
            try
            {
                lock (gate)
                {
                    var context = new LogContext(day, phase, zone, buildVersion);
                    var result = aggregator.Record(ToSeverity(type), message, stackTrace, DateTime.Now, context);

                    // 새 로그는 바로 쓴다 (직후 크래시에도 남도록). 횟수만 늘면 Update에서 모아 쓴다.
                    if (result == LogRecordResult.New)
                        WriteFile();
                    else
                        dirty = true;
                }
            }
            finally
            {
                isHandling = false;
            }
        }

        /// <summary>임시 파일에 쓴 뒤 교체한다. gate를 잡은 상태에서만 호출한다.</summary>
        private void WriteFile()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(sessionFilePath));
                string tempPath = sessionFilePath + ".tmp";
                File.WriteAllText(tempPath, LogLineFormatter.FormatFile(header, aggregator), new UTF8Encoding(false));

                if (File.Exists(sessionFilePath))
                    File.Replace(tempPath, sessionFilePath, null);
                else
                    File.Move(tempPath, sessionFilePath);

                dirty = false;
            }
            catch (Exception)
            {
                // 여기서 로그를 남기면 이 수집기가 다시 받아 반복되므로 조용히 넘기고 다음 기회에 다시 쓴다.
                dirty = true;
            }
        }

        private static LogSeverity ToSeverity(LogType type)
        {
            switch (type)
            {
                case LogType.Assert: return LogSeverity.Assert;
                case LogType.Error: return LogSeverity.Error;
                case LogType.Exception: return LogSeverity.Exception;
                default: return LogSeverity.Warning;
            }
        }
    }
}
