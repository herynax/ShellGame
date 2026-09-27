using System.Linq;

namespace ShellGame.Map
{
    public static class EncounterTypeAssigner
    {
        public static void AssignTypes(MapLayer[] layers, MapNode[] nodes, MapGenerationConfig config, IRunRandom random)
        {
            var byId = nodes.ToDictionary(n => n.Id);
            int regularLayerCount = layers.Length - 2; // без Start и Boss

            for (int i = 1; i < layers.Length - 1; i++)
            {
                var weights = GetWeightsForLayer(i, regularLayerCount, config);
                foreach (var id in layers[i].NodeIds)
                    byId[id].Type = RollType(weights, random);
            }
        }

        public static MapNodeType RollType(EncounterWeights weights, IRunRandom random)
        {
            var types = new[] { MapNodeType.Enemy, MapNodeType.Shop, MapNodeType.Challenge };
            var values = new[] { weights.Enemy, weights.Shop, weights.Challenge };
            return random.Pick(types, values);
        }

        public static EncounterWeights GetWeightsForLayer(int layerIndex, int regularLayerCount, MapGenerationConfig config)
        {
            int regularIndex = layerIndex - 1;
            float zone = (regularIndex + 1f) / regularLayerCount;

            if (zone <= 1f / 3f) return config.EarlyWeights;
            if (zone <= 2f / 3f) return config.MiddleWeights;
            return config.LateWeights;
        }
    }
}