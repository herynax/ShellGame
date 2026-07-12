using System.Collections;
using ShellGame.AI;
using ShellGame.Core;
using ShellGame.Health;
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

        public RoundState State => _state;
        public TurnSide ActiveSide => _activeSide;

        public void Initialize(
            RoundGenerator roundGenerator,
            RoundInputSystem inputSystem,
            ShuffleSystem shuffleSystem,
            HealthController healthController,
            EnemyAIController enemyAI,
            HealthProgressionConfig healthProgressionConfig,
            TurnSide startingSide)
        {
            _roundGenerator = roundGenerator;
            _inputSystem = inputSystem;
            _shuffleSystem = shuffleSystem;
            _healthController = healthController;
            _enemyAI = enemyAI;
            _healthProgressionConfig = healthProgressionConfig;
            _startingSide = startingSide;
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

            _activeSide = _startingSide;

            StartRound();
        }

        public void StartRound()
        {
            if (_roundGenerator == null || _inputSystem == null || _shuffleSystem == null)
            {
                Debug.LogWarning("GameManager dependencies are not initialized yet. Waiting for initialization.");
                return;
            }

            _state = RoundState.Generate;
            StartCoroutine(RunRoundRoutine());
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

                        _currentParameters = _roundGenerator.GenerateRound(_levelIndex, _roundIndex);
                        _state = RoundState.Reveal;
                        break;

                    case RoundState.Reveal:
                        if (_roundGenerator == null)
                        {
                            yield break;
                        }

                        yield return new WaitForSeconds(Mathf.Max(0f, _spawnPauseDuration));

                        _roundGenerator.RevealMarkers(_revealHoldDuration);

                        // Состояние ObserveMarkers из ГДД — противник получает
                        // достоверную информацию о раскладке ровно в тот же
                        // момент, что и игрок визуально её видит.
                        if (_activeSide == TurnSide.Enemy && _enemyAI != null)
                            _enemyAI.EnterObserveMarkers(_roundGenerator.ActiveShells, _currentParameters.DifficultyIndex);

                        yield return new WaitForSeconds(Mathf.Max(0f, _roundGenerator.GetRevealDuration(_revealHoldDuration)));
                        _roundGenerator.HideMarkers();
                        _state = RoundState.Shuffle;
                        break;

                    case RoundState.Shuffle:
                        if (_inputSystem == null || _shuffleSystem == null || _roundGenerator == null)
                        {
                            yield break;
                        }

                        _inputSystem.SetEnabled(false);
                        yield return new WaitForSeconds(_shuffleDelay);

                        // Состояние TrackShuffle — начинаем слушать OnCupSwap только
                        // если это ход противника (для хода игрока это не нужно).
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
                                // Decision + Attack: AI сам выбирает наперсток и вызывает
                                // Select() на нём — дальше событие ShellSelected обрабатывается
                                // ровно так же, как и выбор игрока (единый путь для обеих сторон).
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

                        _selectedShell.RevealResult();
                        yield return new WaitForSeconds(Mathf.Max(_roundEndDelay, _roundGenerator.GetRevealDuration()));

                        if (_selectedShell.HasMarker)
                        {
                            // Попадание: урон получает ДРУГАЯ сторона, инициатива
                            // остаётся у текущей активной стороны (ГДД: "Если
                            // выбранный наперсток содержит метку - соперник
                            // получает урон").
                            var damagedSide = Opposite(_activeSide);
                            int damage = _healthProgressionConfig != null ? _healthProgressionConfig.DamagePerHit : 1;
                            bool died = _healthController != null && _healthController.ApplyDamage(damagedSide, damage);
                            Debug.Log($"GameManager: HIT — {damagedSide} takes {damage} damage, died={died}");

                            if (died)
                            {
                                _state = RoundState.GameOver;
                                break;
                            }
                        }
                        else
                        {
                            // Промах: инициатива переходит противнику (ГДД: "Если
                            // игрок ошибается - инициатива переходит противнику").
                            _activeSide = Opposite(_activeSide);
                            GameEvents.RaiseActiveSideChanged(_activeSide);
                        }

                        _state = RoundState.Cleanup;
                        break;

                    case RoundState.Cleanup:
                        if (_roundGenerator == null || _inputSystem == null)
                        {
                            yield break;
                        }


                        if (_activeSide == TurnSide.Enemy && _enemyAI != null)
                            _enemyAI.EnterEndTurn();

                        _roundGenerator.ClearRound();
                        _inputSystem.SetEnabled(false);
                        _roundIndex++;
                        yield return new WaitForSeconds(0.1f);
                        _state = RoundState.Generate;
                        break;

                    case RoundState.GameOver:
                        _roundGenerator?.ClearRound();
                        _inputSystem?.SetEnabled(false);
                        // TODO: здесь подключается катсцена смерти и экран статистики
                        // (ходы/время/предметы/сохранённые зубы) из раздела "Система
                        // здоровья" ГДД — отдельная итерация, вне текущего скоупа.
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
