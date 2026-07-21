using System.Collections.Generic;
using ShellGame.Core;
using UnityEngine;

namespace ShellGame.Health
{
    /// <summary>
    /// Раньше — здоровье (зубы), убывающее к нулю. Теперь — доза наркотика,
    /// накапливающаяся от нуля к порогу передозировки: чем больше текущее
    /// значение, тем сильнее "накачана" сторона. При достижении/превышении
    /// порога — передозировка, смерть наступает сразу этим же уколом (без
    /// дополнительного "удара при уже нулевом", как было раньше — теперь
    /// сама метафора этого не требует: перебор дозы убивает сразу).
    ///
    /// Названия методов (ApplyDamage/Heal/GetHealth/GetMaxHealth) оставлены
    /// как есть — весь остальной код (GameManager, предметы, фидбек) их уже
    /// использует, менять сигнатуры не потребовалось, поменялась только
    /// внутренняя механика и смысл чисел.
    /// </summary>
    public sealed class HealthController : MonoBehaviour
    {
        private readonly Dictionary<TurnSide, int> _current = new Dictionary<TurnSide, int>();
        private readonly Dictionary<TurnSide, int> _max = new Dictionary<TurnSide, int>();
        private readonly HashSet<TurnSide> _dead = new HashSet<TurnSide>();

        /// <summary>playerMaxHealth/enemyMaxHealth теперь — порог передозировки (толерантность). Доза стартует с нуля, а не с максимума.</summary>
        public void Initialize(int playerMaxHealth, int enemyMaxHealth)
        {
            _max[TurnSide.Player] = playerMaxHealth;
            _max[TurnSide.Enemy] = enemyMaxHealth;
            _current[TurnSide.Player] = 0;
            _current[TurnSide.Enemy] = 0;
            _dead.Clear();

            GameEvents.RaiseHealthChanged(TurnSide.Player, 0, playerMaxHealth);
            GameEvents.RaiseHealthChanged(TurnSide.Enemy, 0, enemyMaxHealth);
        }

        /// <summary>Текущая доза стороны.</summary>
        public int GetHealth(TurnSide side) => _current.TryGetValue(side, out var v) ? v : 0;

        /// <summary>Порог передозировки (толерантность) стороны.</summary>
        public int GetMaxHealth(TurnSide side) => _max.TryGetValue(side, out var v) ? v : 0;

        public bool IsDead(TurnSide side) => _dead.Contains(side);

        /// <summary>Доля дозы от порога (0..1) — удобно для визуальных эффектов (психоделика и т.п.).</summary>
        public float GetDoseFraction(TurnSide side)
        {
            int max = GetMaxHealth(side);
            return max > 0 ? Mathf.Clamp01((float)GetHealth(side) / max) : 0f;
        }

        /// <summary>
        /// Добавляет дозу стороне. Возвращает true, если именно этим уколом
        /// доза достигла или превысила порог — передозировка, сторона умирает
        /// немедленно (в отличие от старой модели "убивает следующий удар при
        /// уже нулевом ХП" — тут порог убивает сразу при пересечении).
        /// </summary>
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                ApplyDamage(TurnSide.Player, 1);
            }
        }

        public bool ApplyDamage(TurnSide side, int amount)
        {
            if (_dead.Contains(side) || amount <= 0)
                return false;

            int max = GetMaxHealth(side);
            int rawDose = GetHealth(side) + amount;
            bool overdosed = rawDose >= max;
            int clampedDose = Mathf.Min(max, rawDose);
            _current[side] = clampedDose;

            GameEvents.RaiseHealthChanged(side, clampedDose, max);
            GameEvents.RaiseDamageTaken(side, amount, clampedDose, max, overdosed);

            if (overdosed)
            {
                _dead.Add(side);
                GameEvents.RaiseSideDied(side);
            }

            return overdosed;
        }

        /// <summary>Снижает дозу (детокс/"Хилка"). Мёртвых не откачивает — воскрешения пока нет.</summary>
        public void Heal(TurnSide side, int amount)
        {
            if (_dead.Contains(side) || amount <= 0)
                return;

            int newDose = Mathf.Max(0, GetHealth(side) - amount);
            _current[side] = newDose;
            GameEvents.RaiseHealthChanged(side, newDose, GetMaxHealth(side));
        }
    }
}
