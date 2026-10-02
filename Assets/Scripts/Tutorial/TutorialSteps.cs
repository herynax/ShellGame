using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ShellGame.Tutorial
{
    public sealed class Sequence : TutorialStep
    {
        private readonly TutorialStep[] _steps;
        public Sequence(params TutorialStep[] steps) => _steps = steps;

        public override IEnumerator Run(MonoBehaviour runner)
        {
            foreach (var step in _steps)
                yield return step.Run(runner);
        }
    }

    public sealed class Parallel : TutorialStep
    {
        private readonly TutorialStep[] _steps;
        public Parallel(params TutorialStep[] steps) => _steps = steps;

        public override IEnumerator Run(MonoBehaviour runner)
        {
            if (_steps == null || _steps.Length == 0)
                yield break;

            int activeCount = _steps.Length;
            for (int i = 0; i < _steps.Length; i++)
            {
                var step = _steps[i];
                runner.StartCoroutine(RunBranch(step, runner, () => activeCount--));
            }

            while (activeCount > 0)
                yield return null;
        }

        private IEnumerator RunBranch(TutorialStep step, MonoBehaviour runner, Action onComplete)
        {
            yield return step.Run(runner);
            onComplete?.Invoke();
        }
    }

    public sealed class DoAction : TutorialStep
    {
        private readonly Action _action;
        public DoAction(Action action) => _action = action;

        public override IEnumerator Run(MonoBehaviour runner)
        {
            _action?.Invoke();
            yield break;
        }
    }

    public sealed class WaitSeconds : TutorialStep
    {
        private readonly float _seconds;
        public WaitSeconds(float seconds) => _seconds = seconds;

        public override IEnumerator Run(MonoBehaviour runner)
        {
            yield return new WaitForSeconds(_seconds);
        }
    }

    public sealed class WaitUntil : TutorialStep
    {
        private readonly Func<bool> _condition;
        public WaitUntil(Func<bool> condition) => _condition = condition;

        public override IEnumerator Run(MonoBehaviour runner)
        {
            while (!_condition())
                yield return null;
        }
    }

    /// <summary>
    /// Явное подтверждение этапа туториала. В отличие от клика, закрывающего
    /// диалог, этот шаг требует отдельного нажатия до следующей анимации.
    /// </summary>
    public sealed class WaitForTutorialAdvance : TutorialStep
    {
        public override IEnumerator Run(MonoBehaviour runner)
        {
            // Один кадр не позволяет тому же клику, который закрыл реплику,
            // случайно подтвердить следующий этап.
            yield return null;
            while (!IsAdvancePressed())
                yield return null;
        }

        /// <summary>
        /// Нажатие, подтверждающее этап: левая кнопка мыши, тап или пробел.
        /// </summary>
        private static bool IsAdvancePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                return true;

            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space);
#endif
        }
    }
}
