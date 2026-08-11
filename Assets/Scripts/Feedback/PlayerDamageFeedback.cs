using DG.Tweening;
using ShellGame.Core;
using Unity.Cinemachine;
using UnityEngine;

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

        [Header("Красная виньетка (UI CanvasGroup на весь экран)")]
        [SerializeField] private CanvasGroup _vignetteCanvasGroup;
        [SerializeField] private float _vignettePeakAlpha = 0.45f;
        [SerializeField] private float _vignetteFadeInDuration = 0.08f;
        [SerializeField] private float _vignetteFadeOutDuration = 0.5f;

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
            if (_impulseSource == null) return;
            
            // Чтобы тряска каждый раз была разной, генерируем случайный вектор направления
            Vector3 randomDirection = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f).normalized;

            // Передаем вектор и умножаем на силу. GenerateImpulse(velocity) создаст случайный рывок.
            _impulseSource.GenerateImpulse(randomDirection * _impulseForce);
        }

        private void FlashVignette()
        {
            if (_vignetteCanvasGroup == null) return;

            _vignetteSequence?.Kill();
            _vignetteCanvasGroup.DOKill(); // Останавливаем предыдущие анимации этого CanvasGroup

            // Сбрасываем прозрачность (у CanvasGroup это свойство alpha, а не color)
            _vignetteCanvasGroup.alpha = 0f;

            _vignetteSequence = DOTween.Sequence()
                .Append(_vignetteCanvasGroup.DOFade(_vignettePeakAlpha, _vignetteFadeInDuration))
                .Append(_vignetteCanvasGroup.DOFade(0f, _vignetteFadeOutDuration));
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _cameraShakeTween?.Kill();
            _vignetteSequence?.Kill();
        }
    }
}