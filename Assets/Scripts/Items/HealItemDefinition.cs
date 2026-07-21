using UnityEngine;

namespace ShellGame.Items
{
    /// <summary>Снижает текущую дозу использующей стороны (детокс).</summary>
    [CreateAssetMenu(fileName = "HealItem", menuName = "ShellGame/Items/Heal Item")]
    public sealed class HealItemDefinition : ItemDefinition
    {
        public int HealAmount = 2;

        public override bool CanUse(ItemEffectContext context)
        {
            if (context?.Health == null) return false;
            // Раньше: полезно, если ХП меньше максимума. Теперь доза
            // накапливается от нуля, поэтому полезно, если есть что снижать.
            return context.Health.GetHealth(context.UserSide) > 0;
        }

        public override bool Apply(ItemEffectContext context)
        {
            if (!CanUse(context)) return false;
            context.Health.Heal(context.UserSide, HealAmount);
            return true;
        }
    }
}
