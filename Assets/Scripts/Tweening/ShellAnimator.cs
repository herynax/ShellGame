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
    /// в пул и был переиспользован под другой наперсток.
    /// </summary>
    [RequireComponent(typeof(Transform))]
    public sealed class ShellAnimator : MonoBehaviour
    {
        private ShellConfig _config;
        private Sequence _activeSequence;
        private Tween _hoverTween;
        private Vector3 _baseScale;

        /// <summary>Исходный масштаб наперстка — нужен Shell'у, чтобы корректно восстановить масштаб при возврате из пула.</summary>
        public Vector3 BaseScale => _baseScale;

        public void Initialize(ShellConfig config)
        {
            _config = config;
            _baseScale = transform.localScale;
        }

        /// <summary>Убить все активные твины на этом наперстке. Вызывать при возврате в пул.</summary>
        public void Kill()
        {
            _activeSequence?.Kill();
            _activeSequence = null;
            _hoverTween?.Kill();
            _hoverTween = null;

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

        /// <summary>Подъём/показ с длительностью паузы из ShellConfig (стандартный случай — выбор игрока/AI).</summary>
        public void PlayReveal(Action onPeakReached, Action onComplete)
        {
            PlayReveal(-1f, onPeakReached, null, onComplete);
        }

        public void PlayReveal(Action onPeakReached, Action onDescendingStarted, Action onComplete)
        {
            PlayReveal(-1f, onPeakReached, onDescendingStarted, onComplete);
        }

        /// <summary>
        /// Подъём/показ с явно заданной длительностью паузы в верхней точке —
        /// используется для предварительного показа меток в начале раунда
        /// (RoundGenerator.RevealMarkers), где пауза берётся из настроек раунда,
        /// а не из ShellConfig.
        /// </summary>
        public void PlayReveal(float holdDuration, Action onPeakReached, Action onComplete)
        {
            PlayReveal(holdDuration, onPeakReached, null, onComplete);
        }

        public void PlayReveal(float holdDuration, Action onPeakReached, Action onDescendingStarted, Action onComplete)
        {
            _activeSequence?.Kill();
            var startPos = transform.localPosition;
            var peakPos = startPos + Vector3.up * _config.LiftHeight;
            var resolvedHold = holdDuration >= 0f ? holdDuration : _config.HoldRevealedDuration;

            _activeSequence = DOTween.Sequence()
                .Append(transform.DOLocalMove(peakPos, _config.LiftDuration).SetEase(_config.LiftEase))
                .AppendCallback(() => onPeakReached?.Invoke())
                .AppendInterval(resolvedHold)
                .AppendCallback(() => onDescendingStarted?.Invoke())
                .Append(transform.DOLocalMove(startPos, _config.LiftDuration).SetEase(Ease.InOutSine))
                .OnComplete(() => onComplete?.Invoke());
        }

        /// <summary>Переместить наперсток на новую мировую позицию — используется во время шаффла.</summary>
        public void PlayMoveTo(Vector3 worldPosition, Action onComplete, float moveDuration)
        {
            _activeSequence?.Kill();
            var resolvedDuration = Mathf.Max(0.01f, moveDuration);
            _activeSequence = DOTween.Sequence()
                .Append(transform.DOMove(worldPosition, resolvedDuration).SetEase(_config.ShuffleEase))
                .OnComplete(() => onComplete?.Invoke());
        }
    }
}
