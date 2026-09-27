// File: Assets/Scripts/Map/Generation/NodeFactory.cs
using System.Collections.Generic;

namespace ShellGame.Map
{
    public static class NodeFactory
    {
        public static MapNode[] CreateNodes(MapLayer[] layers)
        {
            var nodes = new List<MapNode>();
            int bossLayerIndex = layers.Length - 1;

            foreach (var layer in layers)
            {
                var forcedType = layer.Index == 0
                    ? MapNodeType.Start
                    : layer.Index == bossLayerIndex
                        ? MapNodeType.Boss
                        : MapNodeType.Enemy; // временный дефолт — реальный тип назначит EncounterTypeAssigner

                foreach (var id in layer.NodeIds)
                    nodes.Add(new MapNode(id, layer.Index, forcedType));
            }

            return nodes.ToArray();
        }
    }
}