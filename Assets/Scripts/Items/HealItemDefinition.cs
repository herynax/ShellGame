using FMODUnity;
using ShellGame.Audio;
using UnityEngine;

namespace ShellGame.Items
{
    /// <summary>Снижает текущую дозу использующей стороны (детокс).</summary>
    [CreateAssetMenu(fileName = "HealItem", menuName = "ShellGame/Items/Heal Item")]
    public sealed class HealItemDefinition : ItemDefinition
    {
        public int HealAmount = 1;
        public EventReference PlayerHealSound;
        public EventReference EnemyHealSound;

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

        public override void PlayUseFeedback(ItemEffectContext context, IAudioService audioService, Vector3 worldPosition)
        {
            EventReference healSound = context.UserSide == ShellGame.Core.TurnSide.Player
                ? PlayerHealSound
                : EnemyHealSound;

            if (healSound.IsNull)
                healSound = UseSound;

            if (audioService != null && !healSound.IsNull)
                audioService.PlayOneShot(healSound, worldPosition);

            ItemUseEvents.RaiseItemUsed(context.UserSide, this);
        }

        public override float EvaluateEnemyDesire(ItemEffectContext context)
        {
            if (!CanUse(context) || context.Health == null) return 0f;
            return context.Health.GetDoseFraction(context.UserSide);
        }
    }
}
