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
        [SerializeField] private int _swapCount = 6;
        [SerializeField] private float _betweenSwapDelay = 0.1f;
        [SerializeField] private ShellConfig _shellConfig;

        private List<Shell> _shells = new List<Shell>();
        private bool _isRunning;
        private int _currentStep;
        private int _currentLevelIndex;
        private int _currentRoundIndex;
        private float _currentDifficultyIndex;
        private System.Action _onComplete;

        // === ПЕРЕМЕННЫЕ ДЛЯ ОБУЧЕНИЯ ===
        public bool TutorialStepMode { get; set; }
        public bool IsWaitingForStep { get; private set; }
        // ===============================

        public void Initialize(ShellConfig shellConfig)
        {
            _shellConfig = shellConfig;
        }

        public void StartShuffling(IReadOnlyList<Shell> shells, System.Action onComplete, int levelIndex, int roundIndex, float difficultyIndex = 0f)
        {
            if (_isRunning) return;

            _shells = shells.ToList();
            _onComplete = onComplete;
            _currentStep = 0;
            _isRunning = true;
            _currentLevelIndex = Mathf.Max(0, levelIndex);
            _currentRoundIndex = Mathf.Max(0, roundIndex);
            _currentDifficultyIndex = difficultyIndex;
            
            GameEvents.RaiseRoundShuffleStarted();

            // Если режим обучения включен — встаем на паузу, ничего не перемешивая
            IsWaitingForStep = false;
            if (TutorialStepMode)
            {
                IsWaitingForStep = true;
            }
            else
            {
                PerformNextSwap();
            }
        }

        private void PerformNextSwap()
        {
            if (!_isRunning || _currentStep >= _swapCount)
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

                if (_betweenSwapDelay > 0f) Invoke(nameof(PerformNextSwap), _betweenSwapDelay);
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

        private float ResolveShuffleMoveDuration()
        {
            if (_shellConfig == null) return 0.22f;
            float baseDuration = _shellConfig.ShuffleMoveDurationBase;
            float roundReduction = _shellConfig.ShuffleRoundReduction * Mathf.Max(0, _currentRoundIndex);
            float levelReduction = _shellConfig.ShuffleLevelReduction * Mathf.Max(0, _currentLevelIndex);
            float reducedDuration = baseDuration - roundReduction - levelReduction;
            return Mathf.Max(_shellConfig.ShuffleMoveDurationMin, reducedDuration);
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