using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace ShellGame.Feedback
{
    /// <summary>
    /// Чисто визуальный признак "раздумья" противника — без риггинга и
    /// Animator (см. EnemyDamageFeedback: скелетной анимации у модели пока
    /// нет). Перед тем, как враг реально применит предмет, крутит
    /// _headTransform (или весь объект целиком, если голова отдельно не
    /// выделена) в сторону нескольких его предметов, затем — на тот,
    /// который он на самом деле применяет. Никаких решений сам не
    /// принимает — вызывающий код (ItemSpawner) просто просит "покажи, что
    /// подумал" и передаёт, на что смотреть.
    /// </summary>
    public sealed class EnemyLookController : MonoBehaviour
    {
        [Tooltip("Что крутим. Если не назначено — крутится transform этого же объекта.")]
        [SerializeField] private Transform _headTransform;

        [Tooltip("Точка на лице/между глазами. Её локальная ось +Z — линия взгляда врага. Если не назначена, используется +Z _headTransform.")]
        [SerializeField] private Transform _faceForwardTransform;

        [Header("Дыхание")]
        [Tooltip("Что слегка поднимается и опускается при дыхании. Если не назначено, используется объект врага.")]
        [SerializeField] private Transform _breathingTransform;
        [SerializeField] private float _breathingAmplitude = 0.025f;
        [SerializeField] private float _breathingCycleDuration = 2.4f;

        [SerializeField] private float _glanceDuration = 0.35f;
        [SerializeField] private float _glanceHoldDuration = 0.15f;
        [SerializeField] private float _settleDuration = 0.45f;
        [SerializeField] private Ease _lookEase = Ease.InOutSine;

        [Tooltip("Сколько случайных 'посторонних' предметов максимум разглядывает, прежде чем остановиться на выбранном — 0 отключает разглядывание совсем.")]
        [SerializeField] private int _maxGlances = 2;

        private Quaternion _baseRotation;
        private Vector3 _baseFaceForward;
        private Vector3 _breathingBasePosition;
        private Tween _lookTween;
        private Tween _breathingTween;

        private Transform LookTransform => _headTransform != null ? _headTransform : transform;
        private Transform LookOrigin => _faceForwardTransform != null ? _faceForwardTransform : LookTransform;

        private void Awake()
        {
            _baseRotation = LookTransform.rotation;
            _baseFaceForward = LookOrigin.forward;

            if (_breathingTransform == null)
                _breathingTransform = transform;
            _breathingBasePosition = _breathingTransform.localPosition;
            StartBreathing();

            if (_baseFaceForward.sqrMagnitude < 0.0001f)
                _baseFaceForward = Vector3.forward;
            else
                _baseFaceForward.Normalize();
        }

        /// <summary>
        /// Разыгрывает "раздумье": несколько беглых взглядов на случайные
        /// candidatePositions, затем задержка взгляда на chosenPosition.
        /// chosenPosition может совпадать с одной из candidatePositions —
        /// она просто не попадёт в число "посторонних" взглядов.
        /// </summary>
        public IEnumerator PlayConsidering(IReadOnlyList<Vector3> candidatePositions, Vector3 chosenPosition)
        {
            foreach (var target in PickRandomGlanceTargets(candidatePositions, chosenPosition))
            {
                yield return LookAt(target, _glanceDuration);
                yield return new WaitForSeconds(_glanceHoldDuration);
            }

            yield return LookAt(chosenPosition, _settleDuration);
        }

        /// <summary>Осматривает наперстки перед тем, как внешний AI выберет один из них.</summary>
        public IEnumerator PlayAtTargets(IReadOnlyList<Vector3> targetPositions)
        {
            if (targetPositions == null)
                yield break;

            foreach (var targetPosition in targetPositions)
            {
                yield return LookAt(targetPosition, _glanceDuration);
                yield return new WaitForSeconds(_glanceHoldDuration);
            }
        }

        /// <summary>Возвращает взгляд в исходное положение — вызвать, когда смотреть больше не на что (после применения всех предметов за ход).</summary>
        public void ResetLook(float duration = -1f)
        {
            var lookTransform = LookTransform;
            if (lookTransform == null) return;

            _lookTween?.Kill();
            _lookTween = lookTransform.DORotateQuaternion(_baseRotation, duration >= 0f ? duration : _settleDuration)
                .SetEase(_lookEase);
        }

        private IEnumerator LookAt(Vector3 worldPosition, float duration)
        {
            var lookTransform = LookTransform;
            if (lookTransform == null) yield break;

            var direction = worldPosition - LookOrigin.position;
            if (direction.sqrMagnitude < 0.0001f)
                yield break;

            direction.Normalize();
            // Совмещаем исходную линию взгляда с предметом напрямую. Так
            // голова получает и yaw, и pitch, но сохраняет свой базовый roll.
            Quaternion faceDelta = Quaternion.FromToRotation(_baseFaceForward, direction);
            Quaternion targetRotation = faceDelta * _baseRotation;

            _lookTween?.Kill();
            bool done = false;
            _lookTween = lookTransform.DORotateQuaternion(targetRotation, Mathf.Max(0.01f, duration))
                .SetEase(_lookEase)
                .OnComplete(() => done = true);

            while (!done)
                yield return null;
        }

        private void StartBreathing()
        {
            if (_breathingTransform == null || _breathingAmplitude <= 0f)
                return;

            float halfCycle = Mathf.Max(0.05f, _breathingCycleDuration * 0.5f);
            _breathingTween = _breathingTransform
                .DOLocalMoveY(_breathingBasePosition.y + _breathingAmplitude, halfCycle)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        private List<Vector3> PickRandomGlanceTargets(IReadOnlyList<Vector3> candidatePositions, Vector3 chosenPosition)
        {
            var pool = new List<Vector3>();
            if (candidatePositions != null)
            {
                foreach (var position in candidatePositions)
                {
                    if ((position - chosenPosition).sqrMagnitude > 0.0001f)
                        pool.Add(position);
                }
            }

            Shuffle(pool);
            int count = Mathf.Min(_maxGlances, pool.Count);
            return pool.GetRange(0, count);
        }

        private static void Shuffle(IList<Vector3> values)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }

        private void OnDrawGizmosSelected()
        {
            var lookOrigin = _faceForwardTransform != null ? _faceForwardTransform : _headTransform;
            if (lookOrigin == null)
                lookOrigin = transform;

            Gizmos.color = Color.cyan;
            Vector3 start = lookOrigin.position;
            Vector3 end = start + lookOrigin.forward * 0.75f;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawSphere(start, 0.025f);

            Vector3 arrowSide = Quaternion.AngleAxis(150f, Vector3.up) * lookOrigin.forward;
            Vector3 arrowSideOther = Quaternion.AngleAxis(-150f, Vector3.up) * lookOrigin.forward;
            Gizmos.DrawLine(end, end - arrowSide.normalized * 0.12f);
            Gizmos.DrawLine(end, end - arrowSideOther.normalized * 0.12f);
        }

        private void OnDisable()
        {
            _lookTween?.Kill();
            _breathingTween?.Kill();

            if (_breathingTransform != null)
                _breathingTransform.localPosition = _breathingBasePosition;
        }
    }
}