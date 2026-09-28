using System;
using System.Collections.Generic;
using System.Linq;

namespace ShellGame.Map
{
    [Serializable]
    public struct LayerInfo
    {
        public int Index;
        public MapLayerArchetype Archetype;
        public int NodeCount;
        public int StartNodeId;         // First node ID in this layer
        public int[] NodeIds;           // All node IDs in this layer
        public int[] PathIds;           // Path ID per node (same length as NodeIds)
        
        // For merges: which path IDs from previous layer converge here
        public int[] IncomingPathIds;   
        
        // For branches: which new path IDs are created
        public int[] NewPathIds;        

        public LayerInfo(int index, MapLayerArchetype archetype, int nodeCount, int startNodeId)
        {
            Index = index;
            Archetype = archetype;
            NodeCount = nodeCount;
            StartNodeId = startNodeId;
            NodeIds = new int[nodeCount];
            PathIds = new int[nodeCount];
            IncomingPathIds = Array.Empty<int>();
            NewPathIds = Array.Empty<int>();
            
            for (int i = 0; i < nodeCount; i++)
                NodeIds[i] = startNodeId + i;
        }

        public readonly int EndNodeId => StartNodeId + NodeCount - 1;
    }

    public sealed class MapLayout
    {
        public LayerInfo[] Layers;
        public int[] PathIds;           // Path identity per node (for meaningful merges)
        public int MaxParallelPaths;    // Max parallel paths in this layout

        public MapLayout(LayerInfo[] layers, int[] pathIds, int maxParallelPaths)
        {
            Layers = layers;
            PathIds = pathIds;
            MaxParallelPaths = maxParallelPaths;
        }

        public int TotalNodes => Layers != null ? Array.ConvertAll(Layers, l => l.NodeCount).Sum() : 0;
        public int LayerCount => Layers?.Length ?? 0;

        public LayerInfo GetLayer(int index) => (index >= 0 && index < Layers.Length) ? Layers[index] : default;
        public LayerInfo StartLayer => Layers != null && Layers.Length > 0 ? Layers[0] : default;
        public LayerInfo BossLayer => Layers != null && Layers.Length > 0 ? Layers[Layers.Length - 1] : default;
    }
}