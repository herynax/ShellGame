using System;
using UnityEngine;

namespace ShellGame.Map
{
    [CreateAssetMenu(fileName = "MapGenerationConfigAsset", menuName = "ShellGame/Map/Map Generation Config Asset")]
    public sealed class MapGenerationConfigAsset : ScriptableObject
    {
        [Header("--- STRUCTURE ---")]
        public MapStructureConfig StructureConfig;
        
        [Header("--- GENERATION SETTINGS (Legacy) ---")]
        public int[] RegularLayerNodeCounts = { 3, 3, 4, 3, 2 };
        
        public int MinConnections = 1;
        public int MaxConnections = 2;
        public float WideForkChance = 0.15f;
        
        public EncounterWeights EarlyWeights = new EncounterWeights(0.75f, 0.15f, 0.10f, 0.05f);
        public EncounterWeights MiddleWeights = new EncounterWeights(0.55f, 0.20f, 0.25f, 0.0f);
        public EncounterWeights LateWeights = new EncounterWeights(0.65f, 0.15f, 0.20f, 0.0f);

        public int MaxEnemyStreak = 3;
        public int MaxSameTypeRegenAttempts = 5;
        public int MaxFullRegenerationAttempts = 8;

        public MapGenerationConfig ToConfig()
        {
            var config = new MapGenerationConfig
            {
                RegularLayerNodeCounts = RegularLayerNodeCounts,
                MinConnections = MinConnections,
                MaxConnections = MaxConnections,
                WideForkChance = WideForkChance,
                EarlyWeights = EarlyWeights,
                MiddleWeights = MiddleWeights,
                LateWeights = LateWeights,
                MaxEnemyStreak = MaxEnemyStreak,
                MaxSameTypeRegenAttempts = MaxSameTypeRegenAttempts,
                MaxFullRegenerationAttempts = MaxFullRegenerationAttempts
            };

            // New structure config
            if (StructureConfig != null)
            {
                config.StructureConfig = StructureConfig;
            }

            return config;
        }
    }
}