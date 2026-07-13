using System.Collections.Generic;
using ShellGame.Core;

namespace ShellGame.Items
{
    /// <summary>
    /// Простой инвентарь предметов одной стороны (ScriptableObject-определение
    /// → количество). Первая, минимальная версия: без ограничения слотов и
    /// без сохранения между уровнями — эти правила добавятся вместе с
    /// экономикой/магазином отдельной итерацией.
    /// </summary>
    public sealed class ItemInventory
    {
        public readonly TurnSide Owner;
        private readonly Dictionary<ItemDefinition, int> _counts = new Dictionary<ItemDefinition, int>();

        public ItemInventory(TurnSide owner)
        {
            Owner = owner;
        }

        public void Add(ItemDefinition item, int count = 1)
        {
            _counts.TryGetValue(item, out var current);
            _counts[item] = current + count;
        }

        public int GetCount(ItemDefinition item) => _counts.TryGetValue(item, out var c) ? c : 0;

        public bool Has(ItemDefinition item) => GetCount(item) > 0;

        /// <summary>Пробует применить предмет через его Apply(context). Списывает из инвентаря только при реальном успехе.</summary>
        public bool TryUse(ItemDefinition item, ItemEffectContext context)
        {
            if (!Has(item) || !item.CanUse(context))
                return false;

            bool applied = item.Apply(context);
            if (applied)
            {
                var remaining = GetCount(item) - 1;
                if (remaining <= 0)
                    _counts.Remove(item);
                else
                    _counts[item] = remaining;
            }

            return applied;
        }

        public IReadOnlyDictionary<ItemDefinition, int> Snapshot => _counts;
    }
}
