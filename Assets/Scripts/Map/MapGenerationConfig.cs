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
        // [Obsolete("Use MapStructureConfig for layer generation instead")]]
        public int[] RegularLayerNodeCounts = { 3, 3, 4, 3, 2 };

        [Header("--- CONNECTION SETTINGS ---")]
        // [Obsolete("Use MapStructureConfig connection settings instead")]]
        public int MinConnections = 1;
        // [Obsolete("Use MapStructureConfig connection settings instead")]]
        public int MaxConnections = 2;
        // [Obsolete("Use MapStructureConfig connection settings instead")]]
        public float WideForkChance = 0.15f; // шанс редкой развилки на 3 связи

        [Header("--- TYPE WEIGHTS BY ZONE ---")]
        // [Obsolete("Use MapStructureConfig encounter weights instead")]]
        public EncounterWeights EarlyWeights = new EncounterWeights(0.75f, 0.15f, 0.10f, 0.05f);
        // [Obsolete("Use MapStructureConfig encounter weights instead")]]
        public EncounterWeights MiddleWeights = new EncounterWeights(0.55f, 0.20f, 0.25f, 0.0f);
        // [Obsolete("Use MapStructureConfig encounter weights instead")]]
        public EncounterWeights LateWeights = new EncounterWeights(0.65f, 0.15f, 0.20f, 0.0f);

        [Header("--- VALIDATION ---")]
        // [Obsolete("Validation now uses StructureConfig constraints")]]
        public int MaxEnemyStreak = 3;
        public int MaxSameTypeRegenAttempts = 5;   // локальный retry на уровне узла
        public int MaxFullRegenerationAttempts = 8; // полный re-seed карты

        [Header("--- NEW LAYOUT SYSTEM ---")]
        public MapStructureConfig StructureConfig;
        
        // Runtime field, not serialized
        [NonSerialized] public MapLayout Layout;
    }
}