using System;
using System.Collections.Generic;
using System.Linq;

namespace ShellGame.Map
{
    [Serializable]
    public sealed class MapData
    {
        public int Seed;
        public int ConfigVersion;
        public MapLayer[] Layers;
        public MapNode[] Nodes;

        [NonSerialized] private Dictionary<int, MapNode> _byId;

        public MapData(int seed, int configVersion, MapLayer[] layers, MapNode[] nodes)
        {
            Seed = seed;
            ConfigVersion = configVersion;
            Layers = layers;
            Nodes = nodes;
        }

        public MapNode GetNode(int id)
        {
            _byId ??= Nodes.ToDictionary(n => n.Id);
            return _byId[id];
        }

        public IEnumerable<MapNode> GetNodesInLayer(int layerIndex) =>
            Layers[layerIndex].NodeIds.Select(GetNode);

        public MapNode StartNode => GetNode(Layers[0].NodeIds[0]);
        public MapNode BossNode => GetNode(Layers[^1].NodeIds[0]);
    }
}