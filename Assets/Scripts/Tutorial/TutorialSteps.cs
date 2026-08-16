using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
}