using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShellGame.Core
{
    /// <summary>
    /// Глобальная блокировка геймплея. Система, которая что-то показывает
    /// игроку (обучение, реплики врага, катсцена), берёт холд на время
    /// показа, а раунд-луп GameManager сам встаёт на паузу и не возвращает
    /// ввод игроку, пока холд не отпущен.
    ///
    /// Именно счётчик холдов, а не флаг: холды независимы, отмена одного
    /// не отменяет другой. Владелец передаётся ключом (обычно `this`
    /// компонента) — освободить можно только свой холд.
    /// </summary>
    public static class GameplayGate
    {
        private static readonly HashSet<object> _holders = new HashSet<object>();

        public static bool IsBlocked => _holders.Count > 0;

        public static void Block(object owner)
        {
            if (owner != null)
                _holders.Add(owner);
        }

        public static void Release(object owner)
        {
            if (owner != null)
                _holders.Remove(owner);
        }

        public static void Clear() => _holders.Clear();

        /// <summary>
        /// Ждёт, пока геймплей разблокируют. Таймаут — страховка от вечной
        /// паузы, если владелец холда исчез, не отпустив его.
        /// </summary>
        public static IEnumerator WaitUntilUnblocked(float timeoutSeconds = 10f)
        {
            float elapsed = 0f;

            while (IsBlocked)
            {
                if (timeoutSeconds > 0f && elapsed >= timeoutSeconds)
                {
                    Debug.LogWarning($"[GameplayGate] Геймплей не разблокирован за {timeoutSeconds:F1}с — продолжаю.");
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _holders.Clear();
    }
}
