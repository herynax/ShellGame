using DG.Tweening;
using TMPro;
using UnityEngine;

namespace ShellGame.Items
{
    /// <summary>
    /// Мировой тултип магазина над товаром на столе.
    /// Показывает название предмета и цену в формате "Название 15$".
    /// Анимируется через DOTween по аналогии с ItemTooltipView.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class ShopTooltipView : MonoBehaviour
    {
        public static ShopTooltipView Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private TMP_Text _text;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Canvas _canvas;
        [Tooltip("Опциональный контейнер (плашка/панель). Если не задан, анимируется сам _text.")]
        [SerializeField] private RectTransform _contentRoot;

        [Header("Text Format")]
        [Tooltip("Формат текста. {0} — название предмета, {1} — цена.")]
        [SerializeField] private string _format = "{0} {1}$";

        [Header("Position & Facing")]
        [SerializeField] private Transform _lookTarget;
        [SerializeField] private Vector3 _worldOffset = new Vector3(0f, 0.35f, 0f);

        [Header("Animation Settings")]
        [SerializeField] private float _showAnimationDuration = 0.18f;
        [SerializeField] private float _hideAnimationDuration = 0.12f;
        [SerializeField, Range(0.5f, 1f)] private float _showStartScale = 0.85f;
        [SerializeField] private Ease _showEase = Ease.OutBack;
        [SerializeField] private Ease _hideEase = Ease.InCubic;

        private object _currentOwner;
        private ShopItemBehavior _currentShopOwner;
        private Tween _animationTween;
        private Vector3 _baseScale = Vector3.one;

        private Transform TooltipTransform => _contentRoot != null 
            ? _contentRoot 
            : (_text != null ? _text.rectTransform : transform);

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

            if (_canvas != null)
                _canvas.renderMode = RenderMode.WorldSpace;

            _baseScale = TooltipTransform.localScale;

            ResolveLookTarget();
            HideImmediate();
        }

        private void LateUpdate()
        {
            if (_currentOwner == null)
                return;

            if (_currentShopOwner != null)
                TooltipTransform.position = _currentShopOwner.transform.position + _worldOffset;

            ResolveLookTarget();
            FaceLookTarget();
        }

        /// <summary>
        /// Показать тултип над товаром (вызывается из ShopItemBehavior).
        /// </summary>
        public void Show(ShopItemBehavior behavior, int price)
        {
            if (behavior == null) return;

            _currentOwner = behavior;
            _currentShopOwner = behavior;

            ItemDefinition item = behavior.Item;
            string itemName = ResolveItemName(item);

            if (_text != null)
                _text.text = string.Format(_format, itemName, price);

            TooltipTransform.position = behavior.transform.position + _worldOffset;
            ResolveLookTarget();
            FaceLookTarget();

            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;

            PlayShowAnimation();
        }

        /// <summary>
        /// Универсальный вызов Show для обратной совместимости.
        /// </summary>
        public void Show(params object[] args)
        {
            if (args == null || args.Length == 0) return;

            if (args.Length >= 2 && args[0] is ShopItemBehavior behavior)
            {
                int price = args[1] is int p ? p : (behavior.Item != null ? behavior.Item.BuyPrice : 0);
                Show(behavior, price);
                return;
            }

            if (args[0] is ShopItemBehavior bOnly)
            {
                Show(bOnly, bOnly.Item != null ? bOnly.Item.BuyPrice : 0);
            }
        }

        public void Hide(object owner = null)
        {
            if (owner != null && _currentOwner != null && !ReferenceEquals(_currentOwner, owner))
                return;

            _currentOwner = null;
            _currentShopOwner = null;
            PlayHideAnimation();
        }

        private void HideImmediate()
        {
            _currentOwner = null;
            _currentShopOwner = null;
            _animationTween?.Kill();
            _animationTween = null;
            TooltipTransform.localScale = _baseScale;
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
        }

        private void PlayShowAnimation()
        {
            _animationTween?.Kill();
            TooltipTransform.localScale = _baseScale * _showStartScale;

            Sequence showSequence = DOTween.Sequence();
            if (_canvasGroup != null)
                showSequence.Join(_canvasGroup.DOFade(1f, _showAnimationDuration));

            showSequence.Join(TooltipTransform
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

            hideSequence.Join(TooltipTransform
                .DOScale(_baseScale * _showStartScale, _hideAnimationDuration)
                .SetEase(_hideEase));

            hideSequence.OnComplete(() => TooltipTransform.localScale = _baseScale);
            _animationTween = hideSequence;
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

        private void OnDestroy()
        {
            _animationTween?.Kill();
            if (Instance == this)
                Instance = null;
        }

        private static string ResolveItemName(ItemDefinition item)
        {
            if (item == null) return "Товар";

            var prop = item.GetType().GetProperty("Title")
                    ?? item.GetType().GetProperty("DisplayName")
                    ?? item.GetType().GetProperty("ItemName");
            if (prop != null)
            {
                string val = prop.GetValue(item) as string;
                if (!string.IsNullOrEmpty(val)) return val;
            }

            var field = item.GetType().GetField("Title")
                     ?? item.GetType().GetField("DisplayName")
                     ?? item.GetType().GetField("ItemName");
            if (field != null)
            {
                string val = field.GetValue(item) as string;
                if (!string.IsNullOrEmpty(val)) return val;
            }

            return item.name;
        }
    }
}