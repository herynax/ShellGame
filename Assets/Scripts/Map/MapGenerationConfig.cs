using System;

namespace ShellGame.Map
{
    [Serializable]
    public struct EncounterWeights
    {
        public float Enemy;
        public float Shop;
        public float Challenge;

        public EncounterWeights(float enemy, float shop, float challenge)
        {
            Enemy = enemy;
            Shop = shop;
            Challenge = challenge;
        }
    }

    // Обычный POCO. ScriptableObject-обёртка (MapGenerationConfig.asset) появится
    // в Unity-слое позже и будет просто конвертировать сериализованные поля в этот класс —
    // сам Map ничего про ScriptableObject не знает.
    [Serializable]
    public sealed class MapGenerationConfig
    {
        public int[] RegularLayerNodeCounts = { 3, 3, 4, 3, 2 };

        public int MinConnections = 1;
        public int MaxConnections = 2;
        public float WideForkChance = 0.15f; // шанс редкой развилки на 3 связи

        public EncounterWeights EarlyWeights = new EncounterWeights(0.75f, 0.15f, 0.10f);
        public EncounterWeights MiddleWeights = new EncounterWeights(0.55f, 0.20f, 0.25f);
        public EncounterWeights LateWeights = new EncounterWeights(0.65f, 0.15f, 0.20f);

        public int MaxEnemyStreak = 3;
        public int MaxSameTypeRegenAttempts = 5;   // локальный retry на уровне узла
        public int MaxFullRegenerationAttempts = 8; // полный re-seed карты
    }
}