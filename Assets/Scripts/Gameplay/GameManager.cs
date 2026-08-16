using System.Collections;
using System.Collections.Generic;
using ShellGame.AI;
using ShellGame.Core;
using ShellGame.Feedback;
using ShellGame.Health;
using ShellGame.Items;
using ShellGame.Shells;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShellGame.Gameplay
{
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private RoundGenerator _roundGenerator;
        [SerializeField] private RoundInputSystem _inputSystem;
        [SerializeField] private ShuffleSystem _shuffleSystem;
        [SerializeField] private HealthController _healthController;
        [SerializeField] private EnemyAIController _enemyAI;
        [SerializeField] private RoundStartButton _roundStartButton;
        [SerializeField] private HealthProgressionConfig _healthProgressionConfig;
        [SerializeField] private TurnIndicatorController _turnIndicator;
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

        [Header("Следующий раунд после урона игроку")]
        [Tooltip("Задержка перед стартом следующего раунда, если был нанесён урон игроку.")]
        [SerializeField] private float _nextRoundDelayAfterPlayerDamage = 0.8f;

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

        private bool _tutorialBeforeDamagePaused;
        private bool _tutorialAfterDamagePaused;

        private GameSessionProgression _sessionProgression;

        private readonly Dictionary<TurnSide, int> _nextHitMultiplier = new Dictionary<TurnSide, int>
        {
            { TurnSide.Player, 1 },
            { TurnSide.Enemy, 1 },
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
            if (_roundGenerator == null) _roundGenerator = GetComponentInChildren<RoundGenerator>();
            if (_inputSystem == null) _inputSystem = GetComponentInChildren<RoundInputSystem>();
            if (_shuffleSystem == null) _shuffleSystem = GetComponentInChildren<ShuffleSystem>();
            if (_healthController == null) _healthController = GetComponentInChildren<HealthController>();
            if (_enemyAI == null) _enemyAI = GetComponentInChildren<EnemyAIController>();
            if (_turnIndicator == null) _turnIndicator = GetComponentInChildren<TurnIndicatorController>();

            _sessionProgression = FindObjectOfType<GameSessionProgression>();
            if (_sessionProgression == null)
            {
                var progressionObject = new GameObject("GameSessionProgression");
                _sessionProgression = progressionObject.AddComponent<GameSessionProgression>();
            }

            if (IsTutorialScene())
            {
                _sessionProgression.Reset();
                _completedRoundsInSession = 0;
                _levelIndex = 0;
                _roundIndex = 0;
                _firstRoundReadyWaited = false;
                _tutorialPlayerChoiceLocked = true;
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
            };
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

                        if (!_firstRoundReadyWaited && _completedRoundsInSession == 0)
                        {
                            _state = RoundState.WaitForStart;
                            break;
                        }

                        if (!_roundLayoutGenerated)
                        {
                            _currentParameters = _roundGenerator.GenerateRound(_levelIndex, _roundIndex, _completedRoundsInSession);
                            if (_sessionProgression != null)
                            {
                                float persistedDifficulty = _sessionProgression.GetDifficultyForRound(_levelIndex, _roundIndex, _completedRoundsInSession);
                                _currentParameters.DifficultyIndex = persistedDifficulty;
                                _sessionProgression.SetDifficultyIndex(persistedDifficulty + 0.45f);
                            }
                            _roundLayoutGenerated = true;
                        }

                        bool tutorialGate = IsTutorialScene()
                            && _completedRoundsInSession == 0
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
                        _shuffleSystem.StartShuffling(_roundGenerator.GetShellsInPlayOrder(), () => { _state = RoundState.PlayerTurn; }, _levelIndex, _roundIndex, _currentParameters.DifficultyIndex);
                        while (_state == RoundState.Shuffle) yield return null;
                        break;

                    case RoundState.PlayerTurn:
                        if (_inputSystem == null) yield break;

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
                                if (IsTutorialScene())
                                    _enemyAI.ForceCorrectChoice();

                                Debug.Log($"[GameManager] Вызываю MakeDecisionAndAttack, activeSide={_activeSide}, shells={_roundGenerator.ActiveShells.Count}");
                                _enemyAI.MakeDecisionAndAttack(_roundGenerator.ActiveShells, chosen =>
                                {
                                    Debug.Log($"[GameManager] onShellChosen получен, slot={chosen.SlotIndex}, вызываю chosen.Select()");
                                    chosen.Select();
                                });
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

                        if (_selectedShell.HasMarker && Opposite(_activeSide) == TurnSide.Player && _nextRoundDelayAfterPlayerDamage > 0f)
                        {
                            yield return new WaitForSeconds(_nextRoundDelayAfterPlayerDamage);
                        }

                        _activeSide = Opposite(_activeSide);
                        GameEvents.RaiseActiveSideChanged(_activeSide);
                        _turnsCompletedInCurrentRound++;
                        if (_turnsCompletedInCurrentRound < 2) _state = RoundState.InitiativeAnimation;
                        else _state = RoundState.Cleanup;
                        break;

                    case RoundState.Cleanup:
                        if (_roundGenerator == null || _inputSystem == null) yield break;
                        _roundGenerator.ClearRound();
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
                        if (_turnIndicator != null)
                        {
                            bool animationDone = false;
                            _turnIndicator.PlayTransition(_activeSide, () => animationDone = true);
                            while (!animationDone) yield return null;
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

            // Блокируем выбор только для игрока — на ход врага этот замок не распространяется.
            if (_activeSide == TurnSide.Player && IsTutorialScene()
                && _completedRoundsInSession == 0 && _tutorialPlayerChoiceLocked) return;

            _selectedShell = shell;
            _inputSystem.SetEnabled(false);
            _state = RoundState.RevealResult;
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
