using ShellGame.Core;
using ShellGame.Health;
using UnityEngine;
using Zenject;

namespace ShellGame.Feedback
{
    /// <summary>
    /// Общая логика для визуального фидбека на урон: подписка на
    /// GameEvents.DamageTaken, фильтрация по нужной стороне и единообразный
    /// лог здоровья обеих сторон. Конкретные эффекты (тряска камеры, тряска
    /// модели и т.д.) реализуют наследники в PlayFeedback — когда появятся
    /// полноценные анимации, меняется только PlayFeedback, лог и подписка
    /// остаются как есть.
    /// </summary>
    public abstract class DamageFeedbackBase : MonoBehaviour
    {
        [SerializeField] private HealthController _healthController;

        [Inject]
        private void InjectHealthController(HealthController healthController)
        {
            if (_healthController == null)
                _healthController = healthController;
        }

        protected abstract TurnSide WatchedSide { get; }

        protected virtual void Awake()
        {
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
            if (side != WatchedSide)
                return;

            // Логируем только на "своей" стороне — так при наличии в сцене
            // и PlayerDamageFeedback, и EnemyDamageFeedback каждый удар
            // попадёт в лог ровно один раз, а не дважды.
            LogHealthState(side, amount, died);
            PlayFeedback(amount, currentHealth, maxHealth, died);
        }

        private void LogHealthState(TurnSide side, int amount, bool died)
        {
            int playerHp = _healthController != null ? _healthController.GetHealth(TurnSide.Player) : -1;
            int enemyHp = _healthController != null ? _healthController.GetHealth(TurnSide.Enemy) : -1;
            Debug.Log($"[Health] {side} получил {amount} урона (умер={died}) — ХП игрока={playerHp}, ХП врага={enemyHp}");
        }

        /// <summary>Собственно визуальный эффект — реализуется наследником под конкретную сторону.</summary>
        protected abstract void PlayFeedback(int amount, int currentHealth, int maxHealth, bool died);
    }
}
