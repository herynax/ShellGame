using UnityEngine;

namespace ShellGame.Items
{
    /// <summary>Восстанавливает здоровье использующей стороне.</summary>
    [CreateAssetMenu(fileName = "HealItem", menuName = "ShellGame/Items/Heal Item")]
    public sealed class HealItemDefinition : ItemDefinition
    {
        public int HealAmount = 2;

        public override bool CanUse(ItemEffectContext context)
        {
            if (context?.Health == null) return false;
            return context.Health.GetHealth(context.UserSide) < context.Health.GetMaxHealth(context.UserSide);
        }

        public override bool Apply(ItemEffectContext context)
        {
            if (!CanUse(context)) return false;
            context.Health.Heal(context.UserSide, HealAmount);
            return true;
        }
    }
}
