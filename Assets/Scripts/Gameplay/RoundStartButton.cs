using DG.Tweening;
using ShellGame.Core;
using UnityEngine;

namespace ShellGame.Gameplay
{
    [RequireComponent(typeof(Collider))]
    public sealed class RoundStartButton : MonoBehaviour, IRoundInputTarget
    {
        [SerializeField] private Collider _clickCollider;
        [SerializeField] private float _hoverScale = 1.15f;
        [SerializeField] private float _hoverTweenDuration = 0.2f;
        [SerializeField] private Ease _hoverEase = Ease.OutQuad;
        [SerializeField] private float _clickScale = 0.1f;
        [SerializeField] private float _clickTweenDuration = 0.25f;
        [SerializeField] private Ease _clickEase = Ease.InBack;

        private Vector3 _baseScale;
        private Tween _hoverTween;
        private Tween _clickTween;
        private bool _isInteractable = true;

        private Vector3 GetSafeBaseScale()
        {
            if (_baseScale.x <= 0.001f && _baseScale.y <= 0.001f && _baseScale.z <= 0.001f)
                return Vector3.one;

            return _baseScale;
        }

        private void Awake()
        {
            if (_clickCollider == null)
                _clickCollider = GetComponent<Collider>();

            _baseScale = transform.localScale;
            if (_baseScale == Vector3.zero)
                _baseScale = Vector3.one;
        }

        public void SetInteractable(bool interactable)
        {
            _isInteractable = interactable;
            if (_clickCollider == null)
                _clickCollider = GetComponent<Collider>();
            if (_clickCollider != null)
                _clickCollider.enabled = interactable;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            _baseScale = GetSafeBaseScale();
            transform.localScale = _baseScale;
            _hoverTween?.Kill();
            _clickTween?.Kill();
            if (_clickCollider != null)
                _clickCollider.enabled = _isInteractable;
        }

        public void Hide()
        {
            _hoverTween?.Kill();
            _clickTween?.Kill();
            _baseScale = GetSafeBaseScale();
            transform.localScale = _baseScale;
            if (_clickCollider != null)
                _clickCollider.enabled = false;
            gameObject.SetActive(false);
        }

        public void OnHoverEnter()
        {
            if (!gameObject.activeInHierarchy || !_isInteractable)
                return;

            var safeBaseScale = GetSafeBaseScale();
            _baseScale = safeBaseScale;
            _hoverTween?.Kill();
            _hoverTween = transform.DOScale(safeBaseScale * _hoverScale, _hoverTweenDuration).SetEase(_hoverEase);
        }

        public void OnHoverExit()
        {
            if (!gameObject.activeInHierarchy || !_isInteractable)
                return;

            var safeBaseScale = GetSafeBaseScale();
            _baseScale = safeBaseScale;
            _hoverTween?.Kill();
            _hoverTween = transform.DOScale(safeBaseScale, _hoverTweenDuration).SetEase(_hoverEase);
        }

        public void Select()
        {
            if (!gameObject.activeInHierarchy || !_isInteractable || _clickCollider == null || !_clickCollider.enabled)
                return;

            var safeBaseScale = GetSafeBaseScale();
            _baseScale = safeBaseScale;
            _clickCollider.enabled = false;
            _hoverTween?.Kill();
            _clickTween?.Kill();
            _clickTween = transform.DOScale(safeBaseScale * _clickScale, _clickTweenDuration).SetEase(_clickEase)
                .OnComplete(() =>
                {
                    transform.localScale = safeBaseScale;
                    GameEvents.RaiseRoundStartConfirmed();
                });
        }

        private void OnDisable()
        {
            _hoverTween?.Kill();
            _clickTween?.Kill();
        }
    }
}