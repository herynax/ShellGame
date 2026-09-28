using System.Collections;
using FMOD.Studio;
using FMODUnity;
using ShellGame.Health;
using ShellGame.Run;
using UnityEngine;
using Zenject;

namespace ShellGame.Feedback
{
    /// <summary>
    /// Скрипт объекта иглы (Needle_Player / Needle_Enemy). Две независимые
    /// вещи, обе цепляются Animation Event'ами клипов укола:
    ///
    /// 1. Скрежет металла. PlayMetalSqueak() вызывается из Animation Event клипа
    ///    Damage (момент входа иглы в тело). Из-за ADSR-огибающей в FMOD звук
    ///    не должен обрываться резко — завершать его нужно фейд-аутом, который
    ///    триггерит Release-фазу огибающей. Для этого вызывается StopMetalSqueak()
    ///    (Animation Event на моменте возврата иглы), либо задаётся
    ///    _autoStopDelaySeconds.
    ///
    /// 2. Нанесение урона. ApplyDamage() — тот же момент входа иглы в тело, но
    ///    для геймплея: он списывает отложенный урон, который HealthController
    ///    ждёт от текущего укола (см. HealthController.ApplyDamage с
    ///    needNeedleAnim: true). Само анимацию запускает HealthController —
    ///    отсюда событие только и нужно, чтобы момент попадания задавал клип,
    ///    а не код.
    /// </summary>
    public sealed class NeedleMetalSqueak : MonoBehaviour
    {
        [Header("Звук скрежета металла (3D, в точке иглы)")]
        [Tooltip("FMOD-событие скрежета (например, как у ржавой качели). " +
                 "По умолчанию I_MetalItemsHower.")]
        [SerializeField] private EventReference _metalSqueakSound;

        [Header("Точка укола")]
        [Tooltip("Трансформ, откуда позиционировать (spatialize) звук. " +
                 "Если не задан — берётся позиция этого объекта (игла).")]
        [SerializeField] private Transform _injectionPoint;

        [Header("Затухание (Release ADSR)")]
        [Tooltip("Авто-стоп скрежета через это время. 0 = не останавливать " +
                 "автоматически — завершать только из StopMetalSqueak() " +
                 "(Animation Event) либо при выключении объекта. " +
                 "Если > 0 — через это время вызывается StopMetalSqueak().")]
        [SerializeField, Min(0f)] private float _autoStopDelaySeconds = 0f;

        [Header("Нанесение урона (страховка на потерянное событие анимации)")]
        [Tooltip("Сколько секунд после начала укола ждать события ApplyDamage из клипа. " +
                 "Нормально момент попадания задаёт сам клип (событие ApplyDamage), " +
                 "поэтому это поле — только страховка на случай, если событие в клипе " +
                 "забыто или анимация не проигралась. 0 = не списывать урон по таймеру " +
                 "вовсе, ждать только события.")]
        [SerializeField, Min(0f)] private float _damageFallbackDelay = 0.4f;

        private EventInstance _currentInstance;
        private float _stopAtTime = -1f;

        private HealthController _healthController;
        private Coroutine _damageFallbackCoroutine;


        private void Awake()
        {
            _healthController = GetComponentInParent<EncounterRig>().Health;
        }

        /// <summary>
        /// Метод для Animation Event из клипа укола (Needle_Enemy Animator) —
        /// момент, когда игла входит в тело врага.
        /// </summary>
        public void PlayMetalSqueak()
        {
            if (_metalSqueakSound.IsNull) return;

            StopMetalSqueak();

            Vector3 position = _injectionPoint != null
                ? _injectionPoint.position
                : transform.position;

            _currentInstance = RuntimeManager.CreateInstance(_metalSqueakSound);
            _currentInstance.set3DAttributes(RuntimeUtils.To3DAttributes(position));
            _currentInstance.start();

            _stopAtTime = _autoStopDelaySeconds > 0f
                ? Time.unscaledTime + _autoStopDelaySeconds
                : -1f;
        }

        /// <summary>
        /// Метод для Animation Event из клипа укола — момент, когда игла выходит
        /// обратно. Плавно завершает скрежет: stop(ALLOWFADEOUT) проигрывает
        /// Release-фазу ADSR-огибающей в FMOD вместо резкого обрыва.
        /// </summary>
        public void StopMetalSqueak()
        {
            if (_currentInstance.isValid())
            {
                _currentInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                _currentInstance.release();
                _currentInstance.clearHandle();
            }

            _stopAtTime = -1f;
        }

        private void Update()
        {
            if (_stopAtTime >= 0f && Time.unscaledTime >= _stopAtTime)
            {
                StopMetalSqueak();
            }
        }

        /// <summary>
        /// Метод для Animation Event из клипа укола — момент, когда игла входит
        /// в тело: списывает отложенный урон, который HealthController ждёт от
        /// текущего укола. Событие нужно повесить в клип укола ровно в тот кадр,
        /// где игла касается цели, — тогда момент попадания задаёт сама анимация,
        /// а не код.
        ///
        /// Если событие в клипе забыто (или анимация не проигралась вовсе), урон
        /// всё равно спишется по страховочному таймеру, который запускает
        /// ArmDamage.
        /// </summary>
        public void ApplyDamage()
        {
            if (_healthController == null)
            {
                Debug.LogWarning($"[NeedleMetalSqueak] Урон не списан на игле '{name}': HealthController не внедрён.", this);
                return;
            }

            StopDamageFallback();
            _healthController.ApplyPendingDamage();
        }

        /// <summary>
        /// Готовит иглу к уколу: включает страховочный таймер на случай, если
        /// событие ApplyDamage в клипе так и не вызовется. Вызывается фидбеком
        /// стороны (Player/EnemyDamageFeedback) прямо перед SetTrigger("Damage"),
        /// поэтому отсчёт идёт от реального начала входа иглы в тело.
        /// </summary>
        public void ArmDamage()
        {
            StopDamageFallback();

            if (_damageFallbackDelay > 0f)
                _damageFallbackCoroutine = StartCoroutine(DamageFallbackRoutine());
        }

        private void StopDamageFallback()
        {
            if (_damageFallbackCoroutine == null)
                return;

            StopCoroutine(_damageFallbackCoroutine);
            _damageFallbackCoroutine = null;
        }

        private IEnumerator DamageFallbackRoutine()
        {
            yield return new WaitForSeconds(_damageFallbackDelay);

            _damageFallbackCoroutine = null;

            // Урон мог уже списаться по событию анимации (тогда таймер был бы
            // остановлен) — но если отложенного урона нет, трогать нечего.
            if (_healthController == null || !_healthController.HasPendingDamage)
                yield break;

            Debug.LogWarning($"[NeedleMetalSqueak] Событие ApplyDamage в клипе укола не вызвалось за {_damageFallbackDelay} с — списываем урон по страховке.", this);
            _healthController.ApplyPendingDamage();
        }

        private void OnDisable()
        {
            StopDamageFallback();
            StopMetalSqueak();
        }
    }
}
