using System.Collections.Generic;
using System.Linq;

namespace ShellGame.Map
{
    public static class DistributionValidator
    {
        public static IReadOnlyList<ValidationError> ValidateAndFix(
            MapLayer[] layers,
            MapNode[] nodes,
            MapGenerationConfig config,
            IRunRandom random,
            IReadOnlyList<IDistributionRule> rules)
        {
            var errors = new List<ValidationError>();
            var byId = nodes.ToDictionary(n => n.Id);
            int regularLayerCount = layers.Length - 2;

            foreach (var rule in rules)
                rule.Reset();

            for (int i = 1; i < layers.Length - 1; i++)
            {
                var predecessors = layers[i - 1].NodeIds.Select(id => byId[id]).ToList();
                var weights = EncounterTypeAssigner.GetWeightsForLayer(i, regularLayerCount, config);

                foreach (var nodeId in layers[i].NodeIds)
                {
                    var node = byId[nodeId];
                    var incoming = predecessors.Where(p => p.Connections.Contains(nodeId)).ToList();

                    TryFixNode(node, incoming, rules, weights, random, config, errors);
                }
            }

            return errors;
        }

        private static void TryFixNode(
            MapNode node,
            List<MapNode> predecessors,
            IReadOnlyList<IDistributionRule> rules,
            EncounterWeights weights,
            IRunRandom random,
            MapGenerationConfig config,
            List<ValidationError> errors)
        {
            for (int attempt = 0; attempt < config.MaxSameTypeRegenAttempts; attempt++)
            {
                var violated = rules.FirstOrDefault(r => r.IsViolated(node, predecessors));
                if (violated == null)
                    return;

                node.Type = EncounterTypeAssigner.RollType(weights, random);
            }

            errors.Add(new ValidationError($"Узел {node.Id}: не удалось устранить нарушение распределения"));
        }
    }
}