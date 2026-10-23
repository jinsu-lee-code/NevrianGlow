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
            gso.ApplyModifiedPropertiesWithoutUndo();

            // 바닥이 길 바깥까지 덮는지
            var ground = root.Find("Ground");
            if (ground != null && ground.TryGetComponent<Renderer>(out var groundRenderer))
            {
                var g = groundRenderer.bounds;
                foreach (var corner in new[] { At(aMin - p, cMin - p), At(aMax + p, cMax + p), At(aMin - p, cMax + p), At(aMax + p, cMin - p) })
                    if (corner.x < g.min.x || corner.x > g.max.x || corner.z < g.min.z || corner.z > g.max.z)
                    {
                        notes.Add($"경고: 길 모서리 {corner}가 바닥(Ground) 밖");
                        break;
                    }
            }

            int beds = settings.StartPlotCount * settings.CellsPerPlot / FarmLayout.CellsPerBed;
            notes.Insert(0, $"시작 {beds}밭 {aMax - aMin:0.#} × {cMax - cMin:0.#} m · 원점 {origin} · 둘레 길 {PathWidth} m · 금지 구역 {zones.Length}");
            return string.Join(" · ", notes);
        }

        // ── 부품 ──

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
            var piece = AssetDatabase.LoadAssetAtPath<GameObject>(PathPieceArt);
            if (visual.TryGetComponent<Renderer>(out var greybox))
                greybox.enabled = piece == null;
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

        private const string PathPieceArt = "Assets/Art/Environment/farm_path/pf_env_farm_path.prefab";
        private const float PathPieceWidth = 1.5f, PathPieceLength = 4f;

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
