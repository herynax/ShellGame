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

            return _fallbackAvailableItems ?? new List<ItemDefinition>();
        }

        public override IEnumerator SpawnItems()
        {
            if (_hasSpawned || !_itemsAvailable)
            {
                HasFinishedSpawning = true;
                yield break;
            }

            _hasSpawned = true;
            yield return null;

            var runManager = ShellGame.Run.RunManager.Instance;
            bool hasPersistent = _spawnFromPersistentInventory
                && runManager != null
                && runManager.CurrentRun != null
                && runManager.CurrentRun.PlayerInventory != null;

            // 1. Спавним накопленные предметы игрока из постоянного инвентаря рана
            if (hasPersistent)
            {
                var playerInv = runManager.CurrentRun.PlayerInventory;
                var persistentItems = new List<ItemDefinition>();

                foreach (var stack in playerInv.PlayerItems.ToList())
                {
                    if (stack.Item == null || stack.Count <= 0) continue;
                    for (int i = 0; i < stack.Count; i++)
                        persistentItems.Add(stack.Item);
                }

                if (persistentItems.Count == 0)
                {
                    HasFinishedSpawning = true;
                    yield break;
                }

                _spawnCounts.Clear();
                var points = GetPointsForSide(_itemCount);
                int countToSpawn = Mathf.Min(persistentItems.Count, points.Count);

                for (int i = 0; i < countToSpawn; i++)
                {
                    var definition = persistentItems[i];
                    var point = points[i];

                    if (SpawnPersistentItem(point, definition))
                    {
                        if (!_spawnSound.IsNull)
                            _audio?.PlayOneShot(_spawnSound, point.SpawnPosition);
                    }

                    if (_spawnDelay > 0f)
                        yield return new WaitForSeconds(_spawnDelay);
                }

                if (_spawnAnimationDuration > 0f)
                    yield return new WaitForSeconds(_spawnAnimationDuration);

                HasFinishedSpawning = true;
                yield break;
            }

            // 2. Фоллбэк: случайный спавн (для тестов в сценах без запущенного рана)
            yield return base.SpawnItems();
        }

        private bool SpawnPersistentItem(ItemSpawnPoint point, ItemDefinition definition)
        {
            if (point == null || definition == null || definition.WorldPrefab == null)
                return false;

            var spawnPosition = point.SpawnPosition;
            var itemObject = Instantiate(definition.WorldPrefab, spawnPosition, point.Rotation, GetItemsParent(point));
            if (itemObject == null) return false;

            RotateVisualRandomly(itemObject.transform);
            ShellGame.Core.TableSurfacePlacement.PlaceObjectOnSurface(itemObject.transform, spawnPosition);

            var pickup = itemObject.GetComponent<ItemPickupView>() ?? itemObject.AddComponent<ItemPickupView>();
            pickup.SetItem(definition);
            pickup.SetOwner(OwnerSide);
            pickup.Used += HandleItemUsed;
            _spawnedItems.Add(itemObject);

            _spawnCounts.TryGetValue(definition, out var currentCount);
            _spawnCounts[definition] = currentCount + 1;

            var baseScale = itemObject.transform.localScale;
            itemObject.transform.localScale = Vector3.zero;
            itemObject.transform.DOScale(baseScale, Mathf.Max(0f, _spawnAnimationDuration)).SetEase(_spawnEase);

            return true;
        }

        protected override void HandleItemUsed(ItemPickupView pickup)
        {
            if (pickup == null || pickup.Item == null) return;
            var item = pickup.Item;
            bool wasSpawned = _spawnedItems.Contains(pickup.gameObject);

            base.HandleItemUsed(pickup);

            // Если предмет был успешно использован (удалён со стола), списываем его из постоянного инвентаря рана
            if (wasSpawned && !_spawnedItems.Contains(pickup.gameObject))
            {
                var runManager = ShellGame.Run.RunManager.Instance;
                if (runManager != null && runManager.CurrentRun != null && runManager.CurrentRun.PlayerInventory != null)
                {
                    runManager.CurrentRun.PlayerInventory.Remove(item, 1, OwnerSide);
                }
            }
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
            var itemObject = Instantiate(definition.WorldPrefab, spawnPosition, point.Rotation, GetItemsParent(point));
            if (itemObject == null) return;

            RotateVisualRandomly(itemObject.transform);
            ShellGame.Core.TableSurfacePlacement.PlaceObjectOnSurface(itemObject.transform, spawnPosition);

            var pickup = itemObject.GetComponent<ItemPickupView>() ?? itemObject.AddComponent<ItemPickupView>();
            pickup.SetItem(definition);
            pickup.SetOwner(OwnerSide);
            pickup.Used += HandleItemUsed;
            _spawnedItems.Add(itemObject);

            sideCounts.TryGetValue(definition, out var count);
            sideCounts[definition] = count + 1;

            var runManager = ShellGame.Run.RunManager.Instance;
            if (runManager != null && runManager.CurrentRun != null && runManager.CurrentRun.PlayerInventory != null)
            {
                runManager.CurrentRun.PlayerInventory.Add(definition, 1, OwnerSide);
            }
        }

        public bool HasFreeSlot
        {
            get
            {
                var points = GetPointsForSide(_itemCount);
                return _spawnedItems.Count < points.Count;
            }
        }

        public List<ItemDefinition> GetUnlockedItemsPool()
        {
            return ResolveAvailableItems();
        }

        public ItemPickupView SpawnNewItem(ItemDefinition definition)
        {
            if (definition == null || definition.WorldPrefab == null) return null;

            var points = GetPointsForSide(_itemCount);
            var occupied = new HashSet<int>();

            foreach (var itemObj in _spawnedItems)
            {
                if (itemObj == null) continue;
                for (int i = 0; i < points.Count; i++)
                {
                    if (Vector3.Distance(itemObj.transform.position, points[i].SpawnPosition) < 0.15f)
                    {
                        occupied.Add(i);
                        break;
                    }
                }
            }

            ItemSpawnPoint targetPoint = null;
            for (int i = 0; i < points.Count; i++)
            {
                if (!occupied.Contains(i))
                {
                    targetPoint = points[i];
                    break;
                }
            }

            if (targetPoint == null)
                return null;

            var spawnPos = targetPoint.SpawnPosition;
            var itemObjSpawned = Instantiate(definition.WorldPrefab, spawnPos, targetPoint.Rotation, GetItemsParent(targetPoint));
            
            RotateVisualRandomly(itemObjSpawned.transform);
            ShellGame.Core.TableSurfacePlacement.PlaceObjectOnSurface(itemObjSpawned.transform, spawnPos);

            var pickup = itemObjSpawned.GetComponent<ItemPickupView>() ?? itemObjSpawned.AddComponent<ItemPickupView>();
            pickup.SetItem(definition);
            pickup.SetOwner(OwnerSide);
            pickup.Used += HandleItemUsed;
            _spawnedItems.Add(itemObjSpawned);

            var baseScale = itemObjSpawned.transform.localScale;
            itemObjSpawned.transform.localScale = Vector3.zero;
            itemObjSpawned.transform.DOScale(baseScale, _spawnAnimationDuration).SetEase(_spawnEase);

            var runManager = ShellGame.Run.RunManager.Instance;
            if (runManager != null && runManager.CurrentRun != null && runManager.CurrentRun.PlayerInventory != null)
            {
                runManager.CurrentRun.PlayerInventory.Add(definition, 1, OwnerSide);
            }

            return pickup;
        }

        public bool RemoveRandomItem(out ItemDefinition lostItem)
        {
            lostItem = null;
            var playerPickups = new List<ItemPickupView>();

            foreach (var itemObj in _spawnedItems)
            {
                if (itemObj == null) continue;
                var pickup = itemObj.GetComponent<ItemPickupView>();
                if (pickup != null && pickup.Owner == OwnerSide && pickup.Item != null)
                {
                    playerPickups.Add(pickup);
                }
            }

            if (playerPickups.Count == 0)
                return false;

            var targetPickup = playerPickups[UnityEngine.Random.Range(0, playerPickups.Count)];
            lostItem = targetPickup.Item;

            var objToDestroy = targetPickup.gameObject;
            _spawnedItems.Remove(objToDestroy);
            targetPickup.Used -= HandleItemUsed;

            objToDestroy.transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack).OnComplete(() =>
            {
                Destroy(objToDestroy);
            });

            var runManager = ShellGame.Run.RunManager.Instance;
            if (runManager != null && runManager.CurrentRun != null && runManager.CurrentRun.PlayerInventory != null)
            {
                runManager.CurrentRun.PlayerInventory.Remove(lostItem, 1, OwnerSide);
            }

            return true;
        }

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