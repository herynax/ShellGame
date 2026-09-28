using System;
using UnityEngine;

namespace ShellGame.Map
{
    [Serializable]
    public struct MapLayerRecipe
    {
        public MapLayerArchetype Archetype;
        [Range(1, 4)] public int MinNodes;
        [Range(1, 4)] public int MaxNodes;
        public float Weight;
        
        [Tooltip("Optional override for type weights on this specific layer")]
        public EncounterWeights? OverrideTypeWeights;
        
        [Tooltip("Optional forced node count (overrides Min/Max when > 0)")]
        public int ForcedNodeCount;

        public MapLayerRecipe(MapLayerArchetype archetype, int minNodes = 1, int maxNodes = 4, float weight = 1f)
        {
            Archetype = archetype;
            MinNodes = minNodes;
            MaxNodes = maxNodes;
            Weight = weight;
            OverrideTypeWeights = null;
            ForcedNodeCount = 0;
        }

        public int ResolveNodeCount(IRunRandom random)
        {
            if (ForcedNodeCount > 0) return ForcedNodeCount;
            var meta = Archetype.GetMetadata();
            int min = Math.Max(MinNodes, meta.MinNodes);
            int max = Math.Min(MaxNodes, meta.MaxNodes);
            return random.Next(min, max + 1);
        }

        public EncounterWeights GetEffectiveWeights()
        {
            if (OverrideTypeWeights.HasValue) return OverrideTypeWeights.Value;
            var meta = Archetype.GetMetadata();
            if (meta.RequiredTypeWeights.HasValue) return meta.RequiredTypeWeights.Value;
            return new EncounterWeights(0.75f, 0.15f, 0.1f, 0.0f);
        }
    }
}