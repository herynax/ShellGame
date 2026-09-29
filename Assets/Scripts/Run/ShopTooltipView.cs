using TMPro;
using UnityEngine;
using DG.Tweening;
using ShellGame.Items;

namespace ShellGame.Run
{
    /// <summary>
    /// Тултип для магазина: показывает "Название предмета\nN$"
    /// Расширяет ItemTooltipView, добавляя цену
    /// </summary>
    public sealed class ShopTooltipView : MonoBehaviour
    {
        public static ShopTooltipView Instance { get; private set; }

        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _priceText;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Transform _lookTarget;
        [SerializeField] private Vector3 _worldOffset = new Vector3(0f, 0.25f, 0f);
        [SerializeField] private float _showAnimationDuration = 0.18f;
        [SerializeField] private float _hideAnimationDuration = 0.12f;
        [SerializeField, Range(0.5f, 1f)] private float _showStartScale = 0.85f;
        [SerializeField] private Ease _showEase = Ease.OutBack;
        [SerializeField] private Ease _hideEase = Ease.InCubic;

        private Object _currentOwner;
        private ShopItemBehavior _currentShopItem;
        private Tween _animationTween;
        private Vector3 _baseScale = Vector3.one;

        private Transform TooltipTransform => transform;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            if (_canvas == null)
                _canvas = GetComponent<Canvas>();

            _canvas.renderMode = RenderMode.WorldSpace;
            _baseScale = transform.localScale;

            ResolveLookTarget();
            HideImmediate();
        }

        private void LateUpdate()
        {
            if (_currentOwner == null)
                return;

            if (_currentShopItem != null)
                TooltipTransform.position = _currentShopItem.transform.position + _worldOffset;

            ResolveLookTarget();
            FaceLookTarget();
        }

        /// <summary>Показывает тултип для предмета магазина</summary>
        public void Show(ShopItemBehavior shopItem, int price)
        {
            _currentOwner = shopItem;
            _currentShopItem = shopItem;

            if (_nameText != null && shopItem != null && shopItem.Entry != null && shopItem.Entry.Item != null)
                _nameText.text = shopItem.Entry.Item.DisplayName;

            if (_priceText != null)
                _priceText.text = $"{price}$";

            TooltipTransform.position = shopItem.transform.position + _worldOffset;
            ResolveLookTarget();
            FaceLookTarget();

            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;

            PlayShowAnimation();
        }

        public void UpdatePosition(Vector3 worldPosition)
        {
            TooltipTransform.position = worldPosition + _worldOffset;
            ResolveLookTarget();
            FaceLookTarget();
        }

        private void ResolveLookTarget()
        {
            if (_lookTarget == null && Camera.main != null)
                _lookTarget = Camera.main.transform;
        }

        private void FaceLookTarget()
        {
            if (_lookTarget == null)
                return;

            Vector3 direction = _lookTarget.position - TooltipTransform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                TooltipTransform.rotation = Quaternion.LookRotation(-direction, Vector3.up);
        }

        public void Hide(Object owner = null)
        {
            if (owner != null && _currentOwner != owner)
                return;

            _currentOwner = null;
            _currentShopItem = null;
            PlayHideAnimation();
        }

        private void HideImmediate()
        {
            _currentOwner = null;
            _currentShopItem = null;
            _animationTween?.Kill();
            _animationTween = null;
            transform.localScale = _baseScale;
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
        }

        private void PlayShowAnimation()
        {
            _animationTween?.Kill();
            transform.localScale = _baseScale * _showStartScale;

            Sequence showSequence = DOTween.Sequence();
            if (_canvasGroup != null)
                showSequence.Join(_canvasGroup.DOFade(1f, _showAnimationDuration));

            showSequence.Join(transform
                .DOScale(_baseScale, _showAnimationDuration)
                .SetEase(_showEase));
            _animationTween = showSequence;
        }

        private void PlayHideAnimation()
        {
            _animationTween?.Kill();

            Sequence hideSequence = DOTween.Sequence();
            if (_canvasGroup != null)
                hideSequence.Join(_canvasGroup.DOFade(0f, _hideAnimationDuration));

            hideSequence.Join(transform
                .DOScale(_baseScale * _showStartScale, _hideAnimationDuration)
                .SetEase(_hideEase));
            hideSequence.OnComplete(() => transform.localScale = _baseScale);
            _animationTween = hideSequence;
        }
    }
}