#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Systems.Cleaning;
using NevrianGlowFarm.Systems.Farm;
using NevrianGlowFarm.Systems.Sales;
using NevrianGlowFarm.Systems.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NevrianGlowFarm.Editor
{
    /// <summary>
    /// 가게 배치 (TASKS P-4 → 7-9 11/02). 기획 배치도 `CSV/plan/shop_csv/shop_layout.json`을 읽어 Shop 씬 ShopRoot 아래를 놓는다.
    /// 11/02 기획: 가게 16.8 × 14 m(예전 12 × 10의 1.4배) · 과일 진열대 7자리(뒤 계단형 4 · 가운데 판매대 3, 시작 3대 + 추가 구매 4) ·
    /// 위 · 아래 통로 · 계산대(오른쪽 앞) · 꺾인 계산 줄 7칸 · 손님 입구(앞 벽 가운데) · 창고 · 쓰레기통 · 농장 문 · 출발 지점(왼쪽 앞).
    /// 배치도에 없는 것(장바구니 진열대 · 영업 팻말 · 램프)은 예전 자리 × 배율. 아트 가게 껍데기(12 × 10 m)는 벽 · 천장만 늘리고 장식은 자리만 옮긴다.
    /// 여러 번 실행해도 결과가 같다 (이름으로 찾아 위치만 다시 놓음). 끝나면 Bake Shop NavMesh를 실행할 것.
    /// 좌표: ShopRoot 로컬, x = 오른쪽, z = 뒤(벽 선반 쪽). 배치도 원점(왼쪽 위 모서리, z 아래쪽) → x = 배치도 x − 폭/2, z = 깊이/2 − 배치도 z.
    /// </summary>
    public static class ShopLayoutSetup
    {
        private const string ShopScenePath = "Assets/_Project/Scenes/Shop.unity";
        private const string LayoutJsonPath = "shop_csv/shop_layout.json";
        private const string FruitShelfPrefabPath = "Assets/_Project/Prefabs/Shop/fruit_shelf.prefab";
        private const string FruitShelfDataPath = "Assets/_Project/Data/Items/furn_fruit_shelf.asset";
        private const string FruitShelfProductPath = "Assets/_Project/Data/Shop/shop_fruit_shelf.asset";
        private const string GameStartPath = "Assets/_Project/Data/Stats/GameStartSettings.asset";

        // 벽 높이 3.6 m = 아트 가게 벽(pf_env_shop_wall, 2026-10-21) — 그레이박스 벽은 보이지 않는 콜라이더로만 남는다
        private const float WallHeight = 3.6f, WallThickness = 0.2f;
        // 아트 가게 껍데기가 만들어진 크기 (BRANCH_ART 10.21) — 배치도 크기에 맞춰 벽 · 천장을 늘린다
        private const float ArtShellWidth = 12f, ArtShellDepth = 10f;
        // 예전 12 × 10 m 배치의 자리 (배치도에 없는 것) — × 배율
        private static readonly Vector2 OldBasketStand = new Vector2(-0.9f, -3.13f);   // 아트가 앞 벽에서 띄움(BRANCH_ART 10.29)
        private static readonly Vector2 OldOpenSign = new Vector2(-1.7f, -4.5f);
        private static readonly Vector2 OldLampCounter = new Vector2(5.5f, -4.6f);
        private static readonly Vector2 OldLampShelf = new Vector2(5.6f, 3.5f);       // 진열대 램프 2개 — 양쪽 벽(수정사항 10/29)
        private static readonly Vector2 OldLampEntrance = new Vector2(-2.3f, -4.6f);
        private const float BasketPointGap = 0.6f;                                      // 장바구니 진열대 옆 손님이 서는 거리 (m)
        private const float EntranceInside = 0.4f;                                      // 손님이 생기고 사라지는 자리 — 입구 안쪽 (m)
        private const float EntranceArtOutside = 0.1f;                                  // 아트 입구 피벗 — 앞 벽 바깥 (예전 −5.1, m)
        private const float BrowseSpread = 1.6f;                                        // 예전 둘러보기 폭 — × 배율
        private const float CounterLampRange = 3f;                                      // 계산대 램프 점광 범위 — 청록이 방 전체를 덮지 않게 (BRANCH_ART 10.27)
        private const float DecorClearance = 0.2f;                                      // 진열대 자리와 겹치는 바닥 장식을 끄는 여유 (m)

        // 아트 가게 껍데기 (BRANCH_ART 10.21): 벽 · 천장 피벗 = 바닥 중앙(ShopRoot), 입구 피벗 = 통로 바닥 중앙. 콜라이더 없음
        private const string ShopWallArt = "Assets/Art/Environment/shop_wall/pf_env_shop_wall.prefab";
        private const string ShopCeilingArt = "Assets/Art/Environment/shop_ceiling/pf_env_shop_ceiling.prefab";
        private const string ShopEntranceArt = "Assets/Art/Environment/shop_entrance/pf_env_shop_entrance.prefab";

        [MenuItem("Glow Farm/Setup/Build Shop Layout")]
        private static void BuildMenu()
        {
            if (!TryReadLayout(out var layout, out string error))
            {
                Debug.LogError("[Setup] 가게 배치도를 읽지 못함: " + error);
                return;
            }
            var scene = EditorSceneManager.GetSceneByPath(ShopScenePath);
            bool opened = false;
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(ShopScenePath, OpenSceneMode.Additive);
                opened = true;
            }
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "ShopRoot");
            if (root == null)
            {
                Debug.LogError("[Setup] Shop 씬에 ShopRoot가 없음");
                return;
            }
            string product = EnsureFruitShelfProduct();
            string log = Build(root.transform, layout);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
            Debug.Log($"[Setup] 가게 배치: {log} · {product} — 다음: Import Plan CSV(진열대 상품 가격) · Bake Shop NavMesh (Shop 씬을 연 채로)");
        }

        // ── 배치도 (shop_layout.json) ──

        [Serializable]
        private class LayoutFile
        {
            [SerializeField] internal LayoutStore store;
            [SerializeField] internal LayoutItem[] items;
        }

        [Serializable]
        private class LayoutStore
        {
            [SerializeField] internal float width, depth, scale_from_12x10;
        }

        [Serializable]
        private class LayoutItem
        {
            [SerializeField] internal string id, type, label;
            [SerializeField] internal float w, d, center_x, center_z;
        }

        private static bool TryReadLayout(out LayoutFile layout, out string error)
        {
            layout = null;
            string path = Path.Combine(PlanCsvImporter.PlanRoot, LayoutJsonPath);
            if (!File.Exists(path))
            {
                error = $"{path} 없음";
                return false;
            }
            layout = JsonUtility.FromJson<LayoutFile>(File.ReadAllText(path, Encoding.UTF8));
            if (layout?.store == null || layout.items == null || layout.store.width <= 0f || layout.store.depth <= 0f)
            {
                error = "store · items가 없음";
                return false;
            }
            foreach (var type in new[] { "door", "spawn", "storage", "trash", "entry", "counter" })
                if (!layout.items.Any(i => i.type == type))
                {
                    error = $"'{type}' 항목 없음";
                    return false;
                }
            if (!layout.items.Any(i => i.type == "fruit") || !layout.items.Any(i => i.type == "queue") || layout.items.Count(i => i.type == "aisle") < 2)
            {
                error = "과일 진열대 · 계산 줄 · 통로 2줄 중 빠진 것이 있음";
                return false;
            }
            error = null;
            return true;
        }

        private static Vector3 Local(LayoutFile layout, LayoutItem item) =>
            new Vector3(item.center_x - layout.store.width * 0.5f, 0f, layout.store.depth * 0.5f - item.center_z);

        private static LayoutItem One(LayoutFile layout, string type) => layout.items.First(i => i.type == type);

        // "fruit_shelf_tiered (확장3)" → 3, "(기본 제공)" → 0
        private static int StageOf(LayoutItem item)
        {
            var m = Regex.Match(item.label ?? string.Empty, @"확장\s*(\d+)");
            return m.Success ? int.Parse(m.Groups[1].Value) : 0;
        }

        private static int TrailingNumber(string label)
        {
            var m = Regex.Match(label ?? string.Empty, @"(\d+)\s*$");
            return m.Success ? int.Parse(m.Groups[1].Value) : int.MaxValue;
        }

        private static bool IsTiered(LayoutItem item) => (item.label ?? string.Empty).IndexOf("tiered", StringComparison.OrdinalIgnoreCase) >= 0;

        // ── 배치 ──

        private static string Build(Transform root, LayoutFile layout)
        {
            var notes = new List<string>();
            float width = layout.store.width, depth = layout.store.depth;
            float scale = layout.store.scale_from_12x10 > 0f ? layout.store.scale_from_12x10 : width / ArtShellWidth;
            Material wallMaterial = root.GetComponentsInChildren<Renderer>(true).Select(r => r.sharedMaterial).FirstOrDefault(m => m != null);

            // 바닥 (Unity 평면 10 m). 예전 크기 → 장식 자리 배율
            var floor = root.Find("Floor");
            Vector2 decorFactor = Vector2.one;
            if (floor != null)
            {
                decorFactor = new Vector2(width / (floor.localScale.x * 10f), depth / (floor.localScale.z * 10f));
                floor.localScale = new Vector3(width / 10f, 1f, depth / 10f);
            }

            var entry = One(layout, "entry");
            float entranceX = Local(layout, entry).x, entranceGap = entry.w;
            BuildWalls(root, wallMaterial, width, depth, entranceX, entranceGap);
            notes.Add(BuildShell(root, width, depth, entranceX, decorFactor));

            // 진열 가구 프리팹 (그레이박스 — 아트 Variant가 없을 때)
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FruitShelfPrefabPath);
            if (prefab == null)
                return "과일 진열대 프리팹(fruit_shelf) 없음 — 중단";

            // 과일 진열대 자리: 열리는 단계 순 (같으면 뒤 줄 먼저, 왼쪽부터) — 저장 순서(이름)도 이 순서
            var fruit = layout.items.Where(i => i.type == "fruit")
                .OrderBy(StageOf).ThenByDescending(i => Local(layout, i).z).ThenBy(i => Local(layout, i).x).ToList();
            var spots = Child(root, "FurnitureSpots");
            // 예전 일반 진열대 자리(벽 쪽, 2차) · 넘치는 자리 — 기획 배치도에 없음
            foreach (Transform t in spots.Cast<Transform>().ToList())
                if (t.name.StartsWith("WallSpot_") || (t.name.StartsWith("FruitSpot_") && TrailingNumber(t.name) > fruit.Count))
                    Undo.DestroyObjectImmediate(t.gameObject);

            var shelves = new List<ShelfDisplay>();
            var spotRects = new List<Rect>();
            int startCount = 0;
            for (int i = 0; i < fruit.Count; i++)
            {
                var item = fruit[i];
                int stage = StageOf(item);
                if (stage == 0)
                    startCount++;
                Vector3 pos = Local(layout, item);
                spotRects.Add(new Rect(pos.x - item.w * 0.5f, pos.z - item.d * 0.5f, item.w, item.d));
                var spot = Spot(spots, $"FruitSpot_{i + 1}", pos, FurnitureKind.FruitShelf, new Vector2(item.w, item.d), stage);
                var existing = spot.GetComponentInChildren<ShelfDisplay>(true);
                var wanted = AssetDatabase.LoadAssetAtPath<GameObject>(IsTiered(item) ? ArtHookupSetup.FruitShelfTiered : ArtHookupSetup.FruitShelfCounter) ?? prefab;
                if (existing == null)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(wanted, spot);
                    go.name = $"FruitShelf_{i + 1}";
                    existing = go.GetComponent<ShelfDisplay>();
                }
                else if (PrefabUtility.IsAnyPrefabInstanceRoot(existing.gameObject) && PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) != wanted)
                {
                    // 이름(저장 순서) · 진열 상태는 그대로 두고 프리팹만 바꾼다
                    string keep = existing.name;
                    PrefabUtility.ReplacePrefabAssetOfPrefabInstance(existing.gameObject, wanted, InteractionMode.AutomatedAction);
                    existing = spot.GetComponentInChildren<ShelfDisplay>(true);
                    existing.name = keep;
                    notes.Add($"{keep} → {wanted.name}");
                }
                // 손님 쪽(로컬 +Z)이 앞(입구 쪽 통로)으로. 추가 구매 자리는 꺼 두고 FruitShelfSpots가 산 만큼 켠다
                existing.transform.localPosition = Vector3.zero;
                existing.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                existing.gameObject.SetActive(stage == 0);
                shelves.Add(existing);
            }
            if (!spots.TryGetComponent<FruitShelfSpots>(out _))
                spots.gameObject.AddComponent<FruitShelfSpots>();
            notes.Add($"과일 진열대 자리 {fruit.Count} (시작 {startCount} · 추가 {fruit.Count - startCount})");
            var start = AssetDatabase.LoadAssetAtPath<GameStartSettingsData>(GameStartPath);
            if (start != null && start.StartFruitShelves != startCount)
                notes.Add($"주의: GameStartSettings 시작 진열대 {start.StartFruitShelves} ≠ 배치도 기본 제공 {startCount} (측정 봇 값)");
            notes.Add(HideOverlappingDecor(root, spotRects));

            // 고정 오브젝트 (배치도)
            var storage = One(layout, "storage");
            var trash = One(layout, "trash");
            var spawn = One(layout, "spawn");
            var door = One(layout, "door");
            Place(root, "StorageAccessPoint_Shop", Local(layout, storage), 0f);
            Place(root, "TrashBin", Local(layout, trash), 0f);
            Place(root, "ShopSpawn", Local(layout, spawn), 90f);
            var counterArea = One(layout, "counter");
            Vector3 counterPos = PlaceCounter(root, Local(layout, counterArea), counterArea.w);
            Place(root, "BasketStand", Scaled(OldBasketStand, scale), 90f);
            Place(root, "OpenSign", Scaled(OldOpenSign, scale), 0f);
            Place(root, "Lamp_Counter", Scaled(OldLampCounter, scale), 0f);
            Place(root, "Lamp_ShelfLeft", Scaled(new Vector2(-OldLampShelf.x, OldLampShelf.y), scale), 90f);  // 등이 방 안쪽(+x)을 봄
            Place(root, "Lamp_ShelfRight", Scaled(OldLampShelf, scale), 270f);
            Place(root, "Lamp_Entrance", Scaled(OldLampEntrance, scale), 0f);
            SetLampLight(root, "Lamp_Counter", true, CounterLampRange);
            SetLampLight(root, "Lamp_ShelfLeft", false, 0f);
            SetLampLight(root, "Lamp_ShelfRight", false, 0f);

            // 통로 2줄: 위 = 뒤쪽(z 큰 쪽)
            var aisles = layout.items.Where(i => i.type == "aisle").OrderByDescending(i => Local(layout, i).z).ToList();
            var upperAisle = aisles[0];
            var lowerAisle = aisles[aisles.Count - 1];
            Vector3 lowerAislePos = Local(layout, lowerAisle);

            // 계산 줄 (배치도 '계산 줄 N' 번호 순)
            var queue = layout.items.Where(i => i.type == "queue").OrderBy(i => TrailingNumber(i.label)).Select(i => Local(layout, i)).ToList();
            var sales = root.Find("SalesSystem");
            if (sales != null)
            {
                Place(sales, "CustomerEntrance", new Vector3(entranceX, 0f, -depth * 0.5f + EntranceInside), 0f);
                Vector3 basket = Scaled(OldBasketStand, scale);
                Place(sales, "BasketPoint", basket + Vector3.right * BasketPointGap, 270f);
                Place(sales, "ShelfFront", lowerAislePos, 0f);
                Place(sales, "CounterPoint", counterPos, 0f);
                // 줄 첫 칸 (칸 목록이 비었을 때 쓰는 곧은 줄) — 둘째 칸 쪽으로
                Vector3 toSecond = queue.Count > 1 ? queue[1] - queue[0] : Vector3.left;
                Place(sales, "QueueStart", queue[0], Mathf.Atan2(toSecond.x, toSecond.z) * Mathf.Rad2Deg);
                var slotTransforms = new List<Transform>();
                for (int i = 0; i < queue.Count; i++)
                {
                    var slot = Child(sales, $"QueueSlot_{i + 1}");
                    slot.localPosition = queue[i];
                    slot.localRotation = Quaternion.identity;
                    slotTransforms.Add(slot);
                }
                foreach (Transform t in sales.Cast<Transform>().ToList())
                    if (t.name.StartsWith("QueueSlot_") && TrailingNumber(t.name) > queue.Count)
                        Undo.DestroyObjectImmediate(t.gameObject);

                var so = new SerializedObject(sales.GetComponent<SalesSystem>());
                SetList(so.FindProperty("shelves"), shelves);
                SetList(so.FindProperty("queueSlots"), slotTransforms);
                so.FindProperty("browseSpread").floatValue = BrowseSpread * scale;
                so.ApplyModifiedPropertiesWithoutUndo();
                notes.Add($"계산 줄 {queue.Count}칸");
            }

            // 금지 구역 (쓰레기 · 얼룩 · 흩어진 상품을 놓지 않는 곳)
            Zone(root, "ForbiddenZone_Counter", Local(layout, counterArea), new Vector2(counterArea.w, counterArea.d));
            Vector3 qMin = new Vector3(queue.Min(p => p.x), 0f, queue.Min(p => p.z)), qMax = new Vector3(queue.Max(p => p.x), 0f, queue.Max(p => p.z));
            Zone(root, "ForbiddenZone_Queue", (qMin + qMax) * 0.5f, new Vector2(qMax.x - qMin.x + 1f, qMax.z - qMin.z + 1f));
            Zone(root, "ForbiddenZone_Entrance", new Vector3(entranceX, 0f, -depth * 0.5f + 0.6f), new Vector2(entranceGap + 0.6f, 1.2f));
            Zone(root, "ForbiddenZone_TrashBin", Local(layout, trash), new Vector2(trash.w + 0.4f, trash.d + 0.4f));
            Zone(root, "ForbiddenZone_Storage", Local(layout, storage), new Vector2(storage.w, storage.d));
            Zone(root, "ForbiddenZone_ShopSpawn", Local(layout, spawn), new Vector2(1.6f, door.d));
            // 통로 2줄: 쓰레기 · 흩어진 상품은 막고 얼룩 · 발자국은 허용 (기획 회신 10/22 A2)
            Zone(root, "ForbiddenZone_ShelfFront", lowerAislePos, new Vector2(lowerAisle.w, lowerAisle.d), allowStains: true); // 아래 통로
            Zone(root, "ForbiddenZone_UpperAisle", Local(layout, upperAisle), new Vector2(upperAisle.w, upperAisle.d), allowStains: true);
            Zone(root, "ForbiddenZone_BasketStand", Scaled(OldBasketStand, scale), new Vector2(1.2f, 1.0f));
            foreach (Transform spot in spots)
            {
                var fs = spot.GetComponent<FurnitureSpot>();
                if (!spot.TryGetComponent<ForbiddenZone>(out var zone))
                    zone = spot.gameObject.AddComponent<ForbiddenZone>();
                var zso = new SerializedObject(zone);
                zso.FindProperty("boxSize").vector2Value = fs.Size;
                zso.ApplyModifiedPropertiesWithoutUndo();
            }

            var cleaningT = root.Find("CleaningSystem");
            CleaningSystem cleaning = cleaningT != null && cleaningT.TryGetComponent<CleaningSystem>(out var cs) ? cs : null;
            if (cleaning != null)
            {
                var so = new SerializedObject(cleaning);
                so.FindProperty("areaMin").vector2Value = new Vector2(-width * 0.5f + 0.4f, -depth * 0.5f + 0.4f);
                so.FindProperty("areaMax").vector2Value = new Vector2(width * 0.5f - 0.4f, depth * 0.5f - 0.4f);
                var all = root.GetComponentsInChildren<ForbiddenZone>(true);
                SetList(so.FindProperty("forbiddenZones"), all);
                so.ApplyModifiedPropertiesWithoutUndo();
                notes.Add($"금지 구역 {all.Length}");
            }
            return $"가게 {width} × {depth} m · " + string.Join(" · ", notes);
        }

        private static Vector3 Scaled(Vector2 old, float scale) => new Vector3(old.x * scale, 0f, old.y * scale);

        // 계산대: 배치도 계산대 구역 가운데 줄에, 손님 쪽(왼쪽) 가장자리를 구역 왼쪽에 맞춘다 — 뒤(오른쪽 벽 쪽)는 점원 자리
        // 수납장 방향은 그대로 — 계산기(Register)만 점원 쪽을 보게 counter 프리팹에서 돌림 (2026-10-22 사용자)
        private static Vector3 PlaceCounter(Transform root, Vector3 areaCenter, float areaWidth)
        {
            var counter = root.Find("Counter");
            if (counter == null)
                return areaCenter;
            counter.localPosition = areaCenter;
            counter.localRotation = Quaternion.Euler(0f, 90f, 0f);
            var renderers = counter.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var r in renderers)
                    bounds.Encapsulate(r.bounds);
                float minX = root.InverseTransformPoint(bounds.min).x;
                counter.localPosition += Vector3.right * (areaCenter.x - areaWidth * 0.5f - minX);
            }
            return counter.localPosition;
        }

        // 진열대 자리와 겹치는 바닥 장식(아트 ShopDecor/Floor — 예전 뒤 벽 화분)은 끈다. 아트 원본은 그대로 (씬 덮어쓰기)
        private static string HideOverlappingDecor(Transform root, List<Rect> spotRects)
        {
            var floorDecor = root.Find("Shell_Wall/ShopDecor/Floor");
            if (floorDecor == null)
                return "바닥 장식 없음";
            var hidden = new List<string>();
            foreach (Transform t in floorDecor)
            {
                Vector3 p = root.InverseTransformPoint(t.position);
                bool overlaps = spotRects.Any(r => p.x > r.xMin - DecorClearance && p.x < r.xMax + DecorClearance && p.z > r.yMin - DecorClearance && p.z < r.yMax + DecorClearance);
                if (overlaps == t.gameObject.activeSelf)
                {
                    t.gameObject.SetActive(!overlaps);
                    EditorUtility.SetDirty(t.gameObject);
                }
                if (overlaps)
                    hidden.Add(t.name);
            }
            return hidden.Count > 0 ? $"진열대 자리와 겹친 바닥 장식 {hidden.Count}개 끔 ({string.Join(", ", hidden.Distinct())})" : "겹친 바닥 장식 없음";
        }

        private static void SetList<T>(SerializedProperty list, IList<T> values) where T : Object
        {
            list.ClearArray();
            for (int i = 0; i < values.Count; i++)
            {
                list.InsertArrayElementAtIndex(i);
                list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        // 과일 진열대 추가 상품 에셋 (가격 · 단계는 Import Plan CSV)
        private static string EnsureFruitShelfProduct()
        {
            var product = AssetDatabase.LoadAssetAtPath<FruitShelfProductData>(FruitShelfProductPath);
            if (product != null)
                return "shop_fruit_shelf 있음";
            // id를 먼저 넣고 만든다 — 만들자마자 ContentDatabase 수집이 돈다
            product = ScriptableObject.CreateInstance<FruitShelfProductData>();
            var so = new SerializedObject(product);
            so.FindProperty("id").stringValue = "shop_fruit_shelf";
            so.FindProperty("displayName").stringValue = "과일 진열대 추가";
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(product, FruitShelfProductPath);
            AssetDatabase.SaveAssets();
            EditorApplication.ExecuteMenuItem("Glow Farm/Data/Refresh Content Database");
            return "shop_fruit_shelf 생성";
        }

        // ── 부품 ──

        private static void BuildWalls(Transform root, Material material, float width, float depth, float entranceX, float entranceGap)
        {
            var walls = Child(root, "Walls");
            float hw = width * 0.5f, hd = depth * 0.5f;
            Wall(walls, "Wall_Back", new Vector3(0f, 0f, hd), new Vector3(width, WallHeight, WallThickness), material);
            Wall(walls, "Wall_Right", new Vector3(hw, 0f, 0f), new Vector3(WallThickness, WallHeight, depth), material);
            // 앞 벽: 손님 입구 자리를 비운다
            float entranceLeft = entranceX - entranceGap * 0.5f, entranceRight = entranceX + entranceGap * 0.5f;
            Wall(walls, "Wall_FrontLeft", new Vector3((-hw + entranceLeft) * 0.5f, 0f, -hd), new Vector3(entranceLeft + hw, WallHeight, WallThickness), material);
            Wall(walls, "Wall_FrontRight", new Vector3((entranceRight + hw) * 0.5f, 0f, -hd), new Vector3(hw - entranceRight, WallHeight, WallThickness), material);
            // 입구 구멍은 보이지 않는 벽으로 막는다 — 플레이어가 입구를 뚫고 나가 바닥 밖으로 떨어졌음 (2026-10-25). 손님은 입구 안쪽에서 생기고 사라져 영향 없음
            Wall(walls, "Wall_Entrance", new Vector3(entranceX, 0f, -hd), new Vector3(entranceGap, WallHeight, WallThickness), material);
            // 왼쪽 벽: 아트 벽에 농장 문 구멍이 없으므로 한 장 (예전 두 조각 중 앞 조각은 지움)
            Wall(walls, "Wall_LeftBack", new Vector3(-hw, 0f, 0f), new Vector3(WallThickness, WallHeight, depth), material);
            var leftFront = walls.Find("Wall_LeftFront");
            if (leftFront != null)
                Undo.DestroyObjectImmediate(leftFront.gameObject);

            // 아트 껍데기가 있으면 그레이박스 벽은 보이지 않는 콜라이더로만
            bool art = root.Find("Shell_Wall") != null || AssetDatabase.LoadAssetAtPath<GameObject>(ShopWallArt) != null;
            foreach (Transform w in walls)
                if (w.TryGetComponent<Renderer>(out var r))
                    r.enabled = !art;
            BuildOutside(root, depth, entranceX, entranceGap);
        }

        // 가게 바깥 (2026-10-25 사용자: 창문 밖에 땅이 없고 입구가 막혀 보임 → 밭 쪽 배경과 같은 땅으로):
        // 배경 지형을 실행 중에 복제하는 ShopOutsideGround(Outside/Terrain, 콜라이더 없음 — 플레이어는 입구 벽이 막음) + 입구 앞 울타리 두 줄(앞마당 느낌)
        private const float OutsideFenceLength = 7f, OutsideFenceMargin = 0.8f;

        private static void BuildOutside(Transform root, float depth, float entranceX, float entranceGap)
        {
            var outside = Child(root, "Outside");
            outside.localPosition = Vector3.zero;
            outside.localRotation = Quaternion.identity;
            var oldGround = outside.Find("Ground"); // 예전 평평한 판 (mat_env_ground_toon) — 배경과 달라 보여 지움
            if (oldGround != null)
                Undo.DestroyObjectImmediate(oldGround.gameObject);
            var terrainT = Child(outside, "Terrain");
            if (!terrainT.TryGetComponent<ShopOutsideGround>(out _))
                terrainT.gameObject.AddComponent<ShopOutsideGround>();
            terrainT.localPosition = Vector3.zero;

            // 입구 앞 양옆 울타리 (아트 fence — 농장 울타리와 같은 부품)
            float hd = depth * 0.5f, half = entranceGap * 0.5f + OutsideFenceMargin;
            Vector3 left = root.TransformPoint(new Vector3(entranceX - half, 0f, -hd - 0.3f));
            Vector3 right = root.TransformPoint(new Vector3(entranceX + half, 0f, -hd - 0.3f));
            Vector3 outward = -root.forward;
            FarmLayoutSetup.FenceLine(outside, "Fence_EntranceLeft", left, left + outward * OutsideFenceLength, skipLast: false);
            FarmLayoutSetup.FenceLine(outside, "Fence_EntranceRight", right, right + outward * OutsideFenceLength, skipLast: false);
        }

        // 아트 벽 · 천장 · 입구 (없으면 만들고, 있으면 위치 · 크기). 벽 · 천장 · 커튼 · 창밖 판은 늘리고, 장식은 크기 그대로 자리만 옮긴다
        private static string BuildShell(Transform root, float width, float depth, float entranceX, Vector2 decorFactor)
        {
            var stretch = new Vector3(width / ArtShellWidth, 1f, depth / ArtShellDepth);
            float hd = depth * 0.5f;
            bool any = false;
            any |= ArtPiece(root, "Shell_Wall", ShopWallArt, Vector3.zero, Vector3.one);
            any |= ArtPiece(root, "Shell_Ceiling", ShopCeilingArt, Vector3.zero, Vector3.one);
            any |= ArtPiece(root, "Shell_Entrance", ShopEntranceArt, new Vector3(entranceX, 0f, -hd - EntranceArtOutside * stretch.z), new Vector3(stretch.x, 1f, stretch.z));
            if (!any)
                return "아트 껍데기 없음";
            var wall = root.Find("Shell_Wall");
            SetScale(wall.Find("Model"), stretch);
            SetScale(root.Find("Shell_Ceiling/Model"), stretch);
            SetScale(wall.Find("WindowView"), stretch);
            SetScale(wall.Find("ShopInteriorVolume"), stretch);

            // 장식: 예전 크기에서 바뀐 만큼만 자리를 옮긴다 (다시 실행하면 배율 1)
            int moved = 0;
            bool resize = Mathf.Abs(decorFactor.x - 1f) > 0.001f || Mathf.Abs(decorFactor.y - 1f) > 0.001f;
            var decor = wall.Find("ShopDecor");
            if (decor != null)
                foreach (Transform group in decor)
                {
                    if (group.GetComponent<Renderer>() != null)
                    {
                        SetScale(group, stretch); // 천장 커튼 한 장
                        continue;
                    }
                    if (resize)
                        foreach (Transform t in group)
                        {
                            MoveBy(t, decorFactor);
                            moved++;
                        }
                }
            var windows = wall.Find("FrontWindows");
            if (windows != null && resize)
                foreach (Transform t in windows)
                {
                    MoveBy(t, decorFactor);
                    moved++;
                }

            // 입구의 소용돌이 포털 VFX(아트 입구 프리팹의 Portal)는 문 밖이 안 보이게 가린다 — 2026-10-25 사용자: 입구는 손님을 부르는 가게처럼 트여 보이게 → 끔 (EntrancePortalVisible)
            var entrance = root.Find("Shell_Entrance");
            var portal = entrance != null ? entrance.Find("Portal") : null;
            if (portal != null)
                portal.gameObject.SetActive(EntrancePortalVisible);
            return $"아트 벽 · 천장 × ({stretch.x:0.##}, {stretch.z:0.##})" + (resize ? $" · 장식 {moved}개 자리 옮김" : string.Empty);
        }

        private const bool EntrancePortalVisible = false;

        private static void SetScale(Transform t, Vector3 scale)
        {
            if (t == null)
                return;
            t.localScale = scale;
            EditorUtility.SetDirty(t);
        }

        private static void MoveBy(Transform t, Vector2 factor)
        {
            var p = t.localPosition;
            t.localPosition = new Vector3(p.x * factor.x, p.y, p.z * factor.y);
            EditorUtility.SetDirty(t);
        }

        private static bool ArtPiece(Transform root, string name, string path, Vector3 localPos, Vector3 scale)
        {
            var t = root.Find(name);
            if (t == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    return false;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                go.name = name;
                t = go.transform;
            }
            t.localPosition = localPos;
            t.localRotation = Quaternion.identity;
            t.localScale = scale;
            return true;
        }

        private static void Wall(Transform parent, string name, Vector3 basePos, Vector3 size, Material material)
        {
            var t = parent.Find(name);
            if (t == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(parent, false);
                t = go.transform;
                if (material != null)
                    go.GetComponent<Renderer>().sharedMaterial = material;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic); // NavMesh는 ShopRoot의 NavMeshSurface가 콜라이더로 굽는다
            }
            t.localPosition = basePos + Vector3.up * (size.y * 0.5f);
            t.localRotation = Quaternion.identity;
            t.localScale = size;
        }

        private static Transform Spot(Transform parent, string name, Vector3 pos, FurnitureKind kind, Vector2 size, int unlockStage)
        {
            var t = Child(parent, name);
            t.localPosition = pos;
            t.localRotation = Quaternion.identity;
            if (!t.TryGetComponent<FurnitureSpot>(out var spot))
                spot = t.gameObject.AddComponent<FurnitureSpot>();
            var so = new SerializedObject(spot);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("size").vector2Value = size;
            so.FindProperty("unlockStage").intValue = unlockStage;
            so.ApplyModifiedPropertiesWithoutUndo();
            return t;
        }

        private static void Zone(Transform root, string name, Vector3 pos, Vector2 size, bool allowStains = false)
        {
            var t = Child(root, name);
            t.localPosition = pos;
            if (!t.TryGetComponent<ForbiddenZone>(out var zone))
                zone = t.gameObject.AddComponent<ForbiddenZone>();
            var so = new SerializedObject(zone);
            so.FindProperty("radius").floatValue = 0f;
            so.FindProperty("boxSize").vector2Value = size;
            so.FindProperty("allowsStains").boolValue = allowStains;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Place(Transform parent, string name, Vector3 pos, float yaw)
        {
            var t = parent.Find(name);
            if (t == null)
                return;
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // 램프 프리팹 안의 점광원(자식 Light)을 켜고 끈다. 켤 때 range > 0이면 범위도 맞춘다 (램프가 없으면 그냥 지나감 — 아트 샹들리에로 바뀜)
        private static void SetLampLight(Transform root, string lampName, bool on, float range)
        {
            var lamp = root.Find(lampName);
            var light = lamp != null ? lamp.GetComponentInChildren<Light>(true) : null;
            if (light == null)
                return;
            light.enabled = on;
            if (on && range > 0f)
                light.range = range;
            EditorUtility.SetDirty(light);
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
