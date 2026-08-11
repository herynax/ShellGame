using ShellGame.Shells;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShellGame.Gameplay
{
    public sealed class RoundInputSystem : MonoBehaviour
    {
        [SerializeField] private Camera _interactionCamera;
        [SerializeField] private LayerMask _shellLayerMask;

        private bool _isEnabled;
        private IRoundInputTarget _hoveredTarget;
        private RoundStartButton _roundStartButton;

        public void Initialize(Camera interactionCamera, LayerMask shellLayerMask, RoundStartButton roundStartButton)
        {
            _interactionCamera = interactionCamera;
            _shellLayerMask = shellLayerMask;
            _roundStartButton = roundStartButton;
        }

        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            if (!enabled)
            {
                _hoveredTarget?.OnHoverExit();
                _hoveredTarget = null;
            }
        }

        private void Update()
        {
            if (!_isEnabled)
                return;

            HandleHover();
            HandleClick();
        }

        private void HandleHover()
        {
            var interactionCamera = ResolveInteractionCamera();
            if (interactionCamera == null)
                return;

            var mouse = Mouse.current;
            if (mouse == null)
                return;

            Ray ray;
            if (Cursor.lockState == CursorLockMode.Locked || Cursor.visible == false)
            {
                ray = interactionCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            }
            else
            {
                ray = interactionCamera.ScreenPointToRay(mouse.position.ReadValue());
            }

            IRoundInputTarget targetUnderCursor = null;
            bool buttonHit = false;
            if (_roundStartButton != null && _roundStartButton.gameObject.activeInHierarchy)
            {
                var buttonLayerMask = 1 << _roundStartButton.gameObject.layer;
                if (Physics.Raycast(ray, out var buttonHitInfo, 100f, buttonLayerMask))
                {
                    var button = buttonHitInfo.collider.GetComponentInParent<RoundStartButton>();
                    if (button != null)
                    {
                        targetUnderCursor = button;
                        buttonHit = true;
                    }
                }
            }

            if (!buttonHit && Physics.Raycast(ray, out var hit, 100f, _shellLayerMask))
            {
                targetUnderCursor = hit.collider.GetComponentInParent<Shell>();
            }

            if (targetUnderCursor == _hoveredTarget)
                return;

            _hoveredTarget?.OnHoverExit();
            _hoveredTarget = targetUnderCursor;
            _hoveredTarget?.OnHoverEnter();
        }

        private void HandleClick()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            if (!mouse.leftButton.wasPressedThisFrame)
                return;

            _hoveredTarget?.Select();
        }

        private Camera ResolveInteractionCamera()
        {
            if (_interactionCamera != null)
                return _interactionCamera;

            _interactionCamera = Camera.main;
            if (_interactionCamera == null)
                _interactionCamera = FindObjectOfType<Camera>();

            return _interactionCamera;
        }
    }
}
