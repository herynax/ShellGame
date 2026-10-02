using System.Collections.Generic;
using System.Linq;
using ShellGame.Core;
using ShellGame.Shells;
using UnityEngine;
using DG.Tweening;

namespace ShellGame.Gameplay
{
    public sealed class ShuffleSystem : MonoBehaviour
    {
        [Tooltip("Аварийное число обменов. Используется только если не удалось получить ShellConfig (см. ResolveSwapCount).")]
        [SerializeField] private int _swapCount = 6;

        [Tooltip("Пауза между соседними обменами, сек. Если не задана в ShellConfig — берётся отсюда.")]
        [SerializeField] private float _betweenSwapDelay = 0.1f;

        [SerializeField] private ShellConfig _shellConfig;

        private List<Shell> _shells = new List<Shell>();
        private bool _isRunning;
        private int _currentStep;
        private int _currentLevelIndex;
        private int _currentRoundIndex;
        private float _currentDifficultyIndex;
        private float _moveDurationMultiplier = 1f;
        private bool _isEnemyTurn;
        private int _activeSwapCount = 6;
        private float _activeBetweenSwapDelay = 0.1f;
        private System.Action _onComplete;

        // === ПЕРЕМЕННЫЕ ДЛЯ ОБУЧЕНИЯ ===
        public bool TutorialStepMode { get; set; }
        public bool IsWaitingForStep { get; private set; }
        // ===============================

        public void Initialize(ShellConfig shellConfig)
        {
            _shellConfig = shellConfig;
        }

        /// <summary>
        /// Запускает перемешивание.
        /// <paramref name="isEnemyTurn"/> — true, если перемешивает сторона врага
        /// (см. GameManager._activeSide) — тогда применяется
        /// ShellConfig.EnemyShuffleSpeedMultiplier (враг перемешивает быстрее игрока).
        /// <paramref name="shellConfig"/> — необязательный явный ShellConfig на этот
        /// конкретный вызов. Передавайте сюда RoundGenerator.ShellConfig из
        /// GameManager — это надёжнее, чем полагаться на то, что у ShuffleSystem
        /// (который может спавниться отдельно, без назначенного в инспекторе
        /// конфига) свой сериализованный _shellConfig вообще установлен. Если
        /// null — используется локальный _shellConfig как раньше.
        /// </summary>
        public void StartShuffling(IReadOnlyList<Shell> shells, System.Action onComplete, int levelIndex, int roundIndex, float difficultyIndex = 0f, bool isEnemyTurn = false, ShellConfig shellConfig = null)
        {
            if (_isRunning) return;

            if (shellConfig != null)
                _shellConfig = shellConfig;

            _shells = shells.ToList();
            _onComplete = onComplete;
            _currentStep = 0;
            _isRunning = true;
            _currentLevelIndex = Mathf.Max(0, levelIndex);
            _currentRoundIndex = Mathf.Max(0, roundIndex);
            _currentDifficultyIndex = difficultyIndex;
            _isEnemyTurn = isEnemyTurn;

            _activeSwapCount = ResolveSwapCount();
            _activeBetweenSwapDelay = ResolveBetweenSwapDelay();

            Debug.Log($"[ShuffleSystem] START isEnemy={_isEnemyTurn}, swapCount={_activeSwapCount} " +
                      $"(вместо прежнего фиксированного {_swapCount}), betweenSwapDelay={_activeBetweenSwapDelay:F3}, " +
                      $"baseDuration={_shellConfig?.ShuffleMoveDurationBase}, minDuration={_shellConfig?.ShuffleMoveDurationMin}, " +
                      $"enemyMult={_shellConfig?.EnemyShuffleSpeedMultiplier}, difficulty={_currentDifficultyIndex:F2}");

            GameEvents.RaiseRoundShuffleStarted();

            // Первый обмен запускаем сразу даже в режиме обучения. В режиме
            // обучения пауза нужна между шагами, а не перед началом шафла.
            IsWaitingForStep = false;
            PerformNextSwap();
        }

        private void PerformNextSwap()
        {
            if (!_isRunning || _currentStep >= _activeSwapCount)
            {
                Finish();
                return;
            }

            var firstIndex = Random.Range(0, _shells.Count);
            var secondIndex = Random.Range(0, _shells.Count);
            while (secondIndex == firstIndex)
                secondIndex = Random.Range(0, _shells.Count);

            var firstShell = _shells[firstIndex];
            var secondShell = _shells[secondIndex];
            var firstSlot = firstShell.AssignedSlot;
            var secondSlot = secondShell.AssignedSlot;

            if (firstSlot == null || secondSlot == null)
            {
                _currentStep++;
                PerformNextSwap();
                return;
            }

            GameEvents.RaiseCupSwapPerformed(firstSlot.Index, secondSlot.Index);

            int completedMoves = 0;
            void OnShellMoved()
            {
                completedMoves++;
                if (completedMoves < 2) return;

                (_shells[firstIndex], _shells[secondIndex]) = (_shells[secondIndex], _shells[firstIndex]);
                _currentStep++;

                // Останавливаем цикл, если включен режим пошагового обучения
                if (TutorialStepMode)
                {
                    IsWaitingForStep = true;
                    return;
                }

                if (_activeBetweenSwapDelay > 0f) Invoke(nameof(PerformNextSwap), _activeBetweenSwapDelay);
                else PerformNextSwap();
            }

            float moveDuration = ResolveShuffleMoveDuration();
            firstShell.MoveToSlot(secondSlot, OnShellMoved, moveDuration);
            secondShell.MoveToSlot(firstSlot, OnShellMoved, moveDuration);
        }

        public void TriggerNextStep()
        {
            if (!TutorialStepMode || !_isRunning) return;

            if (IsWaitingForStep)
            {
                IsWaitingForStep = false;
                PerformNextSwap();
            }
        }

        /// <summary>
        /// Число обменов берётся из ShellConfig и зависит от сложности рана
        /// (EvaluateShuffleSwapCount). Раньше было фиксированное поле
        /// _swapCount = 6 на префабе, и количество обменов не менялось за ран.
        /// </summary>
        private int ResolveSwapCount()
        {
            if (_shellConfig == null) return _swapCount;
            return _shellConfig.EvaluateShuffleSwapCount(_currentDifficultyIndex, _isEnemyTurn);
        }

        private float ResolveBetweenSwapDelay()
        {
            if (_shellConfig != null && _shellConfig.BetweenSwapDelay > 0f)
                return _shellConfig.BetweenSwapDelay;
            return _betweenSwapDelay;
        }

        private float ResolveShuffleMoveDuration()
        {
            if (_shellConfig == null) return 0.22f;

            float duration = _shellConfig.EvaluateShuffleMoveDuration(
                _currentDifficultyIndex, _isEnemyTurn, _moveDurationMultiplier);

            if (_isEnemyTurn)
            {
                Debug.Log($"[ShuffleSystem] ENEMY reduced={_shellConfig.EvaluateShuffleReducedDuration(_currentDifficultyIndex):F3}, " +
                          $"enemyMin={_shellConfig.EnemyShuffleMoveDurationMin:F3}, FINAL={duration:F3}, " +
                          $"difficulty={_currentDifficultyIndex:F2}");
                return duration;
            }

            Debug.Log($"[ShuffleSystem] PLAYER reduced={_shellConfig.EvaluateShuffleReducedDuration(_currentDifficultyIndex):F3}, " +
                      $"min={_shellConfig.ShuffleMoveDurationMin:F3}, FINAL={duration:F3}, " +
                      $"difficulty={_currentDifficultyIndex:F2}");
            return duration;
        }

        public void SetMoveDurationMultiplier(float multiplier)
        {
            _moveDurationMultiplier = Mathf.Max(1f, multiplier);
        }

        public void ResetMoveDurationMultiplier()
        {
            _moveDurationMultiplier = 1f;
        }

        private void Finish()
        {
            _isRunning = false;
            GameEvents.RaiseRoundShuffleCompleted();
            _onComplete?.Invoke();
            _onComplete = null;
        }

        private void OnDestroy() => DOTween.Kill(this);
    }
}