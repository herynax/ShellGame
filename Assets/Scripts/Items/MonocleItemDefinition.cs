using ShellGame.Core;
using UnityEngine;

namespace ShellGame.Items
{
    /// <summary>
    /// Показывает, где находится метка.
    ///
    /// В руках игрока — физически приподнимает наперстки с меткой на
    /// RevealDuration (переиспользуется Shell.RevealMarker, тот же твин,
    /// что и при первом показе раунда).
    ///
    /// В руках противника — эффект другой по своей природе: показывать
    /// наперстки некому (ИИ не "смотрит глазами"), поэтому по ГДД предмет
    /// восстанавливает его Knowledge. Здесь это сделано как полный ресинк
    /// (EnemyAIController.ResyncKnowledge) — упрощение по сравнению с
    /// точечным "вернуть только потерянные метки", но результат для игрока
    /// неотличим (ИИ снова знает всё), и это самая простая корректная
    /// реализация на первой итерации.
    /// </summary>
    [CreateAssetMenu(fileName = "MonocleItem", menuName = "ShellGame/Items/Monocle Item")]
    public sealed class MonocleItemDefinition : ItemDefinition
    {
        [Tooltip("Длительность показа наперстков с меткой (только для игрока)")]
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
                return true;
            }

            bool didReveal = false;
            foreach (var shell in context.ActiveShells)
            {
                if (!shell.HasMarker) continue;
                shell.RevealMarker(RevealDuration);
                didReveal = true;
            }
            return didReveal;
        }
    }
}
