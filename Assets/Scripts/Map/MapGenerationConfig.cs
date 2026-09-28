using System;
using UnityEngine;

namespace ShellGame.Map
{
    [Serializable]
    public struct EncounterWeights
    {
        public float Enemy;
        public float Shop;
        public float Challenge;
        public float Event;

        public EncounterWeights(float enemy, float shop, float challenge, float eventWeight = 0f)
        {
            Enemy = enemy;
            Shop = shop;
            Challenge = challenge;
            Event = eventWeight;
        }
    }

    // Обычный POCO. ScriptableObject-обёртка (MapGenerationConfigAsset) появится
    // в Unity-слое позже и будет просто конвертировать сериализованные поля в этот класс —
    // сам Map ничего про ScriptableObject не знает.
    [Serializable]
    public sealed class MapGenerationConfig
    {
        [Header("--- LEGACY LAYER COUNTS (used when StructureConfig.UseNewLayoutSystem = false) ---")]
        public int[] RegularLayerNodeCounts = { 3, 3, 4, 3, 2 };

        [Header("--- CONNECTION SETTINGS ---")]
        public int MinConnections = 1;
        public int MaxConnections = 2;
        public float WideForkChance = 0.15f; // шанс редкой развилки на 3 связи

        [Header("--- TYPE WEIGHTS BY ZONE ---")]
        public EncounterWeights EarlyWeights = new EncounterWeights(0.75f, 0.15f, 0.10f, 0.05f);
        public EncounterWeights MiddleWeights = new EncounterWeights(0.55f, 0.20f, 0.25f, 0.0f);
        public EncounterWeights LateWeights = new EncounterWeights(0.65f, 0.15f, 0.20f, 0.0f);

        [Header("--- VALIDATION ---")]
        public int MaxEnemyStreak = 3;
        public int MaxSameTypeRegenAttempts = 5;   // локальный retry на уровне узла
        public int MaxFullRegenerationAttempts = 8; // полный re-seed карты

        [Header("--- NEW LAYOUT SYSTEM ---")]
        public MapStructureConfig StructureConfig;
        
        // Runtime field, not serialized
        [NonSerialized] public MapLayout Layout;
    }
}