using System.Collections.Generic;
using System.Linq;
using ShellGame.Core;
using UnityEngine;

namespace ShellGame.Items
{
    [CreateAssetMenu(fileName = "PlayerInventory", menuName = "ShellGame/Items/Player Inventory")]
    public sealed class PlayerInventorySO : ScriptableObject, IPlayerInventory
    {
        [Header("Capacity")]
        [SerializeField] private int _maxPlayerSlots = 4;
        [SerializeField] private int _maxEnemySlots = 4;

        [Header("Runtime State")]
        [SerializeField] private List<ItemStack> _playerItems = new List<ItemStack>();
        [SerializeField] private List<ItemStack> _enemyItems = new List<ItemStack>();
        [SerializeField] private int _coins = 0;

        public int MaxPlayerSlots => _maxPlayerSlots;
        public int MaxEnemySlots => _maxEnemySlots;
        public int Coins { get => _coins; set => _coins = Mathf.Max(0, value); }
        public IReadOnlyList<ItemStack> PlayerItems => _playerItems;
        public IReadOnlyList<ItemStack> EnemyItems => _enemyItems;

        public int GetCount(ItemDefinition item, TurnSide side = TurnSide.Player)
        {
            var list = side == TurnSide.Player ? _playerItems : _enemyItems;
            var stack = list.Find(s => s.Item == item);
            return stack != null ? stack.Count : 0;
        }

        public bool Has(ItemDefinition item, TurnSide side = TurnSide.Player)
        {
            return GetCount(item, side) > 0;
        }

        public bool HasSpace(TurnSide side = TurnSide.Player)
        {
            var list = side == TurnSide.Player ? _playerItems : _enemyItems;
            int maxSlots = side == TurnSide.Player ? _maxPlayerSlots : _maxEnemySlots;
            int currentCount = 0;
            foreach (var stack in list)
                currentCount += stack.Count;
            return currentCount < maxSlots;
        }

        public void Add(ItemDefinition item, int count = 1, TurnSide side = TurnSide.Player)
        {
            if (item == null || count <= 0) return;

            var list = side == TurnSide.Player ? _playerItems : _enemyItems;
            var stack = list.Find(s => s.Item == item);
            if (stack != null)
            {
                stack.Count += count;
            }
            else
            {
                list.Add(new ItemStack(item, count));
            }
        }

        public bool Remove(ItemDefinition item, int count = 1, TurnSide side = TurnSide.Player)
        {
            if (item == null || count <= 0) return false;

            var list = side == TurnSide.Player ? _playerItems : _enemyItems;
            var stack = list.Find(s => s.Item == item);
            if (stack == null || stack.Count < count) return false;

            stack.Count -= count;
            if (stack.Count <= 0)
                list.Remove(stack);
            return true;
        }

        public void Clear(TurnSide side = TurnSide.Player)
        {
            if (side == TurnSide.Player)
                _playerItems.Clear();
            else
                _enemyItems.Clear();
        }

        public void ClearAll()
        {
            _playerItems.Clear();
            _enemyItems.Clear();
            _coins = 0;
        }

        public List<ShellGame.Meta.ItemStackCheckpointData> GetPlayerCheckpointData()
        {
            var result = new List<ShellGame.Meta.ItemStackCheckpointData>();
            foreach (var stack in _playerItems)
            {
                if (stack.Item != null && stack.Count > 0)
                    result.Add(new ShellGame.Meta.ItemStackCheckpointData { ItemAssetName = stack.Item.name, Count = stack.Count });
            }
            return result;
        }

        public List<ShellGame.Meta.ItemStackCheckpointData> GetEnemyCheckpointData()
        {
            var result = new List<ShellGame.Meta.ItemStackCheckpointData>();
            foreach (var stack in _enemyItems)
            {
                if (stack.Item != null && stack.Count > 0)
                    result.Add(new ShellGame.Meta.ItemStackCheckpointData { ItemAssetName = stack.Item.name, Count = stack.Count });
            }
            return result;
        }

        public void RestoreFromCheckpoint(List<ShellGame.Meta.ItemStackCheckpointData> playerData, List<ShellGame.Meta.ItemStackCheckpointData> enemyData, System.Func<string, ItemDefinition> resolver)
        {
            _playerItems.Clear();
            _enemyItems.Clear();

            if (playerData != null)
            {
                foreach (var data in playerData)
                {
                    var item = resolver(data.ItemAssetName);
                    if (item != null && data.Count > 0)
                        _playerItems.Add(new ItemStack(item, data.Count));
                }
            }

            if (enemyData != null)
            {
                foreach (var data in enemyData)
                {
                    var item = resolver(data.ItemAssetName);
                    if (item != null && data.Count > 0)
                        _enemyItems.Add(new ItemStack(item, data.Count));
                }
            }
        }

        private void OnValidate()
        {
            _maxPlayerSlots = Mathf.Max(1, _maxPlayerSlots);
            _maxEnemySlots = Mathf.Max(1, _maxEnemySlots);
        }
    }
}