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
            new GameObject(nameof(RunManager)).AddComponent<RunManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private EncounterCatalog Catalog =>
            _catalog != null ? _catalog : (_catalog = Resources.Load<EncounterCatalog>(EncounterCatalog.ResourcesPath));

        public void RegisterMap(MapSceneController map) => _map = map;
        public void RegisterHost(EncounterHost host) => _host = host;

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

        public const string PlayerInventoryConfigResourcePath = "Configs/PlayerInventory";

        private static PlayerInventorySO CreateRuntimeInventory()
        {
            var playerInventory = ScriptableObject.CreateInstance<PlayerInventorySO>();
            playerInventory.name = "PlayerInventory_Runtime";

            var template = Resources.Load<PlayerInventorySO>(PlayerInventoryConfigResourcePath);
            if (template != null)
                playerInventory.ApplyStartingSettings(template);

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

        public void NotifyEncounterFinished(TurnSide sideThatDied)
        {
            if (State == RunState.EncounterActive && sideThatDied == TurnSide.Enemy)
                State = RunState.EncounterCleared;
        }

        public static IEnumerator PostSceneLoad()
        {
            if (Instance == null) yield break;
            yield return Instance.PostSceneLoadRoutine();
        }

        public IEnumerator PostSceneLoadRoutine()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            if (sceneName != GameSceneName) yield break;

            yield return null;

            if (_map == null || _host == null) yield break;

            if (!HasActiveRun)
                StartNewRun();

            var current = CurrentRun.Map.GetNode(CurrentRun.MapState.CurrentNodeId);

            if (_resumeEncounterOnLoad)
            {
                _resumeEncounterOnLoad = false;
                yield return EnterEncounterRoutine(current.Id, advanceMap: false);
                yield break;
            }

            bool tutorialDone = GameManager.IsTutorialCompleted();
            bool needsTutorial = !tutorialDone && current.Type == MapNodeType.Start;

            if (needsTutorial)
            {
                yield return EnterTutorialRoutine();
                yield break;
            }

            ShowMap();
        }

        private IEnumerator EnterTutorialRoutine()
        {
            var tutorial = Catalog?.GetTutorial();
            if (tutorial == null || tutorial.RigPrefab == null) yield break;

            CurrentEncounterId = tutorial.Id;
            CurrentEncounterKind = tutorial.Kind;
            State = RunState.EncounterLoading;

            if (_map != null)
                yield return _map.HideRoutine(animate: false);

            yield return _host.EnterRoutine(tutorial);

            State = RunState.EncounterActive;
        }

        public bool SelectNode(int nodeId)
        {
            if (State != RunState.OnMap || CurrentRun == null) return false;
            if (!CurrentRun.MapState.CanMoveTo(CurrentRun.Map, nodeId)) return false;

            StartCoroutine(EnterEncounterRoutine(nodeId));
            return true;
        }

        private IEnumerator EnterEncounterRoutine(int nodeId, bool advanceMap = true)
        {
            var node = CurrentRun.Map.GetNode(nodeId);
            var def = ResolveEncounter(node);

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

            // Карта улетает вверх в потолок
            if (_map != null)
                yield return _map.HideRoutine(animate: true);

            if (def.Kind == EncounterKind.Shop && _host != null)
            {
                _host.OnExitRequested += OnShopExitRequested;
            }

            // Новая комната опускается сверху на стол
            yield return _host.EnterRoutine(def);

            if (_host != null)
            {
                _host.OnExitRequested -= OnShopExitRequested;
            }

            State = RunState.EncounterActive;
        }

        private void OnShopExitRequested()
        {
            StartCoroutine(ReturnToMapRoutine());
        }

        private EncounterDefinition ResolveEncounter(MapNode node)
        {
            var catalog = Catalog;
            if (catalog == null) return null;

            if (!string.IsNullOrEmpty(node.ForcedEncounterId) && catalog.TryGet(node.ForcedEncounterId, out var forced))
                return forced;

            var rng = new SeededRandomSource(unchecked(CurrentRun.Map.Seed * 397 + node.Id));
            var kind = node.Type switch
            {
                MapNodeType.Boss => EncounterKind.Boss,
                MapNodeType.Shop => EncounterKind.Shop,
                MapNodeType.Tutorial => EncounterKind.Tutorial,
                MapNodeType.Challenge => EncounterKind.MiniBoss,
                _ => EncounterKind.Enemy
            };

            return catalog.PickRandom(kind, rng) ?? catalog.PickRandom(EncounterKind.Enemy, rng);
        }

        public IEnumerator ReturnToMapRoutine()
        {
            // Старая комната энкаунтера улетает вверх
            yield return _host.ExitRoutine();

            // Карта опускается сверху вниз
            if (_map != null)
                yield return _map.ShowRoutine(CurrentRun.Map, CurrentRun.MapState, animate: true);
            else
                ShowMap();

            State = RunState.OnMap;
            SaveMapCheckpoint();
        }

        private void ShowMap()
        {
            if (_map == null) return;
            _map.Show(CurrentRun.Map, CurrentRun.MapState, animate: true);
            State = RunState.OnMap;
        }

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
            playerInventory.Coins = data.PlayerCoins;
            playerInventory.RestoreFromCheckpoint(data.PlayerItems, data.EnemyItems, name => null);

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

                int encounters = data.EncountersClearedInRun > 0
                    ? data.EncountersClearedInRun
                    : Mathf.Max(0, data.LevelIndex);
                p.SetEncountersClearedInRun(encounters);
                p.SetRoundsInCurrentEncounter(data.RoundsInCurrentEncounter);
            }
        }
    }
}