using System.Collections;
using ShellGame.Core;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine; // Если у тебя старая синемашина, оставь просто: using Cinemachine;

namespace ShellGame.Feedback
{
    public class PlayerDamageFeedback : DamageFeedbackBase
    {
        protected override TurnSide WatchedSide => TurnSide.Player;

        [Header("Настройки камеры (Cinemachine)")]
        [Tooltip("Источник импульса, висящий на этом же объекте или камере")]
        [SerializeField] private CinemachineImpulseSource _impulseSource;
        [Tooltip("Сила тряски камеры")]
        [SerializeField] private float _impulseForce = 1f;

        [Header("Настройки 'Экрана боли'")]
        [Tooltip("UI Image красного цвета, растянутый на весь экран")]
        [SerializeField] private Image _bloodOverlay;
        [Tooltip("До какого значения альфы падает резко (0.75 = 75%)")]
        [SerializeField] private float _dropAlphaTo = 0.75f;
        [Tooltip("За сколько секунд альфа падает с 1.0 до 0.75 (очень быстро)")]
        [SerializeField] private float _dropDuration = 0.05f;
        [Tooltip("За сколько секунд плавно исчезает остаток (до 0)")]
        [SerializeField] private float _fadeDuration = 1.5f;

        private Coroutine _overlayCoroutine;

        protected override void Awake()
        {
            base.Awake();

            // Прячем экран при старте на всякий случай
            if (_bloodOverlay != null)
            {
                SetOverlayAlpha(0f);
            }
        }

        protected override void PlayFeedback(int amount, int currentHealth, int maxHealth, bool died)
        {
            // Лог ХП
            Debug.Log($"<color=red>Игрок получил урон!</color> Осталось ХП: {currentHealth} / Максимум: {maxHealth}");

            // 1. Тряска камеры (Случайная)
            if (_impulseSource != null)
            {
                // Чтобы тряска каждый раз была разной, генерируем случайный вектор направления
                Vector3 randomDirection = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f).normalized;

                // Передаем вектор и умножаем на силу. GenerateImpulse(velocity) создаст случайный рывок.
                _impulseSource.GenerateImpulse(randomDirection * _impulseForce);
            }

            // 2. Эффект экрана боли
            if (_bloodOverlay != null)
            {
                if (_overlayCoroutine != null) StopCoroutine(_overlayCoroutine);
                _overlayCoroutine = StartCoroutine(BloodOverlayRoutine());
            }
        }

        private IEnumerator BloodOverlayRoutine()
        {
            // МГНОВЕННО ставим альфу на 100% (1.0)
            SetOverlayAlpha(1f);

            // ЭТАП 1: Резкое падение до 75%
            float elapsed = 0f;
            while (elapsed < _dropDuration)
            {
                elapsed += Time.deltaTime;
                float currentAlpha = Mathf.Lerp(1f, _dropAlphaTo, elapsed / _dropDuration);
                SetOverlayAlpha(currentAlpha);
                yield return null;
            }

            SetOverlayAlpha(_dropAlphaTo); // Точно фиксируем 75%

            // ЭТАП 2: Плавное затухание в 0
            elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;
                float currentAlpha = Mathf.Lerp(_dropAlphaTo, 0f, elapsed / _fadeDuration);
                SetOverlayAlpha(currentAlpha);
                yield return null;
            }

            // Убеждаемся, что в конце полностью прозрачно
            SetOverlayAlpha(0f);
        }

        private void SetOverlayAlpha(float alpha)
        {
            Color c = _bloodOverlay.color;
            c.a = alpha;
            _bloodOverlay.color = c;
        }
    }
}