#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Farm;
using NevrianGlowFarm.Systems.Farm;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NevrianGlowFarm.Editor
{
    /// <summary>
    /// 농장 배치 (TASKS P-5, 레벨배치 설정안 1절 · 최종 기획서 9장). Farm 씬 FarmRoot 아래를 놓는다.
    /// 밭 4 × 4 m(2×2칸)가 빈틈 없이 붙은 시작 사각형(GameStartSettings 시작 구획 = 4 × 2밭)을 가게로 가는 문 앞 가운데에 두고,
    /// 둘레 1.5 m 길 + 문에서 길까지 들어오는 길, 문 옆 창고 · 출발 지점. 길은 금지 구역(칸이 겹치면 FarmGrid가 경고)이기도 하다.
    /// 밭 간격 · 구획 수는 데이터(GameStartSettings)에서 읽는다 — 데이터가 바뀌면 이 메뉴만 다시 실행.
    /// 확장 배열(4 × 3 → … → 8 × 8밭)은 P1. 여러 번 실행해도 결과가 같다 (이름으로 찾아 위치만 다시 놓음).
    /// </summary>
    public static class FarmLayoutSetup
    {
        private const string FarmScenePath = "Assets/_Project/Scenes/Farm.unity";
        private const string GameStartPath = "Assets/_Project/Data/Stats/GameStartSettings.asset";
        private const string PathMaterialPath = "Assets/_Project/Art/Materials/mat_greybox_floor_shop.mat";

        private const float PathWidth = 1.5f;       // 밭 둘레 길 (설정안 1절)
        private const float DoorClearance = 3f;     // 문 → 둘레 길 바깥 가장자리 (출발 지점 · 창고가 들어가는 앞마당, m)
        private const float ApproachWidth = 2f;     // 문에서 둘레 길까지 들어오는 길 폭 (m)
        private const float SpawnFromDoor = 2f;     // 출발 지점 = 문 앞 (m)
        private const float StorageFromDoor = 1f, StorageSide = 2.5f; // 창고 = 문 옆 (m)
        private const float GroundMargin = 4f;      // 걷는 바닥이 울타리 바깥으로 더 덮는 여유 (m)

        [MenuItem("Glow Farm/Setup/Build Farm Layout")]
        private static void BuildMenu()
        {
            var scene = EditorSceneManager.GetSceneByPath(FarmScenePath);
            bool opened = false;
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(FarmScenePath, OpenSceneMode.Additive);
                opened = true;
            }
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "FarmRoot");
            var settings = AssetDatabase.LoadAssetAtPath<GameStartSettingsData>(GameStartPath);
            if (root == null || settings == null)
            {
                Debug.LogError("[Setup] Farm 씬의 FarmRoot 또는 GameStartSettings가 없음");
                return;
            }
            string log = Build(root.transform, settings);
            if (log == null)
                return;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
            Debug.Log("[Setup] 농장 배치: " + log);
        }

        private static string Build(Transform root, GameStartSettingsData settings)
        {
            var door = root.Find("ZoneDoor_ToShop");
            var gridT = root.Find("FarmGrid");
            if (door == null || gridT == null || !gridT.TryGetComponent<FarmGrid>(out var grid))
            {
                Debug.LogError("[Setup] FarmRoot에 ZoneDoor_ToShop · FarmGrid가 없음");
                return null;
            }

            // 방향: 안쪽 = 문이 바라보는 쪽(문에서 농장으로), along = 구획이 이어지는 방향(문 벽을 따라), across = 줄 방향(FarmGrid와 같은 식)
            Vector3 inward = Flat(door.forward);
            Vector3 along = settings.PlotExpandDirection.sqrMagnitude > 0f ? settings.PlotExpandDirection.normalized : Vector3.left;
            Vector3 across = Vector3.Cross(Vector3.up, along).normalized;
            if (Vector3.Dot(across, inward) < 0.99f)
            {
                Debug.LogError($"[Setup] 밭 줄 방향 {across}이 문 안쪽 {inward}과 다름 — GameStartSettings 확장 방향(plotExpandDirection)을 문 벽과 나란하게");
                return null;
            }

            // 시작 밭 사각형 (칸 중심 범위 ± 칸 반) → 가까운 가장자리가 문에서 앞마당 + 길만큼, along 가운데가 문 정면
            float half = settings.CellPitch * 0.5f;
            var b = FarmLayout.AreaCenterBounds(settings.StartPlotCount, settings.CellsPerPlot, settings.CellPitch, settings.BedPitch, settings.PlotPitch);
            float aMin = (float)b.AlongMin - half, aMax = (float)b.AlongMax + half;
            float cMin = (float)b.AcrossMin - half, cMax = (float)b.AcrossMax + half;
            float aMid = (aMin + aMax) * 0.5f;
            Vector3 doorPos = new Vector3(door.position.x, gridT.position.y, door.position.z);
            Vector3 origin = doorPos + inward * (DoorClearance + PathWidth - cMin) - along * aMid;
            gridT.position = origin;
            gridT.rotation = Quaternion.identity;

            // 밭 원점 기준 (along, across) → 월드
            Vector3 At(float a, float c) => origin + along * a + across * c;

            // 문 옆 출발 지점 · 창고
            Place(root, "FarmSpawn", doorPos + inward * SpawnFromDoor, inward);
            // 창고 선반 앞면(+Z)이 문 앞 길 쪽을 보게 (아트 창고 선반, 2026-10-22)
            Place(root, "StorageAccessPoint_Farm", doorPos + inward * StorageFromDoor - along * StorageSide, along);

            // 길 (둘레 4줄 + 문에서 들어오는 길) — 회색 바닥 + 금지 구역
            var notes = new List<string>();
            var oldPath = root.Find("ForbiddenZone_Path");
            if (oldPath != null)
            {
                Undo.DestroyObjectImmediate(oldPath.gameObject);
                notes.Add("예전 가운데 길 지움");
            }
            var paths = Child(root, "FarmPaths");
            var material = AssetDatabase.LoadAssetAtPath<Material>(PathMaterialPath);
            float p = PathWidth;
            float cDoor = cMin - PathWidth - DoorClearance;
            Path(paths, "Path_Near", At(aMid, cMin - p * 0.5f), along, across, aMax - aMin + 2f * p, p, material);
            Path(paths, "Path_Far", At(aMid, cMax + p * 0.5f), along, across, aMax - aMin + 2f * p, p, material);
            Path(paths, "Path_SideA", At(aMin - p * 0.5f, (cMin + cMax) * 0.5f), along, across, p, cMax - cMin, material);
            Path(paths, "Path_SideB", At(aMax + p * 0.5f, (cMin + cMax) * 0.5f), along, across, p, cMax - cMin, material);
            Path(paths, "Path_FromDoor", At(aMid, (cDoor + cMin - p) * 0.5f), along, across, ApproachWidth, cMin - p - cDoor, material);

            var zones = root.GetComponentsInChildren<ForbiddenZone>(true);
            var gso = new SerializedObject(grid);
            var list = gso.FindProperty("forbiddenZones");
            list.ClearArray();
            for (int i = 0; i < zones.Length; i++)
            {
                list.InsertArrayElementAtIndex(i);
                list.GetArrayElementAtIndex(i).objectReferenceValue = zones[i];
            }
            // 칸 흙 크기 · 겹침 검사 크기는 칸 간격에 맞춘다 (칸 프리팹 기준 간격 2 m — 2026-10-25 밭 키우기)
            gso.FindProperty("cellFootprint").floatValue = settings.CellPitch * 0.8f;
            gso.ApplyModifiedPropertiesWithoutUndo();

            // 배경 지형 풀이 밭 · 길 위로 솟지 않게 (실행 중 지형 사본에서 지움 — 아트 지형 에셋은 그대로, 2026-10-23)
            if (!gridT.TryGetComponent<FarmGrassClearer>(out var grass))
                grass = gridT.gameObject.AddComponent<FarmGrassClearer>();
            var grassSo = new SerializedObject(grass);
            grassSo.FindProperty("grid").objectReferenceValue = grid;
            grassSo.FindProperty("cellArea").floatValue = settings.CellPitch; // 칸 간격만큼 풀을 지운다
            grassSo.ApplyModifiedPropertiesWithoutUndo();

            // 다 자란 작물의 빛기둥 재질 → 칸 프리팹 (시선 유도 1안, 2026-10-25)
            var cellPrefab = gso.FindProperty("cellPrefab").objectReferenceValue as FarmCellView;
            if (cellPrefab != null)
            {
                var beam = EnsureBeamMaterial();
                var cso = new SerializedObject(cellPrefab);
                if (cso.FindProperty("beamMaterial").objectReferenceValue != beam)
                {
                    cso.FindProperty("beamMaterial").objectReferenceValue = beam;
                    cso.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(cellPrefab);
                    AssetDatabase.SaveAssets();
                    notes.Add("칸 프리팹 빛기둥 재질 연결");
                }
            }

            // 울타리 (아트 fence 10/23: 기둥 · 가로대 2종 — 사용자 결정 2026-10-25: 밭 기준 둘레로 플레이어 이동 범위 제한)
            // 둘레 길 바깥 여유만큼 떨어진 사각형, 문 쪽(앞마당 끝 = 문 줄)은 문 자리만 비운다. 각 줄에 보이지 않는 상자 콜라이더
            float fa0 = aMin - p - FenceMargin, fa1 = aMax + p + FenceMargin, fc0 = cDoor, fc1 = cMax + p + FenceMargin;
            float gapHalf = FenceDoorGap * 0.5f;
            var fence = Child(root, "Fence");
            int pieces = 0;
            pieces += FenceLine(fence, "Fence_SideA", At(fa0, fc0), At(fa0, fc1), skipLast: true);
            // 먼 쪽 가운데 출입구 — 맵 곳곳의 확장 밭으로 걸어 나간다 (수정사항 10/29, Build Farm Field Sites)
            var oldFar = fence.Find("Fence_Far");
            if (oldFar != null)
                Object.DestroyImmediate(oldFar.gameObject);
            float gateHalf = FieldGateGap * 0.5f;
            pieces += FenceLine(fence, "Fence_FarA", At(fa0, fc1), At(aMid - gateHalf, fc1), skipLast: false);
            pieces += FenceLine(fence, "Fence_FarB", At(aMid + gateHalf, fc1), At(fa1, fc1), skipLast: true);
            pieces += FenceLine(fence, "Fence_SideB", At(fa1, fc1), At(fa1, fc0), skipLast: true);
            pieces += FenceLine(fence, "Fence_NearRight", At(fa1, fc0), At(aMid + gapHalf, fc0), skipLast: false);
            pieces += FenceLine(fence, "Fence_NearLeft", At(aMid - gapHalf, fc0), At(fa0, fc0), skipLast: true);
            notes.Add(pieces > 0 ? $"울타리 {fa1 - fa0:0.#} × {fc1 - fc0:0.#} m · 조각 {pieces} · 문 틈 {FenceDoorGap} m · 먼 쪽 출입구 {FieldGateGap} m" : "울타리 모델 없음 (Assets/Art/Environment/fence)");

            // 걷는 바닥(Ground 평면 = 10 × 10 m 메시, 보이지 않는 콜라이더)이 울타리 바깥 GroundMargin까지 덮게 크기를 맞춘다 (2026-10-25: 밭이 커지며 울타리가 바닥 밖으로 나감)
            var ground = root.Find("Ground");
            if (ground != null && ground.TryGetComponent<Renderer>(out var groundRenderer))
            {
                var g = groundRenderer.bounds;
                var corners = new[] { At(fa0, fc0), At(fa1, fc1), At(fa0, fc1), At(fa1, fc0) };
                float minX = corners.Min(c => c.x) - GroundMargin, maxX = corners.Max(c => c.x) + GroundMargin;
                float minZ = corners.Min(c => c.z) - GroundMargin, maxZ = corners.Max(c => c.z) + GroundMargin;
                // 문 앞(문 바깥 쪽)도 덮는다
                minX = Mathf.Min(minX, doorPos.x - GroundMargin); maxX = Mathf.Max(maxX, doorPos.x + GroundMargin);
                minZ = Mathf.Min(minZ, doorPos.z - GroundMargin); maxZ = Mathf.Max(maxZ, doorPos.z + GroundMargin);
                if (minX < g.min.x || maxX > g.max.x || minZ < g.min.z || maxZ > g.max.z)
                {
                    Undo.RecordObject(ground, "Farm Ground");
                    ground.position = new Vector3((minX + maxX) * 0.5f, ground.position.y, (minZ + maxZ) * 0.5f);
                    ground.rotation = Quaternion.identity;
                    ground.localScale = new Vector3((maxX - minX) / 10f, 1f, (maxZ - minZ) / 10f);
                    notes.Add($"바닥을 {maxX - minX:0.#} × {maxZ - minZ:0.#} m로 늘림");
                }
            }

            int beds = settings.StartPlotCount * settings.CellsPerPlot / FarmLayout.CellsPerBed;
            notes.Insert(0, $"시작 {beds}밭 {aMax - aMin:0.#} × {cMax - cMin:0.#} m · 원점 {origin} · 둘레 길 {PathWidth} m · 금지 구역 {zones.Length}");
            return string.Join(" · ", notes);
        }

        // ── 확장 밭 자리 (수정사항 10/29: 해금되는 밭을 맵 곳곳에, 시작 밭과 같은 규격, 해금 전에는 울타리만, 걸어서 이동) ──

        // 처음 만들 때의 자리 (2026-10-30 지형 조사: 평평하고 · 나무 · 바위가 없고 · 울타리 전체가 아트 PlayAreaBounds 다각형 안(벽에서 1.3 m 이상)인 곳).
        // 이미 있으면 씬의 자리를 그대로 둔다 — 옮길 때도 울타리가 플레이 구역 벽 밖으로 나가지 않게 (처음 자리 (-27, 0) · (-41, 0)은 벽 밖이라 걸어 들어갈 수 없었음)
        private static readonly (string Name, string Label, Vector3 Position, float Yaw)[] DefaultSites =
        {
            ("FieldSite_1", "서쪽 밭", new Vector3(-23f, 0f, 0f), 270f),
            ("FieldSite_2", "남동쪽 밭", new Vector3(17f, 0f, -26f), 270f),
        };

        [MenuItem("Glow Farm/Setup/Build Farm Field Sites")]
        private static void BuildSitesMenu()
        {
            var scene = EditorSceneManager.GetSceneByPath(FarmScenePath);
            bool opened = false;
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(FarmScenePath, OpenSceneMode.Additive);
                opened = true;
            }
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "FarmRoot");
            var settings = AssetDatabase.LoadAssetAtPath<GameStartSettingsData>(GameStartPath);
            var grid = root != null ? root.GetComponentInChildren<FarmGrid>(true) : null;
            if (root == null || settings == null || grid == null)
            {
                Debug.LogError("[Setup] Farm 씬의 FarmRoot · FarmGrid 또는 GameStartSettings가 없음");
                return;
            }
            string log = BuildSites(root.transform, grid, settings);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
            Debug.Log("[Setup] 확장 밭 자리: " + log);
        }

        private static string BuildSites(Transform root, FarmGrid grid, GameStartSettingsData settings)
        {
            var parent = Child(root, "FieldSites");
            var sites = new List<FarmFieldSite>();
            var notes = new List<string>();
            foreach (var d in DefaultSites)
            {
                var t = parent.Find(d.Name);
                if (t == null)
                {
                    t = new GameObject(d.Name).transform;
                    t.SetParent(parent, false);
                    t.position = d.Position;
                    t.rotation = Quaternion.Euler(0f, d.Yaw, 0f);
                }
                if (!t.TryGetComponent<FarmFieldSite>(out var site))
                    site = t.gameObject.AddComponent<FarmFieldSite>();
                var so = new SerializedObject(site);
                so.FindProperty("settings").objectReferenceValue = settings;
                if (string.IsNullOrEmpty(so.FindProperty("displayName").stringValue) || so.FindProperty("displayName").stringValue == "새 밭")
                    so.FindProperty("displayName").stringValue = d.Label;
                so.ApplyModifiedPropertiesWithoutUndo();
                int pieces = SiteFence(site, settings);
                sites.Add(site);
                notes.Add($"{site.DisplayName} {t.position} · 구획 {site.Plots} · 울타리 조각 {pieces}");
            }

            var gso = new SerializedObject(grid);
            var list = gso.FindProperty("fieldSites");
            list.ClearArray();
            for (int i = 0; i < sites.Count; i++)
            {
                list.InsertArrayElementAtIndex(i);
                list.GetArrayElementAtIndex(i).objectReferenceValue = sites[i];
            }
            gso.ApplyModifiedPropertiesWithoutUndo();
            int room = settings.StartPlotCount + sites.Sum(s => s.Plots);
            if (settings.MaxPlotCount != room)
                notes.Add($"주의: 최대 구획(GameStartSettings {settings.MaxPlotCount})과 자리 합({room})이 다름 — shop.csv 밭 확장권 단계 · 지급 칸 확인");
            return string.Join(" / ", notes);
        }

        // 확장 밭 울타리: 밭 사각형 + 둘레 길 + 여유, 가까운 쪽(시작 밭 쪽) · 먼 쪽 가운데에 출입구 (걸어서 지나가게)
        private static int SiteFence(FarmFieldSite site, GameStartSettingsData settings)
        {
            var half = site.HalfSize(settings);
            float ha = half.x + site.PathWidth + FenceMargin, hc = half.y + site.PathWidth + FenceMargin;
            float gate = FieldGateGap * 0.5f;
            Vector3 o = site.transform.position, along = site.Along, across = site.Across;
            Vector3 At(float a, float c) => o + along * a + across * c;
            var fence = Child(site.transform, "Fence");
            int pieces = 0;
            pieces += FenceLine(fence, "Fence_SideA", At(-ha, -hc), At(-ha, hc), skipLast: true);
            pieces += FenceLine(fence, "Fence_FarA", At(-ha, hc), At(-gate, hc), skipLast: false);
            pieces += FenceLine(fence, "Fence_FarB", At(gate, hc), At(ha, hc), skipLast: true);
            pieces += FenceLine(fence, "Fence_SideB", At(ha, hc), At(ha, -hc), skipLast: true);
            pieces += FenceLine(fence, "Fence_NearB", At(ha, -hc), At(gate, -hc), skipLast: false);
            pieces += FenceLine(fence, "Fence_NearA", At(-gate, -hc), At(-ha, -hc), skipLast: true);
            return pieces;
        }

        // ── 부품 ──

        // 빛기둥 재질: URP Unlit · 투명 · 가산 (색은 칸이 작물 빛 색으로 블록에 넣는다). 있으면 그대로
        private const string BeamMaterialPath = "Assets/_Project/Art/Materials/mat_vfx_ready_beam.mat";

        private static Material EnsureBeamMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(BeamMaterialPath);
            if (material != null)
                return material;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            material = new Material(shader) { name = "mat_vfx_ready_beam" };
            // URP Unlit의 투명 · 가산 설정 (인스펙터 Surface Transparent · Blend Additive와 같은 값 · 키워드)
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", 2f);
            material.SetColor("_BaseColor", new Color(0.4f, 0.95f, 0.9f, 0.45f));
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(BeamMaterialPath));
            AssetDatabase.CreateAsset(material, BeamMaterialPath);
            return material;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.normalized;
        }

        // 길 한 줄: 얇은 회색 판(콜라이더 없음) + 같은 크기의 금지 구역(상자, 월드 x · z 크기)
        private static void Path(Transform parent, string name, Vector3 center, Vector3 along, Vector3 across, float alongSize, float acrossSize, Material material)
        {
            var t = Child(parent, name);
            t.position = center;
            t.rotation = Quaternion.identity;
            Vector3 extent = along * alongSize + across * acrossSize;
            var size = new Vector2(Mathf.Abs(extent.x), Mathf.Abs(extent.z));

            if (!t.TryGetComponent<ForbiddenZone>(out var zone))
                zone = t.gameObject.AddComponent<ForbiddenZone>();
            var so = new SerializedObject(zone);
            so.FindProperty("radius").floatValue = 0f;
            so.FindProperty("boxSize").vector2Value = size;
            so.ApplyModifiedPropertiesWithoutUndo();

            var visual = t.Find("Visual");
            if (visual == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Visual";
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(t, false);
                visual = go.transform;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
            if (material != null && visual.TryGetComponent<Renderer>(out var renderer))
                renderer.sharedMaterial = material;
            visual.localPosition = new Vector3(0f, 0.01f, 0f);
            visual.localRotation = Quaternion.identity;
            visual.localScale = new Vector3(size.x, 0.02f, size.y);

            // 아트 길 조각(1.5 × 4 m, 길이 = Z)을 긴 쪽으로 맞대어 깐다 — 길이 · 폭은 조금 늘려 꼭 맞춤 (BRANCH_ART 10/22). 있으면 회색 판은 숨김
            var tiles = t.Find("Tiles");
            if (tiles != null)
                Object.DestroyImmediate(tiles.gameObject);
            // 결정 (2026-10-23, 사용자): 길에 판을 깔지 않는다 — 배경 지형 바닥이 그대로 보이고, 풀만 FarmGrassClearer가 지워 흙길처럼 보인다
            var piece = DrawPathSurface ? AssetDatabase.LoadAssetAtPath<GameObject>(PathPieceArt) : null;
            if (visual.TryGetComponent<Renderer>(out var greybox))
                greybox.enabled = false;
            if (piece == null)
                return;
            tiles = new GameObject("Tiles").transform;
            tiles.SetParent(t, false);
            bool alongX = size.x >= size.y;
            float length = alongX ? size.x : size.y, width = alongX ? size.y : size.x;
            int count = Mathf.Max(1, Mathf.CeilToInt(length / PathPieceLength - 0.05f));
            float step = length / count;
            for (int i = 0; i < count; i++)
            {
                var tile = (GameObject)PrefabUtility.InstantiatePrefab(piece, tiles);
                tile.name = $"Tile{i + 1:00}";
                float offset = (i - (count - 1) * 0.5f) * step;
                tile.transform.localPosition = alongX ? new Vector3(offset, 0f, 0f) : new Vector3(0f, 0f, offset);
                tile.transform.localRotation = alongX ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
                tile.transform.localScale = new Vector3(width / PathPieceWidth, 1f, step / PathPieceLength);
                GameObjectUtility.SetStaticEditorFlags(tile, StaticEditorFlags.BatchingStatic);
            }
        }

        // ── 울타리 (아트 env_post 0.14 × 0.94 × 0.14 m · env_rail 1.0 × 0.17 × 0.05 m, 피벗 바닥 · 가로대는 X 방향) ──

        private const string FencePostModel = "Assets/Art/Environment/fence/Models/env_post.fbx";
        private const string FenceRailModel = "Assets/Art/Environment/fence/Models/env_rail.fbx";
        private const float FenceMargin = 0.6f;        // 둘레 길 바깥쪽 여유 (m)
        private const float FenceDoorGap = 3.6f;       // 문 자리 틈 (m, 문 · 계단 폭)
        private const float FieldGateGap = 3f;         // 밭 울타리 출입구 (시작 밭 먼 쪽 · 확장 밭 양쪽, m)
        private const float FencePostSpacing = 1f;     // 기둥 간격 = 가로대 길이 (m)
        private const float FenceColliderHeight = 1.2f, FenceColliderThickness = 0.25f;
        private static readonly float[] FenceRailHeights = { 0.26f, 0.62f }; // 가로대 2단 (바닥 기준 피벗 높이, m)

        /// <summary>from → to 한 줄: 기둥(간격마다) + 가로대 2단 + 보이지 않는 상자 콜라이더. 매번 새로 놓는다(이름으로 찾아 지움). 놓은 조각 수.</summary>
        internal static int FenceLine(Transform parent, string name, Vector3 from, Vector3 to, bool skipLast)
        {
            var old = parent.Find(name);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var post = AssetDatabase.LoadAssetAtPath<GameObject>(FencePostModel);
            var rail = AssetDatabase.LoadAssetAtPath<GameObject>(FenceRailModel);
            if (post == null || rail == null)
                return 0;

            Vector3 delta = to - from;
            delta.y = 0f;
            float length = delta.magnitude;
            if (length < 0.05f)
                return 0;
            Vector3 dir = delta / length;
            var rotation = Quaternion.FromToRotation(Vector3.right, dir); // 가로대의 X가 줄 방향
            int count = Mathf.Max(1, Mathf.RoundToInt(length / FencePostSpacing));
            float step = length / count;

            var line = new GameObject(name).transform;
            line.SetParent(parent, false);
            line.position = from;
            line.rotation = Quaternion.identity;
            int pieces = 0;
            for (int i = 0; i <= count; i++)
            {
                if (i == count && skipLast)
                    break;
                var p = (GameObject)PrefabUtility.InstantiatePrefab(post, line);
                p.name = $"Post{i + 1:00}";
                p.transform.position = from + dir * (step * i);
                p.transform.rotation = rotation;
                GameObjectUtility.SetStaticEditorFlags(p, StaticEditorFlags.BatchingStatic);
                pieces++;
            }
            for (int i = 0; i < count; i++)
                foreach (float h in FenceRailHeights)
                {
                    var r = (GameObject)PrefabUtility.InstantiatePrefab(rail, line);
                    r.name = $"Rail{i + 1:00}_{(h < FenceRailHeights[FenceRailHeights.Length - 1] ? "Low" : "High")}";
                    r.transform.position = from + dir * (step * (i + 0.5f)) + Vector3.up * h;
                    r.transform.rotation = rotation;
                    r.transform.localScale = new Vector3(step / FencePostSpacing, 1f, 1f);
                    GameObjectUtility.SetStaticEditorFlags(r, StaticEditorFlags.BatchingStatic);
                    pieces++;
                }
            // 콜라이더: 줄 전체 하나 (플레이어 CharacterController가 막힘 — 손님은 NavMesh라 무관)
            var col = new GameObject("Collider", typeof(BoxCollider));
            col.transform.SetParent(line, false);
            col.transform.position = from + dir * (length * 0.5f);
            col.transform.rotation = rotation;
            var box = col.GetComponent<BoxCollider>();
            box.center = new Vector3(0f, FenceColliderHeight * 0.5f, 0f);
            box.size = new Vector3(length, FenceColliderHeight, FenceColliderThickness);
            return pieces;
        }

        private const string PathPieceArt = "Assets/Art/Environment/farm_path/pf_env_farm_path.prefab";
        private const float PathPieceWidth = 1.5f, PathPieceLength = 4f;
        private static readonly bool DrawPathSurface = false; // 다시 길 판을 깔려면 true (아트 길 조각)

        private static void Place(Transform parent, string name, Vector3 pos, Vector3? facing)
        {
            var t = parent.Find(name);
            if (t == null)
                return;
            t.position = pos;
            if (facing.HasValue)
                t.rotation = Quaternion.LookRotation(facing.Value, Vector3.up);
        }

        private static Transform Child(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null)
                return t;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
#endif
