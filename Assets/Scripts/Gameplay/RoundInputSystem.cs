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
        [SerializeField, Min(0f)] private float _shellAimAssistRadius = 0.22f;

        [Header("Стабилизация прицела (от тряски камеры на дозе/наркотиках)")]
        [Tooltip("Чем больше — тем медленнее и плавнее луч следует за реальным движением камеры/мыши, тем меньше его дёргает от джиттера. Работает по немасштабированному времени — не плывёт от Time.timeScale.")]
        [SerializeField, Min(0.001f)] private float _aimSmoothingTime = 0.12f;

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
                _hasSmoothedRay = false; // при повторном включении не тянуть луч со старой позиции через всю комнату
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
            if (_roundStartButton != null && _roundStartButton.gameObject.activeInHierarchy)
            {
                var buttonHits = Physics.RaycastAll(
                    ray,
                    100f,
                    Physics.AllLayers,
                    QueryTriggerInteraction.Collide);
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

            if (!buttonHit)
                targetUnderCursor = FindShellUnderAim(ray);

            if (targetUnderCursor == _hoveredTarget)
                return;

            _hoveredTarget?.OnHoverExit();
            _hoveredTarget = targetUnderCursor;
            _hoveredTarget?.OnHoverEnter();
        }

        /// <summary>
        /// Демпфирует луч прицеливания по немасштабированному времени —
        /// сглаживает высокочастотный джиттер камеры (психоделик-эффект от
        /// дозы), не трогая при этом ни саму камеру, ни эффект. Первый
        /// вызов после включения инпута/после долгой паузы просто берёт
        /// сырой луч как есть — без "подтягивания" издалека.
        /// </summary>
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
            float bestAngle = float.MaxValue;
            float bestDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                var shell = hit.collider.GetComponentInParent<Shell>();
                if (shell == null)
                    continue;

                Vector3 toShell = shell.transform.position - ray.origin;
                float angle = Vector3.Angle(ray.direction, toShell);
                if (angle < bestAngle ||
                    (Mathf.Approximately(angle, bestAngle) && hit.distance < bestDistance))
                {
                    bestShell = shell;
                    bestAngle = angle;
                    bestDistance = hit.distance;
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