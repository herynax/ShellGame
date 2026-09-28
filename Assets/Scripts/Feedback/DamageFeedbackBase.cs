using System.Collections;
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
    ///
    /// Здесь же объявлен укол иглой: он НЕ является реакцией на урон и
    /// запускается до списания здоровья (см. HealthController.ApplyDamage).
    /// </summary>
    public abstract class DamageFeedbackBase : MonoBehaviour
    {
        [SerializeField] private HealthController _healthController;

        protected abstract TurnSide WatchedSide { get; }

        /// <summary>Есть ли в сцене игла, которой можно уколоть эту сторону.</summary>
        public abstract bool CanPlayNeedleInjection { get; }

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

        /// <summary>
        /// Готовит урон к списанию: вооружает иглу (NeedleMetalSqueak.ArmDamage) —
        /// после этого отложенный урон спишет либо событие анимации, либо её
        /// страховочный таймер. Если иглы в сцене нет, урон списывается сразу.
        ///
        /// Вызывается наследником прямо перед SetTrigger("Damage"), чтобы отсчёт
        /// страховки шёл от реального входа иглы в тело.
        /// </summary>
        protected void ArmNeedleDamage(Animator needleAnimator)
        {
            var needle = needleAnimator != null
                ? needleAnimator.GetComponent<NeedleMetalSqueak>()
                : null;

            if (needle != null)
            {
                needle.ArmDamage();
                return;
            }

            Debug.LogWarning($"[DamageFeedbackBase] На игле нет NeedleMetalSqueak — отложенный урон списывается сразу, без анимации укола ({WatchedSide}).", this);
            _healthController?.ApplyPendingDamage();
        }

        /// <summary>
        /// Проигрывает укол иглой по этой стороне: первая половина анимации
        /// (вход иглы в тело) и возврат иглы. Корутина — это и есть длительность
        /// укола.
        ///
        /// Сам урон внутри укола НЕ применяется здесь: его списывает событие
        /// анимации на объекте иглы (NeedleMetalSqueak.ApplyDamage) — момент
        /// попадания задаёт клип. Запускает и ждёт укол HealthController
        /// (ApplyDamage с needNeedleAnim: true + WaitForNeedleInjection).
        /// </summary>
        public abstract IEnumerator PlayNeedleInjection();
    }
}
