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

        private void Awake()
        {
            if (_clickCollider == null)
                _clickCollider = GetComponent<Collider>();

            _baseScale = transform.localScale;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            transform.localScale = _baseScale;
            _hoverTween?.Kill();
            _clickTween?.Kill();
            if (_clickCollider != null)
                _clickCollider.enabled = true;
        }

        public void Hide()
        {
            _hoverTween?.Kill();
            _clickTween?.Kill();
            transform.localScale = _baseScale;
            if (_clickCollider != null)
                _clickCollider.enabled = false;
            gameObject.SetActive(false);
        }

        public void OnHoverEnter()
        {
            if (!gameObject.activeInHierarchy)
                return;

            _hoverTween?.Kill();
            _hoverTween = transform.DOScale(_baseScale * _hoverScale, _hoverTweenDuration).SetEase(_hoverEase);
        }

        public void OnHoverExit()
        {
            if (!gameObject.activeInHierarchy)
                return;

            _hoverTween?.Kill();
            _hoverTween = transform.DOScale(_baseScale, _hoverTweenDuration).SetEase(_hoverEase);
        }

        public void Select()
        {
            if (!gameObject.activeInHierarchy || _clickCollider == null || !_clickCollider.enabled)
                return;

            _clickCollider.enabled = false;
            _hoverTween?.Kill();
            _clickTween?.Kill();
            _clickTween = transform.DOScale(_baseScale * _clickScale, _clickTweenDuration).SetEase(_clickEase)
                .OnComplete(() => GameEvents.RaiseRoundStartConfirmed());
        }

        private void OnDisable()
        {
            _hoverTween?.Kill();
            _clickTween?.Kill();
        }
    }
}
