using UnityEngine;

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
        [TextArea] public string Description;
        public GameObject WorldPrefab;

        [Header("Ховер (подъём + увеличение)")]
        public float HoverLiftHeight = 0.08f;
        public float HoverScaleMultiplier = 1.15f;
        public float HoverTweenDuration = 0.15f;

        /// <summary>Можно ли вообще применить предмет сейчас (например, хилка бесполезна на полном ХП).</summary>
        public virtual bool CanUse(ItemEffectContext context) => true;

        /// <summary>Применить эффект. Возвращает true, если эффект реально сработал (и предмет нужно списать из инвентаря).</summary>
        public abstract bool Apply(ItemEffectContext context);
    }
}
