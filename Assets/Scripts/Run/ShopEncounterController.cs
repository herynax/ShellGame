using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using ShellGame.Audio;
using ShellGame.Core;
using ShellGame.Dialogue;
using ShellGame.Items;
using ShellGame.Run;
using UnityEngine;

namespace ShellGame.Run
{
    /// <summary>
    /// Основной контроллер магазина. Управляет:
    /// - Диалогами торговца
    /// - Покупкой/продажей предметов
    /// - Выдачей бесплатного предмета при входе
    /// - Режимом продажи (клик по крестику -> выбор предмета для продажи)
    /// - Выходом из магазина
    /// </summary>
    public sealed class ShopEncounterController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private ShopConfig _config;
        [SerializeField] private ShopEncounterRig _rig;

        [Header("UI Prefabs")]
        [SerializeField] private GameObject _sellMarkerPrefab; // "X" marker for selling
        [SerializeField] private GameObject _exitButtonPrefab; // "Продолжить путь" button

        [Header("Visual Feedback")]
        [SerializeField] private GameObject _selectionHighlightPrefab;

        private EnemyReactionDirector _dialogueDirector;
        private GameObject _sellMarkerInstance;
        private GameObject _exitButtonInstance;
        private bool _isInSellMode = false;
        private List<ShopItemEntry> _currentShopItems = new List<ShopItemEntry>();
        private List<GameObject> _spawnedShopItems = new List<GameObject>();
        private IAudioService _audio;
        private Coroutine _sellModeHintRoutine;

        // Events
        public System.Action OnShopExited;

        private void Awake()
        {
            if (_rig == null)
                _rig = GetComponentInParent<ShopEncounterRig>();

            if (!ServiceLocator.TryGet<IAudioService>(out _audio))
            {
                _audio = new FMODAudioService();
                ServiceLocator.Register(_audio);
            }

            // Setup coin zone reference
            if (_rig != null && _rig.CoinZone != null)
            {
                var pileController = CoinPileController.Instance;
                if (pileController != null)
                {
                    pileController.CoinZone = _rig.CoinZone;
                }
            }
        }

        private void Start()
        {
            InitializeShop();
        }

        private void InitializeShop()
        {
            if (_config == null)
            {
                Debug.LogError("[ShopEncounterController] ShopConfig not assigned!");
                return;
            }

            // Setup dialogue director
            if (_config.MerchantDialogue != null)
            {
                _dialogueDirector = EnemyReactionBootstrap.CreateDirector(_config.MerchantDialogue);
            }

            // Generate shop inventory for this visit
            GenerateShopInventory();

            // Spawn shop items on table
            SpawnShopItems();

            // Spawn sell marker
            SpawnSellMarker();

            // Spawn exit button
            SpawnExitButton();

            // Grant free item on entry
            StartCoroutine(GrantFreeItemRoutine());

            // Play enter dialogue
            PlayDialogue(EnemyReactionContext.GameStart);
        }

        private void GenerateShopInventory()
        {
            _currentShopItems.Clear();
            
            if (_config.BuyItemMasterList == null || _config.BuyItemMasterList.Count == 0)
                return;

            // Shuffle and pick random subset
            var shuffled = new List<ShopItemEntry>(_config.BuyItemMasterList);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var temp = shuffled[i];
                shuffled[i] = shuffled[j];
                shuffled[j] = temp;
            }

            int count = Mathf.Min(_config.BuyItemsPerVisit, shuffled.Count);
            for (int i = 0; i < count; i++)
            {
                _currentShopItems.Add(shuffled[i]);
            }
        }

        private void SpawnShopItems()
        {
            if (_rig == null || _rig.ItemSpawner == null) return;

            var itemSpawner = _rig.ItemSpawner;
            var playerPoints = itemSpawner.GetPointsForSide(TurnSide.Player, _currentShopItems.Count);

            for (int i = 0; i < Mathf.Min(_currentShopItems.Count, playerPoints.Count); i++)
            {
                var entry = _currentShopItems[i];
                if (entry.Item == null || entry.Item.WorldPrefab == null) continue;

                var point = playerPoints[i];
                var itemObject = Instantiate(entry.Item.WorldPrefab, point.SpawnPosition, point.Rotation, itemSpawner.transform);
                
                if (itemObject == null) continue;

                var pickup = itemObject.GetComponent<ItemPickupView>();
                if (pickup == null)
                    pickup = itemObject.AddComponent<ItemPickupView>();

                pickup.SetItem(entry.Item);
                pickup.SetOwner(TurnSide.Player);
                
                // Add shop item behavior
                var shopItem = itemObject.AddComponent<ShopItemBehavior>();
                shopItem.Initialize(this, entry);

                _spawnedShopItems.Add(itemObject);

                // Animate appearance
                var baseScale = itemObject.transform.localScale;
                itemObject.transform.localScale = Vector3.zero;
                itemObject.transform.DOScale(baseScale, 0.3f).SetEase(Ease.OutBack).SetDelay(i * 0.1f);
            }
        }

        private void SpawnSellMarker()
        {
            if (_sellMarkerPrefab == null || _rig?.SellZonePosition == null) return;

            _sellMarkerInstance = Instantiate(_sellMarkerPrefab, _rig.SellZonePosition.position, _rig.SellZonePosition.rotation, transform);
            _sellMarkerInstance.name = "SellMarker";

            var pickup = _sellMarkerInstance.GetComponent<ItemPickupView>();
            if (pickup == null)
                pickup = _sellMarkerInstance.AddComponent<ItemPickupView>();

            // Create a dummy item definition for the sell marker
            var sellMarkerItem = ScriptableObject.CreateInstance<ItemDefinition>();
            sellMarkerItem.DisplayName = "Продажа";
            sellMarkerItem.TooltipDescription = "Кликните, чтобы войти в режим продажи. Выберите свой предмет для продажи.";
            
            pickup.SetItem(sellMarkerItem);
            pickup.SetOwner(TurnSide.Player);

            var sellBehavior = _sellMarkerInstance.AddComponent<SellMarkerBehavior>();
            sellBehavior.Initialize(this);
        }

        private void SpawnExitButton()
        {
            if (_exitButtonPrefab == null || _rig?.ExitButtonPosition == null) return;

            _exitButtonInstance = Instantiate(_exitButtonPrefab, _rig.ExitButtonPosition.position, _rig.ExitButtonPosition.rotation, transform);
            _exitButtonInstance.name = "ExitButton";

            var exitButton = _exitButtonInstance.GetComponent<ExitButtonView>();
            if (exitButton == null)
                exitButton = _exitButtonInstance.AddComponent<ExitButtonView>();

            exitButton.Initialize(this);
        }

        private IEnumerator GrantFreeItemRoutine()
        {
            yield return new WaitForSeconds(1f); // Wait for items to spawn

            if (_config.FreeItemPool == null || _config.FreeItemPool.Count == 0) yield break;

            var runManager = RunManager.Instance;
            if (runManager == null || runManager.CurrentRun == null || runManager.CurrentRun.PlayerInventory == null) yield break;

            var playerInv = runManager.CurrentRun.PlayerInventory;
            if (!playerInv.HasSpace(TurnSide.Player)) yield break;

            // Pick random free item
            var freeItem = _config.FreeItemPool[Random.Range(0, _config.FreeItemPool.Count)];
            if (freeItem == null) yield break;

            playerInv.Add(freeItem, 1, TurnSide.Player);

            // Visual feedback - spawn the item on table
            var itemSpawner = _rig?.ItemSpawner;
            if (itemSpawner != null)
            {
                var points = itemSpawner.GetPointsForSide(TurnSide.Player, 1);
                if (points.Count > 0 && freeItem.WorldPrefab != null)
                {
                    var itemObject = Instantiate(freeItem.WorldPrefab, points[0].SpawnPosition, points[0].Rotation, itemSpawner.transform);
                    var pickup = itemObject.GetComponent<ItemPickupView>();
                    if (pickup == null) pickup = itemObject.AddComponent<ItemPickupView>();
                    pickup.SetItem(freeItem);
                    pickup.SetOwner(TurnSide.Player);
                    
                    var baseScale = itemObject.transform.localScale;
                    itemObject.transform.localScale = Vector3.zero;
                    itemObject.transform.DOScale(baseScale, 0.3f).SetEase(Ease.OutBack);
                }
            }

            // Could play a "free item received" dialogue here
        }

        /// <summary>
        /// Попытка купить предмет
        /// </summary>
        public bool TryBuyItem(ShopItemEntry entry)
        {
            if (entry == null || entry.Item == null) return false;

            var runManager = RunManager.Instance;
            if (runManager == null || runManager.CurrentRun == null || runManager.CurrentRun.PlayerInventory == null) return false;

            var playerInv = runManager.CurrentRun.PlayerInventory;

            // Check coins
            if (playerInv.Coins < entry.BuyPrice)
            {
                PlayDialogue(EnemyReactionContext.ShopNoMoney);
                return false;
            }

            // Check space
            if (!playerInv.HasSpace(TurnSide.Player))
            {
                PlayDialogue(EnemyReactionContext.ShopNoSpace);
                return false;
            }

            // Check stock
            if (entry.MaxStock > 0)
            {
                // Count how many of this item already bought this visit
                int bought = 0; // Could track per-visit stock
                if (bought >= entry.MaxStock)
                {
                    // Out of stock - could play a dialogue
                    return false;
                }
            }

            // Purchase!
            playerInv.Coins -= entry.BuyPrice;
            playerInv.Add(entry.Item, 1, TurnSide.Player);

            // Visual feedback
            PlayDialogue(EnemyReactionContext.ShopPurchase);

            return true;
        }

        /// <summary>
        /// Войти в режим продажи
        /// </summary>
        public void EnterSellMode()
        {
            if (_isInSellMode) return;
            _isInSellMode = true;

            // Enable sell mode on all item pickups
            ItemPickupView.SetSellMode(true);
            ItemPickupView.OnSellModeClick += OnPlayerItemSellClicked;

            // Highlight all player items on table
            HighlightPlayerItemsForSale(true);

            // Show hint
            ShowSellModeHint();

            // Play dialogue
            PlayDialogue(EnemyReactionContext.ShopSell);
        }

        /// <summary>
        /// Выйти из режима продажи
        /// </summary>
        public void ExitSellMode()
        {
            if (!_isInSellMode) return;
            _isInSellMode = false;

            // Disable sell mode on all item pickups
            ItemPickupView.SetSellMode(false);
            ItemPickupView.OnSellModeClick -= OnPlayerItemSellClicked;

            // Remove highlights
            HighlightPlayerItemsForSale(false);

            // Hide hint
            HideSellModeHint();
        }

        private void OnPlayerItemSellClicked(ItemPickupView pickup)
        {
            if (pickup != null && pickup.Item != null)
            {
                TrySellPlayerItem(pickup.Item);
            }
        }

        /// <summary>
        /// Продать предмет игрока
        /// </summary>
        public bool TrySellPlayerItem(ItemDefinition item)
        {
            if (!_isInSellMode || item == null) return false;

            var runManager = RunManager.Instance;
            if (runManager == null || runManager.CurrentRun == null || runManager.CurrentRun.PlayerInventory == null) return false;

            var playerInv = runManager.CurrentRun.PlayerInventory;
            if (!playerInv.Has(item, TurnSide.Player)) return false;

            // Find the shop entry for this item to get buy price
            int buyPrice = 0;
            foreach (var entry in _currentShopItems)
            {
                if (entry.Item == item)
                {
                    buyPrice = entry.BuyPrice;
                    break;
                }
            }

            // If not in current shop, use a default or item's base value
            if (buyPrice == 0)
            {
                buyPrice = 10; // Default fallback
            }

            int sellPrice = Mathf.RoundToInt(buyPrice * _config.SellPriceMultiplier);
            if (sellPrice < 1) sellPrice = 1;

            // Sell!
            playerInv.Remove(item, 1, TurnSide.Player);
            playerInv.Coins += sellPrice;

            // Visual feedback - remove from table
            RemovePlayerItemFromTable(item);

            // Play dialogue
            PlayDialogue(EnemyReactionContext.ShopSell);

            return true;
        }

        private void HighlightPlayerItemsForSale(bool highlight)
        {
            if (_rig?.ItemSpawner == null) return;

            var indicator = SellModeIndicator.Instance;
            if (indicator == null) return;

            var pickups = _rig.ItemSpawner.GetComponentsInChildren<ItemPickupView>(true);
            foreach (var pickup in pickups)
            {
                if (pickup.Owner == TurnSide.Player)
                {
                    indicator.HighlightItem(pickup, highlight);
                }
            }
        }

        private void ShowSellModeHint()
        {
            var indicator = SellModeIndicator.Instance;
            if (indicator != null)
            {
                indicator.Activate();
            }
        }

        private void HideSellModeHint()
        {
            var indicator = SellModeIndicator.Instance;
            if (indicator != null)
            {
                indicator.Deactivate();
            }
            
            if (_sellModeHintRoutine != null)
            {
                StopCoroutine(_sellModeHintRoutine);
                _sellModeHintRoutine = null;
            }
        }

        private void RemovePlayerItemFromTable(ItemDefinition item)
        {
            if (_rig?.ItemSpawner == null) return;

            var spawnedItems = _rig.ItemSpawner.GetComponentsInChildren<ItemPickupView>(true);
            foreach (var pickup in spawnedItems)
            {
                if (pickup.Item == item && pickup.Owner == TurnSide.Player)
                {
                    pickup.transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack).OnComplete(() =>
                    {
                        Destroy(pickup.gameObject);
                    });
                    break;
                }
            }
        }

        /// <summary>
        /// Выход из магазина
        /// </summary>
        public void ExitShop()
        {
            PlayDialogue(EnemyReactionContext.ShopExit);
            
            // Cleanup local objects (the rig will be destroyed by EncounterHost.ExitRoutine)
            Cleanup();

            // Notify exit
            OnShopExited?.Invoke();
        }

        private void Cleanup()
        {
            // Unsubscribe from sell mode event
            ItemPickupView.OnSellModeClick -= OnPlayerItemSellClicked;
            
            if (_dialogueDirector != null)
            {
                Destroy(_dialogueDirector.gameObject);
                _dialogueDirector = null;
            }

            foreach (var item in _spawnedShopItems)
            {
                if (item != null) Destroy(item);
            }
            _spawnedShopItems.Clear();

            if (_sellMarkerInstance != null) Destroy(_sellMarkerInstance);
            if (_exitButtonInstance != null) Destroy(_exitButtonInstance);

            _isInSellMode = false;
        }

        private void PlayDialogue(EnemyReactionContext context)
        {
            if (_dialogueDirector != null)
            {
                _dialogueDirector.ForceReaction(context);
            }
        }

        public bool IsInSellMode => _isInSellMode;
        public List<ShopItemEntry> CurrentShopItems => _currentShopItems;
        public ShopConfig Config => _config;
    }

    // Behavior for shop items (buyable)
    public sealed class ShopItemBehavior : MonoBehaviour
    {
        private ShopEncounterController _controller;
        private ShopItemEntry _entry;
        private ItemPickupView _pickupView;
        private Coroutine _tooltipRoutine;

        public ShopItemEntry Entry => _entry;

        public void Initialize(ShopEncounterController controller, ShopItemEntry entry)
        {
            _controller = controller;
            _entry = entry;
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
                _controller.TryBuyItem(_entry);
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
            if (_entry?.Item == null) return;
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
            yield return new WaitForSeconds(Mathf.Max(0f, _entry.Item.TooltipHoverDelay));
            ShopTooltipView.Instance?.Show(this, _entry.BuyPrice);
        }

        private void OnDestroy()
        {
            if (_pickupView != null)
                _pickupView.Used -= OnUsed;
            StopTooltipTimer();
            ShopTooltipView.Instance?.Hide(this);
        }
    }

    // Behavior for sell marker (X)
    public sealed class SellMarkerBehavior : MonoBehaviour
    {
        private ShopEncounterController _controller;
        private ItemPickupView _pickupView;

        public void Initialize(ShopEncounterController controller)
        {
            _controller = controller;
            _pickupView = GetComponent<ItemPickupView>();
            
            if (_pickupView != null)
            {
                _pickupView.Used += OnUsed;
            }
        }

        private void OnUsed(ItemPickupView pickup)
        {
            if (_controller.IsInSellMode)
            {
                _controller.ExitSellMode();
            }
            else
            {
                _controller.EnterSellMode();
            }
        }

        private void OnDestroy()
        {
            if (_pickupView != null)
                _pickupView.Used -= OnUsed;
        }
    }
}