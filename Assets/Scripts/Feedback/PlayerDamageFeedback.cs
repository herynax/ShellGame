using DG.Tweening;
using ShellGame.Core;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

namespace ShellGame.Feedback
{
    /// <summary>
    /// Фидбек на попадание по игроку: сначала доигрывает анимация укола иглой
    /// (Needle_Player), и только после неё применяется урон и идёт реакция
    /// (тряска камеры + красная виньетка). Разделение на два шага нужно, чтобы
    /// GameManager мог ждать окончания анимации укола ДО списания здоровья —
    /// см. GameManager.RevealResult.
    /// </summary>
    public sealed class PlayerDamageFeedback : DamageFeedbackBase
    {
        [Header("Animator (Damage/Return) — игла, которая колет игрока")]
        [Tooltip("Аниматор иглы. Обычно это объект Needle_Player (===Environment.prefab). " +
                 "Если не задан в инспекторе — ищется по имени объекта автоматически.")]
        [SerializeField] private Animator _animator;
        [Tooltip("Имя объекта с аниматором иглы — запасной вариант, если _animator не задан вручную")]
        [SerializeField] private string _needleAnimatorObjectName = "Needle_Player";
        [SerializeField] private string _damageTriggerName = "Damage";
        [SerializeField] private string _returnAnimationName = "Return";
        [Tooltip("Длительность анимации укола (до момента фактического введения иглы)")]
        [SerializeField] private float _damageAnimDuration = 0.21f;
        [SerializeField] private float _returnAnimDuration = 0.6f;

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

        [Header("Отладка")]
        [Tooltip("Логировать в консоль, когда игла реально укалывает игрока и когда урон обрабатывается только реакцией (виньетка/тряска). Удобно проверять, что игла играет ТОЛЬКО при уроне дозой (после поднятия наперстка с меткой).")]
        [SerializeField] private bool _logDamageFlow = true;

        private Tween _cameraShakeTween;
        private Sequence _vignetteSequence;
        private Coroutine _animationCoroutine;
        private bool _needleSearchWarned;
        private bool _needleInjectionInProgress;

        protected override TurnSide WatchedSide => TurnSide.Player;

        protected override void Awake()
        {
            base.Awake();
            TryEnsureNeedleAnimator();
        }

        /// <summary>
        /// Если аниматор не назначен в инспекторе — ищем объект иглы по имени
        /// (лениво, при каждом вызове, т.к. Needle_Player может спавниться позже
        /// этого компонента). Найденный результат кэшируем; при неудаче
        /// предупреждаем в консоль один раз.
        /// </summary>
        private void TryEnsureNeedleAnimator()
        {
            if (_animator != null || string.IsNullOrEmpty(_needleAnimatorObjectName))
                return;

            var needleObject = GameObject.Find(_needleAnimatorObjectName);
            _animator = needleObject != null ? needleObject.GetComponent<Animator>() : null;

            if (_animator == null && !_needleSearchWarned)
            {
                Debug.LogWarning($"[PlayerDamageFeedback] Игла '{_needleAnimatorObjectName}' не найдена в сцене — анимация укола по игроку не проиграется, но урон всё равно применится.", this);
                _needleSearchWarned = true;
            }
        }

        /// <summary>
        /// Проигрывает анимацию укола иглой по игроку от начала до конца
        /// (укол → возврат иглы). Вызывается GameManager'ом ПЕРЕД нанесением
        /// урона: корутина — это и есть вся длительность анимации, по её
        /// завершению урон можно безопасно списывать.
        ///
        /// Игла — анимация УРОНА ДОЗОЙ (наперсток с меткой поднят), а не реакция
        /// на любой урон: урон от ножа (или любой другой урон не-дозой) НЕ должен
        /// запускать эту анимацию. GameManager вызывает её только из RevealResult.
        /// </summary>
        public IEnumerator PlayNeedleInjectionRoutine()
        {
            if (_needleInjectionInProgress)
            {
                Debug.LogWarning("[PlayerDamageFeedback] Попытка проиграть укол иглой, пока предыдущий ещё не закончился — " +
                                 "пропускаем повторную анимацию (иначе она сломается и проиграется дважды).", this);
                yield break;
            }

            _needleInjectionInProgress = true;
            try
            {
                TryEnsureNeedleAnimator();

                if (_logDamageFlow)
                    Debug.Log("[PlayerDamageFeedback] ИГЛА (урон дозой): укол → возврат", this);

                if (_animator != null)
                    _animator.SetTrigger(_damageTriggerName);

                yield return new WaitForSeconds(_damageAnimDuration);

                if (_animator != null)
                    _animator.SetTrigger(_returnAnimationName);

                yield return new WaitForSeconds(_returnAnimDuration);
            }
            finally
            {
                _needleInjectionInProgress = false;
            }
        }

        protected override void PlayFeedback(int amount, int currentHealth, int maxHealth, bool died)
        {
            if (_animationCoroutine != null)
                StopCoroutine(_animationCoroutine);

            _animationCoroutine = StartCoroutine(PlayDamageReactionCoroutine());
        }

        private IEnumerator PlayDamageReactionCoroutine()
        {
            if (_logDamageFlow)
                Debug.Log("[PlayerDamageFeedback] РЕАКЦИЯ БЕЗ ИГЛЫ (напр. урон от ножа): виньетка + тряска", this);

            // Укол иглой уже отыграл заранее (PlayNeedleInjectionRoutine),
            // урон уже списан — здесь только реакция на попадание.
            ShakeCamera();
            FlashVignette();

            float reactionDuration = _vignetteFadeInDuration + _vignetteFadeOutDuration;
            yield return new WaitForSeconds(reactionDuration);

            _animationCoroutine = null;
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

            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
                _animationCoroutine = null;
            }
        }
    }
}