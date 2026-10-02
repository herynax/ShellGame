using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using ShellGame.Core;
using ShellGame.Dialogue;
using ShellGame.Items;
using ShellGame.Run;
using UnityEngine;

namespace ShellGame.Items
{
    public sealed class ShopItemSpawner : BaseItemSpawner
    {
        public override TurnSide OwnerSide => TurnSide.Player;

        [Header("Shop Spawner Settings")]
        [SerializeField] private List<ItemSpawnPoint> _shopItemPoints = new List<ItemSpawnPoint>();
        [SerializeField] private List<ItemSpawnPoint> _playerItemPoints = new List<ItemSpawnPoint>();

        private ShopEncounterController _shopController;
        private List<ItemDefinition> _currentShopItems = new List<ItemDefinition>();

        public void Initialize(ShopEncounterController controller)
        {
            _shopController = controller;
        }

        public void SetShopItems(List<ItemDefinition> shopItems)
        {
            _currentShopItems = shopItems ?? new List<ItemDefinition>();
        }

        protected override List<ItemDefinition> GetAvailableItemPool()
        {
            // Shop spawner handles two types of items:
            // 1. Player's persistent items (from PlayerInventorySO)
            // 2. Shop items for sale (from _currentShopItems)
            // Both are handled in SpawnItems override
            return new List<ItemDefinition>();
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

            _spawnCounts.Clear();

            // 1. Spawn player's persistent items at player item points
            var runManager = ShellGame.Run.RunManager.Instance;
            if (runManager != null && runManager.CurrentRun != null && runManager.CurrentRun.PlayerInventory != null)
            {
                var playerInv = runManager.CurrentRun.PlayerInventory;
                var playerItems = playerInv.PlayerItems;

                var points = GetPlayerItemPoints();
                int pointIndex = 0;

                foreach (var stack in playerItems)
                {
                    if (stack.Item == null || stack.Count <= 0) continue;
                    if (pointIndex >= points.Count) break;

                    for (int i = 0; i < stack.Count; i++)
                    {
                        if (pointIndex >= points.Count) break;

                        SpawnSpecificItem(points[pointIndex], stack.Item, _spawnCounts, isShopItem: false);
                        pointIndex++;
                    }
                }
            }

            // 2. Spawn shop items for sale at shop item points
            var shopPoints = GetShopItemPoints();
            for (int i = 0; i < Mathf.Min(_currentShopItems.Count, shopPoints.Count); i++)
            {
                var item = _currentShopItems[i];
                if (item == null) continue;

                SpawnSpecificItem(shopPoints[i], item, _spawnCounts, isShopItem: true);

                if (_spawnDelay > 0f)
                    yield return new WaitForSeconds(_spawnDelay);
            }

            if (_spawnAnimationDuration > 0f)
                yield return new WaitForSeconds(_spawnAnimationDuration);

            HasFinishedSpawning = true;
        }

        private List<ItemSpawnPoint> GetPlayerItemPoints()
        {
            if (_playerItemPoints.Count > 0)
            {
                var points = new List<ItemSpawnPoint>(_playerItemPoints);
                points.Sort((left, right) => left.Index.CompareTo(right.Index));
                return points;
            }

            // Fallback to base spawn points for player
            return GetPointsForSide(_itemCount);
        }

        private List<ItemSpawnPoint> GetShopItemPoints()
        {
            if (_shopItemPoints.Count > 0)
            {
                var points = new List<ItemSpawnPoint>(_shopItemPoints);
                points.Sort((left, right) => left.Index.CompareTo(right.Index));
                return points;
            }

            return new List<ItemSpawnPoint>();
        }

        protected override bool SpawnItem(ItemSpawnPoint point, List<ItemDefinition> availableItems, Dictionary<ItemDefinition, int> sideCounts, out Vector3 spawnPosition)
        {
            // Not used - we use SpawnSpecificItem directly
            spawnPosition = Vector3.zero;
            return false;
        }

        private void SpawnSpecificItem(ItemSpawnPoint point, ItemDefinition definition, Dictionary<ItemDefinition, int> sideCounts, bool isShopItem)
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
            _spawnedItems.Add(itemObject);

            sideCounts.TryGetValue(definition, out var count);
            sideCounts[definition] = count + 1;

            // Add to PlayerInventorySO for player items
            if (!isShopItem)
            {
                var runManager = ShellGame.Run.RunManager.Instance;
                if (runManager != null && runManager.CurrentRun != null && runManager.CurrentRun.PlayerInventory != null)
                {
                    runManager.CurrentRun.PlayerInventory.Add(definition, 1, OwnerSide);
                }
            }
            else
            {
                // Add shop behavior for buyable items
                var shopBehavior = itemObject.AddComponent<ShopItemBehavior>();
                shopBehavior.Initialize(_shopController, definition);
            }

            var baseScale = itemObject.transform.localScale;
            itemObject.transform.localScale = Vector3.zero;
            itemObject.transform.DOScale(baseScale, Mathf.Max(0f, _spawnAnimationDuration)).SetEase(_spawnEase);
        }

        /// <summary>
        /// Анимация покупки: предмет перелетает от позиции магазина к следующей свободной позиции игрока
        /// </summary>
        public void AnimateItemPurchase(ItemPickupView pickup, Vector3 targetPosition)
        {
            if (pickup == null || _shopController == null) return;

            var runManager = ShellGame.Run.RunManager.Instance;
            if (runManager != null && runManager.CurrentRun != null && runManager.CurrentRun.PlayerInventory != null)
            {
                runManager.CurrentRun.PlayerInventory.Add(pickup.Item, 1, TurnSide.Player);
            }

            pickup.transform.DOKill();
            pickup.transform.DOMove(targetPosition, 0.5f).SetEase(Ease.OutBack).OnComplete(() =>
            {
                // Re-parent to player item hierarchy
                var playerPoints = GetPlayerItemPoints();
                var occupied = new HashSet<int>();

                foreach (var itemObj in _spawnedItems)
                {
                    if (itemObj == null) continue;
                    var p = itemObj.GetComponent<ItemPickupView>();
                    if (p != null && p.Owner == TurnSide.Player)
                    {
                        for (int i = 0; i < playerPoints.Count; i++)
                        {
                            if (Vector3.Distance(itemObj.transform.position, playerPoints[i].SpawnPosition) < 0.1f)
                            {
                                occupied.Add(i);
                                break;
                            }
                        }
                    }
                }

                int targetIndex = -1;
                for (int i = 0; i < playerPoints.Count; i++)
                {
                    if (!occupied.Contains(i))
                    {
                        targetIndex = i;
                        break;
                    }
                }

                if (targetIndex >= 0)
                {
                    pickup.transform.SetParent(transform);
                    // Item is now at player slot position
                }
            });
        }

        public void RemoveShopItem(ItemPickupView pickup)
        {
            if (pickup == null) return;
            pickup.Used -= HandleItemUsed;
            _spawnedItems.Remove(pickup.gameObject);
        }

        public void ClearShopItems()
        {
            foreach (var itemObj in new List<GameObject>(_spawnedItems))
            {
                if (itemObj == null) continue;
                var pickup = itemObj.GetComponent<ItemPickupView>();
                if (pickup != null && pickup.Item != null)
                {
                    var shopBehavior = itemObj.GetComponent<ShopItemBehavior>();
                    if (shopBehavior != null)
                    {
                        pickup.Used -= HandleItemUsed;
                        _spawnedItems.Remove(itemObj);
                        Destroy(itemObj);
                    }
                }
            }
        }

        public void ClearAll()
        {
            foreach (var item in _spawnedItems)
            {
                if (item == null) continue;
                item.transform.DOKill();
                Destroy(item);
            }
            _spawnedItems.Clear();
            _spawnCounts.Clear();
            _hasSpawned = false;
            HasFinishedSpawning = false;
        }
    }

    public sealed class ShopItemBehavior : MonoBehaviour
    {
        private ShopEncounterController _controller;
        private ItemDefinition _item;
        private ItemPickupView _pickupView;
        private Coroutine _tooltipRoutine;

        public ItemDefinition Item => _item;

        public void Initialize(ShopEncounterController controller, ItemDefinition item)
        {
            _controller = controller;
            _item = item;
            _pickupView = GetComponent<ItemPickupView>();

            if (_pickupView != null)
            {
                _pickupView.Used += OnUsed;
            }
        }

        private void OnUsed(ItemPickupView pickup)
        {
            if (!_controller.IsInSellMode)
            {
                _controller.TryBuyItem(_item);
            }
        }

        private void OnMouseEnter()
        {
            if (_pickupView != null && _pickupView.IsInteractive)
            {
                StartTooltipTimer();
            }
        }

        private void OnMouseExit()
        {
            StopTooltipTimer();
            ShopTooltipView.Instance?.Hide(this);
        }

        private void StartTooltipTimer()
        {
            StopTooltipTimer();
            if (_item == null) return;
            _tooltipRoutine = StartCoroutine(ShowTooltipAfterDelay());
        }

        private void StopTooltipTimer()
        {
            if (_tooltipRoutine == null) return;
            StopCoroutine(_tooltipRoutine);
            _tooltipRoutine = null;
        }

        private IEnumerator ShowTooltipAfterDelay()
        {
            yield return new WaitForSeconds(Mathf.Max(0f, _item.TooltipHoverDelay));
            ShopTooltipView.Instance?.Show(this, _item.BuyPrice);
        }

        private void OnDestroy()
        {
            if (_pickupView != null)
                _pickupView.Used -= OnUsed;
            StopTooltipTimer();
            ShopTooltipView.Instance?.Hide(this);
        }
    }
}