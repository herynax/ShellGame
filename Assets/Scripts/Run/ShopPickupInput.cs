using ShellGame.Run;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ShellGame.Items
{
    public sealed class ShopPickupInput : MonoBehaviour
    {
        [SerializeField] private float _maxDistance = 100f;
        [SerializeField] private LayerMask _mask = ~0;
        [SerializeField] private bool _ignoreWhenOverUI = true;

        private static readonly RaycastHit[] Hits = new RaycastHit[32];

        private Camera _camera;
        private ItemPickupView _hovered;
        private ExitButtonView _hoveredExitButton;

        private void Update()
        {
            if (Time.timeScale <= 0f)
            {
                ClearHover();
                return;
            }

            Camera cam = ResolveCamera();
            if (cam == null || !TryReadPointer(out Vector2 pointer, out bool clicked))
                return;

            ItemPickupView pickup = null;
            ExitButtonView exitBtn = null;

            bool overUI = _ignoreWhenOverUI
                          && EventSystem.current != null
                          && EventSystem.current.IsPointerOverGameObject();

            if (!overUI)
            {
                FindInteractiveUnderPointer(cam.ScreenPointToRay(pointer), out pickup, out exitBtn);
            }

            // Обработка кнопки выхода
            if (exitBtn != _hoveredExitButton)
            {
                if (_hoveredExitButton != null)
                    _hoveredExitButton.TriggerHover(false);

                _hoveredExitButton = exitBtn;

                if (_hoveredExitButton != null)
                    _hoveredExitButton.TriggerHover(true);
            }

            if (_hoveredExitButton != null && clicked)
            {
                _hoveredExitButton.TriggerClick();
                return;
            }

            // Обработка предметов
            if (pickup != _hovered)
            {
                if (_hovered != null)
                    _hovered.SetHovered(false);
                _hovered = pickup;
            }

            if (_hovered == null)
                return;

            _hovered.SetHovered(true);

            if (clicked)
                _hovered.TryUse();
        }

        private void FindInteractiveUnderPointer(Ray ray, out ItemPickupView pickup, out ExitButtonView exitButton)
        {
            pickup = null;
            exitButton = null;

            int count = Physics.RaycastNonAlloc(ray, Hits, _maxDistance, _mask, QueryTriggerInteraction.Collide);
            float bestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = Hits[i];
                if (hit.collider == null || hit.distance >= bestDistance)
                    continue;

                var btn = hit.collider.GetComponentInParent<ExitButtonView>();
                if (btn != null)
                {
                    exitButton = btn;
                    pickup = null;
                    bestDistance = hit.distance;
                    continue;
                }

                var p = hit.collider.GetComponentInParent<ItemPickupView>();
                if (p != null)
                {
                    pickup = p;
                    exitButton = null;
                    bestDistance = hit.distance;
                }
            }
        }

        private Camera ResolveCamera()
        {
            if (_camera != null && _camera.isActiveAndEnabled)
                return _camera;

            _camera = Camera.main;
            if (_camera != null && _camera.isActiveAndEnabled)
                return _camera;

            Camera best = null;
            foreach (Camera candidate in Camera.allCameras)
            {
                if (candidate.targetTexture != null) continue;
                if (best == null || candidate.depth > best.depth) best = candidate;
            }

            _camera = best;
            return _camera;
        }

        private static bool TryReadPointer(out Vector2 position, out bool clicked)
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                position = default;
                clicked = false;
                return false;
            }

            position = mouse.position.ReadValue();
            clicked = mouse.leftButton.wasPressedThisFrame;
            return true;
#else
            position = Input.mousePosition;
            clicked = Input.GetMouseButtonDown(0);
            return true;
#endif
        }

        private void ClearHover()
        {
            if (_hovered != null)
                _hovered.SetHovered(false);
            _hovered = null;

            if (_hoveredExitButton != null)
                _hoveredExitButton.TriggerHover(false);
            _hoveredExitButton = null;
        }

        private void OnDisable() => ClearHover();
    }
}