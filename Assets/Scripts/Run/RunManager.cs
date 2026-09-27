using System.Collections.Generic;
using UnityEngine;
using ShellGame.Map;
using ShellGame.Core;

namespace ShellGame.Run
{
    /// <summary>
    /// DDOL-синглтон. Отвечает ТОЛЬКО за состояние карты текущего рана —
    /// какой узел текущий, куда вести дальше. НЕ грузит сцены сам: момент
    /// загрузки решает вызывающий код (TutorialScenarioManager ждёт финальные
    /// реплики, обычный SceneLoader — свою обычную логику смерти). RunManager
    /// только отвечает на вопрос "куда" через ConsumePendingNextScene().
    ///
    /// Сцены для сгенерированных (не заскриптованных) узлов карты пока не
    /// сопоставлены — EncounterScenes содержит только "Fish"/"Wrath" для
    /// первых двух узлов. Дальше по карте ConsumePendingNextScene() будет
    /// возвращать null и логировать предупреждение, пока не появится
    /// IEncounterResolver/EncounterRegistry (шаг 6 исходного плана).
    /// </summary>
    public sealed class RunManager : MonoBehaviour
    {
        private static RunManager _instance;
        public static RunManager Instance => _instance;

        private static readonly string[] FirstRunPrefix = { "Fish", "Wrath" };

        // Сопоставление ForcedEncounterId → сцена. Пока только то, что нужно
        // для скриптованного префикса первого рана.
        private static readonly Dictionary<string, string> EncounterScenes = new()
        {
            { "Fish", "Tutorial" },
            { "Wrath", "Level_1" },
        };

        public RunData CurrentRun { get; private set; }
        public RunState State { get; private set; } = RunState.None;

        private string _pendingNextScene;

        public static void EnsureExists()
        {
            if (_instance != null) return;

            var go = new GameObject(nameof(RunManager));
            _instance = go.AddComponent<RunManager>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Вызывать в точке старта новой попытки (где сейчас дергается
        /// GameManager.GetNewRunSceneName() — вероятно MainMenuController, его
        /// код я ещё не видел). Возвращает сцену первого узла, если RunManager
        /// взял управление, иначе null — тогда решает старый код.
        /// </summary>
        public string StartNewRun()
        {
            bool isFirstEverRun = !ShellGame.Gameplay.GameManager.IsTutorialCompleted();

            if (!isFirstEverRun)
            {
                Debug.LogWarning("[RunManager] Процедурный ран для НЕ-первого прохождения ещё не реализован — управление остаётся у старого флоу.");
                CurrentRun = null;
                State = RunState.None;
                return null;
            }

            int seed = Random.Range(int.MinValue, int.MaxValue);
            var config = new MapGenerationConfig(); // TODO: заменить на SO-конфиг, когда появится

            var map = FirstRunMapFactory.BuildFirstRun(FirstRunPrefix, seed, config);
            CurrentRun = new RunData
            {
                Map = map,
                MapState = new MapState(map.StartNode.Id),
                IsFirstRun = true
            };

            State = RunState.EncounterActive;
            _pendingNextScene = null;
            return SceneFor(CurrentRun.MapState.CurrentNodeId);
        }

        /// <summary>
        /// Вызывается из GameManager.OnSideDied. НЕ грузит сцену — только
        /// продвигает состояние карты и запоминает, куда грузить дальше.
        /// Если RunManager не ведёт ран (CurrentRun == null) — ничего не
        /// делает, старое поведение отрабатывает как раньше.
        /// </summary>
        public void NotifyEncounterFinished(TurnSide sideThatDied)
        {
            if (CurrentRun == null || State != RunState.EncounterActive)
                return;

            bool playerWon = sideThatDied == TurnSide.Enemy;

            if (!playerWon)
            {
                Debug.Log("[RunManager] Игрок погиб — смерть игрока пока не имеет отдельной логики здесь, отдаю старому флоу.");
                State = RunState.RunEnded;
                CurrentRun = null;
                return;
            }

            var currentNode = CurrentRun.Map.GetNode(CurrentRun.MapState.CurrentNodeId);
            if (currentNode.Connections.Length == 0)
            {
                Debug.Log("[RunManager] Карта пройдена до конца — отдаю управление существующему флоу.");
                State = RunState.RunEnded;
                CurrentRun = null;
                return;
            }

            int nextNodeId = currentNode.Connections[0]; // на первых двух узлах путь линейный
            CurrentRun.MapState.CompleteCurrentAndMoveTo(nextNodeId);

            string nextScene = SceneFor(nextNodeId);
            if (string.IsNullOrEmpty(nextScene))
                Debug.LogWarning($"[RunManager] Нет сцены для узла {nextNodeId} — этот участок карты ещё не подключён к сценам.");

            _pendingNextScene = nextScene;
            State = RunState.AwaitingSceneLoad;
        }

        /// <summary>
        /// Возвращает сцену, куда нужно перейти после последнего
        /// NotifyEncounterFinished, и сбрасывает её. Вызывается тем кодом,
        /// который реально решает МОМЕНТ перехода (TutorialScenarioManager
        /// после финальных реплик, обычный SceneLoader — сразу). Возвращает
        /// null, если RunManager не ведёт ран или сцена для узла ещё не
        /// подключена — в этом случае вызывающий код должен использовать свой
        /// дефолт (как _nextSceneName в TutorialScenarioManager).
        /// </summary>
        public string ConsumePendingNextScene()
        {
            var scene = _pendingNextScene;
            _pendingNextScene = null;

            if (!string.IsNullOrEmpty(scene))
                State = RunState.EncounterActive;

            return scene;
        }

        private string SceneFor(int nodeId)
        {
            var node = CurrentRun.Map.GetNode(nodeId);
            if (string.IsNullOrEmpty(node.ForcedEncounterId))
                return null;

            return EncounterScenes.TryGetValue(node.ForcedEncounterId, out var scene) ? scene : null;
        }
    }
}