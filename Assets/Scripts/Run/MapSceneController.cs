using System.Collections.Generic;
using ShellGame.Map;
using ShellGame.Map.Presentation;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace ShellGame.Run
{
    public sealed class MapSceneController : MonoBehaviour
    {
        [SerializeField] private GameObject _mapRoot;
        [SerializeField] private MapView3D _view;
        [SerializeField] private CinemachineCamera _mapCamera;
        [SerializeField] private Transform _cameraFocus;
        [SerializeField] private int _activePriority = 20;

        [Inject] private Camera _camera = null; // основная камера игры
        [Inject] private List<CinemachineStationaryLook> _lookControllers = null;

        private bool _interactive;

        private void Awake()
        {
            RunManager.EnsureExists();
            RunManager.Instance.RegisterMap(this);
            _view.NodeSelected += OnNodeSelected;
            _mapRoot.SetActive(false);
            SetMapCameraActive(false);
        }

        private void OnDestroy()
        {
            if (_view != null) _view.NodeSelected -= OnNodeSelected;
        }

        public void Show(MapData map, MapState state)
        {
            _mapRoot.SetActive(true);
            _view.Build(map, state);

            SetLookControllers(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_cameraFocus != null && _view.TryGetNodeWorldPosition(state.CurrentNodeId, out var pos))
                _cameraFocus.position = pos;

            SetMapCameraActive(true);
            _interactive = true;
        }

        public void Hide()
        {
            _interactive = false;
            SetMapCameraActive(false);
            _mapRoot.SetActive(false);
            SetLookControllers(true);
        }

        private void SetMapCameraActive(bool active)
        {
            if (_mapCamera == null) return;
            _mapCamera.Priority = new PrioritySettings { Enabled = active, Value = _activePriority };
        }

        private void SetLookControllers(bool enabled)
        {
            if (_lookControllers == null) return;
            foreach (var look in _lookControllers)
                if (look != null) look.enabled = enabled;
        }

        private void OnNodeSelected(int nodeId)
        {
            _interactive = false;
            if (!RunManager.Instance.SelectNode(nodeId))
                _interactive = true;
        }

        private void Update()
        {
            if (!_interactive || _camera == null) return;

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            var ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out var hit, 500f)) return;

            var node = hit.collider.GetComponentInParent<MapNodeView3D>();
            if (node != null)
                _view.HandleNodeClicked(node.NodeId);
        }
    }
}