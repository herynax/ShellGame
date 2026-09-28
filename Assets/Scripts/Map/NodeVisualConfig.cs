using System;
using UnityEngine;
using ShellGame.Map;

namespace ShellGame.Map
{
    [CreateAssetMenu(fileName = "NodeVisualConfig", menuName = "ShellGame/Map/Node Visual Config")]
    public sealed class NodeVisualConfig : ScriptableObject
    {
        public enum VisualMode
        {
            Primitive,
            Prefab
        }

        [Serializable]
        public struct Entry
        {
            public MapNodeType Type;
            public VisualMode Mode;
            public PrimitiveType Primitive;
            public GameObject Prefab;
            public float ScaleMultiplier;
            public Vector3 PositionOffset;

            public Entry(MapNodeType type, VisualMode mode = VisualMode.Primitive, PrimitiveType primitive = PrimitiveType.Sphere, GameObject prefab = null, float scaleMultiplier = 1f, Vector3 positionOffset = default)
            {
                Type = type;
                Mode = mode;
                Primitive = primitive;
                Prefab = prefab;
                ScaleMultiplier = scaleMultiplier;
                PositionOffset = positionOffset;
            }

            public static Entry PrimitiveEntry(MapNodeType type, PrimitiveType primitive = PrimitiveType.Sphere, float scaleMultiplier = 1f, Vector3 positionOffset = default)
            {
                return new Entry(type, VisualMode.Primitive, primitive, null, scaleMultiplier, positionOffset);
            }

            public static Entry PrefabEntry(MapNodeType type, GameObject prefab, float scaleMultiplier = 1f, Vector3 positionOffset = default)
            {
                return new Entry(type, VisualMode.Prefab, PrimitiveType.Sphere, prefab, scaleMultiplier, positionOffset);
            }
        }

        public Entry[] Entries = new Entry[6];

        public Entry GetVisual(MapNodeType type)
        {
            if (Entries != null)
            {
                foreach (var e in Entries)
                    if (e.Type == type) return e;
            }
            return new Entry(type, VisualMode.Primitive, PrimitiveType.Sphere, null, 1f, Vector3.zero);
        }
    }
}