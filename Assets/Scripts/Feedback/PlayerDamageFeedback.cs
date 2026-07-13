using DG.Tweening;
using ShellGame.Core;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

namespace ShellGame.Feedback
{
    /// <summary>
    /// Плейсхолдер-фидбек на попадание по игроку: тряска камеры + красная
    /// виньетка на весь экран. Когда появится полноценный арт (постпроцесс
    /// виньетки, экранные эффекты) — меняется только PlayFeedback.
    /// </summary>
    public sealed class PlayerDamageFeedback : DamageFeedbackBase
    {
        [Header("Настройки камеры (Cinemachine)")]
        [Tooltip("Источник импульса, висящий на этом же объекте или камере")]
        [SerializeField] private CinemachineImpulseSource _impulseSource;
        [Tooltip("Сила тряски камеры")]
        [SerializeField] private float _impulseForce = 1f;

        [Header("Красная виньетка (UI Image на весь экран, alpha=0 по умолчанию)")]
        [SerializeField] private Image _vignetteImage;
        [SerializeField] private float _vignettePeakAlpha = 0.45f;
        [SerializeField] private float _vignetteFadeInDuration = 0.08f;
        [SerializeField] private float _vignetteFadeOutDuration = 0.5f;

        private Vector3 _cameraBasePosition;
        private Tween _cameraShakeTween;
        private Sequence _vignetteSequence;

        protected override TurnSide WatchedSide => TurnSide.Player;

        protected override void Awake()
        {
            base.Awake();
        }

        protected override void PlayFeedback(int amount, int currentHealth, int maxHealth, bool died)
        {
            ShakeCamera();
            FlashVignette();
        }

        private void ShakeCamera()
        {
            // Чтобы тряска каждый раз была разной, генерируем случайный вектор направления
            Vector3 randomDirection = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f).normalized;

            // Передаем вектор и умножаем на силу. GenerateImpulse(velocity) создаст случайный рывок.
            _impulseSource.GenerateImpulse(randomDirection * _impulseForce);
        }

        private void FlashVignette()
        {
            if (_vignetteImage == null) return;

            _vignetteSequence?.Kill();
            _vignetteImage.DOKill();

            var color = _vignetteImage.color;
            color.a = 0f;
            _vignetteImage.color = color;

            _vignetteSequence = DOTween.Sequence()
                .Append(_vignetteImage.DOFade(_vignettePeakAlpha, _vignetteFadeInDuration))
                .Append(_vignetteImage.DOFade(0f, _vignetteFadeOutDuration));
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _cameraShakeTween?.Kill();
            _vignetteSequence?.Kill();
        }
    }
}
