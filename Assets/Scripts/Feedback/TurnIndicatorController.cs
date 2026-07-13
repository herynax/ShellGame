using System;
using DG.Tweening;
using ShellGame.Core;
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
        [SerializeField] private Transform _playerTarget;
        [SerializeField] private Transform _enemyTarget;

        [Header("Анимация поворота")]
        [SerializeField] private float _rotateDuration = 0.6f;
        [SerializeField] private Ease _rotateEase = Ease.InOutBack;

        [Header("Idle-анимация (лёгкое дыхание, пока ход не меняется)")]
        [SerializeField] private bool _playIdleAnimation = true;
        [SerializeField] private float _idleScaleAmplitude = 0.05f;
        [SerializeField] private float _idleDuration = 1.2f;
        [SerializeField] private Ease _idleEase = Ease.InOutSine;

        private Vector3 _baseScale;
        private TurnSide _currentSide;
        private Tween _rotateTween;
        private Tween _idleTween;

        private void Awake()
        {
            if (_pointerTransform != null)
                _baseScale = _pointerTransform.localScale;
        }

        /// <summary>Поставить сторону мгновенно, без анимации поворота (например, в самом начале игры). Запускает idle.</summary>
        public void SetImmediate(TurnSide side)
        {
            _currentSide = side;
            if (_pointerTransform == null) return;

            var euler = _pointerTransform.localEulerAngles;
            euler.y = ComputeTargetLocalAngleY(side);
            _pointerTransform.localEulerAngles = euler;

            PlayIdle();
        }

        /// <summary>Анимированный поворот указателя к новой активной стороне. Idle останавливается на время поворота и возобновляется после.</summary>
        public void PlayTransition(TurnSide side, Action onComplete)
        {
            _currentSide = side;
            if (_pointerTransform == null)
            {
                onComplete?.Invoke();
                return;
            }

            StopIdle();
            _rotateTween?.Kill();

            var targetEuler = _pointerTransform.localEulerAngles;
            targetEuler.y = ComputeTargetLocalAngleY(side);

            _rotateTween = _pointerTransform
                .DOLocalRotate(targetEuler, _rotateDuration, RotateMode.FastBeyond360)
                .SetEase(_rotateEase)
                .OnComplete(() =>
                {
                    PlayIdle();
                    onComplete?.Invoke();
                });
        }

        /// <summary>
        /// Угол по Y (в локальных координатах _pointerTransform), под который
        /// нужно довернуться, чтобы посмотреть на цель нужной стороны.
        /// Направление считается только в горизонтальной плоскости — высота
        /// цели на угол не влияет.
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

            // Если указатель — дочерний объект повёрнутого родителя, переводим
            // мировой угол в локальный, иначе DOLocalRotate довернёт не туда.
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
            StopIdle();
        }
    }
}