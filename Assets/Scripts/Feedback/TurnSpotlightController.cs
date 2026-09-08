using System.Collections;
using DG.Tweening;
using ShellGame.Core;
using ShellGame.Gameplay;
using UnityEngine;
using Zenject;

namespace ShellGame.Feedback
{
    /// <summary>
    /// Поворачивает прожектор по заданным локальным углам X во время смены хода.
    /// </summary>
    public sealed class TurnSpotlightController : MonoBehaviour
    {
        [Header("Что поворачивать")]
        [SerializeField] private Transform _spotlightTransform;

        [Header("Углы по X")]
        [Tooltip("Локальный угол X, когда ход игрока.")]
        [SerializeField] private float _playerAngleX;
        [Tooltip("Локальный угол X, когда ход врага.")]
        [SerializeField] private float _enemyAngleX;

        [Header("Переход хода")]
        [SerializeField] private TurnIndicatorController _turnIndicator;

        private GameManager _gameManager;
        private Tween _rotationTween;
        private float _startY;
        private float _startZ;

        [Inject]
        private void InjectDependencies(GameManager gameManager, TurnIndicatorController turnIndicator)
        {
            _gameManager = gameManager;
            if (_turnIndicator == null)
                _turnIndicator = turnIndicator;
        }

        private void Awake()
        {
            if (_spotlightTransform == null)
                _spotlightTransform = transform;

            Vector3 startAngles = _spotlightTransform.localEulerAngles;
            _startY = startAngles.y;
            _startZ = startAngles.z;

            if (_turnIndicator == null)
                _turnIndicator = GetComponentInParent<TurnIndicatorController>();
        }

        private void OnEnable()
        {
            GameEvents.ActiveSideChanged += HandleActiveSideChanged;
        }

        private IEnumerator Start()
        {
            // GameManager инициализирует ActiveSide в своём Start.
            yield return null;

            if (_gameManager != null)
                AimAt(_gameManager.ActiveSide);
        }

        private void HandleActiveSideChanged(TurnSide side)
        {
            AimAt(side);
        }

        private void AimAt(TurnSide side)
        {
            if (_spotlightTransform == null)
                return;

            float angleX = side == TurnSide.Player ? _playerAngleX : _enemyAngleX;
            Vector3 targetAngles = new Vector3(angleX, _startY, _startZ);
            float duration = _turnIndicator != null
                ? _turnIndicator.RotateDuration
                : 0.6f;

            _rotationTween?.Kill();
            _rotationTween = _spotlightTransform
                .DOLocalRotate(targetAngles, duration, RotateMode.Fast)
                .SetEase(Ease.InOutSine);
        }

        private void OnDisable()
        {
            GameEvents.ActiveSideChanged -= HandleActiveSideChanged;
            _rotationTween?.Kill();
        }
    }
}