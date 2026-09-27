using System.Collections.Generic;
using System.Linq;

namespace ShellGame.Map
{
    public static class GraphValidator
    {
        public static IReadOnlyList<ValidationError> Validate(MapLayer[] layers, MapNode[] nodes)
        {
            var errors = new List<ValidationError>();
            var byId = nodes.ToDictionary(n => n.Id);

            var startId = layers[0].NodeIds[0];
            var bossId = layers[^1].NodeIds[0];

            var reachable = new HashSet<int> { startId };
            var queue = new Queue<int>();
            queue.Enqueue(startId);

            while (queue.Count > 0)
            {
                var current = byId[queue.Dequeue()];
                foreach (var next in current.Connections)
                    if (reachable.Add(next))
                        queue.Enqueue(next);
            }

            if (!reachable.Contains(bossId))
                errors.Add(new ValidationError("Boss недостижим из Start"));

            foreach (var node in nodes)
                if (!reachable.Contains(node.Id))
                    errors.Add(new ValidationError($"Узел {node.Id} недостижим"));

            return errors;
        }
    }
}