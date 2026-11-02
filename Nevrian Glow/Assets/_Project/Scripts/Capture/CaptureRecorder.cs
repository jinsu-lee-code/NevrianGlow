#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace NevrianGlowFarm.Capture
{
    /// <summary>
    /// 영상 촬영용 녹화기 (마감 영상 11/03). UI까지 담으려고 화면을 매 프레임 그대로 뜬다 — Unity Recorder는 URP에서 Overlay UI를 못 담는다.
    /// Stepped = Time.captureFramerate로 게임 시간을 프레임에 맞춤 (티저: 끊김 없음, unscaled 시간을 쓰는 연출은 빨라짐),
    /// Realtime = 실제 시간 그대로 · GPU 비동기 읽기 + 작업 스레드 JPG (플레이 영상: 페이드 · 소리까지 게임 그대로).
    /// 프레임은 folder/f_000000.jpg — Realtime은 시각으로 번호를 매기므로 빠진 번호가 있을 수 있다 (묶을 때 앞 프레임으로 채움).
    /// </summary>
    public sealed class CaptureRecorder : MonoBehaviour
    {
        public enum Mode { Stepped, Realtime }

        private sealed class Frame
        {
            public byte[] Pixels;
            public int Index;
        }

        private string folder;
        private int fps;
        private int quality;
        private Mode mode;
        private bool stopping;
        private RenderTexture rt;
        private int width, height;
        private int lastIndex = -1;
        private float startTime;
        private readonly BlockingCollection<Frame> queue = new BlockingCollection<Frame>();
        private readonly ConcurrentBag<byte[]> pool = new ConcurrentBag<byte[]>();
        private Thread[] workers = Array.Empty<Thread>();
        private int pendingReadbacks;
        private int written;
        private int dropped;
        private StreamWriter markers;
        private AudioTap tap;

        /// <summary>찍은 프레임 수.</summary>
        public int Written => written;
        /// <summary>대기열이 넘쳐 버린 프레임 수 (0이어야 좋다).</summary>
        public int Dropped => dropped;
        /// <summary>녹화 시작 뒤 흐른 시간 (Stepped는 게임 시간).</summary>
        public float Elapsed => mode == Mode.Stepped ? (lastIndex + 1) / (float)fps : Time.realtimeSinceStartup - startTime;
        public string Folder => folder;

        public static CaptureRecorder Begin(string folder, int fps, Mode mode, int quality = 93, bool audio = false)
        {
            Directory.CreateDirectory(folder);
            foreach (var old in Directory.GetFiles(folder))
                File.Delete(old);
            var go = new GameObject("CaptureRecorder");
            DontDestroyOnLoad(go);
            var r = go.AddComponent<CaptureRecorder>();
            r.folder = folder;
            r.fps = fps;
            r.mode = mode;
            r.quality = quality;
            r.width = Screen.width;
            r.height = Screen.height;
            r.startTime = Time.realtimeSinceStartup;
            r.markers = new StreamWriter(Path.Combine(folder, "markers.txt"), false);
            if (mode == Mode.Stepped)
                Time.captureFramerate = fps;
            else
                r.StartWorkers();
            if (audio)
            {
                var listener = FindFirstObjectByType<AudioListener>();
                if (listener != null)
                {
                    r.tap = listener.gameObject.AddComponent<AudioTap>();
                    r.tap.Begin(Path.Combine(folder, "audio.wav"));
                }
            }
            r.StartCoroutine(r.Loop());
            return r;
        }

        /// <summary>지금 시각을 표시해 둔다 (편집할 때 자를 자리).</summary>
        public void Mark(string text) => markers?.WriteLine($"{Elapsed:F3}\t{Mathf.RoundToInt(Elapsed * fps)}\t{text}");

        public void Stop() => stopping = true;

        private void StartWorkers()
        {
            rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            rt.Create();
            workers = new Thread[Mathf.Clamp(SystemInfo.processorCount - 2, 2, 6)];
            for (int i = 0; i < workers.Length; i++)
            {
                workers[i] = new Thread(WorkerLoop) { IsBackground = true, Name = "CaptureEncode" + i };
                workers[i].Start();
            }
        }

        private IEnumerator Loop()
        {
            var wait = new WaitForEndOfFrame();
            while (!stopping)
            {
                yield return wait;
                if (mode == Mode.Stepped)
                    StepFrame();
                else
                    RealtimeFrame();
            }
            Time.captureFramerate = 0;
            if (tap != null)
            {
                tap.End();
                Destroy(tap);
            }
            while (pendingReadbacks > 0)
                yield return null;
            queue.CompleteAdding();
            foreach (var w in workers)
                while (w.IsAlive)
                    yield return null;
            markers?.WriteLine($"# 끝 프레임 {written} 버림 {dropped} 시간 {Elapsed:F2}");
            markers?.Dispose();
            markers = null;
            File.WriteAllText(Path.Combine(folder, "done.txt"), $"{written} {dropped}");
            if (rt != null)
                rt.Release();
            Destroy(gameObject);
        }

        private void StepFrame()
        {
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            int index = ++lastIndex;
            File.WriteAllBytes(FramePath(index), tex.EncodeToJPG(quality));
            Destroy(tex);
            written++;
        }

        private void RealtimeFrame()
        {
            int index = Mathf.RoundToInt((Time.realtimeSinceStartup - startTime) * fps);
            if (index <= lastIndex)
                return;
            if (queue.Count > 90)
            {
                dropped++;
                return;
            }
            lastIndex = index;
            ScreenCapture.CaptureScreenshotIntoRenderTexture(rt);
            pendingReadbacks++;
            AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32, req =>
            {
                pendingReadbacks--;
                if (req.hasError)
                {
                    dropped++;
                    return;
                }
                NativeArray<byte> data = req.GetData<byte>();
                if (!pool.TryTake(out byte[] pixels) || pixels.Length != data.Length)
                    pixels = new byte[data.Length];
                data.CopyTo(pixels);
                queue.Add(new Frame { Pixels = pixels, Index = index });
            });
        }

        private void WorkerLoop()
        {
            int stride = width * 4;
            var row = new byte[stride];
            foreach (var f in queue.GetConsumingEnumerable())
            {
                // D3D는 화면을 위아래 뒤집어 준다
                for (int y = 0; y < height / 2; y++)
                {
                    int a = y * stride, b = (height - 1 - y) * stride;
                    Buffer.BlockCopy(f.Pixels, a, row, 0, stride);
                    Buffer.BlockCopy(f.Pixels, b, f.Pixels, a, stride);
                    Buffer.BlockCopy(row, 0, f.Pixels, b, stride);
                }
                byte[] jpg = ImageConversion.EncodeArrayToJPG(f.Pixels, GraphicsFormat.R8G8B8A8_UNorm, (uint)width, (uint)height, 0, quality);
                File.WriteAllBytes(FramePath(f.Index), jpg);
                Interlocked.Increment(ref written);
                pool.Add(f.Pixels);
            }
        }

        private string FramePath(int index) => Path.Combine(folder, $"f_{index:D6}.jpg");

        private void OnDestroy()
        {
            Time.captureFramerate = 0;
            markers?.Dispose();
        }
    }

    /// <summary>귀에 들리는 소리 그대로 WAV로 (AudioListener와 같은 오브젝트에 붙인다).</summary>
    public sealed class AudioTap : MonoBehaviour
    {
        private readonly ConcurrentQueue<float[]> blocks = new ConcurrentQueue<float[]>();
        private Thread writer;
        private volatile bool running;
        private string path;
        private int channels = 2;
        private int sampleRate;

        public void Begin(string wavPath)
        {
            path = wavPath;
            sampleRate = AudioSettings.outputSampleRate;
            channels = AudioSettings.driverCapabilities == AudioSpeakerMode.Mono ? 1 : 2;
            running = true;
            writer = new Thread(WriteLoop) { IsBackground = true, Name = "CaptureAudio" };
            writer.Start();
        }

        public void End()
        {
            running = false;
            writer?.Join(3000);
        }

        private void OnAudioFilterRead(float[] data, int ch)
        {
            if (!running)
                return;
            channels = ch;
            var copy = new float[data.Length];
            Array.Copy(data, copy, data.Length);
            blocks.Enqueue(copy);
        }

        private void WriteLoop()
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var w = new BinaryWriter(fs);
            w.Write(new byte[44]);
            long samples = 0;
            while (running || !blocks.IsEmpty)
            {
                if (!blocks.TryDequeue(out float[] b))
                {
                    Thread.Sleep(5);
                    continue;
                }
                foreach (float s in b)
                {
                    w.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
                    samples++;
                }
            }
            int bytes = (int)(samples * 2);
            fs.Seek(0, SeekOrigin.Begin);
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + bytes);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)channels);
            w.Write(sampleRate);
            w.Write(sampleRate * channels * 2);
            w.Write((short)(channels * 2));
            w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(bytes);
        }
    }
}
#endif
