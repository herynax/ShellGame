using System.Collections.Generic;
using System.Linq;
using ShellGame.Core;
using ShellGame.Shells;
using UnityEngine;

namespace ShellGame.Gameplay
{
    public sealed class ShuffleSystem : MonoBehaviour
    {
        [SerializeField] private int _swapCount = 6;
        [SerializeField] private float _betweenSwapDelay = 0.1f;

        private List<Shell> _shells = new List<Shell>();
        private bool _isRunning;
        private int _currentStep;
        private System.Action _onComplete;

        public void StartShuffling(IReadOnlyList<Shell> shells, System.Action onComplete)
        {
            if (_isRunning)
                return;

            _shells = shells.ToList();
            _onComplete = onComplete;
            _currentStep = 0;
            _isRunning = true;
            PerformNextSwap();
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

            var firstPosition = firstSlot.Position;
            var secondPosition = secondSlot.Position;
            int completedMoves = 0;
            void OnShellMoved()
            {
                completedMoves++;
                if (completedMoves < 2)
                    return;

                (_shells[firstIndex], _shells[secondIndex]) = (_shells[secondIndex], _shells[firstIndex]);
                _currentStep++;
                if (_betweenSwapDelay > 0f)
                    Invoke(nameof(PerformNextSwap), _betweenSwapDelay);
                else
                    PerformNextSwap();
            }

            firstShell.MoveToSlot(secondSlot, OnShellMoved);
            secondShell.MoveToSlot(firstSlot, OnShellMoved);
        }

        private void Finish()
        {
            _isRunning = false;
            GameEvents.RaiseRoundShuffleCompleted();
            _onComplete?.Invoke();
            _onComplete = null;
        }
    }
}
