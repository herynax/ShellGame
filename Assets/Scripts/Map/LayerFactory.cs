using System.Collections.Generic;
using UnityEngine;

namespace ShellGame.Map
{
    public static class LayerFactory
    {
        public static MapLayer[] BuildLayers(MapGenerationConfig config)
        {
            // Prefer new layout system if StructureConfig is available
            if (config.StructureConfig != null && config.StructureConfig.UseNewLayoutSystem)
            {
                // Generate using the new layout system via MapLayoutGenerator
                // Note: This requires a seed and may need additional setup
                // For now, fall back to regular counts with a warning
                Debug.LogWarning("[LayerFactory] New layout system requested but MapLayoutGenerator.Generate() should be used instead.");
            }

            var regularCounts = config.RegularLayerNodeCounts;
            var layers = new MapLayer[regularCounts.Length + 2]; // + Start + Boss

            layers[0] = BuildLayer(0, 1);

            for (int i = 0; i < regularCounts.Length; i++)
                layers[i + 1] = BuildLayer(i + 1, regularCounts[i]);

            int bossLayerIndex = layers.Length - 1;
            layers[bossLayerIndex] = BuildLayer(bossLayerIndex, 1);

            return layers;
        }

        public static MapLayer[] BuildLayersFromLayout(MapGenerationConfig config, MapLayout layout)
        {
            if (layout == null || layout.Layers == null || layout.Layers.Length == 0)
                return BuildLayers(config);

            var layers = new MapLayer[layout.Layers.Length];
            var nodeIdMap = new Dictionary<int, int>(); // old nodeId -> new nodeId
            int newId = 0;

            for (int i = 0; i < layout.Layers.Length; i++)
            {
                var layerInfo = layout.Layers[i];
                var ids = new int[layerInfo.NodeCount];

                for (int j = 0; j < layerInfo.NodeCount; j++)
                {
                    int oldId = layerInfo.StartNodeId + j;
                    ids[j] = newId;
                    nodeIdMap[oldId] = newId;
                    newId++;
                }

                layers[i] = new MapLayer(layerInfo.Index, ids);
            }

            // Store mapping for ConnectionBuilder if needed
            return layers;
        }

        private static MapLayer BuildLayer(int layerIndex, int nodeCount)
        {
            var ids = new int[nodeCount];
            for (int i = 0; i < nodeCount; i++)
                ids[i] = layerIndex * 100 + i;
            return new MapLayer(layerIndex, ids);
        }
    }
}