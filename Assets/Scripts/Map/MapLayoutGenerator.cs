using System;
using System.Collections.Generic;
using System.Linq;

namespace ShellGame.Map
{
    public static class MapLayoutGenerator
    {
        public static MapLayout Generate(int seed, MapGenerationConfig config)
        {
            var structureConfig = config.StructureConfig;
            if (structureConfig == null || !structureConfig.UseNewLayoutSystem)
                return GenerateLegacy(config);

            var random = new SeededRandomSource(seed);
            structureConfig.Validate();

            int totalLayers = random.Next(structureConfig.MinTotalLayers, structureConfig.MaxTotalLayers + 1);
            
            var layersList = new List<LayerInfo>();
            
            int currentNodeId = 0;
            int currentPathId = 0;
            int maxParallelPaths = 1;
            
            var structureCfg = config.StructureConfig;

            // Layer 0: Start - always 1 node
            var startLayer = new LayerInfo(0, MapLayerArchetype.Start, 1, currentNodeId);
            startLayer.PathIds = new[] { 0 };
            layersList.Add(startLayer);
            currentNodeId = 1;
            currentPathId = 1;

            // Generate middle layers with ARBITRARY node counts
            for (int layerIndex = 1; layerIndex < totalLayers - 1; layerIndex++)
            {
                // Determine node count for this layer - ARBITRARY within min/max bounds
                int minNodes = structureCfg.DefaultMinNodes;
                int maxNodes = Math.Min(structureCfg.DefaultMaxNodes, structureCfg.MaxParallelPaths);
                
                // Pick archetype for this layer
                var archetype = PickArchetypeForLayer(
                    random, layersList, layerIndex, totalLayers - 1,
                    maxParallelPaths, config.StructureConfig);
                
                var meta = archetype.GetMetadata();
                
                // Allow FULL range of arbitrary node counts within min/max
                int nodeCount = meta.MinNodes == meta.MaxNodes 
                    ? meta.MinNodes 
                    : random.Next(meta.MinNodes, meta.MaxNodes + 1);
                
                // Clamp to max parallel paths but allow arbitrary within range
                nodeCount = Math.Clamp(nodeCount, minNodes, Math.Min(maxNodes, structureCfg.MaxParallelPaths));
                maxParallelPaths = Math.Max(maxParallelPaths, nodeCount);

                var layer = new LayerInfo(layerIndex, archetype, nodeCount, currentNodeId);
                currentNodeId += nodeCount;
                
                // Assign path IDs - maintain visual order
                AssignPathIds(layer, layersList, archetype, ref currentPathId, new SeededRandomSource(Environment.TickCount));
                
                layersList.Add(layer);
            }

            // Last layer: Boss - always 1 node
            var bossLayer = new LayerInfo(layersList.Count, MapLayerArchetype.Boss, 1, currentNodeId);
            bossLayer.PathIds = new[] { 0 };
            layersList.Add(bossLayer);

            // Enforce forced special layers
            EnforceSpecialLayers(layersList, new SeededRandomSource(Environment.TickCount), config.StructureConfig);

            // Build path ID array
            int totalNodes = layersList.Sum(l => l.NodeCount);
            var pathIds = new int[totalNodes];
            int idx = 0;
            foreach (var layer in layersList)
            {
                Array.Copy(layer.PathIds, 0, pathIds, idx, layer.NodeCount);
                idx += layer.NodeCount;
            }

            return new MapLayout(layersList.ToArray(), pathIds, layersList.Max(l => l.NodeCount));
        }

        private static MapLayerArchetype PickArchetypeForLayer(
            SeededRandomSource random, 
            List<LayerInfo> layers, 
            int layerIndex, 
            int totalLayers,
            int currentMaxPaths,
            MapStructureConfig structureCfg)
        {
            int remainingLayers = totalLayers - layers.Count - 1; // -1 for boss

            // Force special layers if not present and running out of space
            bool hasElite = layers.Any(l => l.Archetype == MapLayerArchetype.Elite);
            bool hasShop = layers.Any(l => l.Archetype == MapLayerArchetype.Shop);
            bool hasEvent = layers.Any(l => l.Archetype == MapLayerArchetype.Event);

            if (structureCfg.ForceEliteBeforeBoss && !hasElite && remainingLayers <= 3)
                return MapLayerArchetype.Elite;
            if (structureCfg.ForceShopLayer && !hasShop && remainingLayers <= 4)
                return MapLayerArchetype.Shop;
            if (structureCfg.ForceEventLayer && !hasEvent && remainingLayers <= 5)
                return MapLayerArchetype.Event;

            // Build candidate archetypes with weights
            var candidates = new List<(MapLayerArchetype archetype, float weight)>();

            foreach (var archetype in new[] 
            {
                MapLayerArchetype.Standard,
                MapLayerArchetype.Branch,
                MapLayerArchetype.Merge,
                MapLayerArchetype.Elite,
                MapLayerArchetype.Event,
                MapLayerArchetype.Shop
            })
            {
                var meta = archetype.GetMetadata();
                
                // Skip Start and Boss
                if (archetype == MapLayerArchetype.Start || archetype == MapLayerArchetype.Boss)
                    continue;

                // Separation constraints
                if (meta.MinLayerSeparationFromSame > 0)
                {
                    bool recent = layers.Skip(Math.Max(0, layers.Count - meta.MinLayerSeparationFromSame))
                        .Any(l => l.Archetype == archetype);
                    if (recent) continue;
                }

                // Don't exceed max parallel paths
                if (archetype == MapLayerArchetype.Branch && layers[^1].NodeCount >= structureCfg.MaxParallelPaths)
                    continue;

                // Merge needs something to merge
                if (archetype == MapLayerArchetype.Merge && layers[^1].NodeCount <= 1)
                    continue;

                // Don't branch if already at max width
                if (archetype == MapLayerArchetype.Branch && layers[^1].NodeCount >= structureCfg.MaxParallelPaths)
                    continue;

                // Must merge if approaching boss with multiple paths
                int remaining = totalLayers - layers.Count - 1;
                if (archetype != MapLayerArchetype.Merge && remaining <= layers[^1].NodeCount + 1)
                    continue;

                candidates.Add((archetype, meta.DefaultWeight));
            }

            if (candidates.Count == 0)
                return MapLayerArchetype.Standard;

            // Weighted random
            float totalWeight = candidates.Sum(c => c.weight);
            float roll = (float)random.Value * totalWeight;
            float accum = 0f;
            
            foreach (var c in candidates)
            {
                accum += c.weight;
                if (roll <= accum) return c.archetype;
            }
            
            return candidates[0].archetype;
        }

        private static void AssignPathIds(
            LayerInfo layer, 
            List<LayerInfo> previousLayers, 
            MapLayerArchetype archetype, 
            ref int currentPathId, 
            SeededRandomSource random)
        {
            LayerInfo? prevLayer = previousLayers.Count > 0 ? previousLayers[^1] : null;

            switch (archetype)
            {
                case MapLayerArchetype.Branch:
                    // Branch: create new path IDs for each new branch
                    if (prevLayer.HasValue && prevLayer.Value.NodeCount == 1)
                    {
                        // Single source branching out
                        layer.PathIds = new int[layer.NodeCount];
                        for (int i = 0; i < layer.NodeCount; i++)
                            layer.PathIds[i] = currentPathId++;
                        layer.NewPathIds = layer.PathIds;
                        if (previousLayers.Count > 0)
                            layer.IncomingPathIds = new[] { previousLayers[^1].PathIds[0] };
                    }
                    else
                    {
                        // Multiple sources - distribute paths
                        layer.PathIds = new int[layer.NodeCount];
                        for (int i = 0; i < layer.NodeCount; i++)
                            layer.PathIds[i] = currentPathId++;
                        layer.NewPathIds = layer.PathIds;
                        layer.IncomingPathIds = previousLayers.Count > 0 ? previousLayers[^1].PathIds : Array.Empty<int>();
                    }
                    break;

                case MapLayerArchetype.Merge:
                    // Merge: converge paths - each new node gets a path ID from sources
                    if (prevLayer.HasValue)
                    {
                        var prev = prevLayer.Value;
                        layer.PathIds = new int[layer.NodeCount];
                        for (int i = 0; i < layer.NodeCount; i++)
                        {
                            int srcIdx = i % prev.NodeCount;
                            layer.PathIds[i] = prev.PathIds[srcIdx];
                        }
                        layer.IncomingPathIds = prev.PathIds;
                    }
                    break;

                case MapLayerArchetype.Standard:
                case MapLayerArchetype.Elite:
                case MapLayerArchetype.Event:
                case MapLayerArchetype.Shop:
                    // Standard: preserve path continuity
                    if (prevLayer.HasValue)
                    {
                        var prev = prevLayer.Value;
                        layer.PathIds = new int[layer.NodeCount];
                        for (int i = 0; i < layer.NodeCount; i++)
                        {
                            int srcIdx = Math.Min(i, prev.NodeCount - 1);
                            layer.PathIds[i] = prev.PathIds[srcIdx];
                        }
                        layer.IncomingPathIds = prev.PathIds;
                    }
                    break;

                case MapLayerArchetype.Start:
                    layer.PathIds = new[] { 0 };
                    layer.NewPathIds = new[] { 0 };
                    break;

                case MapLayerArchetype.Boss:
                    layer.PathIds = new[] { 0 };
                    layer.IncomingPathIds = previousLayers.Count > 0 ? previousLayers[^1].PathIds : Array.Empty<int>();
                    break;

                default:
                    layer.PathIds = new int[layer.NodeCount];
                    break;
            }
        }

        private static void EnforceSpecialLayers(
            List<LayerInfo> layers, 
            SeededRandomSource random, 
            MapStructureConfig structureCfg)
        {
            var candidates = new List<int>();
            for (int i = 2; i < layers.Count - 2; i++)
            {
                if (layers[i].Archetype == MapLayerArchetype.Standard)
                    candidates.Add(i);
            }

            bool hasElite = layers.Any(l => l.Archetype == MapLayerArchetype.Elite);
            bool hasShop = layers.Any(l => l.Archetype == MapLayerArchetype.Shop);
            bool hasEvent = layers.Any(l => l.Archetype == MapLayerArchetype.Event);

            if (structureCfg.ForceEliteBeforeBoss && !hasElite && candidates.Count > 0)
            {
                int idx = candidates[random.Next(candidates.Count)];
                var layer = layers[idx];
                layers[idx] = new LayerInfo(layer.Index, MapLayerArchetype.Elite, 
                    layer.NodeCount, layer.StartNodeId);
            }

            if (structureCfg.ForceShopLayer && !hasShop && candidates.Count > 0)
            {
                int idx = candidates[random.Next(candidates.Count)];
                var layer = layers[idx];
                layers[idx] = new LayerInfo(layer.Index, MapLayerArchetype.Shop, 
                    layer.NodeCount, layer.StartNodeId);
            }

            if (structureCfg.ForceEventLayer && !hasEvent && candidates.Count > 0)
            {
                int idx = candidates[random.Next(candidates.Count)];
                var layer = layers[idx];
                layers[idx] = new LayerInfo(layer.Index, MapLayerArchetype.Event, 
                    layer.NodeCount, layer.StartNodeId);
            }
        }

        // Legacy fallback
        private static MapLayout GenerateLegacy(MapGenerationConfig config)
        {
            var layersList = new List<LayerInfo>();
            int nodeId = 0;

            var startLayer = new LayerInfo(0, MapLayerArchetype.Start, 1, nodeId);
            startLayer.PathIds = new[] { 0 };
            layersList.Add(startLayer);
            nodeId++;

            for (int i = 0; i < config.RegularLayerNodeCounts.Length; i++)
            {
                int count = config.RegularLayerNodeCounts[i];
                var layer = new LayerInfo(i + 1, MapLayerArchetype.Standard, count, nodeId);
                layer.PathIds = Enumerable.Range(0, count).ToArray();
                layersList.Add(layer);
                nodeId += count;
            }

            var bossLayer = new LayerInfo(layersList.Count, MapLayerArchetype.Boss, 1, nodeId);
            bossLayer.PathIds = new[] { 0 };
            layersList.Add(bossLayer);

            int totalNodes = layersList.Sum(l => l.NodeCount);
            var pathIds = new int[totalNodes];
            int idx = 0;
            foreach (var layer in layersList)
            {
                Array.Copy(layer.PathIds, 0, pathIds, idx, layer.NodeCount);
                idx += layer.NodeCount;
            }

            return new MapLayout(layersList.ToArray(), pathIds, layersList.Max(l => l.NodeCount));
        }
    }
}