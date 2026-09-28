using System;
using System.Collections.Generic;

namespace ShellGame.Map
{
    public static class MapGenerator
    {
        private const int ConfigVersion = 1;

        public static MapData Generate(int seed, MapGenerationConfig config)
        {
            var rules = BuildDefaultRules(config);

            for (int attempt = 0; attempt < config.MaxFullRegenerationAttempts; attempt++)
            {
                int trySeed = attempt == 0 ? seed : HashSeed(seed, attempt);
                var random = new SeededRandomSource(trySeed);

                // Generate layout first (new system)
                MapLayout layout = null;
                if (config.StructureConfig != null && config.StructureConfig.UseNewLayoutSystem)
                {
                    layout = MapLayoutGenerator.Generate(trySeed, config);
                }

                // Store layout in config for ConnectionBuilder
                config.Layout = layout;

                var layers = LayerFactory.BuildLayersFromLayout(config, layout);
                var nodes = NodeFactory.CreateNodes(layers);

                ConnectionBuilder.BuildConnections(layers, nodes, config, random);

                if (GraphValidator.Validate(layers, nodes).Count > 0)
                    continue;

                EncounterTypeAssigner.AssignTypes(layers, nodes, config, random);

                if (DistributionValidator.ValidateAndFix(layers, nodes, config, random, rules).Count > 0)
                    continue;

                return new MapData(trySeed, ConfigVersion, layers, nodes);
            }

            throw new InvalidOperationException(
                $"Не удалось сгенерировать валидную карту за {config.MaxFullRegenerationAttempts} попыток (исходный seed {seed})");
        }

        private static int HashSeed(int seed, int attempt)
        {
            unchecked
            {
                return seed * 397 + attempt;
            }
        }

        private static List<IDistributionRule> BuildDefaultRules(MapGenerationConfig config) => new()
        {
            new NoConsecutiveTypeRule(MapNodeType.Shop),
            new NoConsecutiveTypeRule(MapNodeType.Challenge),
            new MaxEnemyStreakRule(config.MaxEnemyStreak)
        };
    }
}