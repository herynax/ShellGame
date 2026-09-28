using System;
using System.Collections.Generic;
using System.Linq;

namespace ShellGame.Map
{
    public static class ConnectionBuilder
    {
        public static void BuildConnections(MapLayer[] layers, MapNode[] nodes, MapGenerationConfig config, IRunRandom random)
        {
            var byId = nodes.ToDictionary(n => n.Id);
            var layout = config.Layout;

            for (int i = 0; i < layers.Length - 1; i++)
            {
                var currentLayer = layers[i];
                var nextLayer = layers[i + 1];
                var current = currentLayer.NodeIds;
                var next = nextLayer.NodeIds;

                MapLayerArchetype nextArchetype = MapLayerArchetype.Standard;
                MapLayerArchetype currentArchetype = MapLayerArchetype.Standard;

                if (layout != null && layout.Layers != null)
                {
                    if (i < layout.Layers.Length) currentArchetype = layout.Layers[i].Archetype;
                    if (i + 1 < layout.Layers.Length) nextArchetype = layout.Layers[i + 1].Archetype;
                }

                // Boss convergence - all to one
                if (nextArchetype == MapLayerArchetype.Boss && next.Length == 1)
                {
                    foreach (var sourceId in current)
                        byId[sourceId].Connections = new[] { next[0] };
                    continue;
                }

                var incoming = next.ToDictionary(id => id, _ => 0);

                // Build connections based on archetype transition with PathId ordering
                BuildConnectionsForArchetype(
                    currentLayer, nextLayer, 
                    currentArchetype, nextArchetype,
                    byId, incoming, random, layout, i);

                // Ensure every node in next layer has at least one incoming
                foreach (var kv in incoming.Where(kv => kv.Value == 0).ToArray())
                {
                    var sourceId = current[random.Next(current.Length)];
                    var sourceNode = byId[sourceId];
                    if (!sourceNode.Connections.Contains(kv.Key))
                        sourceNode.Connections = sourceNode.Connections.Append(kv.Key).ToArray();
                }

                // Ensure every node in current layer has at least one outgoing connection
                // (except the layer before Boss where all connect to boss, and Boss layer itself)
                bool isLastRegularLayer = nextArchetype == MapLayerArchetype.Boss;
                if (!isLastRegularLayer)
                {
                    foreach (var sourceId in current)
                    {
                        var sourceNode = byId[sourceId];
                        if (sourceNode.Connections == null || sourceNode.Connections.Length == 0)
                        {
                            // Connect to first target as fallback
                            var targetId = next[0];
                            sourceNode.Connections = new[] { targetId };
                            incoming[targetId]++;
                        }
                    }
                }
            }
        }

        private static void BuildConnectionsForArchetype(
            MapLayer currentLayer, MapLayer nextLayer,
            MapLayerArchetype currentArchetype, MapLayerArchetype nextArchetype,
            Dictionary<int, MapNode> byId, Dictionary<int, int> incoming, IRunRandom random, MapLayout layout, int layerIndex)
        {
            var current = currentLayer.NodeIds;
            var next = nextLayer.NodeIds;

            // Get PathIds from layout for visual ordering
            int[] currentPathIds = null;
            int[] nextPathIds = null;
            
            if (layout != null && layout.Layers != null && layerIndex < layout.Layers.Length && layerIndex + 1 < layout.Layers.Length)
            {
                currentPathIds = layout.Layers[layerIndex].PathIds;
                nextPathIds = layout.Layers[layerIndex + 1].PathIds;
            }

            // Sort sources and targets by PathId (left to right visual order)
            var sortedCurrent = currentPathIds != null 
                ? current.Select((id, idx) => new { Id = id, PathId = currentPathIds[idx] }).OrderBy(x => x.PathId).Select(x => x.Id).ToArray()
                : current;
            
            var sortedNext = nextPathIds != null
                ? next.Select((id, idx) => new { Id = id, PathId = nextPathIds[idx] }).OrderBy(x => x.PathId).Select(x => x.Id).ToArray()
                : next;

            switch (currentArchetype)
            {
                case MapLayerArchetype.Start:
                    // Start connects to all next nodes in visual order
                    if (sortedNext.Length == 1)
                    {
                        byId[sortedCurrent[0]].Connections = sortedNext;
                    }
                    else
                    {
                        // Fan out: each source gets contiguous block of targets
                        BuildBranchConnectionsOrdered(sortedCurrent, sortedNext, byId, incoming);
                    }
                    break;

                case MapLayerArchetype.Branch:
                    BuildBranchConnectionsOrdered(sortedCurrent, sortedNext, byId, incoming);
                    break;

                case MapLayerArchetype.Merge:
                    BuildMergeConnectionsOrdered(sortedCurrent, sortedNext, byId, incoming);
                    break;

                case MapLayerArchetype.Boss:
                    // Handled above
                    break;

                case MapLayerArchetype.Standard:
                case MapLayerArchetype.Elite:
                case MapLayerArchetype.Event:
                case MapLayerArchetype.Shop:
                    BuildStandardConnectionsOrdered(sortedCurrent, sortedNext, byId, incoming);
                    break;

                default:
                    BuildStandardConnectionsOrdered(sortedCurrent, sortedNext, byId, incoming);
                    break;
            }
        }

        // --- ORDERED CONNECTION BUILDERS (respect PathId visual order) ---
        // Key principle: sources and targets sorted by PathId, each source gets contiguous block of targets
        // This guarantees NO CROSSINGS because blocks don't overlap

        private static void BuildBranchConnectionsOrdered(
            int[] sortedCurrent, int[] sortedNext,
            Dictionary<int, MapNode> byId, Dictionary<int, int> incoming)
        {
            // Branch: sources fan out to targets maintaining left-to-right order
            // Each source gets a contiguous block of targets
            int currentCount = sortedCurrent.Length;
            int nextCount = sortedNext.Length;

            if (currentCount == 1)
            {
                // Single source fans out to all targets in order
                byId[sortedCurrent[0]].Connections = sortedNext;
                foreach (var t in sortedNext) incoming[t]++;
            }
            else
            {
                // Multiple sources: divide targets into contiguous blocks
                var targets = sortedNext;
                int targetsPerSource = targets.Length / currentCount;
                int remainder = targets.Length % currentCount;

                int idx = 0;
                for (int i = 0; i < currentCount; i++)
                {
                    int count = targetsPerSource + (i < remainder ? 1 : 0);
                    if (count <= 0 || idx >= targets.Length) 
                    {
                        // Fallback: connect to nearest target
                        int nearestIdx = Math.Min(i, targets.Length - 1);
                        byId[sortedCurrent[i]].Connections = new[] { targets[nearestIdx] };
                        incoming[targets[nearestIdx]]++;
                        continue;
                    }

                    var selected = targets.Skip(idx).Take(count).ToArray();
                    byId[sortedCurrent[i]].Connections = selected;
                    foreach (var t in selected) incoming[t]++;
                    idx += count;
                }
            }
        }

        private static void BuildMergeConnectionsOrdered(
            int[] sortedCurrent, int[] sortedNext,
            Dictionary<int, MapNode> byId, Dictionary<int, int> incoming)
        {
            // Merge: sources converge to targets maintaining order
            // Each target gets a contiguous block of sources
            int currentCount = sortedCurrent.Length;
            int nextCount = sortedNext.Length;

            // Distribute sources to targets contiguously
            int idx = 0;
            for (int i = 0; i < nextCount; i++)
            {
                int count = (nextCount > 0) ? (currentCount + nextCount - 1) / nextCount : 1;
                int remainingSources = currentCount - idx;
                int remainingTargets = nextCount - i;
                count = Math.Min(count, remainingSources - remainingTargets + 1);
                if (count <= 0) count = 1;

                var selected = sortedCurrent.Skip(idx).Take(count).ToArray();
                if (selected.Length == 0) continue;

                // All selected sources connect to this target
                foreach (var sourceId in selected)
                {
                    byId[sourceId].Connections = new[] { sortedNext[i] };
                    incoming[sortedNext[i]]++;
                }
                idx += count;
            }

            // Any remaining sources connect to last target
            while (idx < sortedCurrent.Length)
            {
                byId[sortedCurrent[idx]].Connections = new[] { sortedNext[^1] };
                incoming[sortedNext[^1]]++;
                idx++;
            }
        }

        private static void BuildStandardConnectionsOrdered(
            int[] sortedCurrent, int[] sortedNext,
            Dictionary<int, MapNode> byId, Dictionary<int, int> incoming)
        {
            // Standard: preserve paths with minimal crossing
            // Each source connects to target at same visual position (or nearest)
            int currentCount = sortedCurrent.Length;
            int nextCount = sortedNext.Length;

            if (currentCount == nextCount)
            {
                // Perfect 1:1 mapping - no crossing
                for (int i = 0; i < currentCount; i++)
                {
                    var sourceId = sortedCurrent[i];
                    var targetId = sortedNext[i];
                    byId[sourceId].Connections = new[] { targetId };
                    incoming[targetId]++;
                }
            }
            else if (currentCount < nextCount)
            {
                // Expanding: some sources get multiple targets
                // Distribute extra targets to leftmost sources
                int extraTargets = nextCount - currentCount;
                int idx = 0;
                for (int i = 0; i < currentCount; i++)
                {
                    int count = 1 + (i < extraTargets ? 1 : 0);
                    var targets = sortedNext.Skip(idx).Take(count).ToArray();
                    byId[sortedCurrent[i]].Connections = targets;
                    foreach (var t in targets) incoming[t]++;
                    idx += count;
                }
            }
            else
            {
                // Contracting: multiple sources to fewer targets
                // Leftmost sources map 1:1, remaining map to rightmost target
                int idx = 0;
                for (int i = 0; i < nextCount; i++)
                {
                    int count = (currentCount + nextCount - 1) / nextCount;
                    int remainingSources = currentCount - idx;
                    int remainingTargets = nextCount - i;
                    count = Math.Max(1, Math.Min(count, remainingSources - remainingTargets + 1));

                    var targets = sortedNext.Skip(idx).Take(count).ToArray();
                    if (targets.Length == 0) break;

                    // All selected sources connect to this target
                    foreach (var sourceId in sortedCurrent.Skip(idx).Take(count))
                    {
                        byId[sourceId].Connections = new[] { sortedNext[i] };
                        incoming[sortedNext[i]]++;
                    }
                    idx += count;
                }

                // Any remaining sources connect to last target
                while (idx < sortedCurrent.Length)
                {
                    byId[sortedCurrent[idx]].Connections = new[] { sortedNext[^1] };
                    incoming[sortedNext[^1]]++;
                    idx++;
                }
            }
        }
    }
}