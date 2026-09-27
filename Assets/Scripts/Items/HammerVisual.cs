// START OF FILE HammerVisual.cs
using DG.Tweening;
using FMOD.Studio;
using FMODUnity;
using ShellGame.Core;
using ShellGame.Shells;
using UnityEngine;

namespace ShellGame.Items
{
    public class HammerVisual : MonoBehaviour
    {
        private Shell _hoveredShell;
        private bool _isStriking;
        private EventInstance _flightInstance;
        private TrailRenderer _trail;

        [Header("Настройки тревоги")]
        public float ShakeIntensity = 15f;
        public float ShakeSpeed = 30f;

        private void Awake()
        {
            _trail = GetComponent<TrailRenderer>();
            if (_trail != null) _trail.emitting = false;
        }

        private void OnEnable() { GameEvents.ShellHoverEnter += OnHoverEnter; GameEvents.ShellHoverExit += OnHoverExit; }
        private void OnDisable() { GameEvents.ShellHoverEnter -= OnHoverEnter; GameEvents.ShellHoverExit -= OnHoverExit; transform.DOKill(); StopFlightSound(); }

        private void OnHoverEnter(Shell shell) => _hoveredShell = shell;
        private void OnHoverExit(Shell shell) { if (_hoveredShell == shell) _hoveredShell = null; }

        // Включает звук полёта (привязан к молотку) и эмитинг трейла ТОЛЬКО
        // в момент начала полёта. Пока молоток парит в ожидании хода — тишина.
        private void BeginFlight(EventReference flightSound)
        {
            if (_trail != null) _trail.emitting = true;
            StartFlightSound(flightSound);
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
            Vector3 targetPos = _hoveredShell != null ? _hoveredShell.transform.position + Vector3.up * 0.7f : transform.position;
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 8f);

            float t = Time.time * ShakeSpeed;
            transform.rotation = Quaternion.Euler(30f + (Mathf.PerlinNoise(t, 0f) - 0.5f) * ShakeIntensity, (Mathf.PerlinNoise(0f, t) - 0.5f) * ShakeIntensity, (Mathf.PerlinNoise(t, t) - 0.5f) * ShakeIntensity);
        }

        public void Strike(EventReference flightSound, Vector3 targetPos, System.Action onImpact)
        {
            _isStriking = true;
            BeginFlight(flightSound);
            Sequence seq = DOTween.Sequence();
            seq.Append(transform.DOMoveY(transform.position.y + 0.3f, 0.15f).SetEase(Ease.OutQuad));
            seq.Join(transform.DORotate(new Vector3(-45f, 0, 0), 0.15f));
            seq.Append(transform.DOMove(targetPos, 0.1f).SetEase(Ease.InExpo));
            seq.Join(transform.DORotate(new Vector3(90f, 0, 0), 0.1f).SetEase(Ease.InExpo));
            seq.OnComplete(() => { StopFlightSound(); onImpact?.Invoke(); Destroy(gameObject); });
        }

        public void FlyToFace(EventReference flightSound, Vector3 facePos, System.Action onImpact)
        {
            _isStriking = true;
            BeginFlight(flightSound);
            transform.DORotate(new Vector3(360f * 3f, 180f, 0f), 0.4f, RotateMode.FastBeyond360).SetEase(Ease.InBack);
            transform.DOMove(facePos, 0.4f).SetEase(Ease.InBack).OnComplete(() => { StopFlightSound(); onImpact?.Invoke(); Destroy(gameObject); });
        }
    }
}
// END OF FILE