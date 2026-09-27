namespace ShellGame.Map
{
    public static class LayerFactory
    {
        public static MapLayer[] BuildLayers(MapGenerationConfig config)
        {
            var regularCounts = config.RegularLayerNodeCounts;
            var layers = new MapLayer[regularCounts.Length + 2]; // + Start + Boss

            layers[0] = BuildLayer(0, 1);

            for (int i = 0; i < regularCounts.Length; i++)
                layers[i + 1] = BuildLayer(i + 1, regularCounts[i]);

            int bossLayerIndex = layers.Length - 1;
            layers[bossLayerIndex] = BuildLayer(bossLayerIndex, 1);

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