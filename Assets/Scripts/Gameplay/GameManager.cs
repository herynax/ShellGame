using System.Collections;
using System.Collections.Generic;
using ShellGame.AI;
using ShellGame.Core;
using ShellGame.Feedback;
using ShellGame.Health;
using ShellGame.Items;
using ShellGame.Shells;
using UnityEngine;

namespace ShellGame.Gameplay
{
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private RoundGenerator _roundGenerator;
        [SerializeField] private RoundInputSystem _inputSystem;
        [SerializeField] private ShuffleSystem _shuffleSystem;
        [SerializeField] private HealthController _healthController;
        [SerializeField] private EnemyAIController _enemyAI;
        [SerializeField] private HealthProgressionConfig _healthProgressionConfig;
        [SerializeField] private TurnIndicatorController _turnIndicator;
        [SerializeField] private TurnSide _startingSide = TurnSide.Player;

        [SerializeField] private int _levelIndex = 0;
        [SerializeField] private int _roundIndex = 0;
        [SerializeField] private float _spawnPauseDuration = 0.45f;
        [SerializeField] private float _revealHoldDuration = 0.75f;
        [SerializeField] private float _roundEndDelay = 0.5f;
        [SerializeField] private float _shuffleDelay = 0.15f;

        private RoundState _state = RoundState.Idle;
        private RoundParameters _currentParameters;
        private Shell _selectedShell;
        private TurnSide _activeSide;
        private int _healthInitializedForLevel = -1;
        private int _turnsCompletedInCurrentRound;
        private bool _roundLayoutGenerated;

        // Множитель урона на следующий УДАЧНЫЙ удар каждой стороны (предмет "Двойной урон").
        // Промах его не сжигает — см. DoubleDamageItemDefinition.
        private readonly Dictionary<TurnSide, int> _nextHitMultiplier = new Dictionary<TurnSide, int>
        {
            { TurnSide.Player, 1 },
            { TurnSide.Enemy, 1 },
        };

        public RoundState State => _state;
        public TurnSide ActiveSide => _activeSide;

        public void Initialize(
            RoundGenerator roundGenerator,
            RoundInputSystem inputSystem,
            ShuffleSystem shuffleSystem,
            HealthController healthController,
            EnemyAIController enemyAI,
            HealthProgressionConfig healthProgressionConfig,
            TurnSide startingSide,
            TurnIndicatorController turnIndicator)
        {
            _roundGenerator = roundGenerator;
            _inputSystem = inputSystem;
            _shuffleSystem = shuffleSystem;
            _healthController = healthController;
            _enemyAI = enemyAI;
            _healthProgressionConfig = healthProgressionConfig;
            _startingSide = startingSide;
            _turnIndicator = turnIndicator;
        }

        private void Start()
        {
            if (_roundGenerator == null)
                _roundGenerator = GetComponentInChildren<RoundGenerator>();
            if (_inputSystem == null)
                _inputSystem = GetComponentInChildren<RoundInputSystem>();
            if (_shuffleSystem == null)
                _shuffleSystem = GetComponentInChildren<ShuffleSystem>();
            if (_healthController == null)
                _healthController = GetComponentInChildren<HealthController>();
            if (_enemyAI == null)
                _enemyAI = GetComponentInChildren<EnemyAIController>();
            if (_turnIndicator == null)
                _turnIndicator = GetComponentInChildren<TurnIndicatorController>();

            _activeSide = _startingSide;
            _turnIndicator?.SetImmediate(_activeSide);

            StartRound();
        }

        public void StartRound()
        {
            if (_roundGenerator == null || _inputSystem == null || _shuffleSystem == null)
            {
                Debug.LogWarning("GameManager dependencies are not initialized yet. Waiting for initialization.");
                return;
            }

            _turnsCompletedInCurrentRound = 0;
            _roundLayoutGenerated = false;
            _state = RoundState.Generate;
            StartCoroutine(RunRoundRoutine());
        }

        /// <summary>Собрать контекст для использования предмета указанной стороной прямо сейчас (текущий раунд/наперстки/здоровье).</summary>
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
            if (!_nextHitMultiplier.TryGetValue(side, out var multiplier) || multiplier <= 1)
                return 1;

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
                        if (_roundGenerator == null)
                        {
                            yield break;
                        }

                        EnsureHealthInitializedForLevel();

                        if (!_roundLayoutGenerated)
                        {
                            Debug.Log($"GameManager: stage=Generate level={_levelIndex} round={_roundIndex} activeSide={_activeSide}");
                            _currentParameters = _roundGenerator.GenerateRound(_levelIndex, _roundIndex);
                            _roundLayoutGenerated = true;
                        }
                        else
                        {
                            Debug.Log($"GameManager: stage=ReuseRoundLayout level={_levelIndex} round={_roundIndex} activeSide={_activeSide}");
                        }

                        _state = RoundState.Reveal;
                        break;

                    case RoundState.Reveal:
                        if (_roundGenerator == null)
                        {
                            yield break;
                        }

                        Debug.Log("GameManager: stage=SpawnPause");
                        yield return new WaitForSeconds(Mathf.Max(0f, _spawnPauseDuration));

                        Debug.Log("GameManager: stage=RevealMarkers");
                        _roundGenerator.RevealMarkers(_revealHoldDuration);

                        if (_activeSide == TurnSide.Enemy && _enemyAI != null)
                            _enemyAI.EnterObserveMarkers(_roundGenerator.ActiveShells, _currentParameters.DifficultyIndex);

                        yield return new WaitForSeconds(Mathf.Max(0f, _roundGenerator.GetRevealDuration(_revealHoldDuration)));
                        _roundGenerator.HideMarkers();
                        Debug.Log("GameManager: stage=Shuffle");
                        _state = RoundState.Shuffle;
                        break;

                    case RoundState.Shuffle:
                        if (_inputSystem == null || _shuffleSystem == null || _roundGenerator == null)
                        {
                            yield break;
                        }

                        _inputSystem.SetEnabled(false);
                        yield return new WaitForSeconds(_shuffleDelay);

                        if (_activeSide == TurnSide.Enemy && _enemyAI != null)
                            _enemyAI.EnterTrackShuffle();

                        _shuffleSystem.StartShuffling(_roundGenerator.GetShellsInPlayOrder(), () =>
                        {
                            _state = RoundState.PlayerTurn;
                        });
                        while (_state == RoundState.Shuffle)
                            yield return null;
                        break;

                    case RoundState.PlayerTurn:
                        if (_inputSystem == null)
                        {
                            yield break;
                        }

                        Debug.Log($"GameManager: stage=Turn activeSide={_activeSide}");
                        _selectedShell = null;

                        if (_activeSide == TurnSide.Player)
                        {
                            _inputSystem.SetEnabled(true);
                        }
                        else
                        {
                            _inputSystem.SetEnabled(false);
                            if (_enemyAI != null && _roundGenerator != null)
                            {
                                _enemyAI.MakeDecisionAndAttack(_roundGenerator.ActiveShells, chosen => chosen.Select());
                            }
                        }

                        while (_state == RoundState.PlayerTurn)
                            yield return null;
                        break;

                    case RoundState.RevealResult:
                        if (_selectedShell == null || _roundGenerator == null)
                        {
                            _state = RoundState.Cleanup;
                            break;
                        }

                        Debug.Log($"GameManager: stage=RevealResult selected={_selectedShell.name} hasMarker={_selectedShell.HasMarker} activeSide={_activeSide}");
                        _selectedShell.RevealResult();
                        yield return new WaitForSeconds(Mathf.Max(_roundEndDelay, _roundGenerator.GetRevealDuration()));

                        if (_selectedShell.HasMarker)
                        {
                            var damagedSide = Opposite(_activeSide);
                            int baseDamage = _healthProgressionConfig != null ? _healthProgressionConfig.DamagePerHit : 1;
                            int multiplier = ConsumeDamageMultiplier(_activeSide);
                            int damage = baseDamage * multiplier;

                            bool died = _healthController != null && _healthController.ApplyDamage(damagedSide, damage);
                            Debug.Log($"GameManager: HIT — {damagedSide} takes {damage} damage (x{multiplier}), died={died}");

                            if (died)
                            {
                                _state = RoundState.GameOver;
                                break;
                            }
                        }
                        else
                        {
                            Debug.Log("GameManager: MISS");
                        }

                        // Механика сохранения инициативы вырезана: ход всегда
                        // переходит другой стороне после раунда, независимо
                        // от попадания или промаха.
                        _activeSide = Opposite(_activeSide);
                        GameEvents.RaiseActiveSideChanged(_activeSide);

                        _turnsCompletedInCurrentRound++;
                        if (_turnsCompletedInCurrentRound < 2)
                        {
                            Debug.Log("GameManager: stage=NextTurnInRound");
                            _state = RoundState.InitiativeAnimation;
                        }
                        else
                        {
                            _state = RoundState.Cleanup;
                        }
                        break;

                    case RoundState.Cleanup:
                        if (_roundGenerator == null || _inputSystem == null)
                        {
                            yield break;
                        }

                        Debug.Log("GameManager: stage=Cleanup");

                        // Обратите внимание: _activeSide на этот момент уже
                        // относится к СЛЕДУЮЩЕМУ раунду (переключён в RevealResult),
                        // поэтому EnterEndTurn для только что ходившей стороны
                        // здесь вызвать без доп. состояния нельзя — при
                        // необходимости завести отдельное поле _lastActingSide.
                        _roundGenerator.ClearRound();
                        _inputSystem.SetEnabled(false);
                        _turnsCompletedInCurrentRound = 0;
                        _roundLayoutGenerated = false;
                        _roundIndex++;
                        yield return new WaitForSeconds(0.1f);
                        _state = RoundState.InitiativeAnimation;
                        break;

                    case RoundState.InitiativeAnimation:
                        Debug.Log($"GameManager: stage=InitiativeAnimation activeSide={_activeSide}");
                        if (_turnIndicator != null)
                        {
                            bool animationDone = false;
                            _turnIndicator.PlayTransition(_activeSide, () => animationDone = true);
                            while (!animationDone)
                                yield return null;
                        }

                        _state = RoundState.Generate;
                        break;

                    case RoundState.GameOver:
                        Debug.Log("GameManager: stage=GameOver");
                        _roundGenerator?.ClearRound();
                        _inputSystem?.SetEnabled(false);
                        // TODO: катсцена смерти и экран статистики — отдельная итерация.
                        yield break;

                    default:
                        yield break;
                }
            }
        }

        private void EnsureHealthInitializedForLevel()
        {
            if (_healthController == null || _healthInitializedForLevel == _levelIndex)
                return;

            var (playerMax, enemyMax) = _healthProgressionConfig != null
                ? _healthProgressionConfig.GetHealthForLevel(_levelIndex)
                : (10, 10);

            _healthController.Initialize(playerMax, enemyMax);
            _healthInitializedForLevel = _levelIndex;
            Debug.Log($"GameManager: health initialized for level={_levelIndex} player={playerMax} enemy={enemyMax}");
        }

        private static TurnSide Opposite(TurnSide side) => side == TurnSide.Player ? TurnSide.Enemy : TurnSide.Player;

        private void OnEnable()
        {
            GameEvents.ShellSelected += OnShellSelected;
            GameEvents.RoundShuffleCompleted += OnShuffleCompleted;
            GameEvents.ShellRevealed += OnShellRevealed;
        }

        private void OnDisable()
        {
            GameEvents.ShellSelected -= OnShellSelected;
            GameEvents.RoundShuffleCompleted -= OnShuffleCompleted;
            GameEvents.ShellRevealed -= OnShellRevealed;
        }

        private void OnShellSelected(Shell shell)
        {
            if (_state != RoundState.PlayerTurn)
                return;

            _selectedShell = shell;
            _inputSystem.SetEnabled(false);
            _state = RoundState.RevealResult;
        }

        private void OnShellRevealed(Shell shell, bool hasMarker)
        {
            if (_state != RoundState.RevealResult || _selectedShell != shell)
                return;

            Debug.Log(hasMarker ? "Success" : "Fail");
        }

        private void OnShuffleCompleted()
        {
            if (_activeSide == TurnSide.Enemy && _enemyAI != null)
                _enemyAI.ExitTrackShuffle();

            if (_state == RoundState.Shuffle)
                _state = RoundState.PlayerTurn;
        }
    }
}
