using DG.Tweening;
using UnityEngine;

namespace ShellGame.Tweening
{
    /// <summary>
    /// Ховер-анимация предмета в мире: небольшой подъём + увеличение
    /// масштаба. По ощущениям идентично ховеру наперстка, но вынесено
    /// отдельно, т.к. предметы — не Shell и не должны тянуть за собой
    /// ShellConfig/аудио-события наперстков.
    /// </summary>
    public sealed class ItemHoverAnimator : MonoBehaviour
    {
        [SerializeField] private float _liftHeight = 0.08f;
        [SerializeField] private float _hoverScaleMultiplier = 1.15f;
        [SerializeField] private float _tweenDuration = 0.15f;
        [SerializeField] private Ease _tweenEase = Ease.OutBack;

        private Vector3 _baseLocalPosition;
        private Vector3 _baseLocalScale;
        private Sequence _hoverSequence;

        private void Awake()
        {
            _baseLocalPosition = transform.localPosition;
            _baseLocalScale = transform.localScale;
        }

        /// <summary>Позволяет предмету (ItemDefinition) подстроить параметры под себя — вызывать сразу после спавна/инициализации.</summary>
        public void Configure(float liftHeight, float scaleMultiplier, float duration)
        {
            _liftHeight = liftHeight;
            _hoverScaleMultiplier = scaleMultiplier;
            _tweenDuration = duration;
        }

        public void PlayHoverEnter()
        {
            _hoverSequence?.Kill();
            _hoverSequence = DOTween.Sequence()
                .Join(transform.DOLocalMoveY(_baseLocalPosition.y + _liftHeight, _tweenDuration).SetEase(_tweenEase))
                .Join(transform.DOScale(_baseLocalScale * _hoverScaleMultiplier, _tweenDuration).SetEase(_tweenEase));
        }

        public void PlayHoverExit()
        {
            _hoverSequence?.Kill();
            _hoverSequence = DOTween.Sequence()
                .Join(transform.DOLocalMoveY(_baseLocalPosition.y, _tweenDuration).SetEase(_tweenEase))
                .Join(transform.DOScale(_baseLocalScale, _tweenDuration).SetEase(_tweenEase));
        }

        private void OnDisable()
        {
            _hoverSequence?.Kill();
            transform.localPosition = _baseLocalPosition;
            transform.localScale = _baseLocalScale;
        }
    }
}
