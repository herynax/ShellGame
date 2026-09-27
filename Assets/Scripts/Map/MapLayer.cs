using System;

namespace ShellGame.Map
{
    [Serializable]
    public sealed class MapLayer
    {
        public int Index;
        public int[] NodeIds;

        public MapLayer(int index, int[] nodeIds)
        {
            Index = index;
            NodeIds = nodeIds;
        }
    }
}