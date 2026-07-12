using System.Collections.Generic;
using ShellGame.Core;
using UnityEngine;

namespace ShellGame.Health
{
    /// <summary>
    /// Здоровье (зубы) игрока и противника.
    ///
    /// Логика смерти по ГДД: "Когда у игрока не остаётся здоровья (зубов),
    /// при получении урона он умирает" — то есть само достижение нуля ещё не
    /// убивает; убивает СЛЕДУЮЩИЙ удар, нанесённый уже при нулевом здоровье.
    /// ApplyDamage — единственная точка входа для урона, чтобы это правило
    /// проверялось в одном месте.
    /// </summary>
    public sealed class HealthController : MonoBehaviour
    {
        private readonly Dictionary<TurnSide, int> _current = new Dictionary<TurnSide, int>();
        private readonly Dictionary<TurnSide, int> _max = new Dictionary<TurnSide, int>();
        private readonly HashSet<TurnSide> _dead = new HashSet<TurnSide>();

        public void Initialize(int playerMaxHealth, int enemyMaxHealth)
        {
            _max[TurnSide.Player] = playerMaxHealth;
            _max[TurnSide.Enemy] = enemyMaxHealth;
            _current[TurnSide.Player] = playerMaxHealth;
            _current[TurnSide.Enemy] = enemyMaxHealth;
            _dead.Clear();

            GameEvents.RaiseHealthChanged(TurnSide.Player, playerMaxHealth, playerMaxHealth);
            GameEvents.RaiseHealthChanged(TurnSide.Enemy, enemyMaxHealth, enemyMaxHealth);
        }

        public int GetHealth(TurnSide side) => _current.TryGetValue(side, out var v) ? v : 0;
        public int GetMaxHealth(TurnSide side) => _max.TryGetValue(side, out var v) ? v : 0;
        public bool IsDead(TurnSide side) => _dead.Contains(side);

        /// <summary>Наносит урон стороне. Возвращает true, если сторона умерла именно от этого удара.</summary>
        public bool ApplyDamage(TurnSide side, int amount)
        {
            if (_dead.Contains(side))
                return false;

            bool wasAlreadyAtZero = GetHealth(side) <= 0;

            int newHealth = Mathf.Max(0, GetHealth(side) - amount);
            _current[side] = newHealth;
            GameEvents.RaiseHealthChanged(side, newHealth, GetMaxHealth(side));

            bool died = wasAlreadyAtZero;
            if (died)
                _dead.Add(side);

            // Отдельное событие именно "удар произошёл" — на него реагирует
            // визуальный фидбек (тряска/виньетка), в отличие от HealthChanged
            // оно не стреляет при Initialize().
            GameEvents.RaiseDamageTaken(side, amount, newHealth, GetMaxHealth(side), died);

            if (died)
                GameEvents.RaiseSideDied(side);

            return died;
        }
    }
}