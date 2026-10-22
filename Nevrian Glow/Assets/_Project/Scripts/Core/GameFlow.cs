using UnityEngine;
using UnityEngine.SceneManagement;

namespace NevrianGlowFarm.Core
{
    /// <summary>
    /// 타이틀로 돌아가기 · 게임 종료 (TASKS 5-6). 타이틀로 = Core 씬을 처음부터 다시 올린다 → 구역 씬 · 상태가 모두 새로 시작되고 타이틀이 다시 뜬다.
    /// 진행은 그날 아침 자동 저장까지만 남는다 (결정 2026-10-21, 하루 중간 저장 없음).
    /// </summary>
    public static class GameFlow
    {
        /// <summary>Core 씬을 다시 올려 타이틀 화면으로.</summary>
        public static void ReturnToTitle()
        {
            Time.timeScale = 1f;
            var game = GameManager.Instance;
            string core = game != null ? game.gameObject.scene.name : SceneManager.GetActiveScene().name;
            Debug.Log("[GameFlow] 타이틀로");
            SceneManager.LoadScene(core, LoadSceneMode.Single);
        }

        /// <summary>게임 종료 (에디터에서는 플레이 멈춤).</summary>
        public static void Quit()
        {
            Debug.Log("[GameFlow] 게임 종료");
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
