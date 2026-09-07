using UnityEngine;

namespace ShellGame.Items
{
    /// <summary>
    /// Следующая УДАЧНАЯ атака использующей стороны наносит умноженный урон.
    /// Если следующий ход этой стороны окажется промахом — множитель не
    /// сгорает и ждёт следующего попадания.
    /// </summary>
    [CreateAssetMenu(fileName = "DoubleDamageItem", menuName = "ShellGame/Items/Double Damage Item")]
    public sealed class DoubleDamageItemDefinition : ItemDefinition
    {
        public int DamageMultiplier = 2;

        public override bool Apply(ItemEffectContext context)
        {
            if (context?.SetNextHitDamageMultiplier == null) return false;
            context.SetNextHitDamageMultiplier.Invoke(context.UserSide, DamageMultiplier);
            return true;
        }

        /// <summary>
        /// Ценно только когда враг уверен в своём следующем выборе — в
        /// качестве прокси берём долю отслеживаемых меток (чем выше, тем
        /// безопаснее "вложиться" в удвоение урона прямо сейчас).
        /// </summary>
        public override float EvaluateEnemyDesire(ItemEffectContext context)
        {
            if (!CanUse(context) || context.EnemyAI == null) return 0f;
            return context.EnemyAI.GetTrackedKnowledgeFraction();
        }

        public override string GetEnemyUseAnnouncement() => "Враг использовал предмет: следующий удачный удар усилен";
    }
}