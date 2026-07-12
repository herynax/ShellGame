using System.Collections.Generic;
using UnityEngine;

namespace ShellGame.Health
{
    /// <summary>
    /// Стартовое здоровье (зубы) игрока и противника по уровням — см. ГДД:
    /// "В зависимости от уровня у игрока и врага есть разное начальное
    /// количество здоровья". Если для уровня нет точной записи, используется
    /// ближайшая заданная запись с меньшим индексом, иначе — запасная формула.
    /// </summary>
    [CreateAssetMenu(fileName = "HealthProgressionConfig", menuName = "ShellGame/Gameplay/Health Progression Config")]
    public sealed class HealthProgressionConfig : ScriptableObject
    {
        [System.Serializable]
        public sealed class Entry
        {
            public int LevelIndex;
            public int PlayerMaxHealth = 10;
            public int EnemyMaxHealth = 10;
        }

        [SerializeField] private List<Entry> _entries = new List<Entry>();

        [Header("Урон за одно попадание (вырванный зуб)")]
        public int DamagePerHit = 1;

        public (int playerMax, int enemyMax) GetHealthForLevel(int levelIndex)
        {
            Entry best = null;
            foreach (var entry in _entries)
            {
                if (entry.LevelIndex == levelIndex)
                    return (entry.PlayerMaxHealth, entry.EnemyMaxHealth);

                if (entry.LevelIndex <= levelIndex && (best == null || entry.LevelIndex > best.LevelIndex))
                    best = entry;
            }

            if (best != null)
                return (best.PlayerMaxHealth, best.EnemyMaxHealth);

            // Запасная формула, если ни одна запись не задана дизайнером в списке.
            int fallback = 10 + levelIndex * 2;
            return (fallback, fallback);
        }
    }
}
