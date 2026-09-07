using UnityEngine;

namespace ShellGame.Items
{
    /// <summary>
    /// "Наручники". Даёт использующей стороне ещё один ход подряд — со
    /// стороны это выглядит как "противник пропускает ход". Изначально был
    /// доступен только игроку (имя класса — историческое, сохраняю его,
    /// чтобы не сломать ссылки в уже созданных .asset-предметах), но
    /// логика теперь общая для обеих сторон через ItemEffectContext.UserSide —
    /// врагу этот предмет особенно ценен на критически низком HP, чтобы не
    /// дать игроку сходить.
    /// </summary>
    [CreateAssetMenu(fileName = "SkipEnemyTurnItem", menuName = "ShellGame/Items/Skip Enemy Turn Item")]
    public sealed class SkipEnemyTurnItemDefinition : ItemDefinition
    {
        public override bool CanUse(ItemEffectContext context)
        {
            return context?.CanRequestExtraTurn?.Invoke() ?? false;
        }

        public override bool Apply(ItemEffectContext context)
        {
            if (!CanUse(context) || context.RequestExtraTurn == null)
                return false;

            context.RequestExtraTurn.Invoke();
            return true;
        }

        /// <summary>
        /// Резко возрастает при критически низком HP — "продержаться ещё
        /// один цикл" тем ценнее, чем ближе доза к максимуму. Специально
        /// растёт быстрее, чем у обычной хилки, — на грани смерти это
        /// приоритетнее лечения (лечение можно докрутить и следующим ходом).
        /// </summary>
        public override float EvaluateEnemyDesire(ItemEffectContext context)
        {
            if (!CanUse(context) || context.Health == null) return 0f;
            float doseFraction = context.Health.GetDoseFraction(context.UserSide);
            return Mathf.Clamp01(doseFraction * 1.3f);
        }

        public override string GetEnemyUseAnnouncement() => "Враг использовал предмет: приковал вас наручниками (вы пропускаете ход)";
    }
}