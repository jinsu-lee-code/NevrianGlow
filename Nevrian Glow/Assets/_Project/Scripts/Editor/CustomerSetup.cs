#if UNITY_EDITOR
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace NevrianGlowFarm.Editor
{
    /// <summary>
    /// 손님 그레이박스 (BALANCE 9-5): 공용 애니메이터 ac_customer(파라미터 이름 고정 · 빈 클립 5개 + 바구니 레이어) · 손님 프리팹 · 가게 NavMesh.
    /// 여러 번 실행해도 결과가 같다 — 이미 있으면 건드리지 않는다. 김기인 납품 시 클립 파일만 교체하면 된다.
    /// </summary>
    public static class CustomerSetup
    {
        private const string AnimFolder = "Assets/_Project/Art/Animations/Customer";
        private const string ControllerPath = AnimFolder + "/ac_customer.controller";
        private const string CustomerPath = "Assets/_Project/Prefabs/Shop/customer.prefab";
        private const string CustomerMaterialPath = "Assets/_Project/Art/Materials/mat_greybox_customer.mat";
        private const string BasketMaterialPath = "Assets/_Project/Art/Materials/mat_greybox_basket.mat";
        private const string ShelfItemPath = "Assets/_Project/Prefabs/Shop/shelf_item.prefab";
        private const string NavMeshPath = "Assets/_Project/Scenes/Shop/NavMesh-Shop.asset";

        // 파라미터 이름은 지금 고정 (GDD 4-5 · BALANCE 9-5)
        private static readonly string[] Clips = { "anim_customer_walk", "anim_customer_browse", "anim_customer_pick_basket", "anim_customer_put_item", "anim_customer_happy" };

        [MenuItem("Glow Farm/Setup/Create Customer Greybox")]
        private static void CreateMenu()
        {
            string result = EnsureController(out var controller);
            result += EnsurePrefab(controller);
            Debug.Log("[Setup] 손님 그레이박스: " + result);
        }

        [MenuItem("Glow Farm/Setup/Bake Shop NavMesh")]
        private static void BakeMenu()
        {
            var surface = Object.FindFirstObjectByType<NavMeshSurface>();
            if (surface == null)
            {
                Debug.LogError("[Setup] NavMeshSurface가 없음 - Shop 씬을 열고 ShopRoot에 NavMeshSurface를 둔다");
                return;
            }
            surface.BuildNavMesh();
            var data = surface.navMeshData;
            EnsureFolder(Path.GetDirectoryName(NavMeshPath).Replace('\\', '/'));
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(data, existing);
                surface.navMeshData = existing;
            }
            else
                AssetDatabase.CreateAsset(data, NavMeshPath);
            surface.RemoveData();
            surface.AddData();
            EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Setup] 가게 NavMesh 굽기 완료: {NavMeshPath} (씬 저장 필요)");
        }

        private static string EnsureController(out AnimatorController controller)
        {
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller != null)
                return "애니메이터 유지";

            EnsureFolder(AnimFolder);
            var clips = new AnimationClip[Clips.Length];
            for (int i = 0; i < Clips.Length; i++)
            {
                string path = $"{AnimFolder}/{Clips[i]}.anim";
                clips[i] = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clips[i] == null)
                {
                    clips[i] = new AnimationClip { name = Clips[i] };
                    bool loop = i <= 1 || i == 4; // 걷기 · 둘러보기 · 해피는 반복
                    var settings = AnimationUtility.GetAnimationClipSettings(clips[i]);
                    settings.loopTime = loop;
                    AnimationUtility.SetAnimationClipSettings(clips[i], settings);
                    AssetDatabase.CreateAsset(clips[i], path);
                }
            }

            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Browsing", AnimatorControllerParameterType.Bool);
            controller.AddParameter("PickBasket", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("PutItem", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Happy", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HasBasket", AnimatorControllerParameterType.Bool);
            controller.AddParameter("WalkPlayback", AnimatorControllerParameterType.Float); // 걷기 재생 배속 (CustomerAI가 계산, 5-3)

            // 기본 레이어: 걷기(기본 — Speed로 섞기는 클립 납품 후 블렌드 트리로) · 둘러보기 · 장바구니 집기 · 넣기 · 해피
            var sm = controller.layers[0].stateMachine;
            var walk = sm.AddState("Walk");
            walk.motion = clips[0];
            walk.speedParameter = "WalkPlayback";
            walk.speedParameterActive = true;
            var browse = sm.AddState("Browse");
            browse.motion = clips[1];
            var pick = sm.AddState("PickBasket");
            pick.motion = clips[2];
            var put = sm.AddState("PutItem");
            put.motion = clips[3];
            var happy = sm.AddState("Happy");
            happy.motion = clips[4];
            sm.defaultState = walk;

            var toBrowse = walk.AddTransition(browse);
            toBrowse.AddCondition(AnimatorConditionMode.If, 0, "Browsing");
            toBrowse.hasExitTime = false;
            var fromBrowse = browse.AddTransition(walk);
            fromBrowse.AddCondition(AnimatorConditionMode.IfNot, 0, "Browsing");
            fromBrowse.hasExitTime = false;

            AddTriggerState(sm, pick, "PickBasket", walk);
            AddTriggerState(sm, put, "PutItem", browse);
            AddTriggerState(sm, happy, "Happy", walk);

            // 왼팔 바구니 레이어 (아바타 마스크는 리그 납품 후)
            controller.AddLayer("Basket");
            var layers = controller.layers;
            layers[1].defaultWeight = 1f;
            controller.layers = layers;
            var basketSm = controller.layers[1].stateMachine;
            var empty = basketSm.AddState("NoBasket");
            var hold = basketSm.AddState("HoldBasket");
            basketSm.defaultState = empty;
            var toHold = empty.AddTransition(hold);
            toHold.AddCondition(AnimatorConditionMode.If, 0, "HasBasket");
            toHold.hasExitTime = false;
            var toEmpty = hold.AddTransition(empty);
            toEmpty.AddCondition(AnimatorConditionMode.IfNot, 0, "HasBasket");
            toEmpty.hasExitTime = false;

            AssetDatabase.SaveAssets();
            return "애니메이터 생성 (빈 클립 5)";
        }

        // Any State → (트리거) → 상태 → 끝나면 돌아갈 상태
        private static void AddTriggerState(AnimatorStateMachine sm, AnimatorState state, string trigger, AnimatorState back)
        {
            var any = sm.AddAnyStateTransition(state);
            any.AddCondition(AnimatorConditionMode.If, 0, trigger);
            any.hasExitTime = false;
            any.canTransitionToSelf = true;
            var exit = state.AddTransition(back);
            exit.hasExitTime = true;
            exit.exitTime = 1f;
        }

        // 손님: 루트(NavMeshAgent · Animator · CustomerAI) / Model 캡슐(콜라이더 없음 — 플레이어 · 조준과 부딪히지 않음) / Basket(왼손 자리) / BasketModel
        private static string EnsurePrefab(AnimatorController controller)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CustomerPath) != null)
                return " · customer 유지";

            var root = new GameObject("customer");
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f;
            agent.height = 1.6f;
            agent.angularSpeed = 540f;
            agent.acceleration = 12f;
            agent.autoBraking = true;
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            var model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            model.name = "Model";
            Object.DestroyImmediate(model.GetComponent<Collider>());
            model.transform.SetParent(root.transform, false);
            model.GetComponent<Renderer>().sharedMaterial = EnsureMaterial(CustomerMaterialPath, Color.white);

            var basket = new GameObject("Basket").transform;
            basket.SetParent(root.transform, false);
            basket.localPosition = new Vector3(-0.32f, 0.75f, 0.12f);
            var basketModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            basketModel.name = "BasketModel";
            Object.DestroyImmediate(basketModel.GetComponent<Collider>());
            basketModel.transform.SetParent(basket, false);
            basketModel.transform.localScale = new Vector3(0.3f, 0.12f, 0.2f);
            basketModel.GetComponent<Renderer>().sharedMaterial = EnsureMaterial(BasketMaterialPath, new Color(0.6f, 0.45f, 0.3f));

            var ai = root.AddComponent<Systems.Sales.CustomerAI>();
            var so = new SerializedObject(ai);
            so.FindProperty("agent").objectReferenceValue = agent;
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("model").objectReferenceValue = model.transform;
            so.FindProperty("greyboxRenderer").objectReferenceValue = model.GetComponent<Renderer>();
            so.FindProperty("basket").objectReferenceValue = basket;
            so.FindProperty("basketItemPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Renderer>(ShelfItemPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder(Path.GetDirectoryName(CustomerPath).Replace('\\', '/'));
            PrefabUtility.SaveAsPrefabAsset(root, CustomerPath);
            Object.DestroyImmediate(root);
            return " · customer 생성";
        }

        private static Material EnsureMaterial(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = Path.GetFileNameWithoutExtension(path) };
            material.SetColor("_BaseColor", color);
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
#endif
