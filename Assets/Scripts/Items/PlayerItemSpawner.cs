using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using ShellGame.Core;
using ShellGame.Items;
using ShellGame.Meta;
using ShellGame.Run;
using UnityEngine;

namespace ShellGame.Items
{
    public sealed class PlayerItemSpawner : BaseItemSpawner
    {
        public override TurnSide OwnerSide => TurnSide.Player;

        [Header("Player Spawner Settings")]
        [Tooltip("Если true, спавнит предметы из постоянного инвентаря PlayerInventorySO")]
        [SerializeField] private bool _spawnFromPersistentInventory = true;

        protected override List<ItemDefinition> GetAvailableItemPool()
        {
            var runManager = ShellGame.Run.RunManager.Instance;
            if (runManager != null && runManager.CurrentRun != null && runManager.CurrentRun.PlayerInventory != null && _spawnFromPersistentInventory)
            {
                // Return empty pool - we'll restore from checkpoint instead of spawning new
                return new List<ItemDefinition>();
            }

            // Fallback: resolve from unlocks (for first run / testing)
            return ResolveAvailableItems();
        }

        private List<ItemDefinition> ResolveAvailableItems()
        {
            if (_unlockManager != null && _unlocksConfig != null)
            {
                var unlockedItems = new List<ItemDefinition>();
                foreach (var entry in _unlocksConfig.Entries)
                {
                    if (entry.Item != null && _unlockManager.IsUnlocked(entry.Item))
                    {
                        unlockedItems.Add(entry.Item);
                    }
                }
                return unlockedItems;
            }

            // Fallback: return all configured items
            return new List<ItemDefinition>();
        }

        public override IEnumerator SpawnItems()
        {
            // If using persistent inventory, restore from checkpoint instead of random spawn
            var runManager = ShellGame.Run.RunManager.Instance;
            if (runManager != null && runManager.CurrentRun != null && runManager.CurrentRun.PlayerInventory != null && _spawnFromPersistentInventory)
            {
                // Items will be restored via RestoreFromCheckpoint called by GameManager
                // Just wait for any existing items to finish animating
                yield return new WaitForSeconds(_spawnAnimationDuration);
                HasFinishedSpawning = true;
                yield break;
            }

            // Fallback to random spawn
            yield return base.SpawnItems();
        }

        public void RestoreFromCheckpoint(List<ItemStackCheckpointData> playerItems)
        {
            if (_hasSpawned) return;
            _hasSpawned = true;

            _spawnCounts.Clear();

            var points = GetPointsForSide(_itemCount);
            int pointIndex = 0;

            if (playerItems != null)
            {
                foreach (var stack in playerItems)
                {
                    var definition = ResolveItemByName(stack.ItemAssetName);
                    if (definition == null)
                    {
                        Debug.LogWarning($"[PlayerItemSpawner] Чекпоинт ссылается на неизвестный предмет '{stack.ItemAssetName}' — пропускаю.");
                        continue;
                    }

                    for (int i = 0; i < stack.Count; i++)
                    {
                        if (pointIndex >= points.Count)
                        {
                            Debug.LogWarning($"[PlayerItemSpawner] Не хватило точек спавна для восстановления всех предметов игрока.");
                            return;
                        }

                        SpawnSpecificItem(points[pointIndex], definition, _spawnCounts);
                        pointIndex++;
                    }
                }
            }

            HasFinishedSpawning = true;
        }

        private void SpawnSpecificItem(ItemSpawnPoint point, ItemDefinition definition, Dictionary<ItemDefinition, int> sideCounts)
        {
            if (definition == null || definition.WorldPrefab == null) return;

            var spawnPosition = point.SpawnPosition;
            var itemObject = Instantiate(definition.WorldPrefab, spawnPosition, point.Rotation, transform);
            if (itemObject == null) return;

            RotateVisualRandomly(itemObject.transform);
            ShellGame.Core.TableSurfacePlacement.PlaceObjectOnSurface(itemObject.transform, spawnPosition);

            var pickup = itemObject.GetComponent<ItemPickupView>();
            if (pickup == null)
                pickup = itemObject.AddComponent<ItemPickupView>();

            pickup.SetItem(definition);
            pickup.SetOwner(OwnerSide);
            pickup.Used += HandleItemUsed;
            _spawnedItems.Add(itemObject);

            sideCounts.TryGetValue(definition, out var count);
            sideCounts[definition] = count + 1;

            // Add to PlayerInventorySO
            var runManager = ShellGame.Run.RunManager.Instance;
            if (runManager != null && runManager.CurrentRun != null && runManager.CurrentRun.PlayerInventory != null)
            {
                runManager.CurrentRun.PlayerInventory.Add(definition, 1, OwnerSide);
            }

            // Без анимации "появления из ниоткуда" — это восстановление
        }

        /// <summary>
        /// Получить следующую свободную позицию слота для анимации покупки
        /// </summary>
        public Vector3 GetNextEmptySlotPosition()
        {
            var points = GetPointsForSide(_itemCount);
            var occupied = new HashSet<int>();

            foreach (var itemObj in _spawnedItems)
            {
                if (itemObj == null) continue;
                var pickup = itemObj.GetComponent<ItemPickupView>();
                if (pickup != null && pickup.Owner == OwnerSide)
                {
                    // Find which point this item is at
                    for (int i = 0; i < points.Count; i++)
                    {
                        if (Vector3.Distance(itemObj.transform.position, points[i].SpawnPosition) < 0.1f)
                        {
                            occupied.Add(i);
                            break;
                        }
                    }
                }
            }

            for (int i = 0; i < points.Count; i++)
            {
                if (!occupied.Contains(i))
                    return points[i].SpawnPosition;
            }

            // All slots occupied, return last position offset
            if (points.Count > 0)
                return points[points.Count - 1].SpawnPosition + Vector3.right * 0.5f;

            return transform.position;
        }

        public void RegisterSpawnedItem(ItemPickupView pickup)
        {
            if (!_spawnedItems.Contains(pickup.gameObject))
                _spawnedItems.Add(pickup.gameObject);
        }

        public void RemoveItem(ItemPickupView pickup)
        {
            if (pickup == null) return;
            pickup.Used -= HandleItemUsed;
            _spawnedItems.Remove(pickup.gameObject);
        }
    }
}