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
        [Header("References")]
        [SerializeField] private GameObject _mapRoot;
        [SerializeField] private MapView3D _view;
        [SerializeField] private CinemachineCamera _mapCamera;
        [SerializeField] private int _activePriority = 20;

        [Header("Scrolling")]
        [SerializeField] private float _keyboardScrollSpeed = 1.2f;
        [SerializeField] private float _wheelScrollSensitivity = 0.002f;
        [SerializeField] private float _scrollSmoothTime = 0.08f;

        [Inject] private Camera _camera = null; // основная камера игры
        [Inject] private List<CinemachineStationaryLook> _lookControllers = null;
        [Inject] private MapGenerationConfig _mapConfig = null;

        private bool _interactive;
        private float _currentScroll;
        private float _targetScroll;
        private float _scrollVelocity;

        private void Awake()
        {
            RunManager.EnsureExists();
            RunManager.Instance.RegisterMap(this);
            if (_view != null) _view.NodeSelected += OnNodeSelected;
            if (_mapRoot != null) _mapRoot.SetActive(false);
            SetMapCameraActive(false);
        }

        private void Start()
        {
            StartCoroutine(RunManager.PostSceneLoad());
        }

        private void OnDestroy()
        {
            if (_view != null) _view.NodeSelected -= OnNodeSelected;
        }

        public void Show(MapData map, MapState state)
        {
            if (_mapRoot != null) _mapRoot.SetActive(true);
            if (_view != null)
            {
                MapLayout layout = _mapConfig?.Layout;
                _view.Build(map, state, layout);
                _targetScroll = _view.CalculateScrollForNode(state.CurrentNodeId);
                _currentScroll = _targetScroll;
                _scrollVelocity = 0f;
                _view.SetScrollOffset(_currentScroll);
            }

            SetLookControllers(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            SetMapCameraActive(true);
            _interactive = true;
        }

        public void Hide()
        {
            _interactive = false;
            SetMapCameraActive(false);
            if (_mapRoot != null) _mapRoot.SetActive(false);
            SetLookControllers(true);
        }

        private void SetMapCameraActive(bool active)
        {
            if (_mapCamera == null) return;
            _mapCamera.Priority = new PrioritySettings
            {
                Enabled = active,
                Value = active ? _activePriority : 0
            };
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
            if (!_interactive || _view == null) return;

            HandleScrolling();
            HandleNodeSelection();
        }

        private void HandleScrolling()
        {
            float scrollDelta = 0f;

            // 1. Колесо мыши
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f)
                {
                    // Прокрутка колесом вперед -> просмотр будущих слоев (вверх по карте)
                    scrollDelta -= wheel * _wheelScrollSensitivity;
                }
            }

            // 2. Клавиатура (W/S, стрелки вверх/вниз)
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float keyInput = 0f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                    keyInput += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                    keyInput -= 1f;

                if (Mathf.Abs(keyInput) > 0.01f)
                {
                    // W / UpArrow -> прокрутка вперед к боссу
                    scrollDelta -= keyInput * _keyboardScrollSpeed * Time.unscaledDeltaTime;
                }
            }

            // Применяем инпут с ограничениями
            var (minScroll, maxScroll) = _view.GetScrollBounds();
            _targetScroll = Mathf.Clamp(_targetScroll + scrollDelta, minScroll, maxScroll);

            // Плавная интерполяция скролла
            _currentScroll = Mathf.SmoothDamp(_currentScroll, _targetScroll, ref _scrollVelocity, _scrollSmoothTime, float.PositiveInfinity, Time.unscaledDeltaTime);
            _view.SetScrollOffset(_currentScroll);
        }

        private void HandleNodeSelection()
        {
            if (_camera == null) return;

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            var ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out var hit, 500f)) return;

            var node = hit.collider.GetComponentInParent<MapNodeView3D>();
            if (node != null && _view.IsPositionInsideBounds(hit.point) && _view.IsNodeInsideBounds(node.NodeId))
            {
                _view.HandleNodeClicked(node.NodeId);
            }
        }
    }
}
