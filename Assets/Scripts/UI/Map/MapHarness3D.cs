using System.Linq;
using UnityEngine;
using ShellGame.Map;

namespace ShellGame.Map.Presentation
{
    [RequireComponent(typeof(MapView3D))]
    public sealed class MapHarness3D : MonoBehaviour
    {
        [SerializeField] private int _seed = 12345;
        [SerializeField] private bool _useFirstRunPattern = true;
        [SerializeField] private SinArchetype[] _firstRunEncounters = { SinArchetype.Wrath, SinArchetype.Envy };

        private void Start()
        {
            var config = new MapGenerationConfig();

            var map = _useFirstRunPattern
                ? FirstRunMapFactory.Build(_firstRunEncounters.Select(s => s.ToEncounterId()).ToArray())
                : MapGenerator.Generate(_seed, config);

            var state = new MapState(map.StartNode.Id);

            var view = GetComponent<MapView3D>();
            view.Build(map, state);
            view.NodeSelected += nodeId => UnityEngine.Debug.Log($"Выбран узел {nodeId}");
        }
    }
}