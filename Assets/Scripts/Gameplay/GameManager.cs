using System.Collections;
using System.Collections.Generic;
using ShellGame.AI;
using ShellGame.Core;
using ShellGame.Feedback;
using ShellGame.Health;
using ShellGame.Items;
using ShellGame.Shells;
using FMODUnity;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace ShellGame.Gameplay
{
    public sealed class GameManager : MonoBehaviour
    {
        public const string TutorialCompletedPrefKey = "ShellGame.TutorialCompleted";

        [HideInInspector] private RoundGenerator _roundGenerator;
        [HideInInspector] private RoundInputSystem _inputSystem;
        [HideInInspector] private ShuffleSystem _shuffleSystem;
        [HideInInspector] private HealthController _healthController;
        [HideInInspector] private EnemyAIController _enemyAI;
        [HideInInspector] private RoundStartButton _roundStartButton;
        [SerializeField] private HealthProgressionConfig _healthProgressionConfig;
        [HideInInspector] private TurnIndicatorController _turnIndicator;
        [HideInInspector] private ItemSpawner _itemSpawner;
        [SerializeField] private TurnSide _startingSide = TurnSide.Player;

        [SerializeField] private int _levelIndex = 0;
        [SerializeField] private int _roundIndex = 0;
        [SerializeField] private float _spawnPauseDuration = 0.45f;
        [SerializeField] private float _revealHoldDuration = 0.75f;
        [SerializeField] private float _roundEndDelay = 0.5f;
        [SerializeField] private float _shuffleDelay = 0.15f;

        [Header("Урон игроку")]
        [Tooltip("Задержка перед нанесением урона игроку (когда враг поднял наперсток с меткой) — даёт анимации подъёма наперстка доиграть до конца.")]
        [SerializeField] private float _damageToPlayerDelay = 0.5f;

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
        private bool _initiativeAnimationPending;

        private float _activeGameSpeedMultiplier = 1f;
        private bool _gameSpeedEffectActive;
        private TurnSide _gameSpeedEffectOwner;
        private bool _gameSpeedOwnerHasChosen;
        private Coroutine _gameSpeedTransition;

        private const float MinGameSpeed = 0.5f;
        private const float GameSpeedTransitionDuration = 1f;

        private GameSessionProgression _sessionProgression;

        [Inject]
        private void InjectDependencies(
            RoundGenerator roundGenerator,
            RoundInputSystem inputSystem,
            ShuffleSystem shuffleSystem,
            HealthController healthController,
            EnemyAIController enemyAI,
            RoundStartButton roundStartButton,
            TurnIndicatorController turnIndicator,
            ItemSpawner itemSpawner,
            GameSessionProgression sessionProgression)
        {
            Initialize(roundGenerator, inputSystem, shuffleSystem, healthController, enemyAI,
                roundStartButton, _healthProgressionConfig, _startingSide, turnIndicator);
            _itemSpawner = itemSpawner;
            _sessionProgression = sessionProgression;
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
            var currentSceneName = SceneManager.GetActiveScene().name;
            return currentSceneName.Equals("Tutorial", System.StringComparison.OrdinalIgnoreCase)
                || currentSceneName.Contains("Tutorial", System.StringComparison.OrdinalIgnoreCase)
                || currentSceneName.Contains("Level0", System.StringComparison.OrdinalIgnoreCase)
                || currentSceneName.Contains("Level_0", System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTutorialCompleted() =>
            PlayerPrefs.GetInt(TutorialCompletedPrefKey, 0) == 1;

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
            if (_sessionProgression == null)
            {
                var progressionObject = new GameObject("GameSessionProgression");
                _sessionProgression = progressionObject.AddComponent<GameSessionProgression>();
            }

            RunStatsTracker.EnsureExists();

            if (IsTutorialScene())
            {
                bool isTutorialRestartScene = SceneManager.GetActiveScene().buildIndex == 1;
                if (isTutorialRestartScene)
                    _sessionProgression.Reset();

                _completedRoundsInSession = 0;
                _levelIndex = 0;
                _roundIndex = 0;
                RunStatsTracker.Instance.StartRun();

                if (!IsTutorialCompleted())
                {
                    _firstRoundReadyWaited = false;
                    _tutorialPlayerChoiceLocked = true;
                }
            }
            else
            {
                _tutorialPlayerChoiceLocked = false;
            }

            _completedRoundsInSession = _sessionProgression.CompletedRoundsInSession;
            if (_sessionProgression.CurrentLevelIndex > 0) _levelIndex = _sessionProgression.CurrentLevelIndex;
            else if (_levelIndex < 0) _levelIndex = SceneManager.GetActiveScene().buildIndex;

            _sessionProgression.SetCurrentLevelIndex(_levelIndex);
            _activeSide = _startingSide;
            _tutorialRevealPaused = false;
            _turnIndicator?.SetImmediate(_activeSide);

            if (_roundStartButton == null) _roundStartButton = GetComponentInChildren<RoundStartButton>(true);
            if (_roundStartButton != null) _roundStartButton.Hide();

            StartRound();
        }

        public void StartRound()
        {
            if (_roundGenerator == null || _inputSystem == null || _shuffleSystem == null) return;
            _turnsCompletedInCurrentRound = 0;
            _roundLayoutGenerated = false;
            _state = RoundState.Generate;
            StartCoroutine(RunRoundRoutine());
        }

        public ItemEffectContext CreateItemContext(TurnSide userSide)
        {
            return new ItemEffectContext
            {
                UserSide = userSide,
                Health = _healthController,
                ActiveShells = _roundGenerator != null ? _roundGenerator.ActiveShells : null,
                EnemyAI = _enemyAI,
                SetNextHitDamageMultiplier = SetNextHitDamageMultiplier,
                SlowGamePaceUntilNextChoice = SetGameSpeedMultiplier,
                CanSlowGamePace = () => Mathf.Approximately(_activeGameSpeedMultiplier, 1f),
                ReduceEnemyTrackingLossNextShuffle = multiplier => _enemyAI?.ReduceTrackingLossNextShuffle(multiplier),
                CanReduceEnemyTrackingLossNextShuffle = () => _enemyAI?.CanReduceTrackingLossNextShuffle() ?? false,
                CanUseEnemySlowItem = () => _enemySlowItemChoicesRemaining <= 0,
                StartEnemySlowItemCooldown = () => _enemySlowItemChoicesRemaining = 2,
                BeginShellPeek = (holdDuration, onPeeked) => ShellPeekGate.Begin(holdDuration, onPeeked),
                ResolveShellRevealDuration = holdDuration => _roundGenerator != null ? _roundGenerator.GetRevealDuration(holdDuration) : holdDuration,
                SkipCurrentTurn = () => _skipEnemyTurn = true,
                RequestExtraTurn = () => RequestExtraTurn(userSide),
                CanRequestExtraTurn = () => CanRequestExtraTurn(userSide),
            };
        }

        private void SetGameSpeedMultiplier(TurnSide side, float slowdownFactor)
        {
            if (_gameSpeedEffectActive)
                return;

            _gameSpeedEffectActive = true;
            _gameSpeedEffectOwner = side;
            _gameSpeedOwnerHasChosen = false;
            float targetSpeed = Mathf.Clamp(1f / Mathf.Max(1f, slowdownFactor), MinGameSpeed, 1f);
            StartGameSpeedTransition(targetSpeed);
        }

        private void ResetGameSpeedMultiplier()
        {
            if (!_gameSpeedEffectActive && Mathf.Approximately(_activeGameSpeedMultiplier, 1f))
                return;

            _gameSpeedEffectActive = false;
            _gameSpeedOwnerHasChosen = false;
            StartGameSpeedTransition(1f);
        }

        private void HandleGameSpeedOwnerChoice(TurnSide side)
        {
            if (!_gameSpeedEffectActive || side != _gameSpeedEffectOwner)
                return;

            if (!_gameSpeedOwnerHasChosen)
            {
                _gameSpeedOwnerHasChosen = true;
                return;
            }

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
            // На паузе Time.timeScale держит PauseController (0f) и сам восстановит
            // корректное значение при снятии паузы (см. PauseController._timeScaleBeforePause).
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
                switch (_state)
                {
                    case RoundState.Generate:
                        if (_roundGenerator == null) yield break;

                        EnsureHealthInitializedForLevel();

                        if (!_firstRoundReadyWaited)
                        {
                            _state = RoundState.WaitForStart;
                            break;
                        }

                        if (!_roundLayoutGenerated)
                        {
                            float difficultyIndex = _sessionProgression != null
                                ? _sessionProgression.GetDifficultyForRound(
                                    _levelIndex,
                                    _roundIndex,
                                    _completedRoundsInSession)
                                : _levelIndex + 0.45f * (_completedRoundsInSession + _roundIndex);

                            _currentParameters = _roundGenerator.GenerateRound(
                                _levelIndex,
                                _roundIndex,
                                _completedRoundsInSession,
                                difficultyIndex);
                            if (_sessionProgression != null)
                            {
                                _sessionProgression.AdvanceDifficultyForRound();
                            }
                            _roundLayoutGenerated = true;
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
                        if (_itemSpawner != null)
                            yield return _itemSpawner.SpawnItems();

                        if (_roundStartButton != null) _roundStartButton.Show();
                        if (_inputSystem != null) _inputSystem.SetEnabled(true);
                        while (_state == RoundState.WaitForStart) yield return null;
                        if (_roundStartButton != null) _roundStartButton.Hide();
                        if (_inputSystem != null) _inputSystem.SetEnabled(false);
                        break;

                    case RoundState.Reveal:
                        if (_roundGenerator == null) yield break;
                        yield return new WaitForSeconds(Mathf.Max(0f, _spawnPauseDuration));
                        _roundGenerator.RevealMarkers(_revealHoldDuration);
                        if (_activeSide == TurnSide.Enemy && _enemyAI != null)
                            _enemyAI.EnterObserveMarkers(_roundGenerator.ActiveShells, _currentParameters.DifficultyIndex);
                        yield return new WaitForSeconds(Mathf.Max(0f, _roundGenerator.GetRevealDuration(_revealHoldDuration)));
                        _roundGenerator.HideMarkers();
                        _state = RoundState.Shuffle;
                        break;

                    case RoundState.Shuffle:
                        if (_inputSystem == null || _shuffleSystem == null || _roundGenerator == null) yield break;
                        _inputSystem.SetEnabled(false);
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
                            _currentParameters.DifficultyIndex);
                        while (_state == RoundState.Shuffle) yield return null;
                        break;

                    case RoundState.PlayerTurn:
                        if (_inputSystem == null) yield break;

                        if (_extraTurnCooldown[_activeSide] > 0)
                            _extraTurnCooldown[_activeSide]--;

                        // Блокируем только выбор игрока, но не мешаем ходу врага
                        if (_activeSide == TurnSide.Player && IsTutorialScene()
                            && _completedRoundsInSession == 0 && _tutorialPlayerChoiceLocked)
                        {
                            while (_tutorialPlayerChoiceLocked) yield return null;
                        }

                        _selectedShell = null;
                        if (_activeSide == TurnSide.Player)
                        {
                            _inputSystem.SetEnabled(true);
                        }
                        else
                        {
                            _inputSystem.SetEnabled(false);

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
                        if (_itemSpawner != null)
                        {
                            var itemUseResult = new EnemyItemUseResult();
                            yield return _itemSpawner.TryUseEnemyItemsRoutine(this, _currentParameters.DifficultyIndex, itemUseResult);
                            _skipEnemyTurn = itemUseResult.SkippedTurn;
                            itemExtraDelay = itemUseResult.ExtraDelaySeconds;
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

                            // ВАЖНО: выбор врага всегда идёт через
                            // EnemyAIController.MakeDecisionAndAttack, даже в
                            // туториале — никогда не вызывайте shell.Select()
                            // отсюда напрямую. MakeDecisionAndAttack всегда
                            // проходит через собственную корутину с yield
                            // ПЕРЕД вызовом onShellChosen, поэтому Select()
                            // выполняется на отдельном "тике", а не внутри
                            // этого же стека вызовов RunRoundRoutine.
                            //
                            // Раньше здесь был прямой поиск шелла с меткой и
                            // synchronous correctShell.Select() — это вызывало
                            // Select() ПРЯМО из этого switch-case без единого
                            // yield между ними. GameEvents.RaiseShellSelected
                            // внутри Select() синхронно долетал до
                            // OnShellSelected() и реентрантно переключал
                            // _state на RevealResult ещё ДО того, как первый
                            // вызов Select() успевал доиграть свою анимацию
                            // (_animator.PlayReveal ещё не отработал onComplete,
                            // Shell.State ещё оставался Selected). В итоге
                            // RevealResult() проходил свой guard повторно и
                            // запускал PlayReveal ВТОРОЙ раз поверх первого —
                            // после чего корутина RunRoundRoutine падала с
                            // исключением и весь раунд-луп молча останавливался
                            // (TutorialSequencer при этом продолжал жить,
                            // отсюда ощущение "секвенс висит" именно после
                            // выбора наперстка врагом).
                            if (_enemyAI != null && _roundGenerator != null)
                            {
                                // БАГФИКС: раньше форс срабатывал на КАЖДЫЙ ход врага,
                                // пока активна туториальная сцена (IsTutorialScene()
                                // остаётся true все её раунды, не только первый) — из-за
                                // этого враг угадывал маркер всегда, а не только в
                                // единственном скриптованном обучающем раунде.
                                // ForceCorrectChoice сам себя сбрасывает после одного
                                // решения (persistent=false), поэтому форсить нужно
                                // только на входе в самый первый раунд туториала.
                                bool shouldForceTutorialChoice = IsTutorialScene() && _completedRoundsInSession == 0;
                                Debug.Log($"[GameManager] Enemy turn: scene={SceneManager.GetActiveScene().name} level={_levelIndex} round={_roundIndex} completedRounds={_completedRoundsInSession} state={_state} shouldForceTutorialChoice={shouldForceTutorialChoice}");
                                if (shouldForceTutorialChoice)
                                    _enemyAI.ForceCorrectChoice(); // сам найдёт помеченный шелл через FindMarkedShell

                                // Передаём врагу его текущую долю HP (0..1), чтобы
                                // EnemyAIConfig мог снижать точность решения по мере
                                // получения урона — симметрично "поплывшему" экрану
                                // игрока от дозы. См. EnemyAIConfig.EvaluateHealthAccuracyPenalty.
                                //
                                // HealthController.GetDoseFraction(side) возвращает долю
                                // ДОЗЫ (0 = полное здоровье, 1 = смерть от передозировки),
                                // поэтому для доли HP её нужно инвертировать.
                                if (_healthController != null)
                                    _enemyAI.SetHealthFraction(1f - _healthController.GetDoseFraction(TurnSide.Enemy));

                                if (_itemSpawner != null)
                                    yield return _itemSpawner.PlayEnemyLookAtShells(_roundGenerator.ActiveShells);

                                _enemyAI.MakeDecisionAndAttack(_roundGenerator.ActiveShells, chosen => chosen.Select());
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

                        // Пауза перед нанесением урона
                        if (IsTutorialScene()
                            && _completedRoundsInSession == 0 && _tutorialBeforeDamagePaused)
                        {
                            while (_tutorialBeforeDamagePaused) yield return null;
                        }

                        if (_selectedShell.HasMarker)
                        {
                            var damagedSide = Opposite(_activeSide);
                            if (damagedSide == TurnSide.Player && _damageToPlayerDelay > 0f)
                                yield return new WaitForSeconds(_damageToPlayerDelay);

                            int baseDamage = _healthProgressionConfig != null ? _healthProgressionConfig.DamagePerHit : 1;
                            int multiplier = ConsumeDamageMultiplier(_activeSide);
                            int damage = baseDamage * multiplier;
                            bool died = _healthController != null && _healthController.ApplyDamage(damagedSide, damage);
                            if (died)
                            {
                                _state = RoundState.GameOver;
                                break;
                            }
                        }

                        // Пауза после нанесения урона
                        if (IsTutorialScene()
                            && _completedRoundsInSession == 0 && _tutorialAfterDamagePaused)
                        {
                            while (_tutorialAfterDamagePaused) yield return null;
                        }

                        if (_extraTurnRequested[_activeSide])
                        {
                            _extraTurnRequested[_activeSide] = false;
                            _turnsCompletedInCurrentRound = 0;
                            _roundLayoutGenerated = false;
                            _initiativeAnimationPending = true;
                            _state = RoundState.InitiativeAnimation;
                            break;
                        }

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
                        yield break;

                    default: yield break;
                }
            }
        }

        private void EnsureHealthInitializedForLevel()
        {
            if (_healthController == null || _healthInitializedForLevel == _levelIndex) return;
            var (playerMax, enemyMax) = _healthProgressionConfig != null
                ? _healthProgressionConfig.GetHealthForLevel(_levelIndex)
                : (10, 10);
            _healthController.Initialize(playerMax, enemyMax);
            _healthInitializedForLevel = _levelIndex;
        }

        private static TurnSide Opposite(TurnSide side) => side == TurnSide.Player ? TurnSide.Enemy : TurnSide.Player;

        private void OnEnable()
        {
            GameEvents.ShellSelected += OnShellSelected;
            GameEvents.RoundShuffleCompleted += OnShuffleCompleted;
            GameEvents.ShellRevealed += OnShellRevealed;
            GameEvents.RoundStartConfirmed += OnRoundStartConfirmed;
        }

        private void OnDisable()
        {
            GameEvents.ShellSelected -= OnShellSelected;
            GameEvents.RoundShuffleCompleted -= OnShuffleCompleted;
            GameEvents.ShellRevealed -= OnShellRevealed;
            GameEvents.RoundStartConfirmed -= OnRoundStartConfirmed;
        }

        private void OnShellSelected(Shell shell)
        {
            if (_state != RoundState.PlayerTurn) return;

            if (_activeSide == TurnSide.Player && IsTutorialScene()
                && _completedRoundsInSession == 0 && _tutorialPlayerChoiceLocked) return;

            bool isTutorialForcedRound = IsTutorialScene() && _completedRoundsInSession == 0;
            if (!isTutorialForcedRound)
                RunStatsTracker.Instance?.RegisterMove(_activeSide, shell.HasMarker);

            _selectedShell = shell;
            _inputSystem.SetEnabled(false);
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

        public void ContinueTutorialReveal() => _state = _state == RoundState.WaitForTutorialReveal ? RoundState.Reveal : _state;

        public void LockTutorialPlayerChoice() => _tutorialPlayerChoiceLocked = true;
        public void UnlockTutorialPlayerChoice() => _tutorialPlayerChoiceLocked = false;

        public void PauseTutorialBeforeDamage() => _tutorialBeforeDamagePaused = true;
        public void ResumeTutorialBeforeDamage() => _tutorialBeforeDamagePaused = false;

        public void PauseTutorialAfterDamage() => _tutorialAfterDamagePaused = true;
        public void ResumeTutorialAfterDamage() => _tutorialAfterDamagePaused = false;

        private void OnShuffleCompleted()
        {
            if (_activeSide == TurnSide.Enemy && _enemyAI != null) _enemyAI.ExitTrackShuffle();
            if (_state == RoundState.Shuffle) _state = RoundState.PlayerTurn;
        }
    }
}