using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Logic.Save;
using NevrianGlowFarm.Systems.Cleaning;
using NevrianGlowFarm.Systems.Farm;
using NevrianGlowFarm.Systems.Items;
using NevrianGlowFarm.Systems.Player;
using NevrianGlowFarm.Systems.Sales;
using NevrianGlowFarm.Systems.Shop;
using NevrianGlowFarm.Systems.Storage;
using NevrianGlowFarm.Systems.Zones;
using NevrianGlowFarm.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NevrianGlowFarm.Systems.Save
{
    /// <summary>
    /// 저장 · 불러오기 (GDD 4-8, TASKS 6-1). 형식 · 규칙은 Logic SaveFormat · SaveState, 여기는 씬의 시스템과 파일 사이만.
    /// 저장 시점: 다음 날 아침(밤 처리가 끝난 같은 프레임 끝)에 자동. 저장 = "그날 아침" 상태 → 불러오면 그날 상점 단계부터.
    /// 파일: persistentDataPath/파일 이름. 원자적 쓰기(임시 파일 → 교체, 이전 파일은 .bak), 깨진 파일은 읽지 않고 알린다.
    /// Core 씬에 하나.
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("id → 콘텐츠 (없어진 id 걸러내기, Data/ContentDatabase)")]
        [SerializeField] private ContentDatabase database;
        [Tooltip("바닥 아이템 프리팹 (Prefabs/Items/world_item)")]
        [SerializeField] private WorldItem worldItemPrefab;

        [Header("조정값")]
        [Tooltip("저장 파일 이름 (persistentDataPath 안)")]
        [SerializeField] private string fileName = "save_01.ngf";
        [Tooltip("다음 날 아침마다 자동 저장")]
        [SerializeField] private bool autosave = true;

        [Header("디버그")]
        [Tooltip("마지막 저장 · 불러오기 결과 (읽기 전용)")]
        [SerializeField] private string lastResult;

        private static SaveSystem instance;
        private bool savePending;

        public static SaveSystem Instance => instance;
        public string FilePath => Path.Combine(Application.persistentDataPath, fileName);
        public bool HasSave => File.Exists(FilePath);
        public string LastResult => lastResult;

        /// <summary>저장 · 불러오기가 끝났을 때 (성공 여부, 안내 문구).</summary>
        public event Action<bool, string> Finished;

        private void Awake()
        {
            if (database == null || worldItemPrefab == null || string.IsNullOrEmpty(fileName))
            {
                Debug.LogError("[SaveSystem] 참조가 비어 있음 - 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void OnEnable() => GameEvents.PhaseChanged += OnPhaseChanged;
        private void OnDisable() => GameEvents.PhaseChanged -= OnPhaseChanged;

        // 밤 처리(밭) · 상점 입고 초기화가 같은 알림 안에서 끝난 뒤 저장한다 → 이 프레임 끝(LateUpdate)
        private void OnPhaseChanged(PhaseChange change)
        {
            if (autosave && change.DayChanged)
                savePending = true;
        }

        private void LateUpdate()
        {
            if (!savePending)
                return;
            savePending = false;
            Save(out _);
        }

        // ── 저장 ─────────────────────────────────────────

        public SaveSnapshot Capture()
        {
            var s = new SaveSnapshot();
            GameManager.Instance.CaptureTo(s);
            var farm = FindFirstObjectByType<FarmGrid>();
            if (farm != null)
                farm.CaptureTo(s);
            if (PlayerInventory.Instance != null)
                PlayerInventory.Instance.CaptureTo(s);
            if (StorageSystem.Instance != null)
                StorageSystem.Instance.CaptureTo(s);
            foreach (var shelf in OrderedShelves())
                s.Shelves.Add(SaveState.Capture(shelf.State));
            if (ShopSystem.Instance != null)
                ShopSystem.Instance.CaptureTo(s);
            foreach (var w in WorldItem.All)
            {
                if (w == null || string.IsNullOrEmpty(w.ItemId) || w.Count <= 0)
                    continue;
                var p = w.transform.position;
                s.WorldItems.Add(new WorldItemSave
                {
                    ItemId = w.ItemId, Count = w.Count, Zone = w.gameObject.scene.name,
                    X = p.x, Y = p.y, Z = p.z, Yaw = w.transform.eulerAngles.y,
                });
            }
            return s;
        }

        /// <summary>지금 상태를 파일에 쓴다 (임시 파일 → 교체).</summary>
        public bool Save(out string message)
        {
            var game = GameManager.Instance;
            if (!enabled || game == null || !game.IsStarted)
                return Done(false, message = "저장 실패 - 게임이 시작되지 않음");
            try
            {
                var snapshot = Capture();
                string text = SaveFormat.Write(snapshot);
                string path = FilePath;
                string temp = path + ".tmp";
                File.WriteAllText(temp, text, new System.Text.UTF8Encoding(false));
                if (File.Exists(path))
                    File.Replace(temp, path, path + ".bak");
                else
                    File.Move(temp, path);
                message = $"{snapshot.Day}일차 {DayPhaseNames.ToKorean(snapshot.Phase)} 저장 ({snapshot.Cells.Count}칸 · 바닥 아이템 {snapshot.WorldItems.Count}개)";
                Debug.Log($"[SaveSystem] {message} - {path}");
                return Done(true, message);
            }
            catch (Exception e)
            {
                message = "저장 실패 - " + e.Message;
                Debug.LogError($"[SaveSystem] {message}\n{e}", this);
                return Done(false, message);
            }
        }

        // ── 불러오기 ─────────────────────────────────────

        /// <summary>파일을 읽어 지금 게임을 그 상태로 바꾼다. 깨진 파일이면 아무것도 바꾸지 않는다.</summary>
        public bool Load(out string message)
        {
            if (!enabled || GameManager.Instance == null)
                return Done(false, message = "불러오기 실패 - Core 씬이 준비되지 않음");
            if (!HasSave)
                return Done(false, message = "불러오기 실패 - 저장 파일 없음");

            SaveSnapshot snapshot;
            var warnings = new List<string>();
            try
            {
                string text = File.ReadAllText(FilePath);
                if (!SaveFormat.TryRead(text, out snapshot, out string error, warnings))
                {
                    message = "불러오기 실패 - 저장 파일이 깨졌음: " + error;
                    Debug.LogError($"[SaveSystem] {message} ({FilePath})", this);
                    return Done(false, message);
                }
            }
            catch (Exception e)
            {
                message = "불러오기 실패 - " + e.Message;
                Debug.LogError($"[SaveSystem] {message}\n{e}", this);
                return Done(false, message);
            }

            int removed = SaveState.RemoveUnknown(snapshot, id => database.TryGet(id, out ContentData _), warnings);
            foreach (var w in warnings)
                Debug.Log($"[SaveSystem] 불러오기: {w}");

            try
            {
                Apply(snapshot);
            }
            catch (Exception e)
            {
                message = "불러오기 중 오류 - " + e.Message;
                Debug.LogError($"[SaveSystem] {message}\n{e}", this);
                return Done(false, message);
            }
            message = $"{snapshot.Day}일차 {DayPhaseNames.ToKorean(snapshot.Phase)} 불러옴" + (removed > 0 ? $" (없어진 항목 {removed}개 건너뜀)" : string.Empty);
            Debug.Log($"[SaveSystem] {message}");
            return Done(true, message);
        }

        private void Apply(SaveSnapshot s)
        {
            // 1) 지금 진행 중인 것 정리 (장사 손님 · 오늘 얼룩 · 바닥 아이템 · 열린 화면)
            if (GameScreen.Current != null)
                GameScreen.Current.Close();
            if (SalesSystem.Instance != null)
                SalesSystem.Instance.CancelForLoad();
            if (CleaningSystem.Instance != null)
                CleaningSystem.Instance.ResetForLoad();
            foreach (var w in WorldItem.All.ToArray())
            {
                w.gameObject.SetActive(false); // 목록에서 바로 빠지게
                Destroy(w.gameObject);
            }

            // 2) 일차 · 달러 · 레벨 · 스킬 (알림 → 스킬 효과 · 상점 · HUD가 따라온다)
            GameManager.Instance.LoadFrom(s);

            // 3) 각 시스템 내용
            var farm = FindFirstObjectByType<FarmGrid>();
            if (farm != null)
                farm.LoadFrom(s);
            if (PlayerInventory.Instance != null)
                PlayerInventory.Instance.LoadFrom(s);
            if (StorageSystem.Instance != null)
                StorageSystem.Instance.LoadFrom(s);
            var shelves = OrderedShelves();
            for (int i = 0; i < shelves.Count; i++)
                if (i < s.Shelves.Count)
                    shelves[i].LoadSlots(s.Shelves[i]);
            if (ShopSystem.Instance != null)
                ShopSystem.Instance.LoadFrom(s);
            foreach (var w in s.WorldItems)
                SpawnWorldItem(w);

            // 4) 하루 시작 지점으로
            if (ZoneTransition.Instance != null)
                ZoneTransition.Instance.ArriveAtDayStart();
        }

        private void SpawnWorldItem(WorldItemSave w)
        {
            var item = database.Get<ItemData>(w.ItemId);
            if (item == null)
                return;
            var go = Instantiate(worldItemPrefab, new Vector3(w.X, w.Y, w.Z), Quaternion.Euler(0f, w.Yaw, 0f));
            go.Init(item, w.Count);
            var zone = SceneManager.GetSceneByName(w.Zone);
            if (zone.IsValid() && zone.isLoaded)
                SceneManager.MoveGameObjectToScene(go.gameObject, zone);
            else
                Debug.LogWarning($"[SaveSystem] 바닥 아이템 {w.ItemId}의 구역 '{w.Zone}' 씬이 없어 Core에 둠", go);
        }

        /// <summary>저장 파일을 지운다 (디버그 · 새 게임).</summary>
        public void DeleteSave()
        {
            foreach (var p in new[] { FilePath, FilePath + ".bak", FilePath + ".tmp" })
                if (File.Exists(p))
                    File.Delete(p);
            Done(true, "저장 파일 삭제");
        }

        // 진열대 순서는 씬 · 계층 경로로 정한다 (실행마다 바뀌는 인스턴스 번호에 기대지 않게)
        private static List<ShelfDisplay> OrderedShelves()
        {
            var list = FindObjectsByType<ShelfDisplay>(FindObjectsSortMode.None).ToList();
            list.Sort((a, b) => string.CompareOrdinal(PathOf(a.transform), PathOf(b.transform)));
            return list;
        }

        private static string PathOf(Transform t)
        {
            string path = t.name;
            for (var p = t.parent; p != null; p = p.parent)
                path = p.name + "/" + path;
            return t.gameObject.scene.name + ":" + path;
        }

        private bool Done(bool ok, string message)
        {
            lastResult = message;
            Finished?.Invoke(ok, message);
            return ok;
        }
    }
}
