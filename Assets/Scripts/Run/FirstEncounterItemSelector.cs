using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using ShellGame.Core;
using ShellGame.Items;
using UnityEngine;

namespace ShellGame.Run
{
    public sealed class FirstEncounterItemSelector : MonoBehaviour
    {
        [SerializeField] private FirstEncounterConfig _config;
        [SerializeField] private ItemSpawner _itemSpawner;
        [SerializeField] private EncounterHost _encounterHost;

        private List<GameObject> _spawnedItems = new List<GameObject>();
        private List<ItemDefinition> _selectedItems = new List<ItemDefinition>();
        private int _itemsPicked = 0;
        private bool _isActive = false;

        public bool IsActive => _isActive;
        public System.Action OnSelectionComplete;

        private void Awake()
        {
            if (_itemSpawner == null)
                _itemSpawner = FindFirstObjectByType<ItemSpawner>();
            if (_encounterHost == null)
                _encounterHost = FindFirstObjectByType<EncounterHost>();
        }

        public void StartSelection()
        {
            if (_config == null || _itemSpawner == null)
            {
                Debug.LogWarning("[FirstEncounterItemSelector] Config or ItemSpawner not set");
                CompleteSelection();
                return;
            }

            _isActive = true;
            _selectedItems.Clear();
            _itemsPicked = 0;

            if (_config.UseRandomSelection)
            {
                StartCoroutine(GrantRandomItems());
            }
            else
            {
                StartCoroutine(SpawnItemsForSelection());
            }
        }

        private IEnumerator SpawnItemsForSelection()
        {
            // Disable normal item spawning
            _itemSpawner.SetItemsAvailable(false);

            var availableItems = new List<ItemDefinition>(_config.StartingItemPool);
            availableItems.RemoveAll(item => item == null);

            if (availableItems.Count == 0)
            {
                Debug.LogWarning("[FirstEncounterItemSelector] No items in starting pool");
                CompleteSelection();
                yield break;
            }

            // Get player spawn points
            var playerPoints = _itemSpawner.GetPointsForSide(TurnSide.Player, _config.StartingItemPool.Count);
            
            // Spawn items for selection
            for (int i = 0; i < Mathf.Min(availableItems.Count, playerPoints.Count); i++)
            {
                var definition = availableItems[i];
                if (definition == null || definition.WorldPrefab == null) continue;

                var point = playerPoints[i];
                var itemObject = Instantiate(definition.WorldPrefab, point.SpawnPosition, point.Rotation, transform);
                
                if (itemObject == null) continue;

                // Add selection behavior
                var pickup = itemObject.GetComponent<ItemPickupView>();
                if (pickup == null)
                    pickup = itemObject.AddComponent<ItemPickupView>();

                pickup.SetItem(definition);
                pickup.SetOwner(TurnSide.Player);
                
                // Add selection component
                var selector = itemObject.AddComponent<FirstEncounterItemPickup>();
                selector.Initialize(this, definition);

                _spawnedItems.Add(itemObject);

                // Animate appearance
                var baseScale = itemObject.transform.localScale;
                itemObject.transform.localScale = Vector3.zero;
                itemObject.transform.DOScale(baseScale, 0.3f).SetEase(Ease.OutBack);

                yield return new WaitForSeconds(0.15f);
            }

            // Enable interaction
            ItemPickupView.SetUsageWindowFilter(item => _selectedItems.Contains(item) == false && _itemsPicked < _config.ItemsToPick);
        }

        private IEnumerator GrantRandomItems()
        {
            var availableItems = new List<ItemDefinition>(_config.StartingItemPool);
            availableItems.RemoveAll(item => item == null);

            if (availableItems.Count == 0)
            {
                CompleteSelection();
                yield break;
            }

            int count = Random.Range(_config.MinRandomItems, _config.MaxRandomItems + 1);
            count = Mathf.Min(count, availableItems.Count);

            var runManager = RunManager.Instance;
            if (runManager != null && runManager.CurrentRun != null && runManager.CurrentRun.PlayerInventory != null)
            {
                var playerInv = runManager.CurrentRun.PlayerInventory;
                for (int i = 0; i < count; i++)
                {
                    int idx = Random.Range(0, availableItems.Count);
                    playerInv.Add(availableItems[idx], 1, TurnSide.Player);
                    availableItems.RemoveAt(idx);
                    if (availableItems.Count == 0) break;
                }
            }

            CompleteSelection();
        }

        public void OnItemClicked(FirstEncounterItemPickup pickup, ItemDefinition item)
        {
            if (!_isActive || _itemsPicked >= _config.ItemsToPick) return;
            if (_selectedItems.Contains(item)) return;

            _selectedItems.Add(item);
            _itemsPicked++;

            // Add to persistent inventory
            var runManager = RunManager.Instance;
            if (runManager != null && runManager.CurrentRun != null && runManager.CurrentRun.PlayerInventory != null)
            {
                runManager.CurrentRun.PlayerInventory.Add(item, 1, TurnSide.Player);
            }

            // Visual feedback - scale down and fade
            pickup.transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack).OnComplete(() => 
            {
                Destroy(pickup.gameObject);
                _spawnedItems.Remove(pickup.gameObject);
            });

            // Check if done
            if (_itemsPicked >= _config.ItemsToPick)
            {
                CompleteSelection();
            }
        }

        private void CompleteSelection()
        {
            _isActive = false;
            
            // Clean up remaining items
            foreach (var item in _spawnedItems)
            {
                if (item != null)
                    Destroy(item);
            }
            _spawnedItems.Clear();

            // Re-enable normal item spawning for future encounters
            _itemSpawner.SetItemsAvailable(true);
            
            // Reset usage filter
            ItemPickupView.SetUsageWindowFilter(null);

            OnSelectionComplete?.Invoke();
        }

        private void OnDestroy()
        {
            ItemPickupView.SetUsageWindowFilter(null);
        }
    }

    // Helper component for first encounter item pickups
    public sealed class FirstEncounterItemPickup : MonoBehaviour
    {
        private FirstEncounterItemSelector _selector;
        private ItemDefinition _item;
        private ItemPickupView _pickupView;

        public void Initialize(FirstEncounterItemSelector selector, ItemDefinition item)
        {
            _selector = selector;
            _item = item;
            _pickupView = GetComponent<ItemPickupView>();
        }

        private void OnMouseDown()
        {
            if (_pickupView != null && _pickupView.IsInteractive)
                _selector?.OnItemClicked(this, _item);
        }
    }
}