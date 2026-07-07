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
        private Shell _hoveredShell;

        public void Initialize(Camera interactionCamera, LayerMask shellLayerMask)
        {
            _interactionCamera = interactionCamera;
            _shellLayerMask = shellLayerMask;
        }

        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            if (!enabled)
            {
                _hoveredShell?.OnHoverExit();
                _hoveredShell = null;
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
            if (_interactionCamera == null)
                return;

            var mouse = Mouse.current;
            if (mouse == null)
                return;

            var ray = _interactionCamera.ScreenPointToRay(mouse.position.ReadValue());
            Shell shellUnderCursor = null;

            if (Physics.Raycast(ray, out var hit, 100f, _shellLayerMask))
                shellUnderCursor = hit.collider.GetComponentInParent<Shell>();

            if (shellUnderCursor == _hoveredShell)
                return;

            _hoveredShell?.OnHoverExit();
            _hoveredShell = shellUnderCursor;
            _hoveredShell?.OnHoverEnter();
        }

        private void HandleClick()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            if (!mouse.leftButton.wasPressedThisFrame)
                return;

            _hoveredShell?.Select();
        }
    }
}
