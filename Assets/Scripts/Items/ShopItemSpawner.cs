using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using ShellGame.Core;
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

        private readonly Dictionary<int, GameObject> _playerSlots = new Dictionary<int, GameObject>();

        public bool HasFreePlayerSlot => TryGetFreePlayerSlot(out _, out _);

        public void Initialize(ShopEncounterController controller)
        {
            _shopController = controller;
        }

        public void SetShopItems(List<ItemDefinition> shopItems)
        {
            _currentShopItems = shopItems ?? new List<ItemDefinition>();
        }

        protected override List<ItemDefinition> GetAvailableItemPool() => new List<ItemDefinition>();

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
            _playerSlots.Clear();

            var persistent = new List<ItemDefinition>();
            var runManager = ShellGame.Run.RunManager.Instance;
            if (runManager != null && runManager.CurrentRun != null && runManager.CurrentRun.PlayerInventory != null)
            {
                foreach (var stack in runManager.CurrentRun.PlayerInventory.PlayerItems.ToList())
                {
                    if (stack.Item == null || stack.Count <= 0) continue;
                    for (int i = 0; i < stack.Count; i++)
                        persistent.Add(stack.Item);
                }
            }

            foreach (var definition in persistent)
            {
                if (!HasFreePlayerSlot) break;
                SpawnPlayerItem(definition);
            }

            var shopPoints = GetShopItemPoints();
            int shopCount = Mathf.Min(_currentShopItems.Count, shopPoints.Count);
            for (int i = 0; i < shopCount; i++)
            {
                var item = _currentShopItems[i];
                if (item == null) continue;

                CreatePickup(shopPoints[i], item, isForSale: true);

                if (_spawnDelay > 0f)
                    yield return new WaitForSeconds(_spawnDelay);
            }

            if (_spawnAnimationDuration > 0f)
                yield return new WaitForSeconds(_spawnAnimationDuration);

            HasFinishedSpawning = true;
        }

        public ItemPickupView SpawnPlayerItem(ItemDefinition definition)
        {
            if (definition == null || definition.WorldPrefab == null) return null;
            if (!TryGetFreePlayerSlot(out int slotIndex, out var point)) return null;

            var pickup = CreatePickup(point, definition, isForSale: false);
            if (pickup == null) return null;

            _playerSlots[slotIndex] = pickup.gameObject;
            return pickup;
        }

        public List<ItemPickupView> GetPlayerPickups()
        {
            var result = new List<ItemPickupView>();
            foreach (var itemObject in _spawnedItems)
            {
                if (itemObject == null) continue;
                var pickup = itemObject.GetComponent<ItemPickupView>();
                if (pickup != null && pickup.Owner == OwnerSide && !pickup.IsSellExempt)
                    result.Add(pickup);
            }
            return result;
        }

        private ItemPickupView CreatePickup(ItemSpawnPoint point, ItemDefinition definition, bool isForSale)
        {
            if (point == null || definition == null || definition.WorldPrefab == null) return null;

            var spawnPosition = point.SpawnPosition;
            var itemObject = Instantiate(definition.WorldPrefab, spawnPosition, point.Rotation, transform);
            if (itemObject == null) return null;

            RotateVisualRandomly(itemObject.transform);
            ShellGame.Core.TableSurfacePlacement.PlaceObjectOnSurface(itemObject.transform, spawnPosition);

            var pickup = itemObject.GetComponent<ItemPickupView>() ?? itemObject.AddComponent<ItemPickupView>();
            pickup.SetItem(definition);
            pickup.SetOwner(OwnerSide);
            pickup.SetAlwaysInteractive(true);
            pickup.SetSellExempt(isForSale);
            _spawnedItems.Add(itemObject);

            _spawnCounts.TryGetValue(definition, out var count);
            _spawnCounts[definition] = count + 1;

            if (isForSale)
            {
                var shopBehavior = itemObject.AddComponent<ShopItemBehavior>();
                shopBehavior.Initialize(_shopController, definition);
            }

            var baseScale = itemObject.transform.localScale;
            itemObject.transform.localScale = Vector3.zero;
            itemObject.transform.DOScale(baseScale, Mathf.Max(0f, _spawnAnimationDuration)).SetEase(_spawnEase);

            return pickup;
        }

        private bool TryGetFreePlayerSlot(out int index, out ItemSpawnPoint point)
        {
            var points = GetPlayerItemPoints();
            for (int i = 0; i < points.Count; i++)
            {
                if (points[i] == null) continue;
                if (_playerSlots.TryGetValue(i, out var occupant) && occupant != null) continue;

                index = i;
                point = points[i];
                return true;
            }

            index = -1;
            point = null;
            return false;
        }

        private void FreePlayerSlotOf(GameObject itemObject)
        {
            int foundKey = -1;
            foreach (var pair in _playerSlots)
            {
                if (pair.Value == itemObject)
                {
                    foundKey = pair.Key;
                    break;
                }
            }

            if (foundKey >= 0)
                _playerSlots.Remove(foundKey);
        }

        private List<ItemSpawnPoint> GetPlayerItemPoints()
        {
            if (_playerItemPoints != null && _playerItemPoints.Count > 0)
            {
                var points = new List<ItemSpawnPoint>(_playerItemPoints);
                points.RemoveAll(p => p == null);
                points.Sort((left, right) => left.Index.CompareTo(right.Index));
                return points;
            }

            var fallback = GetPointsForSide(_itemCount);
            if (fallback != null)
                fallback.RemoveAll(p => p == null);
            return fallback ?? new List<ItemSpawnPoint>();
        }

        private List<ItemSpawnPoint> GetShopItemPoints()
        {
            if (_shopItemPoints != null && _shopItemPoints.Count > 0)
            {
                var points = new List<ItemSpawnPoint>(_shopItemPoints);
                points.RemoveAll(p => p == null);
                points.Sort((left, right) => left.Index.CompareTo(right.Index));
                return points;
            }

            return new List<ItemSpawnPoint>();
        }

        protected override bool SpawnItem(ItemSpawnPoint point, List<ItemDefinition> availableItems, Dictionary<ItemDefinition, int> sideCounts, out Vector3 spawnPosition)
        {
            spawnPosition = Vector3.zero;
            return false;
        }

        public bool AnimateItemPurchase(ItemPickupView pickup)
        {
            if (pickup == null) return false;
            if (!TryGetFreePlayerSlot(out int slotIndex, out var point)) return false;

            var shopBehavior = pickup.GetComponent<ShopItemBehavior>();
            if (shopBehavior != null)
                shopBehavior.Detach();

            pickup.SetSellExempt(false);

            var itemObject = pickup.gameObject;
            _playerSlots[slotIndex] = itemObject;

            var targetPos = point.SpawnPosition;
            var targetRot = point.Rotation;

            itemObject.transform.DOKill();

            Sequence seq = DOTween.Sequence();
            seq.Join(itemObject.transform.DOJump(targetPos, jumpPower: 0.35f, numJumps: 1, duration: 0.55f).SetEase(Ease.OutQuad));
            seq.Join(itemObject.transform.DORotateQuaternion(targetRot, 0.55f).SetEase(Ease.OutQuad));
            seq.OnComplete(() =>
            {
                if (itemObject != null)
                {
                    ShellGame.Core.TableSurfacePlacement.PlaceObjectOnSurface(itemObject.transform, targetPos);
                    itemObject.transform.DOPunchScale(new Vector3(0.1f, -0.08f, 0.1f), 0.22f, vibrato: 5);
                }
            });

            return true;
        }

        public void SellPickup(ItemPickupView pickup, Vector3 merchantPos = default)
        {
            if (pickup == null) return;

            var itemObject = pickup.gameObject;
            _spawnedItems.Remove(itemObject);
            FreePlayerSlotOf(itemObject);

            pickup.SetSellExempt(true);
            itemObject.transform.DOKill();

            if (merchantPos == default)
                merchantPos = itemObject.transform.position + Vector3.up * 1f;

            Sequence seq = DOTween.Sequence();
            seq.Join(itemObject.transform.DOJump(merchantPos, jumpPower: 0.45f, numJumps: 1, duration: 0.48f).SetEase(Ease.InQuad));
            seq.Join(itemObject.transform.DOScale(Vector3.zero, 0.48f).SetEase(Ease.InBack));
            seq.Join(itemObject.transform.DORotate(new Vector3(360f, 180f, 0f), 0.48f, RotateMode.FastBeyond360));
            seq.OnComplete(() =>
            {
                if (itemObject != null)
                    Destroy(itemObject);
            });
        }

        public void RemoveShopItem(ItemPickupView pickup)
        {
            if (pickup == null) return;
            _spawnedItems.Remove(pickup.gameObject);
        }

        public void ClearShopItems()
        {
            foreach (var itemObject in new List<GameObject>(_spawnedItems))
            {
                if (itemObject == null) continue;

                var pickup = itemObject.GetComponent<ItemPickupView>();
                if (pickup != null && pickup.IsSellExempt)
                {
                    itemObject.transform.DOKill();
                    _spawnedItems.Remove(itemObject);
                    Destroy(itemObject);
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
            _playerSlots.Clear();
            _hasSpawned = false;
            HasFinishedSpawning = false;
        }
    }

    /// <summary>
    /// Поведение товара на прилавке: клик = покупка, ховер = тултип с ценой.
    /// </summary>
    public sealed class ShopItemBehavior : MonoBehaviour
    {
        private ShopEncounterController _controller;
        private ItemDefinition _item;
        private ItemPickupView _pickupView;
        private Coroutine _tooltipRoutine;
        private bool _wasHovered;

        public ItemDefinition Item => _item;

        public void Initialize(ShopEncounterController controller, ItemDefinition item)
        {
            _controller = controller;
            _item = item;
            _pickupView = GetComponent<ItemPickupView>();

            if (_pickupView != null)
                _pickupView.Used += OnUsed;
        }

        public void Detach()
        {
            Destroy(this);
        }

        private void Update()
        {
            bool hovered = _pickupView != null && _pickupView.IsHovered;

            if (hovered && !_wasHovered)
                StartTooltipTimer();
            else if (!hovered && _wasHovered)
            {
                StopTooltipTimer();
                ShopTooltipView.Instance?.Hide(this);
            }

            _wasHovered = hovered;
        }

        private void OnUsed(ItemPickupView pickup)
        {
            if (_controller == null || _controller.IsInSellMode) return;
            _controller.TryBuyItem(_item, _pickupView);
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