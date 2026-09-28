using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ShellGame.Map;
using Unity.Cinemachine;

namespace ShellGame.Map.Presentation
{
    /// <summary>
    /// 3D-презентация карты на столе в духе Inscryption:
    /// - Настраиваемая область (Bounding Box) с отсечением через URP-шейдеры;
    /// - Настольный масштаб (tabletop scale);
    /// - Подложка (доска/свиток) под картой на столе;
    /// - Скроллинг содержимого карты (ContentRoot) внутри фиксированного бокса;
    /// - Визуализация границ бокса в Scene View через Gizmos.
    /// </summary>
    public sealed class MapView3D : MonoBehaviour
    {
        [Header("--- TABLE BOUNDING BOX (MASK) ---")]
        [Tooltip("Центр видимого бокса карты на столе.")]
        [SerializeField] private Vector3 _boxCenter = new Vector3(0f, 0.05f, 0f);

        [Tooltip("Размеры видимой области на столе (ширина X, высота Y, глубина Z).")]
        [SerializeField] private Vector3 _boxSize = new Vector3(2.7f, 0.4f, 2.25f);

        [Tooltip("Ширина мягкого затухания/градиента на краях бокса.")]
        [SerializeField, Range(0.001f, 0.3f)] private float _edgeFadeMargin = 0.18f;

        [Tooltip("Включено ли отсечение по боксу в шейдерах.")]
        [SerializeField] private bool _clipEnabled = true;

        [Header("--- TABLETOP LAYOUT ---")]
        [SerializeField] private float _layerSpacingZ = 0.84f;
        [SerializeField] private float _nodeSpacingX = 0.60f;
        [SerializeField] private float _nodeScale = 0.195f;
        [SerializeField] private float _lineWidth = 0.036f;
        [SerializeField] private float _nodeHeightOffset = 0.025f;

        [Header("--- UNDERLAY (BOARD / PARCHMENT) ---")]
        [SerializeField] private bool _showUnderlay = true;
        [SerializeField] private float _underlayPadding = 0.04f;
        [SerializeField] private Material _underlayMaterial;

        [Header("--- MATERIALS ---")]
        [SerializeField] private Material _nodeMaterial;
        [SerializeField] private Material _lineMaterial;

        [Header("--- COLORS BY VIEW STATE ---")]
        [SerializeField] private Color _hiddenColor = new Color(0.12f, 0.12f, 0.12f, 0.4f);
        [SerializeField] private Color _availableColor = new Color(1.0f, 0.85f, 0.15f, 1.0f);
        [SerializeField] private Color _currentColor = new Color(0.1f, 0.9f, 1.0f, 1.0f);
        [SerializeField] private Color _completedColor = new Color(0.2f, 0.8f, 0.3f, 0.8f);
        [SerializeField] private Color _unavailableColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);

        [Header("--- CAMERA ---")]
        [SerializeField] private CinemachineCamera _mapCamera;

        [Header("--- NODE VISUALS ---")]
        [SerializeField] private NodeVisualConfig _nodeVisualConfig;

        public event Action<int> NodeSelected;

        public Vector3 BoxCenter => _boxCenter;
        public Vector3 BoxSize => _boxSize;
        public float EdgeFadeMargin => _edgeFadeMargin;
        public bool ClipEnabled => _clipEnabled;
        public Transform ContentRoot => _contentRoot;
        public float LayerSpacingZ => _layerSpacingZ;

        private MapData _map;
        private MapState _state;
        private MapLayout _layout;
        private Transform _contentRoot;
        private GameObject _underlayObject;
        private readonly Dictionary<int, MapNodeView3D> _nodeViews = new();
        private readonly List<GameObject> _connectionObjects = new();

        private Material _defaultNodeMaterialInstance;
        private Material _defaultLineMaterialInstance;
        private Material _defaultUnderlayMaterialInstance;

        private void Awake()
        {
            EnsureContentRoot();
            EnsureMaterials();
            UpdateClipShaderGlobals();
        }

        private void OnEnable()
        {
            UpdateClipShaderGlobals();
        }

        private void Update()
        {
            // Обновляем матрицы/параметры клипа на случай перемещения стола или изменения бокса в рантайме
            UpdateClipShaderGlobals();
        }

        private void OnValidate()
        {
            if (_boxSize.x < 0.1f) _boxSize.x = 0.1f;
            if (_boxSize.y < 0.1f) _boxSize.y = 0.1f;
            if (_boxSize.z < 0.1f) _boxSize.z = 0.1f;

            UpdateClipShaderGlobals();
            UpdateUnderlayTransform();
        }

        private void EnsureContentRoot()
        {
            if (_contentRoot == null)
            {
                var existing = transform.Find("MapContentRoot");
                if (existing != null)
                {
                    _contentRoot = existing;
                }
                else
                {
                    var go = new GameObject("MapContentRoot");
                    go.transform.SetParent(transform, false);
                    _contentRoot = go.transform;
                }
            }
        }

        private void EnsureMaterials()
        {
            if (_nodeMaterial == null)
            {
                var shader = Shader.Find("ShellGame/Map/MapNodeClipped") ?? Shader.Find("Universal Render Pipeline/Lit");
                if (_defaultNodeMaterialInstance == null)
                    _defaultNodeMaterialInstance = new Material(shader);
                _nodeMaterial = _defaultNodeMaterialInstance;
            }

            if (_lineMaterial == null)
            {
                var shader = Shader.Find("ShellGame/Map/MapLineClipped") ?? Shader.Find("Sprites/Default");
                if (_defaultLineMaterialInstance == null)
                    _defaultLineMaterialInstance = new Material(shader);
                _lineMaterial = _defaultLineMaterialInstance;
            }

            if (_underlayMaterial == null)
            {
                var shader = Shader.Find("ShellGame/Map/MapUnderlay") ?? Shader.Find("Universal Render Pipeline/Lit");
                if (_defaultUnderlayMaterialInstance == null)
                    _defaultUnderlayMaterialInstance = new Material(shader);
                _underlayMaterial = _defaultUnderlayMaterialInstance;
            }
        }

        public void UpdateClipShaderGlobals()
        {
            var worldToLocal = transform.worldToLocalMatrix;
            Shader.SetGlobalMatrix("_MapClipWorldToLocal", worldToLocal);
            Shader.SetGlobalVector("_MapClipBoxCenter", _boxCenter);
            Shader.SetGlobalVector("_MapClipBoxExtents", _boxSize * 0.5f);
            Shader.SetGlobalFloat("_MapClipBoxFade", _edgeFadeMargin);
            Shader.SetGlobalFloat("_MapClipEnabled", _clipEnabled ? 1f : 0f);
        }

        public void Build(MapData map, MapState state, MapLayout layout = null)
        {
            _map = map;
            _state = state;
            _layout = layout;

            EnsureContentRoot();
            EnsureMaterials();
            UpdateClipShaderGlobals();

            Clear();
            SpawnUnderlay();
            SpawnNodes();
            SpawnConnections();
            RefreshVisualStates();

            // Начальный скролл на текущий узел
            FocusOnNode(state.CurrentNodeId, immediate: true);
        }

        public void RefreshVisualStates()
        {
            if (_map == null || _state == null) return;

            var states = MapStateResolver.ResolveAll(_map, _state);
            foreach (var kv in states)
            {
                if (_nodeViews.TryGetValue(kv.Key, out var view))
                {
                    view.SetColor(ColorFor(kv.Value));
                    view.SetVisualState(kv.Value);
                }
            }
        }

        private void SpawnUnderlay()
        {
            if (!_showUnderlay) return;

            if (_underlayObject == null)
            {
                _underlayObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _underlayObject.name = "MapUnderlay";
                _underlayObject.transform.SetParent(transform, false);

                // Удаляем коллайдер подложки, чтобы он не перехватывал лучи клика по нодам
                var collider = _underlayObject.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
            }

            var renderer = _underlayObject.GetComponent<Renderer>();
            if (renderer != null && _underlayMaterial != null)
                renderer.sharedMaterial = _underlayMaterial;

            UpdateUnderlayTransform();
            _underlayObject.SetActive(_showUnderlay);
        }

        private void UpdateUnderlayTransform()
        {
            if (_underlayObject == null) return;

            _underlayObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _underlayObject.transform.localPosition = new Vector3(
                _boxCenter.x,
                _boxCenter.y - _boxSize.y * 0.5f + 0.002f,
                _boxCenter.z
            );
            _underlayObject.transform.localScale = new Vector3(
                _boxSize.x + _underlayPadding,
                _boxSize.z + _underlayPadding,
                1f
            );
        }

        private void SpawnNodes()
        {
            foreach (var node in _map.Nodes)
            {
                var visual = GetVisualForType(node.Type);
                GameObject go;

                if (visual.Mode == NodeVisualConfig.VisualMode.Prefab && visual.Prefab != null)
                {
                    go = Instantiate(visual.Prefab, _contentRoot);
                }
                else
                {
                    go = GameObject.CreatePrimitive(visual.Primitive);
                    go.transform.SetParent(_contentRoot, false);
                }

                go.name = $"Node_{node.Id}_{node.Type}";
                go.transform.localScale = Vector3.one * _nodeScale * visual.ScaleMultiplier;
                go.transform.localPosition = PositionFor(node, visual);

                var renderers = go.GetComponentsInChildren<Renderer>();
                if (_nodeMaterial != null)
                {
                    foreach (var renderer in renderers)
                        renderer.sharedMaterial = _nodeMaterial;
                }

                var view = go.AddComponent<MapNodeView3D>();
                view.Initialize(node.Id, node.Type, OnNodeClicked);

                _nodeViews[node.Id] = view;
            }
        }

        private NodeVisualConfig.Entry GetVisualForType(MapNodeType type)
        {
            if (_nodeVisualConfig != null)
                return _nodeVisualConfig.GetVisual(type);

            return NodeVisualConfig.Entry.PrimitiveEntry(type, PrimitiveType.Sphere, 1f, Vector3.zero);
        }

        private Vector3 PositionFor(MapNode node, NodeVisualConfig.Entry visual)
        {
            var layer = _map.Layers[node.LayerIndex];
            int count = layer.NodeIds.Length;

            // Determine position index based on path ID if layout is available
            int indexInLayer;
            if (_layout != null && _layout.Layers != null && node.LayerIndex < _layout.Layers.Length)
            {
                var layerInfo = _layout.Layers[node.LayerIndex];
                int pathId = -1;
                for (int i = 0; i < layerInfo.NodeIds.Length; i++)
                {
                    if (layerInfo.NodeIds[i] == node.Id)
                    {
                        pathId = layerInfo.PathIds[i];
                        break;
                    }
                }

                // Sort by path ID to prevent crossing paths
                if (pathId >= 0)
                {
                    var sortedNodes = layer.NodeIds
                        .Select((id, idx) => new { Id = id, PathId = layerInfo.PathIds[idx] })
                        .OrderBy(x => x.PathId)
                        .ToList();
                    indexInLayer = sortedNodes.FindIndex(x => x.Id == node.Id);
                }
                else
                {
                    indexInLayer = Array.IndexOf(layer.NodeIds, node.Id);
                }
            }
            else
            {
                indexInLayer = Array.IndexOf(layer.NodeIds, node.Id);
            }

            float xOffset = count > 1
                ? (indexInLayer - (count - 1) / 2f) * _nodeSpacingX
                : 0f;

            float z = node.LayerIndex * _layerSpacingZ;

            float nodeHeight = _nodeScale * visual.ScaleMultiplier;
            float y = _boxCenter.y - _boxSize.y * 0.5f + _nodeHeightOffset + nodeHeight * 0.5f + visual.PositionOffset.y;

            return new Vector3(xOffset + visual.PositionOffset.x, y, z + visual.PositionOffset.z);
        }

        private void SpawnConnections()
        {
            foreach (var node in _map.Nodes)
            {
                var from = _nodeViews[node.Id].transform.localPosition;
                foreach (var targetId in node.Connections)
                {
                    if (_nodeViews.TryGetValue(targetId, out var targetView))
                    {
                        var to = targetView.transform.localPosition;
                        _connectionObjects.Add(CreateLine(from, to));
                    }
                }
            }
        }

        private GameObject CreateLine(Vector3 fromLocal, Vector3 toLocal)
        {
            var go = new GameObject("Connection");
            go.transform.SetParent(_contentRoot, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, fromLocal);
            line.SetPosition(1, toLocal);
            line.startWidth = line.endWidth = _lineWidth;

            if (_lineMaterial != null)
                line.sharedMaterial = _lineMaterial;

            line.startColor = line.endColor = new Color(1f, 1f, 1f, 0.6f);

            return go;
        }

        // ==========================================
        // SCROLLING & POSITIONING
        // ==========================================

        public void SetScrollOffset(float scrollZ)
        {
            EnsureContentRoot();
            var (minScroll, maxScroll) = GetScrollBounds();
            float clamped = Mathf.Clamp(scrollZ, minScroll, maxScroll);
            _contentRoot.localPosition = new Vector3(0f, 0f, clamped);
        }

        public float GetScrollOffset()
        {
            return _contentRoot != null ? _contentRoot.localPosition.z : 0f;
        }

        public (float minScroll, float maxScroll) GetScrollBounds()
        {
            if (_map == null || _map.Layers.Length == 0)
                return (0f, 0f);

            int layerCount = _map.Layers.Length;
            float maxLayerZ = (layerCount - 1) * _layerSpacingZ;

            // Видимый диапазон по Z относительно boxCenter:
            // В нижней части бокса: _boxCenter.z - _boxSize.z * 0.35f
            // В верхней части бокса: _boxCenter.z + _boxSize.z * 0.35f
            float visibleBottomZ = _boxCenter.z - _boxSize.z * 0.35f;
            float visibleTopZ = _boxCenter.z + _boxSize.z * 0.35f;

            // При maxScroll стартовый слой (Z=0) находится внизу бокса:
            float maxScroll = visibleBottomZ;

            // При minScroll последний слой (Z=maxLayerZ) находится вверху бокса:
            float minScroll = visibleTopZ - maxLayerZ;

            if (minScroll > maxScroll)
            {
                // Если карта меньше размера бокса — центрируем её
                float centerOffset = _boxCenter.z - (maxLayerZ * 0.5f);
                return (centerOffset, centerOffset);
            }

            return (minScroll, maxScroll);
        }

        public float CalculateScrollForNode(int nodeId)
        {
            if (_map == null) return 0f;
            var node = _map.GetNode(nodeId);
            if (node == null) return 0f;

            float nodeLayerZ = node.LayerIndex * _layerSpacingZ;
            // Ставим текущий узел в нижнюю треть видимой области
            float targetZInBox = _boxCenter.z - _boxSize.z * 0.25f;
            float targetScroll = targetZInBox - nodeLayerZ;

            var (minScroll, maxScroll) = GetScrollBounds();
            return Mathf.Clamp(targetScroll, minScroll, maxScroll);
        }

        public void FocusOnNode(int nodeId, bool immediate = false)
        {
            float targetScroll = CalculateScrollForNode(nodeId);
            SetScrollOffset(targetScroll);
        }

        public bool IsPositionInsideBounds(Vector3 worldPosition)
        {
            if (!_clipEnabled) return true;

            Vector3 localPos = transform.InverseTransformPoint(worldPosition) - _boxCenter;
            Vector3 halfSize = _boxSize * 0.5f;

            return Mathf.Abs(localPos.x) <= halfSize.x + 0.02f &&
                   Mathf.Abs(localPos.y) <= halfSize.y + 0.05f &&
                   Mathf.Abs(localPos.z) <= halfSize.z + 0.02f;
        }

        public bool IsNodeInsideBounds(int nodeId)
        {
            if (_nodeViews.TryGetValue(nodeId, out var view))
            {
                return IsPositionInsideBounds(view.transform.position);
            }
            return false;
        }

        public void HandleNodeClicked(int nodeId)
        {
            if (_state == null || _map == null) return;
            if (!IsNodeInsideBounds(nodeId)) return;
            if (!_state.CanMoveTo(_map, nodeId)) return;

            NodeSelected?.Invoke(nodeId);
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
            if (_contentRoot != null)
            {
                foreach (Transform child in _contentRoot)
                    Destroy(child.gameObject);
            }

            _nodeViews.Clear();
            _connectionObjects.Clear();
        }

        private void OnDrawGizmosSelected()
        {
            DrawGizmoBox(new Color(0.1f, 0.7f, 1f, 0.15f), new Color(0.1f, 0.9f, 1f, 0.95f));
        }

        private void OnDrawGizmos()
        {
            DrawGizmoBox(new Color(0.1f, 0.7f, 1f, 0.05f), new Color(0.1f, 0.7f, 1f, 0.4f));
        }

        private void DrawGizmoBox(Color fillColor, Color wireColor)
        {
            var oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            // Полупрозрачный куб
            Gizmos.color = fillColor;
            Gizmos.DrawCube(_boxCenter, _boxSize);

            // Каркасный контур
            Gizmos.color = wireColor;
            Gizmos.DrawWireCube(_boxCenter, _boxSize);

            // Плоскость подложки на дне бокса
            Gizmos.color = new Color(0.9f, 0.7f, 0.2f, 0.5f);
            Vector3 underlayPos = new Vector3(_boxCenter.x, _boxCenter.y - _boxSize.y * 0.5f, _boxCenter.z);
            Vector3 underlaySize = new Vector3(_boxSize.x, 0.001f, _boxSize.z);
            Gizmos.DrawWireCube(underlayPos, underlaySize);

            Gizmos.matrix = oldMatrix;
        }
    }
}
