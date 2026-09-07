// ItemDefinition.cs
using FMODUnity;
using ShellGame.Audio;
using UnityEngine;
using UnityEngine.Serialization;

namespace ShellGame.Items
{
    /// <summary>
    /// Базовое описание предмета. Конкретный эффект реализуется в Apply()
    /// наследником (хилка/двойной урон/монокль и т.п.). Для предметов
    /// противника (см. ГДД, раздел про расходуемые предметы AI) CanUse и
    /// Apply переиспользуются те же самые — разница только в UserSide
    /// внутри ItemEffectContext; ShouldUse/IgnoreChance для AI решаются
    /// отдельно в EnemyAIController.DecisionRoutine (ещё не подключено).
    /// </summary>
    public abstract class ItemDefinition : ScriptableObject
    {
        [Header("Общее")]
        public string DisplayName;
        [Header("Тексты")]
        [FormerlySerializedAs("Description")]
        [TextArea] public string TooltipDescription;
        [TextArea] public string UseDescription;
        public GameObject WorldPrefab;

        [Header("Ховер (подъём + увеличение)")]
        public float HoverLiftHeight = 0.08f;
        public float HoverScaleMultiplier = 1.15f;
        public float HoverTweenDuration = 0.15f;
        public EventReference HoverEnterSound;
        public EventReference HoverExitSound;

        [Header("Подсказка при долгом наведении")]
        [Tooltip("Сколько секунд нужно держать курсор на предмете, прежде чем появится тултип с TooltipDescription.")]
        public float TooltipHoverDelay = 0.6f;

        [Header("Звук использования")]
        public EventReference UseSound;

        [Header("Анимация использования (заготовка — см. ItemUseEvents)")]
        [Tooltip("Имя триггера/сигнала для аниматора игрока — сам аниматор пока не реализован, это только имя, которое он потом прочитает через ItemUseEvents.")]
        public string PlayerUseAnimationTrigger = "UseItem";
        [Tooltip("То же самое для аниматора противника.")]
        public string EnemyUseAnimationTrigger = "EnemyUseItem";

        /// <summary>Можно ли вообще применить предмет сейчас (например, хилка бесполезна на полном ХП).</summary>
        public virtual bool CanUse(ItemEffectContext context) => true;

        /// <summary>Применить эффект. Возвращает true, если эффект реально сработал (и предмет нужно списать из инвентаря).</summary>
        public abstract bool Apply(ItemEffectContext context);

        public virtual float EvaluateEnemyDesire(ItemEffectContext context) => 0f;

        public virtual string GetEnemyUseAnnouncement() => "Враг использовал предмет";

        /// <summary>
        /// Общая обратная связь сразу после успешного Apply(): звук +
        /// сигнал для будущего аниматора. Вызывается ОДИН раз вызывающим
        /// кодом (ItemSpawner) — сами наследники это не вызывают.
        /// </summary>
        public virtual void PlayUseFeedback(ItemEffectContext context, IAudioService audioService, Vector3 worldPosition)
        {
            if (audioService != null && !UseSound.IsNull)
                audioService.PlayOneShot(UseSound, worldPosition);

            ItemUseEvents.RaiseItemUsed(context.UserSide, this);
        }

        /// <summary>
        /// Текстовая обратная связь ИГРОКУ в UI сразу после использования.
        /// По умолчанию — обычное сообщение с описанием предмета.
        /// Переопределяется, если использование требует доп. шага от
        /// игрока (см. MonocleItemDefinition).
        /// </summary>
        public virtual void ShowPlayerUseFeedback(ItemUseMessageView messageView)
        {
            messageView?.Show(this);
        }
    }
}