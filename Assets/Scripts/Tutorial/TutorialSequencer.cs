using System;
using System.Collections;
using UnityEngine;

namespace ShellGame.Tutorial
{
    /// <summary>
    /// Исполнитель сценария обучения. Кладёте сюда корневой TutorialStep
    /// (обычно Sequence(...), собранный через TutorialBuilder) и вызываете Play().
    /// </summary>
    public sealed class TutorialSequencer : MonoBehaviour
    {
        public bool IsPlaying { get; private set; }
        public event Action Completed;

        private Coroutine _routine;

        public void Play(TutorialStep root)
        {
            if (IsPlaying)
            {
                Debug.LogWarning("TutorialSequencer: уже выполняется сценарий, повторный Play проигнорирован.");
                return;
            }

            IsPlaying = true;
            _routine = StartCoroutine(RunRoot(root));
        }

        public void Stop()
        {
            if (_routine != null)
                StopCoroutine(_routine);

            IsPlaying = false;
            _routine = null;
        }

        private IEnumerator RunRoot(TutorialStep root)
        {
            yield return root.Run(this);
            IsPlaying = false;
            Completed?.Invoke();
        }
    }
}
