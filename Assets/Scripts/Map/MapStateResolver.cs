using System.Collections.Generic;
using System.Linq;

namespace ShellGame.Map
{
    public static class MapStateResolver
    {
        public static MapNodeViewState Resolve(MapData map, MapState state, int nodeId)
        {
            if (nodeId == state.CurrentNodeId)
                return MapNodeViewState.Current;

            if (state.IsCompleted(nodeId))
                return MapNodeViewState.Completed;

            var currentNode = map.GetNode(state.CurrentNodeId);
            if (currentNode.Connections.Contains(nodeId))
                return MapNodeViewState.Available;

            var node = map.GetNode(nodeId);
            // Тот же слой, что и текущий узел (соседи, которые не были выбраны),
            // либо более ранний слой, который уже пройден по другой ветке.
            if (node.LayerIndex <= currentNode.LayerIndex)
                return MapNodeViewState.Unavailable;

            return MapNodeViewState.Hidden;
        }

        public static Dictionary<int, MapNodeViewState> ResolveAll(MapData map, MapState state)
        {
            var result = new Dictionary<int, MapNodeViewState>(map.Nodes.Length);
            foreach (var node in map.Nodes)
                result[node.Id] = Resolve(map, state, node.Id);
            return result;
        }

        public static IEnumerable<MapNode> GetAvailableNodes(MapData map, MapState state) =>
            map.GetNode(state.CurrentNodeId).Connections.Select(map.GetNode);
    }
}