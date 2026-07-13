using UnityEngine;

namespace ShellGame.Items
{
    /// <summary>
    /// Следующая УДАЧНАЯ атака использующей стороны наносит умноженный урон.
    /// Если следующий ход этой стороны окажется промахом — множитель не
    /// сгорает и ждёт следующего попадания (см. ГДД: "следующая успешная
    /// атака наносит двойной урон" — про промах ничего не сказано, значит
    /// эффект не тратится впустую).
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
    }
}
