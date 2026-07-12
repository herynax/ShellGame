using System.Collections;
using ShellGame.Core;
using UnityEngine;

namespace ShellGame.Feedback
{
    public class EnemyDamageFeedback : DamageFeedbackBase
    {
        protected override TurnSide WatchedSide => TurnSide.Enemy;

        [Header("Настройки тряски модельки")]
        [Tooltip("Трансформ модельки врага, которую будем трясти")]
        [SerializeField] private Transform _enemyModel;
        [SerializeField] private float _shakeDuration = 0.3f;
        [SerializeField] private float _shakeIntensity = 0.1f;

        private Vector3 _originalPosition;
        private Coroutine _shakeCoroutine;

        protected override void Awake()
        {
            base.Awake();
            if (_enemyModel != null)
            {
                // Запоминаем изначальную позицию модельки
                _originalPosition = _enemyModel.localPosition;
            }
        }

        protected override void PlayFeedback(int amount, int currentHealth, int maxHealth, bool died)
        {
            // Лог по твоему запросу (сколько ХП осталось и сколько максимум)
            Debug.Log($"<color=orange>Враг получил урон!</color> Осталось ХП: {currentHealth} / Максимум: {maxHealth}");

            if (_enemyModel != null)
            {
                // Если модель уже трясется от предыдущего удара - останавливаем
                if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);

                _shakeCoroutine = StartCoroutine(ShakeRoutine());
            }
        }

        private IEnumerator ShakeRoutine()
        {
            float elapsed = 0f;

            while (elapsed < _shakeDuration)
            {
                // Генерируем случайное смещение
                Vector3 randomOffset = Random.insideUnitSphere * _shakeIntensity;
                // Применяем смещение относительно изначальной позиции
                _enemyModel.localPosition = _originalPosition + randomOffset;

                elapsed += Time.deltaTime;
                yield return null; // Ждем следующий кадр
            }

            // Обязательно возвращаем модельку точно на место
            _enemyModel.localPosition = _originalPosition;
        }
    }
}