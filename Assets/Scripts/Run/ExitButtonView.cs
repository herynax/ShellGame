using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ShellGame.Run
{
    /// <summary>
    /// Кнопка выхода из магазина "Продолжить путь"
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

        private void Awake()
        {
            _host = FindFirstObjectByType<EncounterHost>();
        }

        public void Initialize(ShopEncounterController controller)
        {
            // Kept for compatibility, but we use EncounterHost for exit
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_host != null)
            {
                // Press animation
                if (_buttonVisual != null)
                {
                    _buttonVisual.DOKill();
                    _buttonVisual.DOScale(_originalScale * _pressScale, _pressDuration)
                        .SetEase(Ease.OutQuad)
                        .OnComplete(() => _buttonVisual.DOScale(_originalScale * _hoverScale, _hoverDuration).SetEase(Ease.OutBack));
                }

                _host.RequestExit();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_buttonVisual != null)
            {
                _buttonVisual.DOKill();
                _buttonVisual.DOScale(_originalScale * _hoverScale, _hoverDuration).SetEase(Ease.OutBack);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_buttonVisual != null)
            {
                _buttonVisual.DOKill();
                _buttonVisual.DOScale(_originalScale, _hoverDuration).SetEase(Ease.InQuad);
            }
        }

        private void OnDestroy()
        {
            _buttonVisual?.DOKill();
        }
    }
}