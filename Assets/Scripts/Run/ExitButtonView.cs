using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ShellGame.Run
{
    /// <summary>
    /// Кнопка выхода из магазина "Продолжить путь".
    /// Поддерживает 3D-коллайдеры на столе, ввод из ShopPickupInput и стандартный UI EventSystem.
    /// </summary>
    public sealed class ExitButtonView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Transform _buttonVisual;
        [SerializeField] private float _hoverScale = 1.1f;
        [SerializeField] private float _hoverDuration = 0.15f;
        [SerializeField] private float _pressScale = 0.9f;
        [SerializeField] private float _pressDuration = 0.05f;

        private EncounterHost _host;
        private Vector3 _originalScale;
        private bool _isHovered;

        private void Awake()
        {
            ResolveHost();

            if (_buttonVisual == null)
                _buttonVisual = transform;

            _originalScale = _buttonVisual.localScale;

            // Если это 3D-объект и забыли повесить коллайдер — добавляем автоматически
            if (GetComponent<Collider>() == null && GetComponent<RectTransform>() == null)
            {
                gameObject.AddComponent<BoxCollider>();
            }
        }

        private void ResolveHost()
        {
            if (_host == null)
                _host = Object.FindFirstObjectByType<EncounterHost>();
        }

        public void Initialize(ShopEncounterController controller)
        {
            ResolveHost();
        }

        public void TriggerHover(bool hovered)
        {
            if (_isHovered == hovered) return;
            _isHovered = hovered;

            if (_buttonVisual == null) return;
            _buttonVisual.DOKill();

            if (hovered)
            {
                _buttonVisual.DOScale(_originalScale * _hoverScale, _hoverDuration).SetEase(Ease.OutBack);
            }
            else
            {
                _buttonVisual.DOScale(_originalScale, _hoverDuration).SetEase(Ease.InQuad);
            }
        }

        public void TriggerClick()
        {
            ResolveHost();
            if (_host == null) return;

            if (_buttonVisual != null)
            {
                _buttonVisual.DOKill();
                _buttonVisual.DOScale(_originalScale * _pressScale, _pressDuration)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() =>
                    {
                        if (_buttonVisual != null)
                            _buttonVisual.DOScale(_originalScale * _hoverScale, _hoverDuration).SetEase(Ease.OutBack);
                    });
            }

            _host.RequestExit();
        }

        // --- Поддержка стандартного ввода мыши по 3D-коллайдеру ---
        private void OnMouseDown() => TriggerClick();
        private void OnMouseEnter() => TriggerHover(true);
        private void OnMouseExit() => TriggerHover(false);

        // --- Поддержка UI EventSystem ---
        public void OnPointerClick(PointerEventData eventData) => TriggerClick();
        public void OnPointerEnter(PointerEventData eventData) => TriggerHover(true);
        public void OnPointerExit(PointerEventData eventData) => TriggerHover(false);

        private void OnDisable()
        {
            TriggerHover(false);
        }

        private void OnDestroy()
        {
            _buttonVisual?.DOKill();
        }
    }
}