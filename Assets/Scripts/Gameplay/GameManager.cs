using System.Collections;
using System.Collections.Generic;
using ShellGame.AI;
using ShellGame.Core;
using ShellGame.Dialogue;
using ShellGame.Feedback;
using ShellGame.Health;
using ShellGame.Items;
using ShellGame.Meta;
using ShellGame.Shells;
using ShellGame.Run;
using FMODUnity;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

// Явные алиасы исключают коллизии с одноимёнными типами из глобального namespace
using RunCheckpointData = ShellGame.Meta.RunCheckpointData;
using ShellCheckpointData = ShellGame.Meta.ShellCheckpointData;
using RunCheckpointStorage = ShellGame.Meta.RunCheckpointStorage;

namespace ShellGame.Gameplay
{
    public sealed class GameManager : MonoBehaviour
    {
        public const string TutorialCompletedPrefKey = "ShellGame.TutorialCompleted";

        /// <summary>Игрок хотя бы раз загрузился на втором уровне (levelIndex >= 2) — с этого момента на первом уровне должны появляться предметы.</summary>
        public const string FirstLevelItemsUnlockedPrefKey = "ShellGame.FirstLevelItemsUnlocked";

        [HideInInspector] private RoundGenerator _roundGenerator;
        [HideInInspector] private RoundInputSystem _inputSystem;
        [HideInInspector] private ShuffleSystem _shuffleSystem;
        [HideInInspector] private HealthController _healthController;
        [HideInInspector] private EnemyAIController _enemyAI;
        [HideInInspector] private RoundStartButton _roundStartButton;
        [Header("Урон")]
        [Tooltip("Урон дозой (поднят наперсток с меткой) всегда идёт через укол иглой: " +
                 "анимацию запускает HealthController, а урон внутри неё списывает " +
                 "событие анимации. Нож и молоток иглу не запускают. " +
                 "Задержка возврата хода — в HealthProgressionConfig.")]
        [SerializeField] private HealthProgressionConfig _healthProgressionConfig;
        [HideInInspector] private TurnIndicatorController _turnIndicator;
        [HideInInspector] private PlayerItemSpawner _playerItemSpawner;
        [HideInInspector] private EnemyItemSpawner _enemyItemSpawner;
        [HideInInspector] private EnemyLookController _enemyLookController;
        [HideInInspector] private EncounterRig _currentRig;
        [SerializeField] private TurnSide _startingSide = TurnSide.Player;

        [SerializeField] private int _levelIndex = 0;
        [SerializeField] private int _roundIndex = 0;
        [SerializeField] private float _spawnPauseDuration = 0.45f;
        [SerializeField] private float _revealHoldDuration = 0.75f;
        [SerializeField] private float _roundEndDelay = 0.5f;
        [SerializeField] private float _shuffleDelay = 0.15f;

        [SerializeField, Tooltip("true в сцене Game: уровень запускает EncounterHost. false — старое поведение (отдельная тестовая сцена).")]
        private bool _manualStart = false;

        private string _encounterId;
        private bool _encounterIsTutorial;
        private bool _hasEncounterContext;


        private RoundState _state = RoundState.Idle;
        private RoundParameters _currentParameters;
        private Shell _selectedShell;
        private TurnSide _activeSide;
        private int _healthInitializedForLevel = -1;
        private int _turnsCompletedInCurrentRound;
        private int _completedRoundsInSession;
        private bool _roundLayoutGenerated;
        private bool _firstRoundReadyWaited;
        private bool _tutorialRevealPaused;
        private bool _tutorialPlayerChoiceLocked;
        private bool _skipEnemyTurn;
        private int _enemySlowItemChoicesRemaining;

        /// <summary>
        /// Не null между Start() и моментом, когда Generate-кейс реально
        /// применит восстановление (EnsureHealthInitializedForLevel должна
        /// успеть отработать первой, иначе она перетрёт восстановленное HP
        /// свежими нулями) — см. ApplyCheckpointRestore.
        /// </summary>
        private ShellGame.Meta.RunCheckpointData _pendingCheckpointRestore;

        private readonly Dictionary<TurnSide, bool> _extraTurnRequested = new Dictionary<TurnSide, bool>
        {
            { TurnSide.Player, false },
            { TurnSide.Enemy, false },
        };

        private readonly Dictionary<TurnSide, int> _extraTurnCooldown = new Dictionary<TurnSide, int>
        {
            { TurnSide.Player, 0 },
            { TurnSide.Enemy, 0 },
        };

        private bool _tutorialBeforeDamagePaused;
        private bool _tutorialAfterDamagePaused;
        private bool _tutorialGameplayPaused;
        private bool _initiativeAnimationPending;

        private float _activeGameSpeedMultiplier = 1f;
        private bool _gameSpeedEffectActive;
        private TurnSide _gameSpeedEffectOwner;
        private Coroutine _gameSpeedTransition;

        // Таблетки (SlowShuffle) у игрока: замедление живёт только на ходе
        // игрока и только на этапе перемешивания, а заканчивается сразу после
        // его выбора напертка. Если таблетку взяли ПОСЛЕ своего перемешивания,
        // эффект не включается в этом раунде вовсе, а ждёт следующего хода
        // игрока — поэтому он хранится отдельно как «отложенный».
        private bool _slowdownPendingForPlayer;
        private float _pendingSlowdownFactor = 2f;

        /// <summary>Перемешивание игрока в текущем ходу уже закончилось.</summary>
        private bool _playerShuffleCompleted;

        private const float MinGameSpeed = 0.5f;
        private const float GameSpeedTransitionDuration = 1f;

        private GameSessionProgression _sessionProgression;

        /// <summary>
        /// Из контейнера приходит только то, что постоянно лежит в сцене.
        /// Всё, что живёт в EncounterRig, выдаёт EncounterHost через BindRig.
        /// </summary>
        [Inject]
        private void InjectDependencies(
            RoundInputSystem inputSystem,
            GameSessionProgression sessionProgression)
        {
            _inputSystem = inputSystem;
            _sessionProgression = sessionProgression;
        }

        /// <summary>
        /// Привязывает системы стола текущего рига (null — когда рига нет:
        /// карта, магазин, выход из энкаунтера). Вызывается EncounterHost до
        /// BeginEncounter и при выходе.
        /// </summary>
        public void BindRig(EncounterRig rig)
        {
            _currentRig = rig;
            _roundGenerator = rig != null ? rig.RoundGenerator : null;
            _shuffleSystem = rig != null ? rig.ShuffleSystem : null;
            _healthController = rig != null ? rig.Health : null;
            _enemyAI = rig != null ? rig.EnemyAI : null;
            _roundStartButton = rig != null ? rig.RoundStartButton : null;
            _playerItemSpawner = rig != null ? rig.PlayerItemSpawner : null;
            _enemyItemSpawner = rig != null ? rig.EnemyItemSpawner : null;
            _turnIndicator = rig != null ? rig.TurnIndicator : null;

            _inputSystem?.SetRoundStartButton(_roundStartButton);
        }

        /// <summary>
        /// Выдаёт GameManager указатель хода отдельно от BindRig
        /// (null — когда рига нет).
        /// </summary>
        public void SetTurnIndicator(TurnIndicatorController turnIndicator)
        {
            _turnIndicator = turnIndicator;
        }

        private readonly Dictionary<TurnSide, int> _nextHitMultiplier = new Dictionary<TurnSide, int>
        {
            { TurnSide.Player, 1 },
            { TurnSide.Enemy, 1 },
        };

        private readonly Dictionary<TurnSide, float> _nextShuffleDurationMultiplier = new Dictionary<TurnSide, float>
        {
            { TurnSide.Player, 1f },
            { TurnSide.Enemy, 1f },
        };

        public RoundState State => _state;
        public TurnSide ActiveSide => _activeSide;

        private bool IsTutorialScene()
        {
            if (_hasEncounterContext) return _encounterIsTutorial;

            var currentSceneName = SceneManager.GetActiveScene().name;
            return currentSceneName.Equals("Tutorial", System.StringComparison.OrdinalIgnoreCase)
                || currentSceneName.Contains("Tutorial", System.StringComparison.OrdinalIgnoreCase)
                || currentSceneName.Contains("Level0", System.StringComparison.OrdinalIgnoreCase)
                || currentSceneName.Contains("Level_0", System.StringComparison.OrdinalIgnoreCase);
        }

        private bool IsTutorialActive() => IsTutorialScene() && !IsTutorialCompleted();

        public static bool IsTutorialCompleted() =>
            PlayerPrefs.GetInt(TutorialCompletedPrefKey, 0) == 1;

        public const string TutorialSceneName = "Tutorial";
        public const string FirstGameplaySceneName = "Level_1";

        /// <summary>
        /// Сцена, с которой должна начинаться новая попытка или рестарт:
        /// обучение пройдено — первый игровой уровень, не пройдено — обучение.
        /// Единая точка для MainMenuController, PauseController и SceneLoader,
        /// чтобы «новая попытка» и «рестарт» не разъезжались.
        /// Имена сцен проверяются на загрузочность: пустая/битая/переименованная
        /// строка откатывается на встроенный дефолт, а не грузит «никуда».
        /// </summary>
        public static string GetNewRunSceneName(string tutorialScene = null, string firstGameplayScene = null)
        {
            string tutorial = IsLoadableSceneName(tutorialScene) ? tutorialScene : TutorialSceneName;
            string firstGameplay = IsLoadableSceneName(firstGameplayScene) ? firstGameplayScene : FirstGameplaySceneName;
            return IsTutorialCompleted() ? firstGameplay : tutorial;
        }

        private static bool IsLoadableSceneName(string sceneName) =>
            !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);

        /// <summary>Игрок хотя бы раз загрузился на втором уровне — первый уровень больше не "пустой" и предметы на нём должны спавниться.</summary>
        public static bool AreFirstLevelItemsUnlocked() =>
            PlayerPrefs.GetInt(FirstLevelItemsUnlockedPrefKey, 0) == 1;

        private static void MarkFirstLevelItemsUnlocked()
        {
            if (AreFirstLevelItemsUnlocked())
                return;

            PlayerPrefs.SetInt(FirstLevelItemsUnlockedPrefKey, 1);
            PlayerPrefs.Save();
        }

        public void Initialize(
            RoundGenerator roundGenerator,
            RoundInputSystem inputSystem,
            ShuffleSystem shuffleSystem,
            HealthController healthController,
            EnemyAIController enemyAI,
            RoundStartButton roundStartButton,
            HealthProgressionConfig healthProgressionConfig,
            TurnSide startingSide,
            TurnIndicatorController turnIndicator)
        {
            _roundGenerator = roundGenerator;
            _inputSystem = inputSystem;
            _shuffleSystem = shuffleSystem;
            _healthController = healthController;
            _enemyAI = enemyAI;
            _roundStartButton = roundStartButton;
            _healthProgressionConfig = healthProgressionConfig;
            _startingSide = startingSide;
            _turnIndicator = turnIndicator;
        }

        private void Start()
        {
            if (_manualStart) return;

            bool tutorialByName = SceneManager.GetActiveScene().name.Contains("Tutorial", System.StringComparison.OrdinalIgnoreCase);
            BeginEncounter(SceneManager.GetActiveScene().name, tutorialByName);
        }

        public void StopEncounter()
        {
            StopAllCoroutines();
            _gameSpeedTransition = null;
            _state = RoundState.Idle;
        }

        private void ResetEncounterState()
        {
            _state = RoundState.Idle;
            _roundIndex = 0;
            _selectedShell = null;
            _healthInitializedForLevel = -1;   // HealthController.Initialize отработает заново
            _turnsCompletedInCurrentRound = 0;
            _roundLayoutGenerated = false;
            _firstRoundReadyWaited = false;
            _tutorialRevealPaused = false;
            _tutorialPlayerChoiceLocked = false;
            _tutorialBeforeDamagePaused = false;
            _tutorialAfterDamagePaused = false;
            _tutorialGameplayPaused = false;
            _skipEnemyTurn = false;
            _enemySlowItemChoicesRemaining = 0;
            _initiativeAnimationPending = false;
            _playerShuffleCompleted = false;
            _pendingCheckpointRestore = null;

            // Time.timeScale не трогаем: под чёрным экраном им управляет SceneLoader.
            _slowdownPendingForPlayer = false;
            _gameSpeedEffectActive = false;
            _activeGameSpeedMultiplier = 1f;

            foreach (var side in new[] { TurnSide.Player, TurnSide.Enemy })
            {
                _extraTurnRequested[side] = false;
                _extraTurnCooldown[side] = 0;
                _nextHitMultiplier[side] = 1;
                _nextShuffleDurationMultiplier[side] = 1f;
            }
        }

        public void StartRound()
        {
            if (_roundGenerator == null || _inputSystem == null || _shuffleSystem == null) return;
            _turnsCompletedInCurrentRound = 0;
            _roundLayoutGenerated = false;
            _state = RoundState.Generate;
            StartCoroutine(RunRoundRoutine());
        }

        /// <summary>
        /// Применяет отложенное восстановление из чекпоинта — вызывается из
        /// Generate-кейса СРАЗУ ПОСЛЕ EnsureHealthInitializedForLevel(),
        /// потому что та инициализирует HP свежими нулями и должна успеть
        /// отработать первой, иначе перетрёт восстановленные значения.
        /// </summary>
        private void ApplyCheckpointRestore(ShellGame.Meta.RunCheckpointData data)
        {
            _healthController?.RestoreState(TurnSide.Player, data.PlayerHealth, data.PlayerMaxHealth);
            _healthController?.RestoreState(TurnSide.Enemy, data.EnemyHealth, data.EnemyMaxHealth);
            _healthInitializedForLevel = _levelIndex;

            // Restore player items via PlayerItemSpawner
            _playerItemSpawner?.RestoreFromCheckpoint(data.PlayerItems);
            // Enemy items are also restored via PlayerItemSpawner since they're in PlayerInventorySO
            // But we also need to spawn them visually for the enemy side
            _enemyItemSpawner?.RestoreFromCheckpoint(data.EnemyItems);

            // Restore coins to PlayerInventorySO
            var runManager = ShellGame.Run.RunManager.Instance;
            if (runManager != null && runManager.CurrentRun != null && runManager.CurrentRun.PlayerInventory != null)
            {
                runManager.CurrentRun.PlayerInventory.Coins = data.PlayerCoins;
            }

            // Restore coin piles
            var coinPileController = CoinPileController.Instance;
            if (coinPileController != null)
            {
                coinPileController.RestoreFromCheckpoint(data.CoinPiles);
            }

            // Чекпоинт куч мог не попасть в данные — тогда визуал должен догнать счётчик Coins.
            SyncCoinPileWithWallet();

            if (_roundGenerator != null)
                _currentParameters = _roundGenerator.RestoreRound(data.Shells, data.DifficultyIndex, _levelIndex, _roundIndex);

            _roundLayoutGenerated = true;
            _turnsCompletedInCurrentRound = 0;
            _initiativeAnimationPending = false;
        }

        public void BeginEncounter(string encounterId, bool isTutorial)
        {
            StopAllCoroutines();
            ResetEncounterState();

            _encounterId = encounterId;
            _encounterIsTutorial = isTutorial;
            _hasEncounterContext = true;

            if (_sessionProgression == null)
            {
                var progressionObject = new GameObject("GameSessionProgression");
                _sessionProgression = progressionObject.AddComponent<GameSessionProgression>();
            }

            RunStatsTracker.EnsureExists();
            ShellGame.Run.RunManager.EnsureExists();
            RunStatsTracker.Instance?.EnsureRunStarted();

            bool wantsRestore = _sessionProgression.PendingContinueFromCheckpoint;
            _sessionProgression.PendingContinueFromCheckpoint = false;

            ShellGame.Meta.RunCheckpointData checkpoint = null;
            if (wantsRestore)
            {
                checkpoint = ShellGame.Meta.RunCheckpointStorage.Load();
                if (checkpoint == null || !string.Equals(checkpoint.SceneName, _encounterId, System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning("[GameManager] Запрошено продолжение, но чекпоинт отсутствует/не для этого энкаунтера — стартую как обычно.");
                    checkpoint = null;
                }
            }

            if (checkpoint != null)
            {
                _completedRoundsInSession = checkpoint.CompletedRoundsInSession;
                _levelIndex = checkpoint.LevelIndex;
                _activeSide = checkpoint.ActiveSide;

                _sessionProgression.SetCurrentLevelIndex(_levelIndex);
                _sessionProgression.SetDifficultyIndex(checkpoint.DifficultyIndex);
                _sessionProgression.SetCompletedRounds(checkpoint.CompletedRoundsInSession);
                _sessionProgression.SetMaxShellsPenalty(checkpoint.MaxShellsPenalty);

                // Старые чекпоинты не содержат EncountersClearedInRun — JsonUtility
                // отдаёт 0. Тогда восстанавливаем прогресс по индексу уровня,
                // чтобы продолжение не сбросило сложность в начало рана.
                int encounters = checkpoint.EncountersClearedInRun > 0
                    ? checkpoint.EncountersClearedInRun
                    : Mathf.Max(0, checkpoint.LevelIndex);
                _sessionProgression.SetEncountersClearedInRun(encounters);
                _sessionProgression.SetRoundsInCurrentEncounter(checkpoint.RoundsInCurrentEncounter);

                // «Продолжить» должно вернуть ту же сложность, что была выбрана.
                _sessionProgression.SetSelectedPresetById(checkpoint.DifficultyPresetId);

                _pendingCheckpointRestore = checkpoint;
            }
            else
            {
                _completedRoundsInSession = _sessionProgression.CompletedRoundsInSession;

                if (isTutorial)
                {
                    _completedRoundsInSession = 0;
                    _levelIndex = 0;
                    if (!IsTutorialCompleted())
                        _tutorialPlayerChoiceLocked = true;
                }
                else
                {
                    // Один GameManager на все энкаунтеры: уровень берём из прогрессии,
                    // а не из поля в инспекторе (у вернувшегося игрока там 0 → минимум 1).
                    _levelIndex = Mathf.Max(1, _sessionProgression.CurrentLevelIndex);
                }

                _sessionProgression.SetCurrentLevelIndex(_levelIndex);
                _activeSide = _startingSide;
            }

            _turnIndicator?.SetImmediate(_activeSide);

            if (_levelIndex >= 2)
                MarkFirstLevelItemsUnlocked();

            // Предметы: не в обучении, и на уровне 1 только после того, как игрок хоть раз дошёл до 2.
            bool itemsOn = !IsTutorialScene() && (_levelIndex >= 2 || AreFirstLevelItemsUnlocked());
            _playerItemSpawner?.SetItemsAvailable(itemsOn);
            _enemyItemSpawner?.SetItemsAvailable(itemsOn);

            if (_roundStartButton == null) _roundStartButton = GetComponentInChildren<RoundStartButton>(true);
            if (_roundStartButton != null) _roundStartButton.Hide();

            // Физические монеты на столе — визуализация счётчика Coins в PlayerInventorySO.
            // На новом ране здесь появляются стартовые монеты, дальше только догоняет расхождения.
            SyncCoinPileWithWallet();

            StartRound();
        }

        /// <summary>
        /// Приводит количество монет на столе к значению Coins в инвентаре игрока.
        /// </summary>
        private void SyncCoinPileWithWallet()
        {
            var pile = CoinPileController.Instance;
            if (pile == null) return;

            var run = ShellGame.Run.RunManager.Instance;
            if (run == null || run.CurrentRun == null || run.CurrentRun.PlayerInventory == null) return;

            pile.SyncToCount(run.CurrentRun.PlayerInventory.Coins);
        }

        /// <summary>
        /// Сохраняет чекпоинт "начало раунда" — вызывается ровно один раз на
        /// цикл ход-игрока+ход-врага, сразу после того, как стол
        /// сгенерирован заново (см. вызов в Generate-кейсе). Перетирает
        /// предыдущий чекпоинт безусловно — это и есть требуемое "мгновенно
        /// перезаписывается на новом уровне/раунде".
        /// </summary>
        private void SaveRoundStartCheckpoint()
        {
            if (_healthController == null || _roundGenerator == null) return;

            var data = new ShellGame.Meta.RunCheckpointData
            {
                SceneName = _hasEncounterContext ? _encounterId : SceneManager.GetActiveScene().name,
                LevelIndex = _levelIndex,
                DifficultyIndex = _currentParameters.DifficultyIndex,
                CompletedRoundsInSession = _completedRoundsInSession,
                MaxShellsPenalty = _sessionProgression != null ? _sessionProgression.MaxShellsPenalty : 0,
                ActiveSide = _activeSide,
                EncountersClearedInRun = _sessionProgression != null ? _sessionProgression.EncountersClearedInRun : 0,
                RoundsInCurrentEncounter = _sessionProgression != null ? _sessionProgression.RoundsInCurrentEncounter : 0,
                DifficultyPresetId = _sessionProgression != null && _sessionProgression.SelectedPreset != null
                    ? _sessionProgression.SelectedPreset.Id
                    : null,
                PlayerHealth = _healthController.GetHealth(TurnSide.Player),
                PlayerMaxHealth = _healthController.GetMaxHealth(TurnSide.Player),
                EnemyHealth = _healthController.GetHealth(TurnSide.Enemy),
                EnemyMaxHealth = _healthController.GetMaxHealth(TurnSide.Enemy),
            };

            if (_playerItemSpawner != null)
            {
                data.PlayerItems = BuildItemStacks(_playerItemSpawner.GetOwnedItemDefinitions());
            }
            if (_enemyItemSpawner != null)
            {
                data.EnemyItems = BuildItemStacks(_enemyItemSpawner.GetOwnedItemDefinitions());
            }

            // Save coins from PlayerInventorySO
            var runManager = ShellGame.Run.RunManager.Instance;
            if (runManager != null && runManager.CurrentRun != null && runManager.CurrentRun.PlayerInventory != null)
            {
                var playerInv = runManager.CurrentRun.PlayerInventory;
                data.PlayerCoins = playerInv.Coins;
            }

            // Save coin piles
            var coinPileController = CoinPileController.Instance;
            if (coinPileController != null)
            {
                data.CoinPiles = coinPileController.GetCheckpointData();
            }

            foreach (var shell in _roundGenerator.ActiveShells)
            {
                if (shell == null) continue;
                data.Shells.Add(new ShellGame.Meta.ShellCheckpointData { SlotIndex = shell.SlotIndex, HasMarker = shell.HasMarker });
            }
            ShellGame.Run.RunManager.Instance?.FillCheckpoint(data);
            ShellGame.Meta.RunCheckpointStorage.Save(data);
        }

        private static List<ShellGame.Meta.ItemStackCheckpointData> BuildItemStacks(List<ItemDefinition> items)
        {
            var counts = new Dictionary<ItemDefinition, int>();
            foreach (var item in items)
            {
                if (item == null) continue;
                counts.TryGetValue(item, out var count);
                counts[item] = count + 1;
            }

            var result = new List<ShellGame.Meta.ItemStackCheckpointData>();
            foreach (var kvp in counts)
                result.Add(new ShellGame.Meta.ItemStackCheckpointData { ItemAssetName = kvp.Key.name, Count = kvp.Value });
            return result;
        }

        public ItemEffectContext CreateItemContext(TurnSide userSide)
        {
            return new ItemEffectContext
            {
                UserSide = userSide,
                Health = _healthController,
                ActiveShells = _roundGenerator != null ? _roundGenerator.ActiveShells : null,
                EnemyAI = _enemyAI,
                SlowGamePaceUntilNextChoice = SetGameSpeedMultiplier,
                CanSlowGamePace = () => Mathf.Approximately(_activeGameSpeedMultiplier, 1f),
                ReduceEnemyTrackingLossNextShuffle = multiplier => _enemyAI?.ReduceTrackingLossNextShuffle(multiplier),
                CanReduceEnemyTrackingLossNextShuffle = () => _enemyAI?.CanReduceTrackingLossNextShuffle() ?? false,

                CanUsePlayerMonocle = () => _activeSide == TurnSide.Player
                    && _state == RoundState.PlayerTurn
                    && !ShellPeekGate.IsPending
                    && !ShellKnifeGate.IsPending,
                BeginShellPeek = (holdDuration, onPeeked) => ShellPeekGate.Begin(holdDuration, onPeeked),

                CanUsePlayerHammer = () => _activeSide == TurnSide.Player
                    && _state == RoundState.PlayerTurn
                    && !ShellPeekGate.IsPending
                    && !ShellKnifeGate.IsPending
                    && !ShellHammerGate.IsPending,
                BeginHammerAttack = (holdDuration, onTargeted) => ShellHammerGate.Begin(holdDuration, onTargeted),
                ReduceMaxShells = () => _sessionProgression?.AddMaxShellsPenalty(1),
                RemoveShellFromPlay = shell => _roundGenerator?.RemoveShell(shell),

                CanUsePlayerKnife = () => _activeSide == TurnSide.Player
                    && _state == RoundState.PlayerTurn
                    && !ShellPeekGate.IsPending
                    && !ShellKnifeGate.IsPending,
                BeginKnifeAttack = (holdDuration, onTargeted) => ShellKnifeGate.Begin(holdDuration, onTargeted),

                CanUseEnemySlowItem = () => _enemySlowItemChoicesRemaining <= 0,
                StartEnemySlowItemCooldown = () => _enemySlowItemChoicesRemaining = 2,
                ResolveShellRevealDuration = holdDuration => _roundGenerator != null ? _roundGenerator.GetRevealDuration(holdDuration) : holdDuration,
                SkipCurrentTurn = () => _skipEnemyTurn = true,
                RequestExtraTurn = () => RequestExtraTurn(userSide),
                CanRequestExtraTurn = () => CanRequestExtraTurn(userSide),
            };
        }

        /// <summary>
        /// Открыт ли предмет игроку прямо сейчас. Ставится в ItemPickupView как
        /// фильтр ховера/клика, поэтому решает всё: и подсветку, и доступность.
        ///
        /// Фаза: предметы трогать можно только когда идёт ход (RoundState.PlayerTurn
        /// обслуживает обе стороны) либо в ожидании старта первого раунда. В
        /// Reveal/RevealResult/Cleanup/Shuffle ход ещё не идёт — кликать нельзя.
        /// Плюс блокируем ход обучения и висящие шлюзы выбора напертка (пик/нож/
        /// молоток), пока игрок занят другим выбором.
        /// </summary>
        public bool IsItemUsageAllowedNow(ItemDefinition item)
        {
            if (item == null) return false;

            if (_state != RoundState.PlayerTurn && _state != RoundState.WaitForStart)
                return false;

            if (IsTutorialActive() && _tutorialPlayerChoiceLocked)
                return false;

            if (ShellPeekGate.IsPending || ShellKnifeGate.IsPending || ShellHammerGate.IsPending)
                return false;

            switch (item.UsageWindow)
            {
                case ItemUsageWindow.AnyTurn:
                    return true;
                case ItemUsageWindow.EnemyTurnOnly:
                    return _activeSide == TurnSide.Enemy;
                default:
                    return _activeSide == TurnSide.Player;
            }
        }

        private void SetGameSpeedMultiplier(TurnSide side, float slowdownFactor)
        {
            // Не стакается: пока эффект держится, вторую дозу взять нельзя
            // (CanSlowGamePace это уже проверяет, здесь — страховка).
            if (_gameSpeedEffectActive) return;

            _pendingSlowdownFactor = slowdownFactor;

            // Таблетки включаются ТОЛЬКО на ходу игрока и ТОЛЬКО до конца его
            // перемешивания. Поэтому включаем эффект сразу лишь когда игрок
            // ещё не перемешивал в этом ходу. В остальных случаях таблетка
            // ждёт следующего хода игрока:
            //   • взята ПОСЛЕ своего перемешивания (уже выбирает наперток) —
            //     на этом раунде эффекта быть не должно;
            //   • взята на ходу ВРАГА — на ходу врага эффекта быть не должно.
            bool activatesNow = _activeSide == TurnSide.Player && !_playerShuffleCompleted;

            if (!activatesNow)
            {
                _slowdownPendingForPlayer = true;
                return;
            }

            ActivatePlayerSlowdown(slowdownFactor);
        }

        /// <summary>
        /// Собственно включение замедления. Вызывается сразу при использовании
        /// таблетки (если перемешивание впереди) и на следующем перемешивании
        /// игрока, если таблетка была взята уже после него.
        /// </summary>
        private void ActivatePlayerSlowdown(float slowdownFactor)
        {
            _slowdownPendingForPlayer = false;
            _gameSpeedEffectActive = true;
            _gameSpeedEffectOwner = TurnSide.Player;
            float targetSpeed = Mathf.Clamp(1f / Mathf.Max(1f, slowdownFactor), MinGameSpeed, 1f);
            StartGameSpeedTransition(targetSpeed);
        }

        /// <summary>
        /// Точка включения отложенного эффекта: перемешивание игрока.
        /// Таблетки «должны включаться именно когда ход игрока и этап
        /// перемешивания», поэтому позже (выбор напертка) эффект не включается.
        /// </summary>
        private void TryActivatePendingPlayerSlowdown()
        {
            if (!_slowdownPendingForPlayer || _gameSpeedEffectActive)
                return;

            ActivatePlayerSlowdown(_pendingSlowdownFactor);
        }

        private void ResetGameSpeedMultiplier()
        {
            _slowdownPendingForPlayer = false;

            if (!_gameSpeedEffectActive && Mathf.Approximately(_activeGameSpeedMultiplier, 1f))
                return;

            _gameSpeedEffectActive = false;
            StartGameSpeedTransition(1f);
        }

        /// <summary>
        /// Таблетки держат замедление ровно до выбора напертка их владельцем:
        /// после выбора эффект снимается, на ход врага он не переносится.
        /// </summary>
        private void HandleGameSpeedOwnerChoice(TurnSide side)
        {
            if (!_gameSpeedEffectActive || side != _gameSpeedEffectOwner)
                return;

            ResetGameSpeedMultiplier();
        }

        private bool CanSlowGamePace()
        {
            return !_gameSpeedEffectActive;
        }

        private void StartGameSpeedTransition(float targetSpeed)
        {
            if (_gameSpeedTransition != null)
                StopCoroutine(_gameSpeedTransition);

            _gameSpeedTransition = StartCoroutine(GameSpeedTransition(targetSpeed));
        }

        private IEnumerator GameSpeedTransition(float targetSpeed)
        {
            float startSpeed = _activeGameSpeedMultiplier;
            float elapsed = 0f;

            while (elapsed < GameSpeedTransitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / GameSpeedTransitionDuration);
                _activeGameSpeedMultiplier = Mathf.Lerp(startSpeed, targetSpeed, progress);
                ApplyGameSpeed();
                yield return null;
            }

            _activeGameSpeedMultiplier = targetSpeed;
            ApplyGameSpeed();
            _gameSpeedTransition = null;
        }

        private void ApplyGameSpeed()
        {
            if (PauseController.Instance != null && PauseController.Instance.IsPaused)
                return;

            Time.timeScale = _activeGameSpeedMultiplier;
            RuntimeManager.StudioSystem.setParameterByName("TimeScale", _activeGameSpeedMultiplier);
        }

        private void RequestExtraTurn(TurnSide side)
        {
            if (_activeSide != side || _state != RoundState.PlayerTurn)
                return;

            _extraTurnRequested[side] = true;
            _extraTurnCooldown[side] = 3;
        }

        private bool CanRequestExtraTurn(TurnSide side)
        {
            return _activeSide == side
                && _state == RoundState.PlayerTurn
                && !_extraTurnRequested[side]
                && _extraTurnCooldown[side] <= 0;
        }

        private void SetNextShuffleDurationMultiplier(TurnSide side, float multiplier)
        {
            _nextShuffleDurationMultiplier[side] = Mathf.Max(1f, multiplier);
        }

        private float ConsumeNextShuffleDurationMultiplier(TurnSide side)
        {
            if (!_nextShuffleDurationMultiplier.TryGetValue(side, out var multiplier))
                return 1f;

            _nextShuffleDurationMultiplier[side] = 1f;
            return Mathf.Max(1f, multiplier);
        }

        public void SetNextHitDamageMultiplier(TurnSide side, int multiplier)
        {
            _nextHitMultiplier[side] = Mathf.Max(1, multiplier);
        }

        private int ConsumeDamageMultiplier(TurnSide side)
        {
            if (!_nextHitMultiplier.TryGetValue(side, out var multiplier) || multiplier <= 1) return 1;
            _nextHitMultiplier[side] = 1;
            return multiplier;
        }

        private IEnumerator RunRoundRoutine()
        {
            while (true)
            {
                if (IsTutorialActive())
                {
                    while (_tutorialGameplayPaused)
                    {
                        _inputSystem?.SetEnabled(false);
                        _inputSystem?.SetItemInteractionEnabled(false);
                        yield return null;
                    }
                }

                switch (_state)
                {
                    case RoundState.Generate:
                        if (_roundGenerator == null) yield break;

                        EnsureHealthInitializedForLevel();

                        if (_pendingCheckpointRestore != null)
                        {
                            ApplyCheckpointRestore(_pendingCheckpointRestore);
                            _pendingCheckpointRestore = null;
                        }

                        if (!_firstRoundReadyWaited)
                        {
                            _state = RoundState.WaitForStart;
                            break;
                        }

                        if (!_roundLayoutGenerated)
                        {
                            // Сложность = функция позиции в ране (пройденные
                            // энкаунтеры + раунды внутри текущего боя), а не
                            // L + 0.45*(CompletedRounds + t). Старая формула
                            // копила раунды весь ран и выходила за 45 уже в
                            // первой карте.
                            float difficultyIndex = _sessionProgression != null
                                ? _sessionProgression.RecomputeDifficultyIndex()
                                : 0f;

                            _currentParameters = _roundGenerator.GenerateRound(
                                _levelIndex,
                                _roundIndex,
                                _completedRoundsInSession,
                                difficultyIndex,
                                _sessionProgression != null ? _sessionProgression.MaxShellsPenalty : 0);
                            if (_sessionProgression != null)
                            {
                                _sessionProgression.AdvanceDifficultyForRound();
                            }
                            _roundLayoutGenerated = true;

                            SaveRoundStartCheckpoint();

                            if (_roundGenerator.LayoutTransitionDuration > 0f)
                                yield return new WaitForSeconds(_roundGenerator.LayoutTransitionDuration);

                            if (!_initiativeAnimationPending)
                            {
                                if (_turnIndicator != null)
                                    _turnIndicator.ApplySide(_activeSide, _roundGenerator.ActiveShells);
                                else
                                    _roundGenerator.SetSide(_activeSide);
                            }
                        }

                        if (_initiativeAnimationPending)
                        {
                            _initiativeAnimationPending = false;
                            _state = RoundState.InitiativeAnimation;
                            break;
                        }

                        bool tutorialGate = IsTutorialScene()
                            && _completedRoundsInSession == 0
                            && !IsTutorialCompleted()
                            && !_tutorialRevealPaused;

                        if (tutorialGate)
                        {
                            _tutorialRevealPaused = true;
                            _state = RoundState.WaitForTutorialReveal;
                            break;
                        }

                        _state = RoundState.Reveal;
                        break;

                    case RoundState.WaitForTutorialReveal:
                        while (_state == RoundState.WaitForTutorialReveal) yield return null;
                        break;

                case RoundState.WaitForStart:
                    // Spawn player items
                    if (_playerItemSpawner != null)
                        yield return _playerItemSpawner.SpawnItems();
                    
                    // Spawn enemy items
                    if (_enemyItemSpawner != null)
                        yield return _enemyItemSpawner.SpawnItems();

                    // Проверяем, проходим ли мы обучение прямо сейчас
                    bool isTutorialActive = IsTutorialActive();

                    if (!isTutorialActive && _roundStartButton != null)
                    {
                        // Кнопка появляется на столе сразу вместе с предметами,
                        // но остаётся некликабельной: враг ещё не договорил
                        // вступление, и трогать её раньше времени нельзя.
                        _roundStartButton.SetInteractable(false);
                        _roundStartButton.Show();
                    }

                    // Враг может встречать игрока репликами на входе — не даём
                    // начать раунд, пока он не договорит. Кадр ждём, чтобы
                    // директор реакций (он поднимается по смене активной сцены)
                    // успел занять гейт.
                    yield return new WaitForEndOfFrame();
                    yield return EnemyReactionGate.WaitWhileBusy();

                    // Если это НЕ обучение — включаем ввод и кнопку
                    if (!isTutorialActive)
                    {
                        if (_roundStartButton != null) _roundStartButton.SetInteractable(true);
                        if (_inputSystem != null)
                        {
                            _inputSystem.SetEnabled(true);
                            _inputSystem.SetItemInteractionEnabled(true);
                        }
                    }

                    // Ждем. В обычной игре это ожидание снимет клик по кнопке, 
                    // а в туториале — наш метод SpawnCupsOnTable() через GameEvents.
                    while (_state == RoundState.WaitForStart) yield return null;

                    if (!isTutorialActive)
                    {
                        if (_roundStartButton != null) _roundStartButton.Hide();
                        if (_inputSystem != null)
                        {
                            _inputSystem.SetEnabled(false);
                            _inputSystem.SetItemInteractionEnabled(false);
                        }
                    }
                    break;
                    case RoundState.Reveal:
                        if (_roundGenerator == null) yield break;
                        yield return new WaitForSeconds(Mathf.Max(0f, _spawnPauseDuration));
                        _roundGenerator.RevealMarkers(_revealHoldDuration);
                        if (_activeSide == TurnSide.Enemy && _enemyAI != null)
                            _enemyAI.EnterObserveMarkers(
                                _roundGenerator.ActiveShells,
                                _currentParameters.DifficultyIndex,
                                _levelIndex == 0);
                        yield return new WaitForSeconds(Mathf.Max(0f, _roundGenerator.GetRevealDuration(_revealHoldDuration)));
                        _roundGenerator.HideMarkers();
                        _state = RoundState.Shuffle;
                        break;

                case RoundState.Shuffle:
                    if (_inputSystem == null || _shuffleSystem == null || _roundGenerator == null) yield break;
                    _inputSystem.SetEnabled(false);
                    _inputSystem.SetItemInteractionEnabled(false);
                    _playerShuffleCompleted = false;

                    // Таблетка, взятая на прошлом ходу игрока уже ПОСЛЕ его
                    // перемешивания, включается ровно здесь — когда ход вернулся
                    // к игроку и началось перемешивание.
                    if (_activeSide == TurnSide.Player)
                        TryActivatePendingPlayerSlowdown();
                    yield return new WaitForSeconds(_shuffleDelay);
                    if (_activeSide == TurnSide.Enemy && _enemyAI != null) _enemyAI.EnterTrackShuffle();
                    _shuffleSystem.SetMoveDurationMultiplier(ConsumeNextShuffleDurationMultiplier(_activeSide));
                    _shuffleSystem.StartShuffling(
                        _roundGenerator.GetShellsInPlayOrder(),
                        () =>
                        {
                            _shuffleSystem.ResetMoveDurationMultiplier();
                            _state = RoundState.PlayerTurn;
                        },
                        _levelIndex,
                        _roundIndex,
                        _currentParameters.DifficultyIndex,
                        _activeSide == TurnSide.Enemy,
                        _roundGenerator.ShellConfig);
                    while (_state == RoundState.Shuffle) yield return null;
                    break;

                    case RoundState.PlayerTurn:
                        if (_inputSystem == null) yield break;

                        if (_extraTurnCooldown[_activeSide] > 0)
                            _extraTurnCooldown[_activeSide]--;

                        if (_activeSide == TurnSide.Player && IsTutorialActive()
                            && _tutorialPlayerChoiceLocked)
                        {
                            while (_tutorialPlayerChoiceLocked) yield return null;
                        }

                        _selectedShell = null;
                        if (_activeSide == TurnSide.Player)
                        {
                            yield return WaitForGameplayGate();
                            _inputSystem.SetEnabled(true);
                            _inputSystem.SetItemInteractionEnabled(true);
                        }
                        else
                        {
                            // Наперток в этот момент выбирает враг, поэтому канал
                            // напертка закрыт. Но предметы, которые выбор напертка
                            // не отменяют (хилка, крест, таблетки — см.
                            // ItemDefinition.UsageWindow), игрок применить может.
                            _inputSystem.SetEnabled(false);
                            _inputSystem.SetItemInteractionEnabled(true);

                            if (_extraTurnRequested[TurnSide.Player])
                            {
                                _extraTurnRequested[TurnSide.Player] = false;
                                _activeSide = TurnSide.Player;
                                GameEvents.RaiseActiveSideChanged(_activeSide);
                                _initiativeAnimationPending = true;
                                _state = RoundState.InitiativeAnimation;
                                break;
                            }

                            _skipEnemyTurn = false;
                            float itemExtraDelay = 0f;
                            bool enemyTurnResolvedByItem = false;
                            if (_enemyItemSpawner != null)
                            {
                                var itemUseResult = new EnemyItemUseResult();
                                yield return _enemyItemSpawner.TryUseEnemyItemsRoutine(this, _currentParameters.DifficultyIndex, itemUseResult);
                                _skipEnemyTurn = itemUseResult.SkippedTurn;
                                itemExtraDelay = itemUseResult.ExtraDelaySeconds;
                                enemyTurnResolvedByItem = itemUseResult.TurnResolvedByItem;
                            }

                            if (_skipEnemyTurn)
                            {
                                _activeSide = Opposite(_activeSide);
                                GameEvents.RaiseActiveSideChanged(_activeSide);
                                _turnsCompletedInCurrentRound++;
                                _initiativeAnimationPending = true;
                                _state = _turnsCompletedInCurrentRound < 2
                                    ? RoundState.InitiativeAnimation
                                    : RoundState.Cleanup;
                                break;
                            }

                            if (itemExtraDelay > 0f)
                                yield return new WaitForSeconds(itemExtraDelay);

                            // Предмет мог заменить врагу его обычный выбор напёртка
                            // (нож бьёт по напёртку сам) — тогда ниже запускать
                            // ничего нельзя, иначе враг ударит дважды за ход. Сам
                            // выбор предмета придёт через ShellSelected и уведёт
                            // машину состояний в RevealResult сам.
                            if (_enemyAI != null && _roundGenerator != null && !enemyTurnResolvedByItem)
                            {

                                if (_healthController != null)
                                    _enemyAI.SetHealthFraction(1f - _healthController.GetDoseFraction(TurnSide.Enemy));

                                if (_enemyItemSpawner != null)
                                    yield return _enemyItemSpawner.PlayEnemyLookAtShells(_roundGenerator.ActiveShells);

                                _enemyAI.MakeDecisionAndAttack(_roundGenerator.ActiveShells, chosen => chosen.Select(TurnSide.Enemy));
                            }
                        }
                        while (_state == RoundState.PlayerTurn) yield return null;
                        break;

                    case RoundState.RevealResult:
                        if (_selectedShell == null || _roundGenerator == null)
                        {
                            _state = RoundState.Cleanup;
                            break;
                        }

                        _selectedShell.RevealResult();
                        yield return new WaitForSeconds(Mathf.Max(_roundEndDelay, _roundGenerator.GetRevealDuration()));

                        if (IsTutorialActive() && _tutorialBeforeDamagePaused)
                        {
                            while (_tutorialBeforeDamagePaused) yield return null;
                        }

                        if (_selectedShell.HasMarker && _healthController != null)
                        {
                            var damagedSide = Opposite(_activeSide);

                            // Урон дозой всегда идёт через укол иглой: анимацию
                            // запускает сам HealthController, а урон внутри укола
                            // списывает событие анимации на объекте иглы
                            // (NeedleMetalSqueak.ApplyDamage) в момент входа иглы
                            // в тело. Нож и молоток иглу не запускают — они бьют
                            // через ApplyDamage без needNeedleAnim.
                            int baseDamage = _healthProgressionConfig != null ? _healthProgressionConfig.DamagePerHit : 1;
                            int multiplier = ConsumeDamageMultiplier(_activeSide);
                            int damage = baseDamage * multiplier;

                            // Возвращаемое значение здесь игнорируется: урон
                            // отложен, поэтому смерть читаем после укола.
                            _healthController.ApplyDamage(damagedSide, damage, needNeedleAnim: true);
                            yield return _healthController.WaitForNeedleInjection();

                            if (_healthController.IsDead(damagedSide))
                            {
                                _state = RoundState.GameOver;
                                break;
                            }

                            // Ход возвращается игроку с задержкой — пауза после списания
                            // урона, чтобы реакция (виньетка, тряска) успела прочитаться.
                            float turnReturnDelay = _healthProgressionConfig != null
                                ? _healthProgressionConfig.TurnReturnDelayAfterPlayerDamage : 0f;
                            if (damagedSide == TurnSide.Player && turnReturnDelay > 0f)
                                yield return new WaitForSeconds(turnReturnDelay);
                        }

                        if (IsTutorialActive() && _tutorialAfterDamagePaused)
                        {
                            while (_tutorialAfterDamagePaused) yield return null;
                        }

                        if (IsTutorialActive())
                        {
                            while (_tutorialGameplayPaused) yield return null;
                        }

                        // Реакция врага на открытый напёрток: держим геймплей, пока
                        // он не договорит. Финальные реплики уровня держат сцену
                        // сами (SceneTransitionGate), урон к этому моменту уже списан.
                        yield return WaitForGameplayGate();

                        if (_extraTurnRequested[_activeSide])
                        {
                            _extraTurnRequested[_activeSide] = false;
                            _turnsCompletedInCurrentRound = 0;
                            _roundLayoutGenerated = false;
                            _initiativeAnimationPending = true;
                            _state = RoundState.InitiativeAnimation;
                            break;
                        }

                        // В конце хода враг поворачивает голову на игрока: наперсток
                        // уже выбран, урон списан — пусть смотрит на жертву.
                        if (_activeSide == TurnSide.Enemy)
                            LookEnemyAtPlayer();

                        _activeSide = Opposite(_activeSide);
                        GameEvents.RaiseActiveSideChanged(_activeSide);
                        _turnsCompletedInCurrentRound++;
                        _initiativeAnimationPending = true;
                        if (_turnsCompletedInCurrentRound < 2) _state = RoundState.InitiativeAnimation;
                        else _state = RoundState.Cleanup;
                        break;

                    case RoundState.Cleanup:
                        if (_roundGenerator == null || _inputSystem == null) yield break;
                        _inputSystem.SetEnabled(false);
                        _inputSystem.SetItemInteractionEnabled(false);
                        _turnsCompletedInCurrentRound = 0;
                        _roundLayoutGenerated = false;
                        _completedRoundsInSession++;
                        _sessionProgression?.IncrementCompletedRounds();
                        _roundIndex++;
                        yield return new WaitForSeconds(0.1f);
                        _state = RoundState.InitiativeAnimation;
                        break;

                    case RoundState.InitiativeAnimation:
                        if (_initiativeAnimationPending)
                        {
                            _roundLayoutGenerated = false;
                            _state = RoundState.Generate;
                            break;
                        }

                        if (_turnIndicator != null)
                        {
                            bool animationDone = false;
                            _turnIndicator.PlayTransition(_activeSide, _roundGenerator?.ActiveShells, () => animationDone = true);
                            while (!animationDone) yield return null;
                        }
                        else
                        {
                            _roundGenerator?.SetSide(_activeSide);
                        }
                        _state = RoundState.Generate;
                        break;

                    case RoundState.GameOver:
                        _roundGenerator?.ClearRound();
                        _inputSystem?.SetEnabled(false);
                        _inputSystem?.SetItemInteractionEnabled(false);
                        yield break;

                    default: yield break;
                }
            }
        }

        private void EnsureHealthInitializedForLevel()
        {
            if (_healthController == null || _healthInitializedForLevel == _levelIndex) return;

            // Здоровье теперь растёт по прогрессу РАНА (доля пройденных
            // энкаунтеров), а не по индексу уровня — при ~55 энкаунтерах
            // старая шкала уровней давала бы запредельные 118 HP на боссе.
            float runProgress = _sessionProgression != null && _sessionProgression.RunDifficulty != null
                ? _sessionProgression.RunDifficulty.EvaluateRunProgress01(_sessionProgression.EncountersClearedInRun)
                : Mathf.Clamp01(_levelIndex / 12f);

            // Пресет сложности задаёт статичное HP на весь ран. Если каталога/
            // пресета нет — работает прежняя HP-кривая HealthProgressionConfig.
            var preset = _sessionProgression != null ? _sessionProgression.SelectedPreset : null;

            int playerMax, enemyMax;
            if (preset != null)
            {
                playerMax = Mathf.Max(1, preset.PlayerHealth);
                enemyMax = Mathf.Max(1, preset.EnemyHealth);
            }
            else
            {
                (playerMax, enemyMax) = _healthProgressionConfig != null
                    ? _healthProgressionConfig.GetHealthForProgress(runProgress)
                    : (5, 5);
            }

            // Особые бои (минибосс/босс) живучее обычного врага — множитель
            // живёт в EnemyAIConfig текущего энкаунтера, а не в кривой рана,
            // чтобы не раздувать HP всей линии.
            var enemyAIConfig = _enemyAI != null ? _enemyAI.Config : null;
            if (enemyAIConfig != null)
                enemyMax = enemyAIConfig.ApplyHealthMultiplier(enemyMax);

            _healthController.Initialize(playerMax, enemyMax);
            _healthInitializedForLevel = _levelIndex;
        }

        private static TurnSide Opposite(TurnSide side) => side == TurnSide.Player ? TurnSide.Enemy : TurnSide.Player;

        private void OnEnable()
        {
            ItemPickupView.SetUsageWindowFilter(IsItemUsageAllowedNow);
            GameEvents.ShellSelected += OnShellSelected;
            GameEvents.RoundShuffleCompleted += OnShuffleCompleted;
            GameEvents.ShellRevealed += OnShellRevealed;
            GameEvents.RoundStartConfirmed += OnRoundStartConfirmed;
            GameEvents.SideDied += OnSideDied;
        }

        private void OnDisable()
        {
            // Статический фильтр не должен пережить сцену: иначе новый
            // GameManager подхватит правило от.destroyed экземпляра.
            ItemPickupView.SetUsageWindowFilter(null);
            GameEvents.ShellSelected -= OnShellSelected;
            GameEvents.RoundShuffleCompleted -= OnShuffleCompleted;
            GameEvents.ShellRevealed -= OnShellRevealed;
            GameEvents.RoundStartConfirmed -= OnRoundStartConfirmed;
            GameEvents.SideDied -= OnSideDied;
        }

        private void OnSideDied(TurnSide side)
        {
            if (side == TurnSide.Player)
            {
                ResetGameSpeedMultiplier();
                _enemySlowItemChoicesRemaining = 0;
                _enemyAI?.ResetDrugEffects();
            }
            else
            {
                // Enemy died - award coins equal to player's current HP
                int coinsAwarded = _healthController != null ? _healthController.GetHealth(TurnSide.Player) : 0;
                
                if (coinsAwarded > 0)
                {
                    // Add to persistent inventory
                    var runManager = RunManager.Instance;
                    if (runManager != null && runManager.CurrentRun != null && runManager.CurrentRun.PlayerInventory != null)
                    {
                        runManager.CurrentRun.PlayerInventory.Coins += coinsAwarded;
                    }

                    // Spawn physical coins animation
                    var coinReward = CoinRewardController.Instance;
                    var pileController = CoinPileController.Instance;
                    if (coinReward != null && pileController != null && pileController.CoinZone != null)
                    {
                        // Find enemy position
                        Vector3 enemyPos = _currentRig?.EnemyPos?.transform.position ?? transform.position;
                        coinReward.SpawnCoins(coinsAwarded, enemyPos, pileController.CoinZone);
                    }
                    else
                    {
                        // Нет контроллера награды/зоны — догоняем счётчик без анимации,
                        // иначе физические монеты разойдутся с Coins.
                        SyncCoinPileWithWallet();
                    }
                }

                ResetGameSpeedMultiplier();
                _enemySlowItemChoicesRemaining = 0;
                _enemyAI?.ResetDrugEffects();
                _healthController?.ResetDose(TurnSide.Player);
            }

            RunManager.Instance?.NotifyEncounterFinished(side);
        }

        private void OnShellSelected(Shell shell, TurnSide selectedBy)
        {
            if (_state != RoundState.PlayerTurn) return;

            if (_activeSide == TurnSide.Player && IsTutorialActive()
                && _tutorialPlayerChoiceLocked)
                return;

            bool isTutorialForcedRound = IsTutorialScene() && _completedRoundsInSession == 0;

            if (!isTutorialForcedRound)
                RunStatsTracker.Instance?.RegisterMove(_activeSide, shell.HasMarker);

            _selectedShell = shell;
            _inputSystem.SetEnabled(false);
            _inputSystem.SetItemInteractionEnabled(false);
            _state = RoundState.RevealResult;

            if (_activeSide == TurnSide.Enemy && _enemySlowItemChoicesRemaining > 0)
                _enemySlowItemChoicesRemaining--;

            HandleGameSpeedOwnerChoice(_activeSide);
        }

        private void OnShellRevealed(Shell shell, bool hasMarker)
        {
            if (_state != RoundState.RevealResult || _selectedShell != shell) return;
        }

        private void OnRoundStartConfirmed()
        {
            if (_state != RoundState.WaitForStart) return;
            _firstRoundReadyWaited = true;
            _state = RoundState.Generate;
        }

        /// <summary>
        /// Доводит голову врага до игрока. Цель — позиция камеры игрока: в игре
        /// от первого лица именно она соответствует тому, где стоит игрок.
        /// Вызов неблокирующий, геймплей при этом не встаёт.
        /// </summary>
        private void LookEnemyAtPlayer()
        {
            if (_enemyLookController == null)
                _enemyLookController = FindFirstObjectByType<EnemyLookController>();

            if (_enemyLookController == null)
                return;

            var playerCamera = Camera.main;
            if (playerCamera == null)
                return;

            _enemyLookController.LookAtPoint(playerCamera.transform.position);
        }

        public void ContinueTutorialReveal() => _state = _state == RoundState.WaitForTutorialReveal ? RoundState.Reveal : _state;
        public void ContinueTutorialShuffle() => _state = _state == RoundState.WaitForTutorialReveal ? RoundState.Shuffle : _state;

        public void LockTutorialPlayerChoice() => _tutorialPlayerChoiceLocked = true;
        public void UnlockTutorialPlayerChoice() => _tutorialPlayerChoiceLocked = false;
        // Kept as harmless compatibility calls for older tutorial sequences.
        public void RequireTutorialPlayerChoice(bool hasMarker) { }
        public void ClearTutorialPlayerChoiceRequirement() { }
        public void RequireTutorialEnemyChoice(bool hasMarker) { }

        public void PauseTutorialBeforeDamage() => _tutorialBeforeDamagePaused = true;
        public void ResumeTutorialBeforeDamage() => _tutorialBeforeDamagePaused = false;

        public void PauseTutorialAfterDamage() => _tutorialAfterDamagePaused = true;
        public void ResumeTutorialAfterDamage() => _tutorialAfterDamagePaused = false;

        public void PauseTutorialGameplay() => _tutorialGameplayPaused = true;
        public void ResumeTutorialGameplay() => _tutorialGameplayPaused = false;

        private void OnShuffleCompleted()
        {
            if (_activeSide == TurnSide.Enemy && _enemyAI != null) _enemyAI.ExitTrackShuffle();
            if (_activeSide == TurnSide.Player) _playerShuffleCompleted = true;
            if (_state == RoundState.Shuffle) _state = RoundState.PlayerTurn;
        }

        /// <summary>
        /// Ждёт, пока враг договорит (реакции держат GameplayGate). Таймаут
        /// внутри гейта страхует от зависшего хода, если реплику нечем закрыть.
        /// </summary>
        private static IEnumerator WaitForGameplayGate()
        {
            if (GameplayGate.IsBlocked)
                yield return GameplayGate.WaitUntilUnblocked();
        }
    }
}
