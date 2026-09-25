using DG.Tweening;
using ShellGame.Core;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

namespace ShellGame.Feedback
{
    /// <summary>
    /// Фидбек на попадание по игроку. Урон дозой проигрывается уколом иглой
    /// (Needle_Player): его запускает HealthController ДО списания здоровья, а
    /// сам урон внутри укола списывает событие анимации — NeedleMetalSqueak.ApplyDamage.
    /// Реакция (тряска камеры + красная виньетка) приходит отдельно, по
    /// GameEvents.DamageTaken, то есть на любой урон, включая урон от ножа.
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

        [Header("Камеры для смены")]
        public CinemachineCamera enemyCamera;
        public CinemachineCamera needleCamera;
        public CinemachineCamera mainCamera;
        public float cameraChangeDuration = 0.5f;

        private Tween _cameraShakeTween;
        private Sequence _vignetteSequence;
        private Coroutine _animationCoroutine;
        private bool _needleSearchWarned;
        private bool _needleInjectionInProgress;

        protected override TurnSide WatchedSide => TurnSide.Player;

        public override bool CanPlayNeedleInjection => TryEnsureNeedleAnimator() != null;

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
        private Animator TryEnsureNeedleAnimator()
        {
            if (_animator != null || string.IsNullOrEmpty(_needleAnimatorObjectName))
                return _animator;

            var needleObject = GameObject.Find(_needleAnimatorObjectName);
            _animator = needleObject != null ? needleObject.GetComponent<Animator>() : null;

            if (_animator == null && !_needleSearchWarned)
            {
                Debug.LogWarning($"[PlayerDamageFeedback] Игла '{_needleAnimatorObjectName}' не найдена в сцене — анимация укола по игроку не проиграется, но урон всё равно применится.", this);
                _needleSearchWarned = true;
            }

            return _animator;
        }

        /// <summary>
        /// Укол иглой по игроку от начала до конца (укол → возврат иглы).
        /// Запускается из HealthController.ApplyDamage(needNeedleAnim: true) и
        /// означает ровно одно: проиграть анимацию. Урон здесь не применяется —
        /// его списывает событие анимации внутри первой половины (момент входа
        /// иглы в тело), а дожидается вызывающий код через
        /// HealthController.WaitForNeedleInjection.
        /// </summary>
        public override IEnumerator PlayNeedleInjection()
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
                if (_logDamageFlow)
                    Debug.Log("[PlayerDamageFeedback] ИГЛА (урон дозой): укол → возврат", this);

                Animator needleAnimator = TryEnsureNeedleAnimator();

                if (needleCamera != null)
                    needleCamera.Priority = 3; // Камера иглы на время анимации в приоритете
                yield return new WaitForSeconds(cameraChangeDuration);

                // Урон спишет событие анимации в момент входа иглы в тело —
                // сначала "вооружаем" иглу, чтобы страховка тоже считала отсчёт
                // от начала укола.
                ArmNeedleDamage(needleAnimator);

                if (needleAnimator != null)
                    needleAnimator.SetTrigger(_damageTriggerName);

                yield return new WaitForSeconds(_damageAnimDuration);

                if (needleAnimator != null)
                    needleAnimator.SetTrigger(_returnAnimationName);

                yield return new WaitForSeconds(_returnAnimDuration);
            }
            finally
            {
                ResetCameraPriority();
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

            // Укол иглой к этому моменту уже отыгран (его запускает
            // HealthController), урон уже списан — здесь только реакция.
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


        override protected void OnEnable()
        {
            base.OnEnable();
            GameEvents.SideDied += EnemyDeathCameraChange;
        }

        private void EnemyDeathCameraChange(TurnSide side)
        {
            if (side == TurnSide.Enemy)
            {
                enemyCamera.Priority = 2;
            }
            else
            {
                return;
            }
        }

        private void ResetCameraPriority()
        {
            enemyCamera.Priority = 0;
            needleCamera.Priority = 0;
            mainCamera.Priority = 1;
        }
    }
}