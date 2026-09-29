using System.Collections.Generic;
using ShellGame.Core;

namespace ShellGame.Items
{
    public interface IPlayerInventory
    {
        int MaxPlayerSlots { get; }
        int MaxEnemySlots { get; }
        int Coins { get; set; }
        IReadOnlyList<ItemStack> PlayerItems { get; }
        IReadOnlyList<ItemStack> EnemyItems { get; }
        int GetCount(ItemDefinition item, TurnSide side = TurnSide.Player);
        bool Has(ItemDefinition item, TurnSide side = TurnSide.Player);
        bool HasSpace(TurnSide side = TurnSide.Player);
        void Add(ItemDefinition item, int count = 1, TurnSide side = TurnSide.Player);
        bool Remove(ItemDefinition item, int count = 1, TurnSide side = TurnSide.Player);
        void Clear(TurnSide side = TurnSide.Player);
    }

    [System.Serializable]
    public class ItemStack
    {
        public ItemDefinition Item;
        public int Count;

        public ItemStack(ItemDefinition item, int count)
        {
            Item = item;
            Count = count;
        }
    }
}