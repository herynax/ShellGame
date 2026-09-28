using System;
using System.Collections.Generic;
using UnityEngine;
using ShellGame.Map;
using Unity.Cinemachine;

namespace ShellGame.Map.Presentation
{
    // Первая рабочая 3D-презентация карты: примитивы вместо арта,
    // но реальная раскладка по слоям (глубина) и позициям в слое (ширина),
    // реальный клик и реальная подсветка состояний из MapStateResolver.
    // Финальный арт/раскладка в духе Inscryption заменит генерацию геометрии
    // здесь, но контракт (Build/NodeSelected) останется тем же.
    public sealed class MapView3D : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private float _layerSpacingZ = 6f;
        [SerializeField] private float _nodeSpacingX = 3f;
        [SerializeField] private float _nodeScale = 0.8f;

        [Header("Colors by view state")]
        [SerializeField] private Color _hiddenColor = new Color(0.15f, 0.15f, 0.15f);
        [SerializeField] private Color _availableColor = Color.yellow;
        [SerializeField] private Color _currentColor = Color.cyan;
        [SerializeField] private Color _completedColor = Color.green;
        [SerializeField] private Color _unavailableColor = Color.gray;

        [Header("Camera")]
        [SerializeField] private CinemachineCamera _mapCamera;

        public event Action<int> NodeSelected;

        private MapData _map;
        private MapState _state;
        private readonly Dictionary<int, MapNodeView3D> _nodeViews = new();
        private readonly List<GameObject> _connectionObjects = new();

        public void Build(MapData map, MapState state)
        {
            _map = map;
            _state = state;

            Clear();
            SpawnNodes();
            SpawnConnections();
            RefreshVisualStates();

            _mapCamera.Priority = 5;
        }

        public void RefreshVisualStates()
        {
            var states = MapStateResolver.ResolveAll(_map, _state);
            foreach (var kv in states)
                _nodeViews[kv.Key].SetColor(ColorFor(kv.Value));
        }

        private void SpawnNodes()
        {
            foreach (var node in _map.Nodes)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"Node_{node.Id}_{node.Type}";
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * _nodeScale;
                go.transform.localPosition = PositionFor(node);

                var view = go.AddComponent<MapNodeView3D>();
                view.Initialize(node.Id, OnNodeClicked);

                _nodeViews[node.Id] = view;
            }
        }

        private Vector3 PositionFor(MapNode node)
        {
            var layer = _map.Layers[node.LayerIndex];
            int indexInLayer = Array.IndexOf(layer.NodeIds, node.Id);
            int count = layer.NodeIds.Length;

            float xOffset = (indexInLayer - (count - 1) / 2f) * _nodeSpacingX;
            float z = node.LayerIndex * _layerSpacingZ;

            return new Vector3(xOffset, 0f, z);
        }

        private void SpawnConnections()
        {
            foreach (var node in _map.Nodes)
            {
                var from = _nodeViews[node.Id].transform.position;
                foreach (var targetId in node.Connections)
                {
                    var to = _nodeViews[targetId].transform.position;
                    _connectionObjects.Add(CreateLine(from, to));
                }
            }
        }

        private GameObject CreateLine(Vector3 from, Vector3 to)
        {
            var go = new GameObject("Connection");
            go.transform.SetParent(transform, false);

            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startWidth = line.endWidth = 0.05f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = line.endColor = new Color(1f, 1f, 1f, 0.4f);

            return go;
        }

        public void HandleNodeClicked(int nodeId)
        {
            if (!_state.CanMoveTo(_map, nodeId))
                return;

            NodeSelected?.Invoke(nodeId);
            _mapCamera.Priority = 0; // Сбрасываем при клике, чтобы не мешать энкаунтерной камере
        }

        private void OnNodeClicked(int nodeId) => HandleNodeClicked(nodeId);

        public bool TryGetNodeWorldPosition(int nodeId, out Vector3 position)
        {
            if (_nodeViews.TryGetValue(nodeId, out var view))
            {
                position = view.transform.position;
                return true;
            }
            position = default;
            return false;
        }

        private Color ColorFor(MapNodeViewState viewState) => viewState switch
        {
            MapNodeViewState.Hidden => _hiddenColor,
            MapNodeViewState.Available => _availableColor,
            MapNodeViewState.Current => _currentColor,
            MapNodeViewState.Completed => _completedColor,
            MapNodeViewState.Unavailable => _unavailableColor,
            _ => Color.magenta
        };

        private void Clear()
        {
            foreach (Transform child in transform)
                Destroy(child.gameObject);
            _nodeViews.Clear();
            _connectionObjects.Clear();
        }
    }
}