using ShellGame.Core;
using UnityEngine;

namespace ShellGame.Items
{
    /// <summary>
    /// Показывает, где находится метка.
    ///
    /// В руках ИГРОКА — не поднимает все наперстки сразу. Вместо этого
    /// запускает ShellPeekGate (через ItemEffectContext.BeginShellPeek):
    /// следующий клик по ЛЮБОМУ наперстку поднимает именно его на
    /// RevealDuration и опускает обратно (Shell.RevealMarker) — без того,
    /// чтобы этот клик засчитался как финальный выбор раунда. Игрок видит,
    /// есть ли под ним метка, и только СЛЕДУЮЩИМ кликом (уже обычным
    /// Select()) делает реальный выбор — в т.ч. может выбрать тот же
    /// наперсток, что и подглядывал.
    ///
    /// В руках ПРОТИВНИКА — по ГДД предмет восстанавливает его Knowledge
    /// (ResyncKnowledge). Но чтобы это не выглядело как "мгновенная атака
    /// без раздумий", враг ещё и визуально поднимает ОДИН случайный
    /// наперсток (чисто косметически — то, что там окажется, никак не
    /// влияет на дальнейший выбор), и только ПОСЛЕ того, как эта анимация
    /// полностью доиграет (см. ItemEffectContext.ConsumedExtraDelay,
    /// которую читает ItemSpawner/GameManager), враг переходит к обычному
    /// MakeDecisionAndAttack — то есть к реальному выбору, независимо от
    /// того, была метка под подглядываемым наперстком или нет.
    /// </summary>
    [CreateAssetMenu(fileName = "MonocleItem", menuName = "ShellGame/Items/Monocle Item")]
    public sealed class MonocleItemDefinition : ItemDefinition
    {
        [Tooltip("Насколько долго держится подглядываемый наперсток приподнятым")]
        public float RevealDuration = 0.8f;

        public override bool CanUse(ItemEffectContext context)
        {
            return context?.ActiveShells != null && context.ActiveShells.Count > 0;
        }

        public override bool Apply(ItemEffectContext context)
        {
            if (!CanUse(context)) return false;

            if (context.UserSide == TurnSide.Enemy)
            {
                if (context.EnemyAI == null) return false;

                context.EnemyAI.ResyncKnowledge(context.ActiveShells);

                var peekedShell = context.ActiveShells[Random.Range(0, context.ActiveShells.Count)];
                peekedShell.RevealMarker(RevealDuration);
                context.ConsumedExtraDelay = context.ResolveShellRevealDuration != null
                    ? context.ResolveShellRevealDuration(RevealDuration)
                    : RevealDuration;

                return true;
            }

            if (context.BeginShellPeek == null) return false;
            context.BeginShellPeek(RevealDuration, null);
            return true;
        }

        public override void ShowPlayerUseFeedback(ItemUseMessageView messageView)
        {
            messageView?.ShowPersistent("Выберите наперсток, чтобы его проверить");
        }

        public override float EvaluateEnemyDesire(ItemEffectContext context)
        {
            if (!CanUse(context) || context.EnemyAI == null) return 0f;
            return 1f - context.EnemyAI.GetTrackedKnowledgeFraction();
        }

        public override string GetEnemyUseAnnouncement() => "Враг использовал предмет: Монокль (проверяет наперстки)";
    }
}