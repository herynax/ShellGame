using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace ShellGame.Feedback
{
    /// <summary>
    /// Скрежет металла в момент укола иглы. Висит на объекте с Animator
    /// (Needle_Enemy). PlayMetalSqueak() вызывается из Animation Event клипа
    /// Damage (момент входа иглы в тело). Из-за ADSR-огибающей в FMOD звук
    /// не должен обрываться резко — завершать его нужно фейд-аутом, который
    /// триггерит Release-фазу огибающей. Для этого вызывается StopMetalSqueak()
    /// (Animation Event на моменте возврата иглы), либо задаётся
    /// _autoStopDelaySeconds.
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

        private EventInstance _currentInstance;
        private float _stopAtTime = -1f;

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

        private void OnDisable()
        {
            StopMetalSqueak();
        }
    }
}