using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Farm
{
    /// <summary>
    /// 배경 지형(아트 BackGroundScene의 Terrain) 풀이 밭 · 길 위로 솟지 않게, 게임이 시작될 때 밭 칸 · 길(금지 구역) 자리의 풀을 지운다.
    /// 아트 지형 데이터(파트 폴더 에셋)는 바꾸지 않는다 — 실행 중에 지형 데이터 사본을 만들어 그 사본에서만 지운다(에디터 플레이에서도 원본 그대로).
    /// 배경 씬은 Additive로 늦게 올라올 수 있어 지형이 나타날 때까지 기다린다. 밭 구획이 늘면 Refresh.
    /// </summary>
    public class FarmGrassClearer : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("밭 (칸 자리 · 금지 구역 목록)")]
        [SerializeField] private FarmGrid grid;

        [Header("조정값")]
        [Tooltip("칸 하나에서 풀을 지우는 정사각형 한 변 — 칸 간격과 같게 하면 밭 테두리까지 덮음 (m)")]
        [SerializeField, Min(0.1f)] private float cellArea = 2f;
        [Tooltip("지우는 구역 바깥 여유 — 풀잎이 기울어 들어오지 않게 (m)")]
        [SerializeField, Min(0f)] private float margin = 0.3f;
        [Tooltip("지형을 기다리는 최대 시간 (초)")]
        [SerializeField, Min(0f)] private float waitTimeout = 10f;

        [Header("디버그")]
        [Tooltip("지운 풀 수 (읽기 전용)")]
        [SerializeField] private int clearedInstances;

        private Terrain terrain;
        private bool cloned;

        private void Reset() => grid = GetComponent<FarmGrid>();

        // 칸이 생길 때(새 게임 · 불러오기 · 구획 확장) FarmGrid가 Refresh를 부른다. 그때 배경 씬이 아직이면 여기서 기다렸다 지운다.
        private IEnumerator Start()
        {
            float start = Time.realtimeSinceStartup;
            while (FindTerrain() == null)
            {
                if (Time.realtimeSinceStartup - start > waitTimeout)
                    yield break; // 배경 씬이 없는 테스트 씬 등
                yield return null;
            }
            if (grid != null && grid.CellCount > 0)
                Refresh();
        }

        /// <summary>칸을 다 만든 뒤 지우게 한 프레임 미룬다 (FarmGrid가 칸을 만드는 도중에 부른다).</summary>
        public void RefreshNextFrame()
        {
            if (isActiveAndEnabled)
                StartCoroutine(RefreshLater());
        }

        private IEnumerator RefreshLater()
        {
            yield return null;
            Refresh();
        }

        /// <summary>지금 밭 칸 · 금지 구역 자리의 풀을 지운다 (여러 번 불러도 됨 — 지운 자리는 0).</summary>
        public void Refresh()
        {
            if (FindTerrain() == null || grid == null)
                return;
            var rects = new List<Rect>();
            float half = cellArea * 0.5f + margin;
            for (int i = 0; i < grid.CellCount; i++)
            {
                Vector3 c = grid.CellPosition(i);
                rects.Add(new Rect(c.x - half, c.z - half, half * 2f, half * 2f));
            }
            foreach (var zone in grid.ForbiddenZones)
            {
                if (zone == null)
                    continue;
                var r = zone.FloorRect;
                rects.Add(new Rect(r.xMin - margin, r.yMin - margin, r.width + margin * 2f, r.height + margin * 2f));
            }
            // 열린 확장 밭(흩어진 밭, 수정사항 10/29)은 울타리 안 둘레 길까지
            rects.AddRange(grid.OpenSiteGroundRects());
            if (rects.Count == 0)
                return;

            // 원본 에셋을 건드리지 않게 처음 한 번 사본으로 바꾼다 (지형 · 충돌 둘 다)
            if (!cloned)
            {
                var copy = Instantiate(terrain.terrainData);
                copy.name = terrain.terrainData.name + " (farm)";
                terrain.terrainData = copy;
                if (terrain.TryGetComponent<TerrainCollider>(out var collider))
                    collider.terrainData = copy;
                cloned = true;
            }
            clearedInstances += ClearDetails(terrain, rects);
        }

        /// <summary>지형의 풀(디테일)을 월드 x · z 사각형 안에서 지운다. 지운 포기 수. 사본 지형에만 쓸 것 (가게 바깥 땅도 쓴다).</summary>
        internal static int ClearDetails(Terrain terrain, List<Rect> rects)
        {
            var td = terrain.terrainData;
            int res = td.detailResolution;
            int layers = td.detailPrototypes.Length;
            if (res <= 0 || layers == 0)
                return 0;
            Vector3 origin = terrain.GetPosition();
            float cell = td.size.x / res;

            // 모든 사각형을 감싸는 디테일 칸 범위만 읽고 쓴다
            var all = rects[0];
            foreach (var r in rects)
                all = Rect.MinMaxRect(Mathf.Min(all.xMin, r.xMin), Mathf.Min(all.yMin, r.yMin), Mathf.Max(all.xMax, r.xMax), Mathf.Max(all.yMax, r.yMax));
            int x0 = Mathf.Clamp(Mathf.FloorToInt((all.xMin - origin.x) / cell), 0, res);
            int y0 = Mathf.Clamp(Mathf.FloorToInt((all.yMin - origin.z) / cell), 0, res);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((all.xMax - origin.x) / cell), 0, res);
            int y1 = Mathf.Clamp(Mathf.CeilToInt((all.yMax - origin.z) / cell), 0, res);
            int w = x1 - x0, h = y1 - y0;
            if (w <= 0 || h <= 0)
                return 0;

            int removed = 0;
            for (int p = 0; p < layers; p++)
            {
                int[,] d = td.GetDetailLayer(x0, y0, w, h, p); // [y, x]
                bool changed = false;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (d[y, x] == 0)
                        continue;
                    var point = new Vector2(origin.x + (x0 + x + 0.5f) * cell, origin.z + (y0 + y + 0.5f) * cell);
                    foreach (var r in rects)
                        if (r.Contains(point))
                        {
                            removed += d[y, x];
                            d[y, x] = 0;
                            changed = true;
                            break;
                        }
                }
                if (changed)
                    td.SetDetailLayer(x0, y0, p, d);
            }
            return removed;
        }

        // 밭이 있는 지형 (배경 씬이 Additive로 올라온 뒤)
        private Terrain FindTerrain()
        {
            if (terrain != null)
                return terrain;
            if (grid == null || grid.CellCount == 0)
                return null;
            Vector3 p = grid.CellPosition(0);
            foreach (var t in Terrain.activeTerrains)
            {
                if (t == null || t.terrainData == null)
                    continue;
                Vector3 o = t.GetPosition(), s = t.terrainData.size;
                if (p.x >= o.x && p.x <= o.x + s.x && p.z >= o.z && p.z <= o.z + s.z)
                    return terrain = t;
            }
            return null;
        }
    }
}
