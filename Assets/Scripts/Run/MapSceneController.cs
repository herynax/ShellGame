using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
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

        [Header("Анимация полета карты")]
        [SerializeField] private float _flyDownDuration = 0.65f;
        [SerializeField] private float _flyUpDuration = 0.55f;
        [SerializeField] private float _flyHeightOffset = 30f;
        [SerializeField] private Ease _flyDownEase = Ease.OutCubic;
        [SerializeField] private Ease _flyUpEase = Ease.InCubic;

        [Header("Импакт камеры")]
        [SerializeField] private float _cameraImpactShakeStrength = 0.12f;
        [SerializeField] private float _cameraImpactShakeDuration = 0.3f;

        [Inject] private Camera _camera = null;
        [Inject] private List<CinemachineStationaryLook> _lookControllers = null;
        [Inject] private MapGenerationConfig _mapConfig = null;

        private bool _interactive;
        private float _currentScroll;
        private float _targetScroll;
        private float _scrollVelocity;
        private Vector3 _baseMapPosition;

        private void Awake()
        {
            RunManager.EnsureExists();
            RunManager.Instance.RegisterMap(this);

            if (_mapRoot != null)
            {
                _baseMapPosition = _mapRoot.transform.position;
                _mapRoot.SetActive(false);
            }

            SetMapCameraActive(false);

            if (_view != null)
                _view.NodeSelected += OnNodeSelected;
        }

        private void Start()
        {
            StartCoroutine(RunManager.PostSceneLoad());
        }

        private void OnDestroy()
        {
            if (_view != null)
                _view.NodeSelected -= OnNodeSelected;
        }

        public void Show(MapData map, MapState state, bool animate = true)
        {
            StartCoroutine(ShowRoutine(map, state, animate));
        }

        /// <summary>
        /// Карта опускается сверху вниз, слегка сотрясает камеру при ударе о стол и становится интерактивной.
        /// </summary>
        public IEnumerator ShowRoutine(MapData map, MapState state, bool animate = true)
        {
            _interactive = false;

            if (_mapRoot != null)
            {
                _mapRoot.SetActive(true);
                _mapRoot.transform.DOKill();
            }

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
            SetMapCameraActive(true);

            if (animate && _mapRoot != null)
            {
                Vector3 spawnPos = _baseMapPosition + Vector3.up * _flyHeightOffset;
                _mapRoot.transform.position = spawnPos;

                Camera mainCam = _camera != null ? _camera : Camera.main;
                if (mainCam != null)
                {
                    mainCam.transform.DOKill();
                    mainCam.transform.DOPunchRotation(new Vector3(-2f, 0f, 0f), _flyDownDuration, vibrato: 3, elasticity: 0.5f).SetUpdate(true);
                }

                Tween moveTween = _mapRoot.transform.DOMove(_baseMapPosition, _flyDownDuration).SetEase(_flyDownEase).SetUpdate(true);
                yield return moveTween.WaitForCompletion();

                _mapRoot.transform.position = _baseMapPosition;

                // Упругий отскок карты и сотрясение камеры
                _mapRoot.transform.DOPunchPosition(new Vector3(0f, -0.06f, 0f), 0.22f, vibrato: 5).SetUpdate(true);

                if (mainCam != null)
                {
                    mainCam.transform.DOShakePosition(_cameraImpactShakeDuration, strength: new Vector3(0f, _cameraImpactShakeStrength, 0f), vibrato: 12, randomness: 45).SetUpdate(true);
                    mainCam.transform.DOPunchRotation(new Vector3(-1.8f, 0f, 0f), _cameraImpactShakeDuration, vibrato: 6).SetUpdate(true);
                }

                yield return new WaitForSeconds(0.08f);
            }
            else if (_mapRoot != null)
            {
                _mapRoot.transform.position = _baseMapPosition;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _interactive = true;
        }

        public void Hide(bool animate = false)
        {
            StartCoroutine(HideRoutine(animate));
        }

        /// <summary>
        /// Карта улетает вверх в потолок, освобождая место для падающей комнаты энкаунтера.
        /// </summary>
        public IEnumerator HideRoutine(bool animate = true)
        {
            _interactive = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (animate && _mapRoot != null && _mapRoot.activeSelf)
            {
                _mapRoot.transform.DOKill();

                Camera mainCam = _camera != null ? _camera : Camera.main;
                if (mainCam != null)
                {
                    mainCam.transform.DOKill();
                    mainCam.transform.DOPunchRotation(new Vector3(2f, 0f, 0f), _flyUpDuration, vibrato: 3, elasticity: 0.5f).SetUpdate(true);
                    mainCam.transform.DOShakePosition(_flyUpDuration, strength: new Vector3(0.01f, 0.025f, 0.01f), vibrato: 12).SetUpdate(true);
                }

                Tween exitTween = _mapRoot.transform.DOMoveY(_baseMapPosition.y + _flyHeightOffset, _flyUpDuration).SetEase(_flyUpEase).SetUpdate(true);
                yield return exitTween.WaitForCompletion();
            }

            SetMapCameraActive(false);

            if (_mapRoot != null)
            {
                _mapRoot.SetActive(false);
                _mapRoot.transform.position = _baseMapPosition;
            }

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

            var mouse = Mouse.current;
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f)
                    scrollDelta -= wheel * _wheelScrollSensitivity;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float keyInput = 0f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) keyInput += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) keyInput -= 1f;

                if (Mathf.Abs(keyInput) > 0.01f)
                    scrollDelta -= keyInput * _keyboardScrollSpeed * Time.unscaledDeltaTime;
            }

            var (minScroll, maxScroll) = _view.GetScrollBounds();
            _targetScroll = Mathf.Clamp(_targetScroll + scrollDelta, minScroll, maxScroll);
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