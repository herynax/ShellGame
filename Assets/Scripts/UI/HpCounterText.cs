using System.Collections;
using ShellGame.Core;
using ShellGame.Health;
using TMPro;
using UnityEngine;

namespace ShellGame.UI
{
    /// <summary>
    /// ВРЕМЕННЫЙ debug-счётчик ХП текстом, вида "3/3". Показывает не саму
    /// внутреннюю дозу (которая растёт от 0 к max), а привычные "оставшиеся
    /// ХП" = max - dose, чтобы было интуитивно понятно на глаз во время
    /// тестирования. Слушает GameEvents.HealthChanged, поэтому обновляется
    /// сам при любом уколе/детоксе/сбросе дозы — никуда самому дёргать не
    /// нужно.
    ///
    /// HealthController не лежит на сцене заранее (спавнится в рантайме),
    /// поэтому ссылку на него в инспекторе не назначить — компонент сам
    /// ищет его через FindObjectOfType при включении, с задержкой в один
    /// кадр (на случай если HealthController.Initialize() вызывается в
    /// Start() чего-то, что его заспавнило, а не в Awake()). Дальше он
    /// уже не нужен — все обновления идут через событие GameEvents.HealthChanged.
    /// </summary>
    public sealed class HpCounterText : MonoBehaviour
    {
        [Header("Чьё ХП показывать")]
        public TurnSide side = TurnSide.Player;

        [Header("Куда выводить")]
        public TMP_Text hpText;

        private void OnEnable()
        {
            GameEvents.HealthChanged += HandleHealthChanged;
            StartCoroutine(RefreshFromExistingControllerNextFrame());
        }

        private void OnDisable()
        {
            GameEvents.HealthChanged -= HandleHealthChanged;
        }

        /// <summary>
        /// Ждём кадр, затем, если на сцене уже есть заспавненный и
        /// инициализированный HealthController, сразу подтягиваем текущее
        /// значение — иначе счётчик будет пустым/неверным до первого
        /// урона/лечения, которое сгенерирует событие.
        /// </summary>
        private IEnumerator RefreshFromExistingControllerNextFrame()
        {
            yield return null;

            var healthController = FindObjectOfType<HealthController>();
            if (healthController == null)
                yield break;

            Refresh(side, healthController.GetHealth(side), healthController.GetMaxHealth(side));
        }

        private void HandleHealthChanged(TurnSide changedSide, int currentDose, int max)
        {
            if (changedSide != side)
                return;

            Refresh(changedSide, currentDose, max);
        }

        private void Refresh(TurnSide _, int currentDose, int max)
        {
            if (hpText == null)
                return;

            int remaining = Mathf.Max(0, max - currentDose);
            hpText.text = $"{remaining}/{max}";
        }
    }
}