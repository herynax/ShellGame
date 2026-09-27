// File: Assets/Scripts/Map/MapNode.cs
using System;

namespace ShellGame.Map
{
    [Serializable]
    public sealed class MapNode
    {
        public int Id;
        public int LayerIndex;
        public MapNodeType Type;
        public int[] Connections; // Id узлов в LayerIndex + 1

        // Если задано — IEncounterResolver обязан использовать именно этот EncounterId
        // (например "Boar", "Hyena") вместо случайного выбора из пула. Пусто/null = обычный ролл.
        public string ForcedEncounterId;

        public MapNode(int id, int layerIndex, MapNodeType type)
        {
            Id = id;
            LayerIndex = layerIndex;
            Type = type;
            Connections = Array.Empty<int>();
            ForcedEncounterId = null;
        }
    }
}