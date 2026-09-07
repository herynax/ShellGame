// ShellGame.Items / ItemTooltipView.cs
using TMPro;
using UnityEngine;
using DG.Tweening;

namespace ShellGame.Items
{
    /// <summary>
    /// Мировой тултип с описанием предмета — показывается, если игрок
    /// держит курсор на предмете дольше ItemDefinition.TooltipHoverDelay
    /// (см. ItemPickupView). Один общий экземпляр на сцену, по аналогии с
    /// HealthSoundProvider.Instance — разместить один Canvas в world space,
    /// назначить TMP_Text и CanvasGroup в инспекторе. К предмету привязывается
    /// только текст внутри Canvas, сам Canvas остаётся общим контейнером.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class ItemTooltipView : MonoBehaviour
    {
        public static ItemTooltipView Instance { get; private set; }

        [SerializeField] private TMP_Text _text;
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
        private ItemPickupView _currentItemOwner;
        private Tween _animationTween;
        private Vector3 _baseTextScale = Vector3.one;

        private Transform TooltipTransform => _text != null ? _text.rectTransform : transform;

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
            if (_text != null)
                _baseTextScale = _text.rectTransform.localScale;

            ResolveLookTarget();

            HideImmediate();
        }

        private void LateUpdate()
        {
            if (_currentOwner == null)
                return;

            if (_currentItemOwner != null)
                TooltipTransform.position = _currentItemOwner.transform.position + _worldOffset;

            ResolveLookTarget();
            FaceLookTarget();
        }

        /// <summary>owner — тот ItemPickupView, что запросил показ; нужен, чтобы чужой Hide() не погасил чужой тултип.</summary>
        public void Show(string description, Vector3 worldPosition, Object owner = null)
        {
            _currentOwner = owner;
            _currentItemOwner = owner as ItemPickupView;

            if (_text != null)
                _text.text = description;

            TooltipTransform.position = worldPosition + _worldOffset;
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
            _currentItemOwner = null;
            PlayHideAnimation();
        }

        private void HideImmediate()
        {
            _currentOwner = null;
            _currentItemOwner = null;
            _animationTween?.Kill();
            _animationTween = null;
            TooltipTransform.localScale = _baseTextScale;
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
        }

        private void PlayShowAnimation()
        {
            _animationTween?.Kill();
            TooltipTransform.localScale = _baseTextScale * _showStartScale;

            Sequence showSequence = DOTween.Sequence();
            if (_canvasGroup != null)
                showSequence.Join(_canvasGroup.DOFade(1f, _showAnimationDuration));

            showSequence.Join(TooltipTransform
                .DOScale(_baseTextScale, _showAnimationDuration)
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
                .DOScale(_baseTextScale * _showStartScale, _hideAnimationDuration)
                .SetEase(_hideEase));
            hideSequence.OnComplete(() => TooltipTransform.localScale = _baseTextScale);
            _animationTween = hideSequence;
        }
    }
}