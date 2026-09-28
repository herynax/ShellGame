using System;
using UnityEngine;

namespace ShellGame.Map
{
    [CreateAssetMenu(fileName = "MapStructureConfig", menuName = "ShellGame/Map/Map Structure Config")]
    public sealed class MapStructureConfig : ScriptableObject
    {
        [Header("--- MAP DEPTH ---")]
        [Range(8, 15)] public int MinTotalLayers = 10;
        [Range(8, 15)] public int MaxTotalLayers = 12;
        
        [Header("--- BRANCHING ---")]
        [Range(1, 4)] public int MaxParallelPaths = 4;
        [Range(2, 6)] public int MinLayersBetweenBranches = 3;
        [Range(2, 6)] public int MaxLayersBetweenBranches = 5;
        
        [Header("--- ARCHETYPE WEIGHTS (for random selection) ---")]
        [Tooltip("Relative weights for archetype selection. Higher = more likely.")]
        public float BranchWeight = 10f;
        public float StandardWeight = 30f;
        public float EliteWeight = 8f;
        public float EventWeight = 10f;
        public float ShopWeight = 6f;
        public float MergeWeight = 12f;
        
        [Header("--- SPECIAL LAYER CONSTRAINTS ---")]
        [Tooltip("Minimum layers between same special archetype (Elite, Event, Shop)")]
        public int MinSpecialLayerSeparation = 2;
        
        [Tooltip("Force at least one Elite layer before Boss")]
        public bool ForceEliteBeforeBoss = true;
        
        [Tooltip("Force at least one Shop layer")]
        public bool ForceShopLayer = true;
        
        [Tooltip("Force at least one Event layer")]
        public bool ForceEventLayer = true;
        
        [Header("--- NODE COUNTS ---")]
        [Range(1, 4)] public int DefaultMinNodes = 1;
        [Range(1, 4)] public int DefaultMaxNodes = 3;
        
        [Header("--- LEGACY COMPAT ---")]
        [Tooltip("If true, uses new layout system. If false, uses legacy RegularLayerNodeCounts.")]
        public bool UseNewLayoutSystem = true;

        public float GetWeight(MapLayerArchetype archetype)
        {
            return archetype switch
            {
                MapLayerArchetype.Branch => BranchWeight,
                MapLayerArchetype.Standard => StandardWeight,
                MapLayerArchetype.Elite => EliteWeight,
                MapLayerArchetype.Event => EventWeight,
                MapLayerArchetype.Shop => ShopWeight,
                MapLayerArchetype.Merge => MergeWeight,
                _ => 0f
            };
        }

        public void Validate()
        {
            if (MinTotalLayers > MaxTotalLayers) MaxTotalLayers = MinTotalLayers;
            if (MaxParallelPaths < 2) MaxParallelPaths = 2;
            if (MaxParallelPaths > 4) MaxParallelPaths = 4;
            if (MinLayersBetweenBranches < 2) MinLayersBetweenBranches = 2;
            if (MaxLayersBetweenBranches < MinLayersBetweenBranches) MaxLayersBetweenBranches = MinLayersBetweenBranches;
        }
    }
}