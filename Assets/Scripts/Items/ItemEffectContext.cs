using System;
using System.Collections.Generic;
using ShellGame.AI;
using ShellGame.Core;
using ShellGame.Health;
using ShellGame.Shells;

namespace ShellGame.Items
{
    /// <summary>
    /// Всё, что может понадобиться предмету, чтобы применить свой эффект.
    /// Собирается вызывающим кодом (сейчас — GameManager.CreateItemContext)
    /// и передаётся в ItemDefinition.Apply/CanUse — сам предмет не знает
    /// про GameManager напрямую.
    /// </summary>
    public sealed class ItemEffectContext
    {
        /// <summary>Кто использует предмет — на эту сторону и распространяется эффект (хилка/двойной урон/монокль).</summary>
        public TurnSide UserSide;

        public HealthController Health;

        /// <summary>Наперстки текущего раунда — нужны Монокли, чтобы найти помеченные.</summary>
        public IReadOnlyList<Shell> ActiveShells;

        /// <summary>Может быть null, если предмет использует игрок и AI ни при чём.</summary>
        public EnemyAIController EnemyAI;

        /// <summary>Делегат в GameManager — предмет "Двойной урон" ставит множитель на следующий удачный удар этой стороны.</summary>
        public Action<TurnSide, int> SetNextHitDamageMultiplier;
    }
}
