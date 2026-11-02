#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Logic.Save;
using NevrianGlowFarm.Systems.Farm;
using UnityEngine;
using UnityEngine.VFX;

namespace NevrianGlowFarm.Capture
{
    /// <summary>
    /// 티저 장면들 (11/03): 풀에서 날아오르는 나비 → 맵 전경 → 폭포 → 숲 → 작물이 자라는 밭 → 등대 → 빛기둥. 장면마다 폴더 하나로 녹화하고 Blender에서 잇는다.
    /// 나비는 지도의 나비 떼(ButterflyFlock VFX)를 복제해 작게 줄여 쓴다.
    /// Run(이름, 녹화?) — 녹화가 아니면 실시간으로 돌리며 1초마다 시험 컷만 찍는다.
    /// </summary>
    public sealed class TeaserShots : MonoBehaviour
    {
        private const float BeamPreRoll = 3.75f;
        private const int HeroFlockCount = 4;

        public string OutRoot;
        public int Fps = 30;

        private CineCamera cine;
        private readonly List<VisualEffect> heroFlocks = new List<VisualEffect>();

        private static CineCamera.Key K(float a, float b, float c, float d, float e, float f) => new CineCamera.Key(new Vector3(a, b, c), new Vector3(d, e, f));

        public static TeaserShots Create(string outRoot)
        {
            var go = new GameObject("TeaserShots");
            DontDestroyOnLoad(go);
            var t = go.AddComponent<TeaserShots>();
            t.OutRoot = outRoot;
            t.cine = FindFirstObjectByType<CineCamera>();
            if (t.cine == null)
                t.cine = CineCamera.Create();
            return t;
        }

        /// <summary>장면 하나를 돌린다. 끝나면 outRoot/이름/done.txt.</summary>
        public void Run(string shot, bool record)
        {
            StopAllCoroutines();
            StartCoroutine(RunShot(shot, record));
        }

        private IEnumerator RunShot(string shot, bool record)
        {
            string folder = Path.Combine(OutRoot, shot);
            Directory.CreateDirectory(folder);
            string doneFile = Path.Combine(folder, "done.txt");
            if (File.Exists(doneFile))
                File.Delete(doneFile);
            CaptureRecorder rec = null;
            Coroutine preview = null;
            IEnumerator body = shot switch
            {
                "hero" => Hero(),
                "overview" => Simple(new[] { K(62f, 40f, -44f, 0f, 6f, -2f), K(40f, 44f, -60f, -2f, 6f, 0f), K(10f, 46f, -70f, -4f, 8f, 2f) }, 7f, 46f),
                "lake" => Simple(new[] { K(-16f, 2.2f, -33f, 5f, 1f, -60f), K(-5f, 2.4f, -36f, 6f, 2f, -62f), K(7f, 2.8f, -40f, 8f, 3f, -66f) }, 6f, 50f),
                "forest" => Simple(new[] { K(-22f, 1.7f, -16.5f, -45f, 5f, -35f), K(-26.5f, 2.2f, -20.5f, -47f, 5.5f, -36f), K(-31f, 3f, -24f, -50f, 6f, -37f) }, 6.5f, 52f),
                "glade" => Simple(new[] { K(-23.5f, 1.9f, 19f, -45f, 6f, 35f), K(-26.5f, 2.4f, 21f, -46f, 6.5f, 36f), K(-29.5f, 3.2f, 23.5f, -48f, 7f, 37f) }, 5.5f, 52f),
                "crops" => Crops(),
                "tower" => Tower(),
                "beam" => Beam(),
                _ => null,
            };
            if (body == null)
                yield break;
            if (shot == "beam")
            {
                // 빛기둥이 녹화 4초쯤에 솟도록 미리 틀어 둔다 (엔딩 소리의 고조와 같은 시각)
                cine.Place(new Vector3(9f, 9.8f, 73f), new Vector3(-5f, 26f, 32f), 50f);
                var goal = LighthouseGoal.Active;
                if (goal != null)
                    goal.PlayEnding();
                yield return new WaitForSeconds(BeamPreRoll);
            }
            if (shot == "hero")
            {
                // 나비가 다 태어나 날고 있을 때부터 찍는다 — 풀 밑에 숨겨 두고 미리 돌린다
                SpawnHeroFlocks(HeroFlockCount);
                yield return new WaitForSeconds(2.5f);
            }
            // 접사 장면: 공기 중 큰 입자는 초점 흐림을 먹으면 거친 덩어리로 보이고, 지도 나비 떼는 복제한 나비와 겹친다
            SetAmbient(shot != "hero" && shot != "crops", shot != "hero");
            if (record)
            {
                yield return null;
                rec = CaptureRecorder.Begin(folder, Fps, CaptureRecorder.Mode.Stepped, 95);
            }
            else
                preview = StartCoroutine(PreviewShots(folder));
            yield return body;
            if (rec != null)
            {
                rec.Stop();
                yield return null;
                yield return null;
            }
            if (preview != null)
                StopCoroutine(preview);
            ClearFlocks();
            cine.ClearFocus();
            File.WriteAllText(doneFile, "ok");
        }

        private static void SetAmbient(bool airParticles, bool flock)
        {
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r.name == "AirSpores" || r.name == "AirDust")
                    r.enabled = airParticles;
                else if (r.name.StartsWith("ButterflyFlock"))
                    r.enabled = flock;
            }
        }

        private IEnumerator PreviewShots(string folder)
        {
            for (int i = 0; ; i++)
            {
                yield return new WaitForSeconds(i == 0 ? 0.2f : 1f);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, $"p_{i:D2}.png"));
            }
        }

        private IEnumerator Simple(CineCamera.Key[] keys, float seconds, float fov)
        {
            cine.ClearFocus();
            yield return cine.Fly(keys, seconds, fov, fov, 0.08f);
        }

        // ── 나비 ──

        private void ClearFlocks()
        {
            foreach (var f in heroFlocks)
                if (f != null)
                    Destroy(f.gameObject);
            heroFlocks.Clear();
        }

        private void SpawnHeroFlocks(int count)
        {
            VisualEffect source = null;
            foreach (var v in FindObjectsByType<VisualEffect>(FindObjectsSortMode.None))
                if (v.name.StartsWith("ButterflyFlock"))
                {
                    source = v;
                    break;
                }
            for (int i = 0; i < count && source != null; i++)
            {
                var flock = Instantiate(source);
                flock.name = "HeroFlock";
                foreach (var behaviour in flock.GetComponents<MonoBehaviour>())
                    Destroy(behaviour); // 무리 중심을 옮기는 CreatureWander 빼고 여기서 움직인다
                flock.GetComponent<VFXRenderer>().enabled = true;
                flock.transform.localScale = Vector3.one * 0.45f;
                flock.transform.position = new Vector3(13.2f, -6f, 7f);
                flock.startSeed = (uint)(1000 + i * 37);
                flock.Reinit();
                heroFlocks.Add(flock);
            }
        }

        // 지도의 나비 떼를 복제해 풀 밑에 숨겨 두었다가 한 무리씩 올린다. 올라간 뒤에는 카메라가 보는 곳을 따라 등대 쪽으로 날아간다.
        private IEnumerator Hero()
        {
            const float seconds = 13.5f;
            var keys = new[]
            {
                // 풀 속 낮은 곳에서 올려다본다 — 앞쪽 풀잎 사이로 나비가 올라오는 구도
                K(12.9f, 0.16f, 6.1f, 13.3f, 0.4f, 7.6f),
                K(12.92f, 0.17f, 6.12f, 13.32f, 0.46f, 7.65f),
                K(12.95f, 0.3f, 6.15f, 13.5f, 1.2f, 7.9f),
                K(13.0f, 1.2f, 6.0f, 14.0f, 3.2f, 8.8f),
                K(13.0f, 3.4f, 5.4f, 13.2f, 6f, 9.8f),
                K(12.5f, 8f, 4f, 8f, 10.5f, 16f),
                K(10.5f, 13f, -2f, 0f, 14f, 26f),
                K(8f, 19f, -10f, -5f, 16.5f, 32f),
            };
            float[] start = { 0f, 0.6f, 1.3f, 2f };
            float[] lag = { 0f, 0.035f, 0.07f, 0.1f };
            Vector3[] offset = { new Vector3(0f, 0f, 0.9f), new Vector3(-0.5f, 0f, 1.2f), new Vector3(0.55f, 0f, 1f), new Vector3(0.1f, 0f, 1.5f) };
            for (float time = 0f; time <= seconds; time += Time.deltaTime)
            {
                float x = Mathf.Clamp01(time / seconds);
                // 처음엔 거의 멈춰 풀을 보다가 나비를 따라 올라간다
                float cx = Mathf.Pow(x, 1.25f);
                CineCamera.Sample(keys, cx, out var p, out var l);
                cine.Place(p, l, Mathf.Lerp(36f, 52f, Mathf.SmoothStep(0.1f, 0.85f, x)));
                float blur = 1f - Mathf.SmoothStep(0.25f, 0.5f, x);
                if (blur > 0.01f)
                    cine.SetFocus(Mathf.Lerp(40f, 4.5f, blur), Mathf.Lerp(160f, 14f, blur), 1.2f);
                else
                    cine.ClearFocus();
                for (int i = 0; i < heroFlocks.Count; i++)
                {
                    CineCamera.Sample(keys, cx - lag[i], out _, out var at);
                    float rise = 1f - Mathf.SmoothStep(0f, 1f, (time - start[i]) / 1.8f);
                    heroFlocks[i].transform.position = at + offset[i] + Vector3.down * (0.2f + rise * 1f);
                    heroFlocks[i].transform.localScale = Vector3.one * Mathf.Lerp(0.45f, 1.1f, Mathf.SmoothStep(0.45f, 1f, x));
                }
                yield return null;
            }
        }

        // ── 자라는 작물 ──

        private IEnumerator Crops()
        {
            const float seconds = 12f;
            cine.ClearFocus();
            var grid = FindFirstObjectByType<FarmGrid>();
            // 밭 안 고랑에서 낮게 시작 → 끝에서 올라가 밭 전체와 뒤의 등대를 함께 본다
            var keys = new[] { K(-2.5f, 0.55f, -9.7f, -2.5f, 0.8f, 2f), K(-2.5f, 0.6f, -8.4f, -2.5f, 0.85f, 3f), K(-2.5f, 0.75f, -7f, -2.5f, 1f, 4f), K(-2.5f, 1.6f, -6.6f, -2.5f, 1.4f, 5f), K(-2.5f, 4.2f, -8f, -2.8f, 2f, 6f), K(-2.8f, 7.5f, -10.5f, -3.2f, 3.2f, 8f) };

            var snap = new SaveSnapshot();
            grid.CaptureTo(snap);
            int n = snap.Cells.Count;
            // 앞 구획은 월광 · 버섯꽃, 뒤 구획은 등불 열매 — 카메라에서 먼 쪽이 나중에 자란다
            var crop = new string[n];
            for (int i = 0; i < n; i++)
            {
                crop[i] = i < 16 ? "crop_lanternfruit" : (i / 4) % 2 == 0 ? "crop_moonroot" : "crop_mushroomflower";
                var c = snap.Cells[i];
                c.TillProgress = 1f;
                c.IsTilled = true;
                c.CropId = null;
                c.Age = 0;
                c.WetDaysLeft = 0;
                c.HarvestCount = 0;
                c.FertilizerBonus = 0;
                c.PendingFertilizer = 0;
                snap.Cells[i] = c;
            }
            var cellViews = grid.GetComponentsInChildren<FarmCellView>(true);
            var viewOf = new FarmCellView[n];
            Vector3 origin = new Vector3(-2.5f, 0f, -9.7f);
            var distance = new float[n];
            foreach (var v in cellViews)
                if (v.Index < n)
                {
                    viewOf[v.Index] = v;
                    distance[v.Index] = Vector3.Distance(v.transform.position, origin);
                }
            float maxDist = 0f;
            foreach (float d in distance)
                maxDist = Mathf.Max(maxDist, d);
            grid.LoadFrom(snap);

            // 파도 시각: 심기 → 자람 4번
            float[] waves = { 0.1f, 1.4f, 3.0f, 4.8f, 6.7f };
            const float spread = 1.5f;
            var done = new int[n];
            var rnd = new System.Random(3);
            var jitter = new float[n];
            for (int i = 0; i < n; i++)
                jitter[i] = (float)rnd.NextDouble() * 0.25f;
            // 단계가 오를 때 모델이 작았다가 튀어 오르는 연출 (게임의 비료 성장과 같은 이징) — 새로고침 뒤에 같은 프레임에서 크기를 덮어써 깜빡임이 없다
            var shownField = typeof(FarmCellView).GetField("shownModel", BindingFlags.NonPublic | BindingFlags.Instance);
            var popStart = new float[n];
            var baseScale = new Vector3[n];
            var bumped = new bool[n];
            for (int i = 0; i < n; i++)
                popStart[i] = -10f;
            for (float time = 0f; time <= seconds; time += Time.deltaTime)
            {
                float x = Mathf.Clamp01(time / seconds);
                CineCamera.Sample(keys, x, out var p, out var l);
                cine.Place(p, l, Mathf.Lerp(50f, 54f, x));
                bool changed = false;
                for (int i = 0; i < n; i++)
                {
                    bumped[i] = false;
                    while (done[i] < waves.Length && time >= waves[done[i]] + distance[i] / maxDist * spread + jitter[i])
                    {
                        var c = snap.Cells[i];
                        if (done[i] == 0)
                            c.CropId = crop[i];
                        else
                            c.Age = done[i];
                        snap.Cells[i] = c;
                        done[i]++;
                        bumped[i] = true;
                        changed = true;
                    }
                }
                if (changed)
                    grid.LoadFrom(snap);
                for (int i = 0; i < n; i++)
                {
                    var v = viewOf[i];
                    if (v == null)
                        continue;
                    var model = shownField.GetValue(v) as GameObject;
                    if (changed && model != null)
                        baseScale[i] = model.transform.localScale;
                    if (bumped[i])
                    {
                        popStart[i] = time;
                        if (done[i] == 1)
                            v.Planted();
                        else
                            VfxManager.Play(VfxCues.FertilizerGrow, v.transform.position + Vector3.up * 0.1f);
                    }
                    float age = time - popStart[i];
                    if (model != null && age < 0.8f)
                    {
                        float k = age / 0.8f - 1f;
                        float ease = 1f + 2.7f * k * k * k + 1.7f * k * k;
                        model.transform.localScale = baseScale[i] * Mathf.LerpUnclamped(0.55f, 1f, ease);
                    }
                    else if (model != null && changed)
                        model.transform.localScale = baseScale[i];
                }
                yield return null;
            }
        }

        // ── 등대 ──

        // 낮 하늘에서 별 하늘 쪽으로 돌아간다 (나무에 부딪히기 전에 끊는다)
        private IEnumerator Tower()
        {
            var keys = new[] { K(-34f, 4f, 8f, -5f, 20f, 32f), K(-33.5f, 4.8f, 22f, -5f, 21.5f, 32f), K(-32f, 5.5f, 34f, -5f, 23f, 32f), K(-28f, 6.3f, 46f, -5f, 24.5f, 32f) };
            cine.ClearFocus();
            yield return cine.Fly(keys, 9f, 50f, 50f, 0.1f);
        }

        // 별 하늘 아래에서 올려다보는 빛기둥. 앞서 RunShot이 PlayEnding을 틀어 둔다.
        private IEnumerator Beam()
        {
            var keys = new[] { K(9f, 9.8f, 73f, -5f, 26f, 32f), K(15f, 10.8f, 71f, -5f, 29f, 32f), K(22f, 12f, 68f, -5f, 32f, 32f) };
            cine.ClearFocus();
            yield return cine.Fly(keys, 10f, 50f, 54f, 0.05f);
        }
    }
}
#endif
