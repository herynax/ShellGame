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
    ///
    /// Добавлено: синхронизация с глобальным параметром FMOD "Dose Counter"
    /// (дискретный, 0..5). Параметр обновляется по стороне игрока при любом
    /// изменении дозы (укол/детокс), значение зажимается в 0..5 независимо
    /// от того, чему равен геймплейный "max" для этой стороны.
    /// </summary>
    public sealed class HealthController : MonoBehaviour
    {
        private const string DoseCounterParameterName = "Dose Counter";
        private const int DoseCounterMax = 5;

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

            UpdateDoseCounterParameter(TurnSide.Player);
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

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                ApplyDamage(TurnSide.Player, 1);
            }
        }

        /// <summary>
        /// Добавляет дозу стороне. Возвращает true, если именно этим уколом
        /// доза достигла или превысила порог — передозировка, сторона умирает
        /// немедленно (в отличие от старой модели "убивает следующий удар при
        /// уже нулевом ХП" — тут порог убивает сразу при пересечении).
        /// </summary>
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
            UpdateDoseCounterParameter(side);

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
            UpdateDoseCounterParameter(side);
        }

        /// <summary>
        /// Сбрасывает дозу стороны в 0 (например, при переходе между сценами
        /// в MusicManager). В отличие от Heal — не требует указывать amount
        /// и не трогает мёртвых.
        /// </summary>
        public void ResetDose(TurnSide side)
        {
            if (_dead.Contains(side))
                return;

            _current[side] = 0;
            GameEvents.RaiseHealthChanged(side, 0, GetMaxHealth(side));
            UpdateDoseCounterParameter(side);
        }

        /// <summary>
        /// Синхронизирует глобальный параметр FMOD "Dose Counter" с текущей
        /// дозой игрока. Параметр дискретный, 0..5, но геймплейный порог
        /// передозировки (max) на разных уровнях разный — поэтому дозу
        /// нельзя просто зажимать, её нужно растягивать на диапазон 0..5.
        ///
        /// Всего "живых" состояний дозы — max штук: 0, 1, ..., max-1 (сама
        /// доза == max — это уже смерть, отдельного состояния под неё не
        /// нужно). Их и распределяем равномерно по 6 значениям параметра
        /// (0..5): value = round(dose / (max - 1) * 5).
        ///
        /// Пример (max = 3): dose 0 -> 0, dose 1 -> 3, dose 2 (последний
        /// хит перед смертью) -> 5.
        /// Пример (max = 6, старое поведение): dose 0..5 -> 0..5 один в один.
        ///
        /// Округление — "к ближайшему, .5 вверх" (не банковское округление
        /// Mathf.Round/RoundToInt, которое дало бы 2 вместо 3 в примере выше).
        ///
        /// Для стороны Enemy параметр не трогаем (он глобальный и отражает
        /// состояние именно игрока, под это заточен звук).
        /// </summary>
        private void UpdateDoseCounterParameter(TurnSide side)
        {
            if (side != TurnSide.Player)
                return;

            int max = GetMaxHealth(side);
            int dose = GetHealth(side);

            int value;
            if (max <= 1)
            {
                // Вырожденный случай: один хит убивает — живых состояний,
                // кроме "полного здоровья", нет.
                value = 0;
            }
            else
            {
                float raw = (float)dose * DoseCounterMax / (max - 1);
                value = Mathf.Clamp(Mathf.FloorToInt(raw + 0.5f), 0, DoseCounterMax);
            }

            FMODUnity.RuntimeManager.StudioSystem.setParameterByName(DoseCounterParameterName, value);
        }
    }
}