// ShellGame.Items / ItemUseEvents.cs
using System;
using ShellGame.Core;

namespace ShellGame.Items
{
    /// <summary>
    /// Заготовка под анимации использования предметов. Сам этот класс не
    /// проигрывает никакой анимации — он просто сигнализирует "предмет
    /// использован такой-то стороной", а конкретный аниматор игрока/врага
    /// (пока не реализован) подписывается и сам решает, что играть, читая
    /// ItemDefinition.PlayerUseAnimationTrigger / EnemyUseAnimationTrigger.
    /// </summary>
    public static class ItemUseEvents
    {
        public static event Action<TurnSide, ItemDefinition> ItemUsed;

        public static void RaiseItemUsed(TurnSide side, ItemDefinition item)
        {
            ItemUsed?.Invoke(side, item);
        }
    }
}