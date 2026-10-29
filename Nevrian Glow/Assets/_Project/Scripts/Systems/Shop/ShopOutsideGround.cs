using System.Collections;
using System.Collections.Generic;
using NevrianGlowFarm.Systems.Farm;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Shop
{
    /// <summary>
    /// 가게 바깥 땅 (2026-10-25 사용자: 창문 · 문 밖이 밭 쪽 배경과 같은 땅으로 보이게).
    /// 가게는 배경 지형(아트 BackGroundScene의 Terrain, 농장 둘레 500 m) 밖 x ≈ 500에 있어 바깥에 아무것도 없다 →
    /// 게임이 시작될 때 그 지형을 가게 옆으로 복제해(지형 데이터 사본 · 같은 재질 · 같은 풀) 농장이 서 있는 자리와 같은 위치에 가게가 오게 놓는다.
    /// 건물 바닥 자리의 풀은 지운다(FarmGrassClearer와 같은 방식). 아트 지형 에셋은 바꾸지 않는다. 콜라이더는 없다 — 플레이어는 입구 벽이 막는다.
    /// </summary>
    public class ShopOutsideGround : MonoBehaviour
    {
        [Header("조정값")]
        [Tooltip("풀을 지우는 건물 바닥 자리 — 가게 중심 기준 가로 × 세로 (m)")]
        [SerializeField] private Vector2 clearSize = new Vector2(13f, 11f);
        [Tooltip("복제 지형을 가게 바닥보다 내리는 높이 — 바닥과 겹쳐 깜빡이지 않게 (m)")]
        [SerializeField] private float yOffset = -0.05f;
        [Tooltip("배경 지형을 기다리는 최대 시간 (초)")]
        [SerializeField, Min(0f)] private float waitTimeout = 10f;

        [Header("디버그")]
        [Tooltip("복제한 원본 지형 이름 (읽기 전용)")]
        [SerializeField] private string sourceName;

        private Terrain terrain;

        private IEnumerator Start()
        {
            float start = Time.realtimeSinceStartup;
            Terrain source;
            while ((source = FindSource()) == null)
            {
                if (Time.realtimeSinceStartup - start > waitTimeout)
                    yield break; // 배경 씬이 없는 테스트 씬 등
                yield return null;
            }
            Build(source);
        }

        // 배경 지형 = 월드 원점(농장)을 덮는 지형
        private static Terrain FindSource()
        {
            foreach (var t in Terrain.activeTerrains)
            {
                if (t == null || t.terrainData == null)
                    continue;
                Vector3 o = t.GetPosition(), s = t.terrainData.size;
                if (0f >= o.x && 0f <= o.x + s.x && 0f >= o.z && 0f <= o.z + s.z)
                    return t;
            }
            return null;
        }

        private void Build(Terrain source)
        {
            if (terrain != null)
                return;
            sourceName = source.name;
            var copy = Instantiate(source.terrainData);
            copy.name = source.terrainData.name + " (shop outside)";

            // 농장(월드 원점)이 원본 지형 위에 있는 자리와 같은 자리에 가게(이 오브젝트의 부모 = ShopRoot)가 오게
            Transform anchor = transform.parent != null ? transform.parent : transform;
            Vector3 origin = source.GetPosition() + new Vector3(anchor.position.x, 0f, anchor.position.z);
            origin.y = source.GetPosition().y + yOffset;
            transform.SetPositionAndRotation(origin, Quaternion.identity);
            transform.localScale = Vector3.one;

            terrain = gameObject.AddComponent<Terrain>();
            terrain.terrainData = copy;
            terrain.materialTemplate = source.materialTemplate;
            terrain.drawInstanced = source.drawInstanced;
            terrain.drawTreesAndFoliage = source.drawTreesAndFoliage;
            terrain.detailObjectDistance = source.detailObjectDistance;
            terrain.detailObjectDensity = source.detailObjectDensity;
            terrain.treeDistance = source.treeDistance;
            terrain.treeBillboardDistance = source.treeBillboardDistance;
            terrain.heightmapPixelError = source.heightmapPixelError;
            terrain.basemapDistance = source.basemapDistance;
            terrain.shadowCastingMode = source.shadowCastingMode;
            terrain.reflectionProbeUsage = source.reflectionProbeUsage;
            terrain.allowAutoConnect = false;

            // 건물 바닥 자리의 풀 (사본에서만)
            var rect = new Rect(anchor.position.x - clearSize.x * 0.5f, anchor.position.z - clearSize.y * 0.5f, clearSize.x, clearSize.y);
            FarmGrassClearer.ClearDetails(terrain, new List<Rect> { rect });
        }

        private void OnDestroy()
        {
            if (terrain != null && terrain.terrainData != null)
                Destroy(terrain.terrainData);
        }
    }
}
