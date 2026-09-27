// START OF FILE KnifeVisual.cs
using DG.Tweening;
using FMOD.Studio;
using FMODUnity;
using ShellGame.Core;
using ShellGame.Shells;
using UnityEngine;

namespace ShellGame.Items
{
    /// <summary>
    /// Визуал ножа во время использования. Пока нож «выбран и парит в ожидании
    /// хода игрока» — только тряска вращения, ТОЧНО такая же, как у молотка
    /// (HammerVisual): Perlin-шум по осям, звук броска при этом НЕ играет.
    /// Звук полёта и эмитинг трейла включаются только когда нож начинает лететь
    /// в цель (ThrowAttackAt). Звук привязан к этому объекту и останавливается
    /// при его разрушении (OnDisable).
    /// </summary>
    public class KnifeVisual : MonoBehaviour
    {
        private Shell _hoveredShell;
        private bool _isStriking;
        private EventInstance _flightInstance;
        private TrailRenderer _trail;
        private Vector3 _anchorPos;
        private bool _anchorSet;

        [Header("Настройки тряски (как у молотка)")]
        public float ShakeIntensity = 15f;
        public float ShakeSpeed = 30f;

        private void Awake()
        {
            _trail = GetComponent<TrailRenderer>();
            if (_trail != null) _trail.emitting = false;
        }

        private void OnEnable()
        {
            GameEvents.ShellHoverEnter += OnHoverEnter;
            GameEvents.ShellHoverExit += OnHoverExit;
        }

        private void OnDisable()
        {
            GameEvents.ShellHoverEnter -= OnHoverEnter;
            GameEvents.ShellHoverExit -= OnHoverExit;
            transform.DOKill();
            StopFlightSound();
        }

        private void OnHoverEnter(Shell shell) => _hoveredShell = shell;
        private void OnHoverExit(Shell shell) { if (_hoveredShell == shell) _hoveredShell = null; }

        public void SetAnchor(Vector3 anchorPosition)
        {
            _anchorPos = anchorPosition;
            _anchorSet = true;
        }

        public void StartFlightSound(EventReference soundEvent)
        {
            if (soundEvent.IsNull) return;
            _flightInstance = RuntimeManager.CreateInstance(soundEvent);
            RuntimeManager.AttachInstanceToGameObject(_flightInstance, gameObject);
            _flightInstance.start();
        }

        private void StopFlightSound()
        {
            if (_flightInstance.isValid())
            {
                _flightInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                _flightInstance.release();
                _flightInstance.clearHandle();
            }
        }

        private void Update()
        {
            if (_isStriking) return;

            // Паттерн тряски — идентичен молотку (HammerVisual): парение над
            // анкором/наведённым наперстком + Perlin-дрожь вращения.
            Vector3 targetPos = _hoveredShell != null ? _hoveredShell.transform.position + Vector3.up * 0.7f : _anchorPos;
            if (_anchorSet)
                transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 8f);

            float t = Time.time * ShakeSpeed;
            transform.rotation = Quaternion.Euler(30f + (Mathf.PerlinNoise(t, 0f) - 0.5f) * ShakeIntensity, (Mathf.PerlinNoise(0f, t) - 0.5f) * ShakeIntensity, (Mathf.PerlinNoise(t, t) - 0.5f) * ShakeIntensity);
        }

        /// <summary>
        /// Бросок в цель: звук полёта + эмитинг трейла включаются именно здесь
        /// (в момент начала полёта), после попадания звук останавливается и нож
        /// уничтожается.
        /// </summary>
        public void ThrowAttackAt(EventReference flightSound, Vector3 targetPos, float duration, bool spin, System.Action onImpact)
        {
            _isStriking = true;
            transform.DOKill();

            if (_trail != null) _trail.emitting = true;
            StartFlightSound(flightSound);

            transform.LookAt(targetPos);

            if (spin)
                transform.DORotate(new Vector3(360f * 3f, 0, 0), duration, RotateMode.LocalAxisAdd).SetEase(Ease.Linear);

            transform.DOMove(targetPos, duration)
                .SetEase(Ease.InCubic)
                .OnComplete(() =>
                {
                    StopFlightSound();
                    onImpact?.Invoke();
                    Destroy(gameObject);
                });
        }
    }
}
// END OF FILE