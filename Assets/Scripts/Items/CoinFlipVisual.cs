using System;
using System.Collections;
using DG.Tweening;
using FMODUnity;
using ShellGame.Audio;
using ShellGame.Core;
using UnityEngine;

namespace ShellGame.Items
{
    /// <summary>
    /// Визуал и анимация полёта монетки при использовании.
    /// Заранее знает исход (Орёл/Решка) и крутится ровно так, чтобы упасть нужной стороной.
    /// Камера следит за полётом и делает плавный лёгкий зум на результат без тряски и взрывов частиц.
    /// </summary>
    public sealed class CoinFlipVisual : MonoBehaviour
    {
        public static event Action<Transform, float> TossStarted;
        public static event Action<Transform> TossLanded;

        [Header("Trail")]
        [SerializeField] private TrailRenderer _trail;

        [Header("Углы сторон монеты")]
        [Tooltip("Ориентация монеты, когда выпал Орёл (лицом вверх)")]
        [SerializeField] private Vector3 _headsEuler = Vector3.zero;

        [Tooltip("Ориентация монеты, когда выпала Решка (рубашкой вверх)")]
        [SerializeField] private Vector3 _tailsEuler = new Vector3(180f, 0f, 0f);

        private IAudioService _audio;
    private bool _tossStarted;
    private bool _tossLanded;

        private void Awake()
        {
            if (_trail == null)
                _trail = GetComponentInChildren<TrailRenderer>();

            if (_trail != null)
                _trail.emitting = false;

            ServiceLocator.TryGet(out _audio);
        }

        /// <summary>
        /// Запуск анимации броска.
        /// </summary>
        public void Launch(
            Vector3 startPosition,
            Vector3 landingPosition,
            bool isHeads,
            float duration,
            float tossHeight,
            float zoomFovOffset,
            EventReference tossSound,
            EventReference landSound,
            Action onLanded)
        {
            StartCoroutine(TossRoutine(
                startPosition,
                landingPosition,
                isHeads,
                duration,
                tossHeight,
                zoomFovOffset,
                tossSound,
                landSound,
                onLanded));
        }

        private IEnumerator TossRoutine(
            Vector3 startPos,
            Vector3 landPos,
            bool isHeads,
            float duration,
            float height,
            float fovOffset,
            EventReference tossSound,
            EventReference landSound,
            Action onLanded)
        {
            transform.position = startPos;
            _tossStarted = true;
            TossStarted?.Invoke(transform, fovOffset);

            // Включаем трейл
            if (_trail != null)
            {
                _trail.Clear();
                _trail.emitting = true;
            }

            // Звук броска
            if (!tossSound.IsNull)
                _audio?.PlayOneShot(tossSound, startPos);

            // 1. Расчёт вращения:
            // Чтобы монетка эффектно кувыркалась в воздухе (например, 5 полных оборотов),
            // но при приземлении гарантированно легла нужной гранью:
            int fullSpins = UnityEngine.Random.Range(4, 7);
            float targetX = (isHeads ? _headsEuler.x : _tailsEuler.x) + (360f * fullSpins);
            float randomY = UnityEngine.Random.Range(-35f, 35f);
            Vector3 targetRotation = new Vector3(targetX, randomY, isHeads ? _headsEuler.z : _tailsEuler.z);

            // Твин дуги броска
            transform.DOJump(landPos, height, 1, duration).SetEase(Ease.OutQuad);
            transform.DORotate(targetRotation, duration, RotateMode.FastBeyond360).SetEase(Ease.OutQuad);

            // Плавное слежение камеры за монеткой во время её подъема
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Отключаем трейл
            if (_trail != null)
                _trail.emitting = false;

            // Звук приземления (звонкий стук монеты о стол)
            if (!landSound.IsNull)
                _audio?.PlayOneShot(landSound, landPos);

            // Маленький аккуратный отскок монетки при падении (микро-баунс)
            Sequence bounceSeq = DOTween.Sequence();
            bounceSeq.Append(transform.DOMoveY(landPos.y + 0.04f, 0.08f).SetEase(Ease.OutQuad));
            bounceSeq.Append(transform.DOMoveY(landPos.y, 0.06f).SetEase(Ease.InQuad));

            yield return bounceSeq.WaitForCompletion();
            NotifyLanded();

            // Даём игроку секунду рассмотреть результат броска
            yield return new WaitForSeconds(1.1f);

            // Применяем игровой результат
            onLanded?.Invoke();

            // Плавное исчезновение монетки
            transform.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InBack).OnComplete(() =>
            {
                Destroy(gameObject);
            });
        }

        private void OnDestroy()
        {
            NotifyLanded();
            transform.DOKill();
        }

        private void NotifyLanded()
        {
            if (!_tossStarted || _tossLanded)
                return;

            _tossLanded = true;
            TossLanded?.Invoke(transform);
        }
    }
}