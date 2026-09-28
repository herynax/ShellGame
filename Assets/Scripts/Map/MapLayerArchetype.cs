using System;

namespace ShellGame.Map
{
    public enum MapLayerArchetype
    {
        Start,      // 1 node, entry point
        Branch,     // 1→2/3/4, create meaningful choice (can branch from center)
        Standard,   // N→N, maintain width
        Elite,      // N→N, hard fights (same structure, different type weights)
        Event,      // N→N, random events
        Shop,       // N→N, heal/buy
        Merge,      // N→1/2, funnel paths
        Boss        // N→1, single boss
    }

    public static class MapLayerArchetypeExtensions
    {
        public static ArchetypeMetadata GetMetadata(this MapLayerArchetype archetype)
        {
            return s_metadata[(int)archetype];
        }

        private static readonly ArchetypeMetadata[] s_metadata = new ArchetypeMetadata[8]
        {
            new ArchetypeMetadata // Start
            {
                Archetype = MapLayerArchetype.Start,
                MinNodes = 1,
                MaxNodes = 1,
                DefaultOutDegree = 1,
                CanBranchFromCenter = false,
                MinLayerSeparationFromSame = 0,
                DefaultWeight = 0f, // Not randomly picked, always first
                RequiredTypeWeights = null
            },
            new ArchetypeMetadata // Branch
            {
                Archetype = MapLayerArchetype.Branch,
                MinNodes = 1,
                MaxNodes = 4,
                DefaultOutDegree = 1,
                CanBranchFromCenter = true,
                MinLayerSeparationFromSame = 3,
                DefaultWeight = 10f,
                RequiredTypeWeights = new EncounterWeights(0.7f, 0.1f, 0.15f, 0.05f)
            },
            new ArchetypeMetadata // Standard
            {
                Archetype = MapLayerArchetype.Standard,
                MinNodes = 1,
                MaxNodes = 4,
                DefaultOutDegree = 1,
                CanBranchFromCenter = false,
                MinLayerSeparationFromSame = 1,
                DefaultWeight = 30f,
                RequiredTypeWeights = null
            },
            new ArchetypeMetadata // Elite
            {
                Archetype = MapLayerArchetype.Elite,
                MinNodes = 1,
                MaxNodes = 4,
                DefaultOutDegree = 1,
                CanBranchFromCenter = false,
                MinLayerSeparationFromSame = 2,
                DefaultWeight = 8f,
                RequiredTypeWeights = new EncounterWeights(0.9f, 0.0f, 0.1f, 0.0f)
            },
            new ArchetypeMetadata // Event
            {
                Archetype = MapLayerArchetype.Event,
                MinNodes = 1,
                MaxNodes = 4,
                DefaultOutDegree = 1,
                CanBranchFromCenter = false,
                MinLayerSeparationFromSame = 2,
                DefaultWeight = 10f,
                RequiredTypeWeights = new EncounterWeights(0.3f, 0.1f, 0.1f, 0.5f)
            },
            new ArchetypeMetadata // Shop
            {
                Archetype = MapLayerArchetype.Shop,
                MinNodes = 1,
                MaxNodes = 4,
                DefaultOutDegree = 1,
                CanBranchFromCenter = false,
                MinLayerSeparationFromSame = 3,
                DefaultWeight = 6f,
                RequiredTypeWeights = new EncounterWeights(0.0f, 1.0f, 0.0f, 0.0f)
            },
            new ArchetypeMetadata // Merge
            {
                Archetype = MapLayerArchetype.Merge,
                MinNodes = 1,
                MaxNodes = 4,
                DefaultOutDegree = 1,
                CanBranchFromCenter = false,
                MinLayerSeparationFromSame = 2,
                DefaultWeight = 12f,
                RequiredTypeWeights = null
            },
            new ArchetypeMetadata // Boss
            {
                Archetype = MapLayerArchetype.Boss,
                MinNodes = 1,
                MaxNodes = 1,
                DefaultOutDegree = 0,
                CanBranchFromCenter = false,
                MinLayerSeparationFromSame = 0,
                DefaultWeight = 0f, // Not randomly picked, always last
                RequiredTypeWeights = new EncounterWeights(0.0f, 0.0f, 0.0f, 0.0f)
            }
        };

        public struct ArchetypeMetadata
        {
            public MapLayerArchetype Archetype;
            public int MinNodes;
            public int MaxNodes;
            public int DefaultOutDegree;
            public bool CanBranchFromCenter;
            public int MinLayerSeparationFromSame;
            public float DefaultWeight;
            public EncounterWeights? RequiredTypeWeights;
        }
    }
}