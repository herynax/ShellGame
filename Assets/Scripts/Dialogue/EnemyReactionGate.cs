using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShellGame.Dialogue
{
    /// <summary>
    /// «Кто-то сейчас говорит реплику врага». Позволяет игровому коду
    /// (например, ItemSpawner) подождать, пока враг договорит, прежде чем
    /// делать следующий шаг — чтобы предмет А не использовался раньше, чем
    /// игрок прочитает комментарий к нему.
    /// </summary>
    public static class EnemyReactionGate
    {
        public const float DefaultWaitTimeout = 10f;

        private static readonly HashSet<object> _presenters = new HashSet<object>();

        public static bool IsBusy => _presenters.Count > 0;

        public static void SetBusy(object presenter, bool busy)
        {
            if (presenter == null)
                return;

            if (busy)
                _presenters.Add(presenter);
            else
                _presenters.Remove(presenter);
        }

        /// <summary>
        /// Ждёт конца всех показываемых реплик. Таймаут — страховка от вечной
        /// блокировки хода (например, если реплику нечем закрыть).
        /// </summary>
        public static IEnumerator WaitWhileBusy(float timeoutSeconds = DefaultWaitTimeout)
        {
            float elapsed = 0f;

            while (IsBusy)
            {
                if (timeoutSeconds > 0f && elapsed >= timeoutSeconds)
                {
                    Debug.LogWarning($"[EnemyReactionGate] Реплики не закончились за {timeoutSeconds:F1}с — продолжаю без ожидания.");
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _presenters.Clear();
    }
}
