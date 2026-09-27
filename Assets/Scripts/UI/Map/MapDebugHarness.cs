using UnityEngine;
using ShellGame.Map;

namespace ShellGame.MapDebug
{
    // Временный ручной драйвер для MapDebugView, пока не готов RunManager.
    // После шага 3 этот файл можно удалить — его роль полностью займёт RunManager.
    [RequireComponent(typeof(MapDebugView))]
    public sealed class MapDebugHarness : MonoBehaviour
    {
        [SerializeField] private int _seed = 12345;
        [SerializeField] private bool _useFirstRunPattern;
        [SerializeField] private string[] _firstRunEncounters = { "Boar", "Hyena" };

        private MapData _map;
        private MapState _state;

        private void Start()
        {
            var config = new MapGenerationConfig();

            _map = _useFirstRunPattern
                ? FirstRunMapFactory.Build(_firstRunEncounters)
                : MapGenerator.Generate(_seed, config);

            _state = new MapState(_map.StartNode.Id);

            var view = GetComponent<MapDebugView>();
            view.Bind(_map, _state);
            view.NodeSelected += OnNodeSelected;
        }

        private void OnNodeSelected(int nodeId)
        {
            if (!_state.CanMoveTo(_map, nodeId))
            {
                UnityEngine.Debug.LogWarning($"Недопустимый переход в узел {nodeId}");
                return;
            }

            _state.CompleteCurrentAndMoveTo(nodeId);

            if (nodeId == _map.BossNode.Id)
                UnityEngine.Debug.Log("Достигнут Boss — карта пройдена");
        }
    }
}