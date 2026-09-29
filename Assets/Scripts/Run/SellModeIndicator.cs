using DG.Tweening;
using ShellGame.Items;
using UnityEngine;

namespace ShellGame.Run
{
    /// <summary>
    /// Визуальный индикатор режима продажи. Подсвечивает предметы игрока,
    /// показывает текст "Выберите свой предмет для продажи"
    /// </summary>
    public sealed class SellModeIndicator : MonoBehaviour
    {
        public static SellModeIndicator Instance { get; private set; }

        [SerializeField] private GameObject _hintTextPrefab;
        [SerializeField] private Transform _hintContainer;
        [SerializeField] private float _pulseDuration = 1f;
        [SerializeField] private Color _highlightColor = Color.yellow;
        [SerializeField] private float _highlightIntensity = 1.5f;

        private GameObject _hintTextInstance;
        private Tween _pulseTween;
        private bool _isActive = false;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }

            if (_hintContainer == null)
                _hintContainer = transform;
        }

        /// <summary>
        /// Активировать режим продажи
        /// </summary>
        public void Activate()
        {
            if (_isActive) return;
            _isActive = true;

            // Show hint text
            if (_hintTextPrefab != null && _hintContainer != null)
            {
                _hintTextInstance = Instantiate(_hintTextPrefab, _hintContainer);
                _hintTextInstance.name = "SellModeHint";
            }

            // Pulse animation on hint
            if (_hintTextInstance != null)
            {
                var canvasGroup = _hintTextInstance.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = _hintTextInstance.AddComponent<CanvasGroup>();

                _pulseTween = DOTween.Sequence()
                    .Append(canvasGroup.DOFade(0.5f, _pulseDuration / 2f))
                    .Append(canvasGroup.DOFade(1f, _pulseDuration / 2f))
                    .SetLoops(-1, LoopType.Yoyo);
            }
        }

        /// <summary>
        /// Деактивировать режим продажи
        /// </summary>
        public void Deactivate()
        {
            if (!_isActive) return;
            _isActive = false;

            _pulseTween?.Kill();
            _pulseTween = null;

            if (_hintTextInstance != null)
            {
                Destroy(_hintTextInstance);
                _hintTextInstance = null;
            }
        }

        /// <summary>
        /// Подсветить предмет для продажи
        /// </summary>
        public void HighlightItem(ItemPickupView pickup, bool highlight)
        {
            if (pickup == null) return;

            // Could add outline, glow, or scale effect here
            if (highlight)
            {
                pickup.transform.DOKill();
                pickup.transform.DOScale(pickup.transform.localScale * _highlightIntensity, 0.2f).SetEase(Ease.OutBack);
            }
            else
            {
                pickup.transform.DOKill();
                pickup.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.InQuad);
            }
        }

        private void OnDestroy()
        {
            _pulseTween?.Kill();
            if (Instance == this)
                Instance = null;
        }
    }
}