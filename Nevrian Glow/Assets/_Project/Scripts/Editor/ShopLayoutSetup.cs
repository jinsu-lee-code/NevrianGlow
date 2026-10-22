#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Systems.Cleaning;
using NevrianGlowFarm.Systems.Farm;
using NevrianGlowFarm.Systems.Sales;
using NevrianGlowFarm.Systems.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NevrianGlowFarm.Editor
{
    /// <summary>
    /// 가게 배치 (TASKS P-4, 레벨 배치도 14 · 레벨배치 설정안 2절 · 최종 기획서 7-1). Shop 씬 ShopRoot 아래를 배치도의 순서 · 위치 관계대로 놓는다.
    /// 결정 (2026-10-21, 사용자): 기획 크기 40 × 30 m는 실제 화면에서 너무 넓어 **크기는 원래 가게(12 × 10 m)로 두고 배치만 맞춘다**.
    /// 뒤 벽: 일반 진열대 자리 4(2차) → 위 통로 → 가운데: 과일 진열대 자리 4(시작 3대) → 아래 통로 →
    /// 앞쪽: 왼쪽 창고 구역 · 쓰레기통(농장 문 쪽), 오른쪽 계산대 · 계산 줄, 앞 벽 가운데 손님 입구, 왼쪽 벽 농장 문 · 출발 지점.
    /// 금지 구역(쓰레기 · 얼룩 생성 = 바닥 − 가구 자리 − 통로 − 계산대 − 창고 − 쓰레기통), 손님 · 청소 시스템 연결까지.
    /// 여러 번 실행해도 결과가 같다 (이름으로 찾아 위치만 다시 놓음). 끝나면 Bake Shop NavMesh를 실행할 것.
    /// 좌표: ShopRoot 로컬, x = 오른쪽, z = 뒤(벽 선반 쪽).
    /// </summary>
    public static class ShopLayoutSetup
    {
        private const string ShopScenePath = "Assets/_Project/Scenes/Shop.unity";
        private const string FruitShelfPrefabPath = "Assets/_Project/Prefabs/Shop/fruit_shelf.prefab";
        private const string FruitShelfDataPath = "Assets/_Project/Data/Items/furn_fruit_shelf.asset";
        private const string GameStartPath = "Assets/_Project/Data/Stats/GameStartSettings.asset";

        // 벽 높이 3.6 m = 아트 가게 벽(pf_env_shop_wall, 2026-10-21) — 그레이박스 벽은 보이지 않는 콜라이더로만 남는다
        private const float Width = 12f, Depth = 10f, WallHeight = 3.6f, WallThickness = 0.2f;
        private const float UpperAisleZ = 3.3f, UpperAisleWidth = 1.2f;
        private const float LowerAisleZ = 0f, LowerAisleWidth = 1.6f;
        private const float FruitRowZ = 1.6f, WallRowZ = 4.3f;
        private static readonly float[] SpotX = { -4.35f, -1.45f, 1.45f, 4.35f };
        private static readonly Vector2 FruitSpotSize = new Vector2(2.6f, 1.3f);
        private static readonly Vector2 WallSpotSize = new Vector2(2.6f, 0.8f);
        private const float EntranceX = 0f, EntranceGap = 1.6f;               // 앞 벽(z = −5) 가운데 — 아트 벽의 문 구멍 1.6 × 2.4 m (x 0)
        private const float FarmDoorZ = -2.3f, FarmDoorGap = 1.6f;            // 왼쪽 벽(x = −6) 출발 지점 — 아트 벽엔 농장 문 구멍이 없어 벽은 막힘(순간이동)

        // 아트 가게 껍데기 (BRANCH_ART 10.21): 벽 · 천장 피벗 = 바닥 중앙(ShopRoot), 입구 피벗 = 통로 바닥 중앙. 콜라이더 없음
        private const string ShopWallArt = "Assets/Art/Environment/shop_wall/pf_env_shop_wall.prefab";
        private const string ShopCeilingArt = "Assets/Art/Environment/shop_ceiling/pf_env_shop_ceiling.prefab";
        private const string ShopEntranceArt = "Assets/Art/Environment/shop_entrance/pf_env_shop_entrance.prefab";
        private const float EntranceArtZ = -5.1f;

        [MenuItem("Glow Farm/Setup/Build Shop Layout")]
        private static void BuildMenu()
        {
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
            string log = Build(root.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
            Debug.Log("[Setup] 가게 배치: " + log + " — 다음: Glow Farm/Setup/Bake Shop NavMesh (Shop 씬을 연 채로)");
        }

        private static string Build(Transform root)
        {
            var notes = new List<string>();
            var oldShelfT = root.Find("Shelf");
            ShelfDisplay oldShelf = oldShelfT != null && oldShelfT.TryGetComponent<ShelfDisplay>(out var found) ? found : null;
            var oldBody = root.Find("Shelf/Body");
            Material wallMaterial = oldBody != null && oldBody.TryGetComponent<Renderer>(out var bodyRenderer) ? bodyRenderer.sharedMaterial : null;
            if (wallMaterial == null)
                wallMaterial = root.GetComponentsInChildren<Renderer>(true).Select(r => r.sharedMaterial).FirstOrDefault(m => m != null);

            // 바닥 (Unity 평면 10 m)
            var floor = root.Find("Floor");
            if (floor != null)
                floor.localScale = new Vector3(Width / 10f, 1f, Depth / 10f);

            BuildWalls(root, wallMaterial);

            // 진열 가구 프리팹 (예전 진열대의 참조 · 재질을 이어받아 처음 한 번 만든다)
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FruitShelfPrefabPath);
            if (prefab == null)
            {
                if (oldShelf == null)
                    return "과일 진열대 프리팹도 예전 진열대(Shelf)도 없음 — 중단";
                prefab = CreateFruitShelfPrefab(oldShelf, wallMaterial);
                notes.Add("fruit_shelf 프리팹 생성");
            }

            // 가구 자리 8 + 시작 과일 진열대
            var start = AssetDatabase.LoadAssetAtPath<GameStartSettingsData>(GameStartPath);
            int startShelves = start != null ? start.StartFruitShelves : 3;
            var spots = Child(root, "FurnitureSpots");
            var shelves = new List<ShelfDisplay>();
            for (int i = 0; i < SpotX.Length; i++)
            {
                var fruit = Spot(spots, $"FruitSpot_{i + 1}", new Vector3(SpotX[i], 0f, FruitRowZ), FurnitureKind.FruitShelf, FruitSpotSize);
                Spot(spots, $"WallSpot_{i + 1}", new Vector3(SpotX[i], 0f, WallRowZ), FurnitureKind.GoodsShelf, WallSpotSize);
                var existing = fruit.GetComponentInChildren<ShelfDisplay>();
                if (i < startShelves && existing == null)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, fruit);
                    go.name = $"FruitShelf_{i + 1}";
                    existing = go.GetComponent<ShelfDisplay>();
                }
                if (existing != null)
                {
                    // 손님 쪽(로컬 +Z)이 아래 통로(앞)로
                    existing.transform.localPosition = Vector3.zero;
                    existing.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    shelves.Add(existing);
                }
            }
            notes.Add($"가구 자리 8 · 과일 진열대 {shelves.Count}대");

            if (oldShelf != null)
            {
                Undo.DestroyObjectImmediate(oldShelf.gameObject);
                notes.Add("예전 진열대(6칸) 지움");
            }

            // 고정 오브젝트 (배치도 14의 위치 관계)
            Place(root, "StorageAccessPoint_Shop", new Vector3(-4.7f, 0f, -4.1f), 0f);
            Place(root, "TrashBin", new Vector3(-3.1f, 0f, -4.3f), 0f);
            Place(root, "ShopSpawn", new Vector3(-5.2f, 0f, FarmDoorZ), 90f);
            Place(root, "Counter", new Vector3(4.7f, 0f, -4.0f), 90f);
            Place(root, "BasketStand", new Vector3(-0.9f, 0f, -4.3f), 90f);
            Place(root, "OpenSign", new Vector3(-1.7f, 0f, -4.5f), 0f);
            Place(root, "Lamp_Counter", new Vector3(5.5f, 0f, -4.6f), 0f);
            Place(root, "Lamp_ShelfLeft", new Vector3(-2.9f, 0f, 2.6f), 180f);
            Place(root, "Lamp_ShelfRight", new Vector3(2.9f, 0f, 2.6f), 180f);
            Place(root, "Lamp_Entrance", new Vector3(-2.3f, 0f, -4.6f), 0f);

            var sales = root.Find("SalesSystem");
            if (sales != null)
            {
                Place(sales, "CustomerEntrance", new Vector3(EntranceX, 0f, -4.6f), 0f);
                Place(sales, "BasketPoint", new Vector3(-0.3f, 0f, -4.3f), 270f);
                Place(sales, "ShelfFront", new Vector3(0f, 0f, LowerAisleZ), 0f);
                Place(sales, "CounterPoint", new Vector3(4.7f, 0f, -4.0f), 0f);
                Place(sales, "QueueStart", new Vector3(3.6f, 0f, -4.0f), 270f); // 줄은 계산대 왼쪽으로 (−x)
                var so = new SerializedObject(sales.GetComponent<SalesSystem>());
                var list = so.FindProperty("shelves");
                list.ClearArray();
                for (int i = 0; i < shelves.Count; i++)
                {
                    list.InsertArrayElementAtIndex(i);
                    list.GetArrayElementAtIndex(i).objectReferenceValue = shelves[i];
                }
                so.FindProperty("browseSpread").floatValue = 1.6f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 금지 구역 (쓰레기 · 얼룩 · 흩어진 상품을 놓지 않는 곳)
            Zone(root, "ForbiddenZone_Counter", new Vector3(4.7f, 0f, -4.0f), new Vector2(1.6f, 2.0f));
            Zone(root, "ForbiddenZone_Queue", new Vector3(2.4f, 0f, -4.0f), new Vector2(3.0f, 1.0f));
            Zone(root, "ForbiddenZone_Entrance", new Vector3(EntranceX, 0f, -4.5f), new Vector2(EntranceGap + 0.6f, 1.2f));
            Zone(root, "ForbiddenZone_TrashBin", new Vector3(-3.1f, 0f, -4.3f), new Vector2(1.2f, 1.2f));
            Zone(root, "ForbiddenZone_Storage", new Vector3(-4.7f, 0f, -4.1f), new Vector2(2.0f, 1.6f));
            Zone(root, "ForbiddenZone_ShopSpawn", new Vector3(-5.2f, 0f, FarmDoorZ), new Vector2(1.6f, FarmDoorGap));
            Zone(root, "ForbiddenZone_ShelfFront", new Vector3(0f, 0f, LowerAisleZ), new Vector2(Width, LowerAisleWidth)); // 아래 통로
            Zone(root, "ForbiddenZone_UpperAisle", new Vector3(0f, 0f, UpperAisleZ), new Vector2(Width, UpperAisleWidth));
            Zone(root, "ForbiddenZone_BasketStand", new Vector3(-0.9f, 0f, -4.3f), new Vector2(1.2f, 1.0f));
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
                so.FindProperty("areaMin").vector2Value = new Vector2(-Width * 0.5f + 0.4f, -Depth * 0.5f + 0.4f);
                so.FindProperty("areaMax").vector2Value = new Vector2(Width * 0.5f - 0.4f, Depth * 0.5f - 0.4f);
                var zones = so.FindProperty("forbiddenZones");
                var all = root.GetComponentsInChildren<ForbiddenZone>(true);
                zones.ClearArray();
                for (int i = 0; i < all.Length; i++)
                {
                    zones.InsertArrayElementAtIndex(i);
                    zones.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                notes.Add($"금지 구역 {all.Length}");
            }
            return string.Join(" · ", notes);
        }

        // ── 부품 ──

        private static void BuildWalls(Transform root, Material material)
        {
            var walls = Child(root, "Walls");
            float hw = Width * 0.5f, hd = Depth * 0.5f;
            Wall(walls, "Wall_Back", new Vector3(0f, 0f, hd), new Vector3(Width, WallHeight, WallThickness), material);
            Wall(walls, "Wall_Right", new Vector3(hw, 0f, 0f), new Vector3(WallThickness, WallHeight, Depth), material);
            // 앞 벽: 손님 입구 자리를 비운다
            float entranceLeft = EntranceX - EntranceGap * 0.5f, entranceRight = EntranceX + EntranceGap * 0.5f;
            Wall(walls, "Wall_FrontLeft", new Vector3((-hw + entranceLeft) * 0.5f, 0f, -hd), new Vector3(entranceLeft + hw, WallHeight, WallThickness), material);
            Wall(walls, "Wall_FrontRight", new Vector3((entranceRight + hw) * 0.5f, 0f, -hd), new Vector3(hw - entranceRight, WallHeight, WallThickness), material);
            // 왼쪽 벽: 아트 벽에 농장 문 구멍이 없으므로 한 장 (예전 두 조각 중 앞 조각은 지움)
            Wall(walls, "Wall_LeftBack", new Vector3(-hw, 0f, 0f), new Vector3(WallThickness, WallHeight, Depth), material);
            var leftFront = walls.Find("Wall_LeftFront");
            if (leftFront != null)
                Undo.DestroyObjectImmediate(leftFront.gameObject);

            // 아트 껍데기가 있으면 그레이박스 벽은 보이지 않는 콜라이더로만
            bool art = BuildShell(root);
            foreach (Transform w in walls)
                if (w.TryGetComponent<Renderer>(out var r))
                    r.enabled = !art;
        }

        // 아트 벽 · 천장 · 입구 (없으면 만들고, 있으면 위치만)
        private static bool BuildShell(Transform root)
        {
            bool any = false;
            any |= ArtPiece(root, "Shell_Wall", ShopWallArt, Vector3.zero);
            any |= ArtPiece(root, "Shell_Ceiling", ShopCeilingArt, Vector3.zero);
            any |= ArtPiece(root, "Shell_Entrance", ShopEntranceArt, new Vector3(EntranceX, 0f, EntranceArtZ));
            return any;
        }

        private static bool ArtPiece(Transform root, string name, string path, Vector3 localPos)
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
            t.localScale = Vector3.one;
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

        private static Transform Spot(Transform parent, string name, Vector3 pos, FurnitureKind kind, Vector2 size)
        {
            var t = Child(parent, name);
            t.localPosition = pos;
            t.localRotation = Quaternion.identity;
            if (!t.TryGetComponent<FurnitureSpot>(out var spot))
                spot = t.gameObject.AddComponent<FurnitureSpot>();
            var so = new SerializedObject(spot);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("size").vector2Value = size;
            so.ApplyModifiedPropertiesWithoutUndo();
            return t;
        }

        private static void Zone(Transform root, string name, Vector3 pos, Vector2 size)
        {
            var t = Child(root, name);
            t.localPosition = pos;
            if (!t.TryGetComponent<ForbiddenZone>(out var zone))
                zone = t.gameObject.AddComponent<ForbiddenZone>();
            var so = new SerializedObject(zone);
            so.FindProperty("radius").floatValue = 0f;
            so.FindProperty("boxSize").vector2Value = size;
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

        private static Transform Child(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null)
                return t;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // 과일 진열대: 1층 낮은 판매대(2.4 × 1.0 m) · 한 칸 · 30개(10 × 3줄). 예전 진열대의 설정 · 홀로그램 · 작물 겉모습 참조를 이어받는다.
        private static GameObject CreateFruitShelfPrefab(ShelfDisplay old, Material bodyMaterial)
        {
            const float pitch = 2.2f, top = 0.75f, depth = 1.0f;
            var go = new GameObject("fruit_shelf");
            go.layer = old.gameObject.layer;
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(pitch + 0.2f, top + 0.1f, depth);
            box.center = new Vector3(0f, (top + 0.1f) * 0.5f, 0f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.layer = go.layer;
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, top * 0.5f, 0f);
            body.transform.localScale = new Vector3(pitch + 0.2f, top, depth);
            if (bodyMaterial != null)
                body.GetComponent<Renderer>().sharedMaterial = bodyMaterial;

            var oldSo = new SerializedObject(old);
            var oldHologram = oldSo.FindProperty("hologram").objectReferenceValue as Renderer;
            var hologram = Object.Instantiate(oldHologram.gameObject, go.transform);
            hologram.name = "Hologram";

            var shelf = go.AddComponent<ShelfDisplay>();
            var so = new SerializedObject(shelf);
            so.FindProperty("settings").objectReferenceValue = oldSo.FindProperty("settings").objectReferenceValue;
            so.FindProperty("database").objectReferenceValue = oldSo.FindProperty("database").objectReferenceValue;
            so.FindProperty("itemPrefab").objectReferenceValue = oldSo.FindProperty("itemPrefab").objectReferenceValue;
            so.FindProperty("furniture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<FurnitureData>(FruitShelfDataPath);
            so.FindProperty("body").objectReferenceValue = body.transform;
            so.FindProperty("aimCollider").objectReferenceValue = box;
            so.FindProperty("hologram").objectReferenceValue = hologram.GetComponent<Renderer>();
            so.FindProperty("slotPitch").floatValue = pitch;
            so.FindProperty("topHeight").floatValue = top;
            so.FindProperty("stackSpacing").floatValue = 0.22f;
            so.FindProperty("stackColumns").intValue = 10;
            so.FindProperty("itemSize").floatValue = 0.18f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, FruitShelfPrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }
    }
}
#endif
