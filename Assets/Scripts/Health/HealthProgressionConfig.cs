using System.Collections.Generic;
using UnityEngine;

namespace ShellGame.Health
{
    /// <summary>
    /// Стартовое здоровье (зубы) игрока и противника по прогрессу РАНА — см.
    /// ГДД: "в зависимости от уровня у игрока и врага есть разное начальное
    /// количество здоровья".
    ///
    /// Раньше здоровье бралось из таблицы по индексу уровня, а при промахе —
    /// запасная формула 10 + 2*levelIndex. Для инкрементального рана из ~55
    /// энкаунтеров это давало 118 HP на финальном боссе и разрыв 6 → 22 HP
    /// сразу после L6.
    ///
    /// Теперь здоровье задаётся долей пройденного рана
    /// (RunDifficultyConfig): HP = round(Base + Gain · p), где p ∈ [0,1].
    /// Для обычной линии Gain = 0: и игрок, и обычный враг держат статичное
    /// HP весь ран (напр. 5/5), а сложность растёт за счёт точности врага,
    /// а не «туши». Особые бои (минибосс/босс) получают больше HP через
    /// EnemyAIConfig.HealthMultiplier. Таблица _entries — тонкий
    /// переопределитель поверх кривой.
    /// </summary>
    [CreateAssetMenu(fileName = "HealthProgressionConfig", menuName = "ShellGame/Gameplay/Health Progression Config")]
    public sealed class HealthProgressionConfig : ScriptableObject
    {
        [System.Serializable]
        public sealed class Entry
        {
            [Tooltip("Доля пройденного рана 0..1, к которой относится запись. Раньше здесь был индекс уровня.")]
            [Range(0f, 1f)] public float RunProgress01;
            public int PlayerMaxHealth = 10;
            public int EnemyMaxHealth = 10;
        }

        [SerializeField] private List<Entry> _entries = new List<Entry>();

        [Header("Кривая здоровья по прогрессу рана (используется, если нет точной записи)")]
        [Min(1)] public int PlayerHealthBase = 5;
        [Tooltip("Для обычной линии 0 — HP игрока статично весь ран.")]
        [Min(0)] public int PlayerHealthGain = 0;
        [Min(1)] public int EnemyHealthBase = 5;
        [Tooltip("Для обычной линии 0 — HP обычного врага статично; особые бои получают бонус через EnemyAIConfig.HealthMultiplier.")]
        [Min(0)] public int EnemyHealthGain = 0;

        [Header("Урон за одно попадание (вырванный зуб)")]
        public int DamagePerHit = 1;

        [Header("Задержки урона игроку (раньше лежали в инспекторе GameManager)")]
        [Tooltip("Пауза после урона игроку, прежде чем ход вернётся к нему " +
                 "(идёт после отыгрыша анимации укола, реакции и списания урона). " +
                 "0 — без дополнительной паузы. Задержка ДО списания урона игроку " +
                 "больше не нужна: её задаёт момент входа иглы в тело, то есть " +
                 "событие анимации (NeedleMetalSqueak.ApplyDamage).")]
        public float TurnReturnDelayAfterPlayerDamage = 0.35f;

        /// <summary>
        /// Здоровье по доле пройденного рана. Точная запись из _entries
        /// побеждает кривую; иначе берётся ближайшая запись с меньшим
        /// прогрессом; иначе — сама кривая.
        /// </summary>
        public (int playerMax, int enemyMax) GetHealthForProgress(float runProgress01)
        {
            float p = Mathf.Clamp01(runProgress01);

            Entry best = null;
            foreach (var entry in _entries)
            {
                if (Mathf.Approximately(entry.RunProgress01, p))
                    return (Mathf.Max(1, entry.PlayerMaxHealth), Mathf.Max(1, entry.EnemyMaxHealth));

                if (entry.RunProgress01 <= p && (best == null || entry.RunProgress01 > best.RunProgress01))
                    best = entry;
            }

            if (best != null)
                return (Mathf.Max(1, best.PlayerMaxHealth), Mathf.Max(1, best.EnemyMaxHealth));

            int player = Mathf.Max(1, Mathf.RoundToInt(PlayerHealthBase + PlayerHealthGain * p));
            int enemy = Mathf.Max(1, Mathf.RoundToInt(EnemyHealthBase + EnemyHealthGain * p));
            return (player, enemy);
        }

        /// <summary>Совместимость со старыми вызовами: прогресс берётся из индекса уровня по старой шкале 12 уровней.</summary>
        public (int playerMax, int enemyMax) GetHealthForLevel(int levelIndex) =>
            GetHealthForProgress(Mathf.Clamp01(Mathf.Max(0, levelIndex) / 12f));
    }
}
