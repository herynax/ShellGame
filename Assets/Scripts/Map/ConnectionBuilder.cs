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

            for (int i = 0; i < layers.Length - 1; i++)
            {
                var current = layers[i].NodeIds;
                var next = layers[i + 1].NodeIds;

                // сходимость всего предпоследнего слоя в единственный Boss-узел
                if (i + 1 == layers.Length - 1 && next.Length == 1)
                {
                    foreach (var sourceId in current)
                        byId[sourceId].Connections = new[] { next[0] };
                    continue;
                }

                var incoming = next.ToDictionary(id => id, _ => 0);

                foreach (var sourceId in current)
                {
                    int count = Math.Min(RollConnectionCount(config, random), next.Length);
                    var targets = PickTargets(next, count, random);

                    byId[sourceId].Connections = targets;
                    foreach (var t in targets)
                        incoming[t]++;
                }

                // гарантия: у каждого узла следующего слоя есть хотя бы одна входящая связь
                foreach (var kv in incoming.Where(kv => kv.Value == 0).ToArray())
                {
                    var sourceId = current[random.Next(current.Length)];
                    var sourceNode = byId[sourceId];
                    if (!sourceNode.Connections.Contains(kv.Key))
                        sourceNode.Connections = sourceNode.Connections.Append(kv.Key).ToArray();
                }
            }
        }

        private static int RollConnectionCount(MapGenerationConfig config, IRunRandom random)
        {
            if (random.Value < config.WideForkChance)
                return config.MaxConnections + 1;
            return random.Next(config.MinConnections, config.MaxConnections + 1);
        }

        private static int[] PickTargets(int[] candidates, int count, IRunRandom random)
        {
            var pool = candidates.ToList();
            random.Shuffle(pool);
            return pool.Take(count).ToArray();
        }
    }
}