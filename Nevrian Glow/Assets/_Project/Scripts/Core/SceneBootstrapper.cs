using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NevrianGlowFarm.Core
{
    /// <summary>
    /// Core 씬에서 구역 씬(Farm, Shop)을 Additive로 함께 올리고, 전부 끝나면 시작 가능을 알린다.
    /// </summary>
    public class SceneBootstrapper : MonoBehaviour
    {
        [Header("조정값")]
        [Tooltip("Core와 함께 올릴 구역 씬 이름 (Build Settings 등록 필수)")]
        [SerializeField] private string[] zoneScenes = { "Farm", "Shop" };

        [Header("디버그")]
        [Tooltip("구역 씬이 모두 올라왔는지 (플레이 중 확인용, 직접 바꾸지 않는다)")]
        [SerializeField] private bool isReady;

        /// <summary>구역 씬이 모두 올라와 시작할 수 있는 상태.</summary>
        public bool IsReady => isReady;

        /// <summary>구역 씬이 모두 올라온 순간 1회 발생. 늦게 구독했다면 IsReady를 먼저 확인한다.</summary>
        public event Action Ready;

        private IEnumerator Start()
        {
            isReady = false;
            var pending = new List<AsyncOperation>();
            bool failed = false;

            foreach (var sceneName in zoneScenes)
            {
                // 에디터에서 여러 씬을 함께 열어 둔 채 플레이하면 이미 올라와 있다.
                if (SceneManager.GetSceneByName(sceneName).isLoaded)
                    continue;

                var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (op == null)
                {
                    Debug.LogError($"[SceneBootstrapper] 구역 씬을 불러올 수 없음: '{sceneName}' — Build Settings 등록 확인", this);
                    failed = true;
                    continue;
                }
                pending.Add(op);
            }

            foreach (var op in pending)
            {
                while (!op.isDone)
                    yield return null;
            }

            if (failed)
                yield break;

            isReady = true;
            Ready?.Invoke();
        }
    }
}
