using ShellGame.Core;
using UnityEngine;

namespace ShellGame.Items
{
    /// <summary>
    /// У игрока — "наркотический" эффект: замедляет общий темп игры
    /// (Time.timeScale) до ближайшего РЕАЛЬНОГО выбора наперстка.
    /// У противника — эффект другой по природе: замедлять НЕЧЕГО (у ИИ нет
    /// экрана), поэтому вместо этого снижает его собственный шанс потерять
    /// отслеживание метки на следующем перемешивании (см.
    /// EnemyAIController.ReduceTrackingLossNextShuffle) — "меньше плывёт",
    /// а не "видит мир медленнее".
    /// </summary>
    [CreateAssetMenu(fileName = "SlowShuffleItem", menuName = "ShellGame/Items/Slow Shuffle Item")]
    public sealed class SlowShuffleItemDefinition : ItemDefinition
    {
        [Min(1f)] public float GameSpeedSlowdownMultiplier = 2f;

        [Range(0f, 1f)]
        [Tooltip("Множитель на шанс потерять метку для противника (0.3 = -70% шанса потерять).")]
        public float EnemyTrackingLossMultiplier = 0.3f;

        public override bool CanUse(ItemEffectContext context)
        {
            if (context == null) return false;

            if (context.UserSide == TurnSide.Enemy)
            {
                return context.ReduceEnemyTrackingLossNextShuffle != null
                    && (context.CanReduceEnemyTrackingLossNextShuffle?.Invoke() ?? false)
                    && (context.CanUseEnemySlowItem?.Invoke() ?? false);
            }

            return context.CanSlowGamePace?.Invoke() ?? false;
        }

        public override bool Apply(ItemEffectContext context)
        {
            if (!CanUse(context)) return false;

            if (context.UserSide == TurnSide.Enemy)
            {
                context.ReduceEnemyTrackingLossNextShuffle.Invoke(EnemyTrackingLossMultiplier);
                context.StartEnemySlowItemCooldown?.Invoke();
                return true;
            }

            context.SlowGamePaceUntilNextChoice.Invoke(context.UserSide, GameSpeedSlowdownMultiplier);
            return true;
        }

        /// <summary>Врагу тем нужнее подстраховаться, чем больше сейчас есть что защищать (чем лучше он отслеживает метки).</summary>
        public override float EvaluateEnemyDesire(ItemEffectContext context)
        {
            if (!CanUse(context) || context.EnemyAI == null) return 0f;
            return context.EnemyAI.GetTrackedKnowledgeFraction() * 0.8f;
        }

        public override string GetEnemyUseAnnouncement() => "Враг использовал предмет: подстраховался перед следующим перемешиванием";
    }
}