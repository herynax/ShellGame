using System.Collections;
using System.Collections.Generic;
using ShellGame.Core;
using ShellGame.Gameplay;
using ShellGame.Items;
using ShellGame.Map;
using ShellGame.Meta;
using ShellGame;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShellGame.Run
{
    public sealed class RunManager : MonoBehaviour
    {
        public const string GameSceneName = "GameScene";

        private static readonly string[] FirstRunPrefix = { "Wrath" };
        private static readonly string[] ReturningPlayerPrefix = { "Wrath" };

        public static RunManager Instance { get; private set; }

        public RunData CurrentRun { get; private set; }
        public RunState State { get; private set; } = RunState.None;
        public string CurrentEncounterId { get; private set; }
        public EncounterKind CurrentEncounterKind { get; private set; }

        public bool HasActiveRun =>
            CurrentRun != null && State != RunState.None && State != RunState.RunEnded;

        public bool CurrentEncounterIsFinal =>
            HasActiveRun &&
            CurrentRun.Map.GetNode(CurrentRun.MapState.CurrentNodeId).Connections.Length == 0;

        private MapSceneController _map;
        private EncounterHost _host;
        private EncounterCatalog _catalog;
        private bool _resumeEncounterOnLoad;

        public static void EnsureExists()
        {
            if (Instance != null) return;
            Debug.Log("[RunManager] EnsureExists: создаём RunManager.");
            new GameObject(nameof(RunManager)).AddComponent<RunManager>();
        }

        private void Awake()
        {
            Debug.Log("[RunManager] Awake.");
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private EncounterCatalog Catalog =>
            _catalog != null ? _catalog : (_catalog = Resources.Load<EncounterCatalog>(EncounterCatalog.ResourcesPath));

        public void RegisterMap(MapSceneController map) { Debug.Log("[RunManager] RegisterMap."); _map = map; }
        public void RegisterHost(EncounterHost host) { Debug.Log("[RunManager] RegisterHost."); _host = host; }

        // ---------------- жизненный цикл ----------------

        public void StartNewRun()
        {
            GameSessionProgression.Instance?.Reset();

            bool tutorialDone = GameManager.IsTutorialCompleted();
            var prefix = tutorialDone ? ReturningPlayerPrefix : FirstRunPrefix;
            int seed = Random.Range(int.MinValue, int.MaxValue);

            var map = FirstRunMapFactory.BuildFirstRun(prefix, seed, new MapGenerationConfig());
            
            var playerInventory = CreateRuntimeInventory();
            playerInventory.BeginNewRun();

            CurrentRun = new RunData
            {
                Map = map,
                MapState = new MapState(map.StartNode.Id),
                IsFirstRun = !tutorialDone,
                PlayerInventory = playerInventory
            };

            _resumeEncounterOnLoad = false;
            State = RunState.OnMap;
        }

        /// <summary>
        /// Путь к конфиг-ассету с начальными слотами и стартовым кошельком.
        /// </summary>
        public const string PlayerInventoryConfigResourcePath = "Configs/PlayerInventory";

        /// <summary>
        /// Создаёт рантайм-инвентарь, применяя стартовые настройки из конфиг-ассета.
        /// Рантайм-инвентарь всегда новый (CreateInstance), поэтому значения ассета
        /// нужно скопировать вручную — иначе он не участвует в игре.
        /// </summary>
        private static PlayerInventorySO CreateRuntimeInventory()
        {
            var playerInventory = ScriptableObject.CreateInstance<PlayerInventorySO>();
            playerInventory.name = "PlayerInventory_Runtime";

            var template = Resources.Load<PlayerInventorySO>(PlayerInventoryConfigResourcePath);
            if (template != null)
                playerInventory.ApplyStartingSettings(template);
            else
                Debug.LogWarning($"[RunManager] Не найден конфиг инвентаря по пути Resources/{PlayerInventoryConfigResourcePath} — используются значения по умолчанию.");

            DontDestroyOnLoad(playerInventory);
            return playerInventory;
        }

        public void EndRun()
        {
            CurrentRun = null;
            CurrentEncounterId = null;
            _resumeEncounterOnLoad = false;
            State = RunState.None;
        }

        /// <summary>Из GameManager.OnSideDied. Сцены не грузит, карту не двигает.</summary>
        public void NotifyEncounterFinished(TurnSide sideThatDied)
        {
            if (State == RunState.EncounterActive && sideThatDied == TurnSide.Enemy)
                State = RunState.EncounterCleared;
        }

        // ---------------- вход после загрузки сцены Game ----------------

        public static IEnumerator PostSceneLoad()
        {
            if (Instance == null)
            {
                Debug.LogWarning("[RunManager] PostSceneLoad вызван, но Instance == null (не вызван EnsureExists?).");
                yield break;
            }
            Debug.Log("[RunManager] PostSceneLoad: старт.");
            yield return Instance.PostSceneLoadRoutine();
        }

        public IEnumerator PostSceneLoadRoutine()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            Debug.Log($"[RunManager] PostSceneLoadRoutine: активная сцена = '{sceneName}', ожидается '{GameSceneName}'.");
            if (sceneName != GameSceneName)
            {
                Debug.LogWarning("[RunManager] Имя сцены не совпадает, выходим.");
                yield break;
            }

            yield return null;

            Debug.Log($"[RunManager] _map={(_map != null)}, _host={(_host != null)}, HasActiveRun={HasActiveRun}, State={State}");

            if (_map == null || _host == null)
            {
                Debug.LogError("[RunManager] В сцене Game нет MapSceneController или EncounterHost.");
                yield break;
            }

            if (!HasActiveRun)
            {
                Debug.Log("[RunManager] Активного рана нет, StartNewRun().");
                StartNewRun();
            }

            var current = CurrentRun.Map.GetNode(CurrentRun.MapState.CurrentNodeId);
            Debug.Log($"[RunManager] Текущий узел: id={current.Id}, type={current.Type}, connections={current.Connections.Length}, resume={_resumeEncounterOnLoad}");

            if (_resumeEncounterOnLoad)
            {
                Debug.Log("[RunManager] Возобновляем энкаунтер из чекпоинта.");
                _resumeEncounterOnLoad = false;
                yield return EnterEncounterRoutine(current.Id, advanceMap: false);
                yield break;
            }

            bool tutorialDone = GameManager.IsTutorialCompleted();
            bool needsTutorial =
                !tutorialDone &&
                current.Type == MapNodeType.Start;

            Debug.Log($"[RunManager] tutorialDone={tutorialDone}, needsTutorial={needsTutorial}");

            if (needsTutorial)
            {
                yield return EnterTutorialRoutine();
                yield break;
            }

            ShowMap();
        }

        private IEnumerator EnterTutorialRoutine()
        {
            var tutorial = Catalog?.GetTutorial(); // конкретную реализацию надо посмотреть

            if (tutorial == null || tutorial.RigPrefab == null)
            {
                Debug.LogError("[RunManager] Tutorial не найден в EncounterCatalog.");
                yield break;
            }

            CurrentEncounterId = tutorial.Id;
            CurrentEncounterKind = tutorial.Kind;
            State = RunState.EncounterLoading;

            _map.Hide();

            yield return _host.EnterRoutine(tutorial);

            State = RunState.EncounterActive;
        }

        // ---------------- карта -> энкаунтер ----------------

        public bool SelectNode(int nodeId)
        {
            if (State != RunState.OnMap || CurrentRun == null || SceneLoader.Instance == null) return false;
            if (!CurrentRun.MapState.CanMoveTo(CurrentRun.Map, nodeId)) return false;

            var def = ResolveEncounter(CurrentRun.Map.GetNode(nodeId));
            return SceneLoader.Instance.RunTransition(EnterEncounterRoutine(nodeId), def?.TipsGroup);
        }

        private IEnumerator EnterEncounterRoutine(int nodeId, bool advanceMap = true)
        {
            var node = CurrentRun.Map.GetNode(nodeId);
            var def = ResolveEncounter(node);
            Debug.Log($"[RunManager] EnterEncounter: node={nodeId}, type={node.Type}, forced='{node.ForcedEncounterId}', def={(def != null ? def.Id : "NULL")}, rig={(def != null && def.RigPrefab != null)}");

            if (def == null || def.RigPrefab == null)
            {
                Debug.LogError($"[RunManager] Для узла {nodeId} нет EncounterDefinition с RigPrefab.");
                ShowMap();
                yield break;
            }

            if (advanceMap)
                CurrentRun.MapState.CompleteCurrentAndMoveTo(nodeId);

            CurrentEncounterId = def.Id;
            CurrentEncounterKind = def.Kind;
            State = RunState.EncounterLoading;
            _map.Hide();

            Debug.Log($"[RunManager] Вызываем _host.EnterRoutine({def.Id})...");
            
            // Subscribe to exit request for shop encounters
            if (def.Kind == EncounterKind.Shop && _host != null)
            {
                _host.OnExitRequested += OnShopExitRequested;
            }
            
            yield return _host.EnterRoutine(def);
            
            // Unsubscribe
            if (_host != null)
            {
                _host.OnExitRequested -= OnShopExitRequested;
            }
            
            Debug.Log("[RunManager] _host.EnterRoutine завершён.");

            State = RunState.EncounterActive;
        }

        private void OnShopExitRequested()
        {
            // Called when player exits shop - return to map
            StartCoroutine(ReturnToMapRoutine());
        }

        private EncounterDefinition ResolveEncounter(MapNode node)
        {
            var catalog = Catalog;
            if (catalog == null)
            {
                Debug.LogError($"[RunManager] Нет EncounterCatalog в Resources/{EncounterCatalog.ResourcesPath}.");
                return null;
            }

            if (!string.IsNullOrEmpty(node.ForcedEncounterId) && catalog.TryGet(node.ForcedEncounterId, out var forced))
                return forced;

            // Детерминированно: повторный вход в узел (загрузка чекпоинта) даёт того же врага.
            var rng = new SeededRandomSource(unchecked(CurrentRun.Map.Seed * 397 + node.Id));

            var kind = node.Type switch
            {
                MapNodeType.Boss => EncounterKind.Boss,
                MapNodeType.Shop => EncounterKind.Shop,
                MapNodeType.Tutorial => EncounterKind.Tutorial,
                MapNodeType.Challenge => EncounterKind.MiniBoss,
                _ => EncounterKind.Enemy
            };
            var result = catalog.PickRandom(kind, rng) ?? catalog.PickRandom(EncounterKind.Enemy, rng);
            Debug.Log($"[RunManager] ResolveEncounter: node={node.Id}, type={node.Type}, kind={kind}, result={(result != null ? result.Id : "NULL")}");
            return result;
        }

        // ---------------- энкаунтер -> карта (вызывает SceneLoader, экран чёрный) ----------------

        public IEnumerator ReturnToMapRoutine()
        {
            yield return _host.ExitRoutine();
            ShowMap();
            SaveMapCheckpoint();
        }

        private void ShowMap()
        {
            if (_map == null) return;
            _map.Show(CurrentRun.Map, CurrentRun.MapState);
            State = RunState.OnMap;
        }

        // ---------------- чекпоинт ----------------
        // Типы чекпоинта указаны полными именами: в проекте есть RunCheckpointData
        // в глобальном namespace, и он перекрывает using ShellGame.Meta.

        public void FillCheckpoint(ShellGame.Meta.RunCheckpointData data)
        {
            if (!HasActiveRun) return;

            data.HasRunData = true;
            data.OnMap = State == RunState.OnMap;
            data.RunSeed = CurrentRun.Map.Seed;
            data.RunIsFirstRun = CurrentRun.IsFirstRun;
            data.MapCurrentNodeId = CurrentRun.MapState.CurrentNodeId;
            data.MapCompletedNodeIds = new List<int>(CurrentRun.MapState.CompletedNodeIds);
        }

        private void SaveMapCheckpoint()
        {
            var p = GameSessionProgression.Instance;
            var data = new ShellGame.Meta.RunCheckpointData
            {
                SceneName = GameSceneName,
                LevelIndex = p != null ? p.CurrentLevelIndex : 0,
                DifficultyIndex = p != null ? p.CurrentDifficultyIndex : 0f,
                CompletedRoundsInSession = p != null ? p.CompletedRoundsInSession : 0,
                EncountersClearedInRun = p != null ? p.EncountersClearedInRun : 0,
                RoundsInCurrentEncounter = p != null ? p.RoundsInCurrentEncounter : 0,
            };
            FillCheckpoint(data);
            ShellGame.Meta.RunCheckpointStorage.Save(data);
        }

        public void RestoreFromCheckpoint(ShellGame.Meta.RunCheckpointData data)
        {
            var prefix = data.RunIsFirstRun ? FirstRunPrefix : ReturningPlayerPrefix;
            var map = FirstRunMapFactory.BuildFirstRun(prefix, data.RunSeed, new MapGenerationConfig());

            var playerInventory = CreateRuntimeInventory();

            // Restore coins
            playerInventory.Coins = data.PlayerCoins;
            
            // Restore items - we need a resolver function
            // The resolver will be set up when ItemSpawner restores
            // For now, store the checkpoint data to be applied later
            playerInventory.RestoreFromCheckpoint(data.PlayerItems, data.EnemyItems, name => 
            {
                // This will be properly resolved when ItemSpawner restores
                // For now, return null - items will be restored by ItemSpawner
                return null;
            });

            CurrentRun = new RunData
            {
                Map = map,
                MapState = new MapState(data.MapCurrentNodeId)
                {
                    CompletedNodeIds = new List<int>(data.MapCompletedNodeIds)
                },
                IsFirstRun = data.RunIsFirstRun,
                PlayerInventory = playerInventory
            };

            _resumeEncounterOnLoad = !data.OnMap;
            State = data.OnMap ? RunState.OnMap : RunState.EncounterActive;

            var p = GameSessionProgression.Instance;
            if (p != null)
            {
                p.SetCurrentLevelIndex(data.LevelIndex);
                p.SetDifficultyIndex(data.DifficultyIndex);
                p.SetCompletedRounds(data.CompletedRoundsInSession);
                p.SetMaxShellsPenalty(data.OnMap ? 0 : data.MaxShellsPenalty);

                // Совместимость со старыми чекпоинтами: если прогресс рана не
                // сохранён, восстанавливаем его по индексу уровня.
                int encounters = data.EncountersClearedInRun > 0
                    ? data.EncountersClearedInRun
                    : Mathf.Max(0, data.LevelIndex);
                p.SetEncountersClearedInRun(encounters);
                p.SetRoundsInCurrentEncounter(data.RoundsInCurrentEncounter);
            }
        }
    }
}