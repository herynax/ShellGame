// START OF FILE RoundInputSystem.cs
using ShellGame.Items; // Добавлено для доступа к ItemPickupView
using ShellGame.Shells;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace ShellGame.Gameplay
{
    public sealed class RoundInputSystem : MonoBehaviour
    {
        [SerializeField] private Camera _interactionCamera;
        [SerializeField] private LayerMask _shellLayerMask;
        
        [Header("Aim Assist")]
        [Tooltip("Радиус толстого луча. Чем больше, тем легче попасть, но тем сильнее магнитит.")]
        [SerializeField, Min(0f)] private float _shellAimAssistRadius = 0.22f;
        [Tooltip("Вес дистанции до камеры при выборе цели. Если 0 - выбирается строго тот, к кому ближе прицел.")]
        [SerializeField, Range(0f, 1f)] private float _depthWeight = 0.1f;

        [Header("Стабилизация прицела")]
        [SerializeField, Min(0.001f)] private float _aimSmoothingTime = 0.03f;

        private bool _isEnabled;
        private IRoundInputTarget _hoveredTarget;
        private RoundStartButton _roundStartButton;

        private bool _hasSmoothedRay;
        private Vector3 _smoothedRayOrigin;
        private Vector3 _smoothedRayDirection = Vector3.forward;
        private Vector3 _rayOriginVelocity;

        [Inject]
        private void InjectDependencies(Camera interactionCamera, RoundStartButton roundStartButton)
        {
            if (_interactionCamera == null)
                _interactionCamera = interactionCamera;
            if (_roundStartButton == null)
                _roundStartButton = roundStartButton;
        }

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
                _hasSmoothedRay = false;
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

            Ray rawRay;
            if (Cursor.lockState == CursorLockMode.Locked || Cursor.visible == false)
            {
                rawRay = interactionCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            }
            else
            {
                rawRay = interactionCamera.ScreenPointToRay(mouse.position.ReadValue());
            }

            var ray = SmoothAimRay(rawRay);

            IRoundInputTarget targetUnderCursor = null;
            bool buttonHit = false;
            
            // 1. Проверяем кнопку старта
            if (_roundStartButton != null && _roundStartButton.gameObject.activeInHierarchy)
            {
                var buttonHits = Physics.RaycastAll(ray, 100f, Physics.AllLayers, QueryTriggerInteraction.Collide);
                System.Array.Sort(buttonHits, (left, right) => left.distance.CompareTo(right.distance));
                foreach (var buttonHitInfo in buttonHits)
                {
                    var button = buttonHitInfo.collider.GetComponentInParent<RoundStartButton>();
                    if (button != null)
                    {
                        targetUnderCursor = button;
                        buttonHit = true;
                        break;
                    }
                }
            }

            // 2. БЛОКИРОВКА АИМ-АССИСТА: Проверяем, не смотрим ли мы прямо на предмет
            bool itemHit = false;
            if (!buttonHit)
            {
                if (Physics.Raycast(ray, out RaycastHit directHit, 100f, Physics.AllLayers, QueryTriggerInteraction.Collide))
                {
                    if (directHit.collider.GetComponentInParent<ItemPickupView>() != null)
                    {
                        itemHit = true; // Мы смотрим прямо на предмет, магнит наперстков отключается!
                    }
                }
            }

            // 3. Ищем наперсток толстым лучом, только если не смотрим на кнопку или предмет
            if (!buttonHit && !itemHit)
            {
                targetUnderCursor = FindShellUnderAim(ray);
            }

            if (targetUnderCursor == _hoveredTarget)
                return;

            _hoveredTarget?.OnHoverExit();
            _hoveredTarget = targetUnderCursor;
            _hoveredTarget?.OnHoverEnter();
        }

        private Ray SmoothAimRay(Ray rawRay)
        {
            if (!_hasSmoothedRay)
            {
                _smoothedRayOrigin = rawRay.origin;
                _smoothedRayDirection = rawRay.direction;
                _rayOriginVelocity = Vector3.zero;
                _hasSmoothedRay = true;
                return rawRay;
            }

            _smoothedRayOrigin = Vector3.SmoothDamp(
                _smoothedRayOrigin, rawRay.origin, ref _rayOriginVelocity, _aimSmoothingTime,
                Mathf.Infinity, Time.unscaledDeltaTime);

            float slerpT = 1f - Mathf.Exp(-Time.unscaledDeltaTime / _aimSmoothingTime);
            _smoothedRayDirection = Vector3.Slerp(_smoothedRayDirection, rawRay.direction, slerpT).normalized;

            return new Ray(_smoothedRayOrigin, _smoothedRayDirection);
        }

        private Shell FindShellUnderAim(Ray ray)
        {
            var hits = Physics.SphereCastAll(
                ray,
                _shellAimAssistRadius,
                100f,
                _shellLayerMask,
                QueryTriggerInteraction.Collide);

            Shell bestShell = null;
            float bestScore = float.MaxValue;

            foreach (var hit in hits)
            {
                var shell = hit.collider.GetComponentInParent<Shell>();
                if (shell == null)
                    continue;

                Vector3 toShell = shell.transform.position - ray.origin;
                float distanceFromRay = Vector3.Cross(ray.direction, toShell).magnitude;
                float score = distanceFromRay + (hit.distance * _depthWeight);

                if (score < bestScore)
                {
                    bestShell = shell;
                    bestScore = score;
                }
            }

            return bestShell;
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

            return _interactionCamera;
        }
    }
}