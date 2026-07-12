using ShellGame.Core;
using ShellGame.Health;
using UnityEngine;

namespace ShellGame.Feedback
{
    public abstract class DamageFeedbackBase : MonoBehaviour
    {
        private HealthController _healthController;

        protected abstract TurnSide WatchedSide { get; }

        protected virtual void Awake()
        {
            // Оставляем метод пустым, чтобы вызов base.Awake() 
            // в наследниках (PlayerDamageFeedback и EnemyDamageFeedback) не выдавал ошибку.
        }

        // Ленивое получение контроллера
        private HealthController GetHealthController()
        {
            if (_healthController == null)
            {
#if UNITY_2023_1_OR_NEWER
                _healthController = FindAnyObjectByType<HealthController>();
#pragma warning disable CS0618
#else
                _healthController = FindObjectOfType<HealthController>();
#endif
#pragma warning restore CS0618
            }
            return _healthController;
        }

        protected virtual void OnEnable()
        {
            GameEvents.DamageTaken += OnDamageTaken;
        }

        protected virtual void OnDisable()
        {
            GameEvents.DamageTaken -= OnDamageTaken;
        }

        private void OnDamageTaken(TurnSide side, int amount, int currentHealth, int maxHealth, bool died)
        {
            LogHealthState(side, amount, died);

            if (side != WatchedSide)
                return;

            PlayFeedback(amount, currentHealth, maxHealth, died);
        }

        private void LogHealthState(TurnSide side, int amount, bool died)
        {
            // Используем метод GetHealthController(), который сам найдёт скрипт, если он ещё не найден
            var hc = GetHealthController();

            int playerHp = hc != null ? hc.GetHealth(TurnSide.Player) : -1;
            int enemyHp = hc != null ? hc.GetHealth(TurnSide.Enemy) : -1;

            Debug.Log($"[Health] {side} получил {amount} урона (умер={died}) — ХП игрока={playerHp}, ХП врага={enemyHp}");
        }

        protected abstract void PlayFeedback(int amount, int currentHealth, int maxHealth, bool died);
    }
}