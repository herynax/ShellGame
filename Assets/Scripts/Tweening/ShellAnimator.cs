using System;
using DG.Tweening;
using ShellGame.Shells;
using UnityEngine;

namespace ShellGame.Tweening
{
    /// <summary>
    /// Инкапсулирует все DOTween-анимации одного наперстка: спавн, наведение,
    /// подъём/показ метки, перемещение во время шаффла.
    ///
    /// Важно для пула: все Sequence/Tween сохраняются в полях и убиваются в
    /// Kill() — это обязательно вызывать из Shell.OnReturnToPool(), иначе
    /// твин с колбэком может выстрелить уже после того, как объект вернулся
    /// в пул и был переиспользован под другой наперсток (частый источник
    /// багов при пулинге + твинах).
    /// </summary>
    [RequireComponent(typeof(Transform))]
    public sealed class ShellAnimator : MonoBehaviour
    {
        private ShellConfig _config;
        private Sequence _activeSequence;
        private Tween _hoverTween;
        private Vector3 _baseScale;

        public Vector3 BaseScale => _baseScale;

        public void Initialize(ShellConfig config)
        {
            _config = config;
            _baseScale = transform.localScale;
            if (_baseScale == Vector3.zero)
            {
                _baseScale = Vector3.one;
                transform.localScale = _baseScale;
            }
        }

        /// <summary>Убить все активные твины на этом наперстке. Вызывать при возврате в пул.</summary>
        public void Kill()
        {
            _activeSequence?.Kill();
            _activeSequence = null;
            _hoverTween?.Kill();
            _hoverTween = null;

            // DOTween полезно убивать по конкретному Transform целиком —
            // страхует от твинов, запущенных не через это поле (на будущее).
            transform.DOKill();
            transform.localScale = _baseScale;
        }

        public void PlaySpawnIn()
        {
            transform.localScale = Vector3.zero;
            _activeSequence?.Kill();
            _activeSequence = DOTween.Sequence()
                .Append(transform.DOScale(_baseScale, _config.SpawnScaleDuration).SetEase(_config.SpawnEase));
        }

        public void PlayHover(bool isHovering)
        {
            _hoverTween?.Kill();
            var targetScale = isHovering ? _baseScale * _config.HoverScale : _baseScale;
            _hoverTween = transform.DOScale(targetScale, _config.HoverTweenDuration).SetEase(Ease.OutQuad);
        }

        /// <summary>
        /// Поднять наперсток, подержать паузу (видно, что под ним) и опустить обратно.
        /// onPeakReached вызывается в верхней точке — удобно синхронизировать
        /// момент показа метки (включение визуала метки) со временем взлёта.
        /// </summary>
        public void PlayReveal(Action onPeakReached, Action onComplete)
        {
            PlayReveal(_config.HoldRevealedDuration, onPeakReached, onComplete);
        }

        public void PlayReveal(float holdDuration, Action onPeakReached, Action onComplete)
        {
            _activeSequence?.Kill();
            var startPos = transform.localPosition;
            var peakPos = startPos + Vector3.up * _config.LiftHeight;

            _activeSequence = DOTween.Sequence()
                .Append(transform.DOLocalMove(peakPos, _config.LiftDuration).SetEase(_config.LiftEase))
                .AppendCallback(() => onPeakReached?.Invoke())
                .AppendInterval(holdDuration)
                .Append(transform.DOLocalMove(startPos, _config.LiftDuration).SetEase(Ease.InOutSine))
                .OnComplete(() => onComplete?.Invoke());
        }

        /// <summary>Переместить наперсток на новую мировую позицию — используется во время шаффла.</summary>
        public void PlayMoveTo(Vector3 worldPosition, Action onComplete)
        {
            _activeSequence?.Kill();
            _activeSequence = DOTween.Sequence()
                .Append(transform.DOMove(worldPosition, _config.ShuffleMoveDuration).SetEase(_config.ShuffleEase))
                .OnComplete(() => onComplete?.Invoke());
        }
    }
}
