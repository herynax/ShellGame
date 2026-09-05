using System;
using System.Collections.Generic;
using DG.Tweening;
using ShellGame.Core;
using ShellGame.Shells;
using UnityEngine;

namespace ShellGame.Feedback
{
    /// <summary>
    /// Указатель текущей инициативы — предмет/стрелка на столе. Цели (игрок/
    /// противник) задаются трансформами в сцене — указатель сам считает,
    /// на сколько градусов по Y довернуться, чтобы "посмотреть" в их сторону.
    /// Вращение — строго вокруг Y: даже если цель выше/ниже указателя,
    /// наклон по X/Z игнорируется (направление проецируется на горизонталь
    /// перед вычислением угла).
    /// </summary>
    public sealed class TurnIndicatorController : MonoBehaviour
    {
        [SerializeField] private Transform _pointerTransform;
        [SerializeField] private Renderer[] _sideRenderers;
        [SerializeField] private Transform _playerTarget;
        [SerializeField] private Transform _enemyTarget;

        [Header("Анимация поворота")]
        [SerializeField] private float _rotateDuration = 0.6f;
        [SerializeField] private Ease _rotateEase = Ease.InOutBack;
        
        // ИСПРАВЛЕНИЕ 1: Используем EventReference для выбора события FMOD в Инспекторе
        [SerializeField] private FMODUnity.EventReference _rotateSoundEvent;

        [Header("Idle-анимация (лёгкое дыхание, пока ход не меняется)")]
        [SerializeField] private bool _playIdleAnimation = true;
        [SerializeField] private float _idleScaleAmplitude = 0.05f;
        [SerializeField] private float _idleDuration = 1.2f;
        [SerializeField] private Ease _idleEase = Ease.InOutSine;

        private Vector3 _baseScale;
        private TurnSide _currentSide;
        private Tween _rotateTween;
        private Tween _idleTween;
        private Tween _sideTween;

        // ИСПРАВЛЕНИЕ 2: Добавляем переменную для инстанса (самого играющего звука)
        private FMOD.Studio.EventInstance _rotateSoundInstance;
        private MaterialPropertyBlock _sidePropertyBlock;
        private static readonly int SidePropertyId = Shader.PropertyToID("_Side");

        private void Awake()
        {
            if (_pointerTransform != null)
                _baseScale = _pointerTransform.localScale;
            if (_sideRenderers == null || _sideRenderers.Length == 0)
                _sideRenderers = _pointerTransform != null
                    ? _pointerTransform.GetComponentsInChildren<Renderer>(true)
                    : Array.Empty<Renderer>();
            _sidePropertyBlock = new MaterialPropertyBlock();
        }

        /// <summary>Поставить сторону мгновенно, без анимации поворота (например, в самом начале игры). Запускает idle.</summary>
        public void SetImmediate(TurnSide side)
        {
            _currentSide = side;
            ApplySide(side, null);
            if (_pointerTransform == null) return;

            var euler = _pointerTransform.localEulerAngles;
            euler.y = ComputeTargetLocalAngleY(side);
            _pointerTransform.localEulerAngles = euler;

            PlayIdle();
        }

        /// <summary>Анимированный поворот указателя к новой активной стороне. Idle останавливается на время поворота и возобновляется после.</summary>
        public void PlayTransition(TurnSide side, Action onComplete)
        {
            PlayTransition(side, null, onComplete);
        }

        public void PlayTransition(TurnSide side, IReadOnlyList<Shell> shells, Action onComplete)
        {
            _currentSide = side;
            if (_pointerTransform == null)
            {
                ApplySide(side, shells);
                onComplete?.Invoke();
                return;
            }

            StopIdle();
            _rotateTween?.Kill();
            _sideTween?.Kill();

            // Если звук уже играет, останавливаем предыдущий
            if (_rotateSoundInstance.isValid())
            {
                _rotateSoundInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            }

            var targetEuler = _pointerTransform.localEulerAngles;
            targetEuler.y = ComputeTargetLocalAngleY(side);
            float targetSideValue = side == TurnSide.Enemy ? 1f : 0f;
            _sideTween = DOTween.To(
                    () => GetSideValue(),
                    SetSideValue,
                    targetSideValue,
                    _rotateDuration)
                .SetEase(Ease.Linear);
            foreach (var shell in shells ?? Array.Empty<Shell>())
                shell?.AnimateSide(side, _rotateDuration);

            // Создаем экземпляр события (инстанс)
            _rotateSoundInstance = FMODUnity.RuntimeManager.CreateInstance(_rotateSoundEvent);
            // Устанавливаем позицию источника звука для 3D
            _rotateSoundInstance.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(_pointerTransform));
            // Запускаем звук
            _rotateSoundInstance.start();
            // Освобождаем память (звук уничтожится сам после полной остановки)
            _rotateSoundInstance.release();

            _rotateTween = _pointerTransform
                .DOLocalRotate(targetEuler, _rotateDuration, RotateMode.FastBeyond360)
                .SetEase(_rotateEase)
                .OnComplete(() =>
                {
                    // Останавливаем звук с фейдаутом по завершении анимации
                    if (_rotateSoundInstance.isValid())
                    {
                        _rotateSoundInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                    }

                    PlayIdle();
                    onComplete?.Invoke();
                });
        }

        public void ApplySide(TurnSide side, IReadOnlyList<Shell> shells)
        {
            SetSideValue(side == TurnSide.Enemy ? 1f : 0f);

            if (shells == null)
                return;

            foreach (var shell in shells)
                shell?.SetSide(side);
        }

        private float GetSideValue()
        {
            if (_sideRenderers == null || _sideRenderers.Length == 0 || _sidePropertyBlock == null)
                return 0f;

            _sideRenderers[0].GetPropertyBlock(_sidePropertyBlock);
            return _sidePropertyBlock.GetFloat(SidePropertyId);
        }

        private void SetSideValue(float value)
        {
            if (_sidePropertyBlock == null)
                return;

            _sidePropertyBlock.SetFloat(SidePropertyId, value);
            foreach (var renderer in _sideRenderers)
            {
                if (renderer != null)
                    renderer.SetPropertyBlock(_sidePropertyBlock);
            }
        }

        /// <summary>
        /// Угол по Y (в локальных координатах _pointerTransform), под который
        /// нужно довернуться, чтобы посмотреть на цель нужной стороны.
        /// </summary>
        private float ComputeTargetLocalAngleY(TurnSide side)
        {
            if (_pointerTransform == null)
                return 0f;

            var target = side == TurnSide.Player ? _playerTarget : _enemyTarget;
            if (target == null)
                return _pointerTransform.localEulerAngles.y;

            var direction = target.position - _pointerTransform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return _pointerTransform.localEulerAngles.y;

            var worldAngleY = Quaternion.LookRotation(direction, Vector3.up).eulerAngles.y;

            var parentAngleY = _pointerTransform.parent != null ? _pointerTransform.parent.eulerAngles.y : 0f;
            return worldAngleY - parentAngleY;
        }

        private void PlayIdle()
        {
            if (!_playIdleAnimation || _pointerTransform == null) return;

            StopIdle();
            _idleTween = _pointerTransform
                .DOScale(_baseScale * (1f + _idleScaleAmplitude), _idleDuration)
                .SetEase(_idleEase)
                .SetLoops(-1, LoopType.Yoyo);
        }

        private void StopIdle()
        {
            _idleTween?.Kill();
            _idleTween = null;
            if (_pointerTransform != null)
                _pointerTransform.localScale = _baseScale;
        }

        private void OnDisable()
        {
            _rotateTween?.Kill();
            _sideTween?.Kill();
            StopIdle();
            
            // ИСПРАВЛЕНИЕ 3: Хорошая практика глушить звук сразу, если объект выключается (например, меняется сцена)
            if (_rotateSoundInstance.isValid())
            {
                _rotateSoundInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                _rotateSoundInstance.release();
            }
        }
    }
}