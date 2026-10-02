using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using ShellGame.Audio;
using ShellGame.Core;
using ShellGame.Dialogue;
using ShellGame.Items;
using ShellGame.Meta;
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
        [SerializeField] private GameObject _exitButtonPrefab; // "Продолжить путь" button

        [Header("Visual Feedback")]
        [SerializeField] private GameObject _selectionHighlightPrefab;

        private EnemyReactionDirector _dialogueDirector;
        private GameObject _sellMarkerInstance;
        private GameObject _exitButtonInstance;
        private bool _isInSellMode = false;
        private List<ItemDefinition> _currentShopItems = new List<ItemDefinition>();
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

            // Спавнеру товаров нужен контроллер: он берёт из него конфиг и зоны.
            _rig?.ShopItemSpawner?.Initialize(this);
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

            // Pass shop items to spawner
            if (_rig?.ShopItemSpawner != null)
            {
                _rig.ShopItemSpawner.SetShopItems(_currentShopItems);

                // ShopItemSpawner сам раскладывает и товары, и постоянные предметы игрока
                // (см. его SpawnItems). Отдельный PlayerItemSpawner тут только для доступа
                // к анлокам, поэтому второй раз предметы не спавним.
                StartCoroutine(_rig.ShopItemSpawner.SpawnItems());
            }

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

            if (_config.UseUnlockedItems)
            {
                // Get unlocked items from UnlockManager
                if (_rig?.PlayerItemSpawner != null)
                {
                    // Use the unlock manager from player spawner
                    var unlockManager = _rig.PlayerItemSpawner.UnlockManager;
                    var unlocksConfig = _rig.PlayerItemSpawner.UnlocksConfigAsset;

                    if (unlockManager != null && unlocksConfig != null)
                    {
                        foreach (var entry in unlocksConfig.Entries)
                        {
                            if (entry.Item != null && unlockManager.IsUnlocked(entry.Item))
                            {
                                // Filter: only items with BuyPrice > 0 if OnlySellableItems
                                if (_config.OnlySellableItems && entry.Item.BuyPrice <= 0)
                                    continue;

                                _currentShopItems.Add(entry.Item);
                            }
                        }
                    }
                }

                // Shuffle and pick random subset
                var shuffled = new List<ItemDefinition>(_currentShopItems);
                for (int i = shuffled.Count - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    var temp = shuffled[i];
                    shuffled[i] = shuffled[j];
                    shuffled[j] = temp;
                }

                int count = Mathf.Min(_config.MaxShopItemsPerVisit, shuffled.Count);
                _currentShopItems = shuffled.GetRange(0, count);
            }
            else
            {
                // Legacy: use manual list (not supported anymore, but keep for compat)
                Debug.LogWarning("[ShopEncounterController] UseUnlockedItems is false but manual list is not supported. Shop will be empty.");
            }

            // Assign to spawner
            if (_rig?.ShopItemSpawner != null)
            {
                _rig.ShopItemSpawner.SetShopItems(_currentShopItems);
            }
        }

        private void SpawnSellMarker()
        {
            if (_config.SellMarkerItemAsset == null || _rig?.SellZonePosition == null) return;

            _sellMarkerInstance = new GameObject("SellMarker");
            _sellMarkerInstance.transform.SetPositionAndRotation(_rig.SellZonePosition.position, _rig.SellZonePosition.rotation);
            _sellMarkerInstance.transform.SetParent(transform);

            var pickup = _sellMarkerInstance.AddComponent<ItemPickupView>();
            pickup.SetItem(_config.SellMarkerItemAsset);
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

            if (!_config.GrantFreeUnlockedItemOnEntry) yield break;

            var runManager = RunManager.Instance;
            if (runManager == null || runManager.CurrentRun == null || runManager.CurrentRun.PlayerInventory == null) yield break;

            var playerInv = runManager.CurrentRun.PlayerInventory;
            if (!playerInv.HasSpace(TurnSide.Player)) yield break;

            // Get unlocked items for free item pool
            var unlockedItems = new List<ItemDefinition>();
            if (_rig?.PlayerItemSpawner != null)
            {
                var unlockManager = _rig.PlayerItemSpawner.UnlockManager;
                var unlocksConfig = _rig.PlayerItemSpawner.UnlocksConfigAsset;

                if (unlockManager != null && unlocksConfig != null)
                {
                    foreach (var entry in unlocksConfig.Entries)
                    {
                        if (entry.Item != null && unlockManager.IsUnlocked(entry.Item))
                        {
                            unlockedItems.Add(entry.Item);
                        }
                    }
                }
            }

            if (unlockedItems.Count == 0) yield break;

            // Pick random free item
            var freeItem = unlockedItems[UnityEngine.Random.Range(0, unlockedItems.Count)];
            if (freeItem == null) yield break;

            playerInv.Add(freeItem, 1, TurnSide.Player);

            // Visual feedback - spawn the item on table
            var itemSpawner = _rig?.PlayerItemSpawner;
            if (itemSpawner != null && freeItem.WorldPrefab != null)
            {
                var targetPos = itemSpawner.GetNextEmptySlotPosition();
                var itemObject = Instantiate(freeItem.WorldPrefab, targetPos, Quaternion.identity, itemSpawner.transform);
                var pickup = itemObject.GetComponent<ItemPickupView>();
                if (pickup == null) pickup = itemObject.AddComponent<ItemPickupView>();
                pickup.SetItem(freeItem);
                pickup.SetOwner(TurnSide.Player);

                var baseScale = itemObject.transform.localScale;
                itemObject.transform.localScale = Vector3.zero;
                itemObject.transform.DOScale(baseScale, 0.3f).SetEase(Ease.OutBack);
            }

            // Could play a "free item received" dialogue here
        }

        /// <summary>
        /// Попытка купить предмет
        /// </summary>
        public bool TryBuyItem(ItemDefinition item)
        {
            if (item == null) return false;

            var runManager = RunManager.Instance;
            if (runManager == null || runManager.CurrentRun == null || runManager.CurrentRun.PlayerInventory == null) return false;

            var playerInv = runManager.CurrentRun.PlayerInventory;

            // Check coins
            int buyPrice = item.BuyPrice;
            if (playerInv.Coins < buyPrice)
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

            // Purchase!
            playerInv.Coins -= buyPrice;
            playerInv.Add(item, 1, TurnSide.Player);

            // Списать физические монеты со стола
            CoinPileController.Instance?.RemoveCoins(buyPrice);

            // Animate item moving to player slot
            if (_rig?.PlayerItemSpawner != null && _rig?.ShopItemSpawner != null)
            {
                var targetPos = _rig.PlayerItemSpawner.GetNextEmptySlotPosition();
                var pickup = _rig.ShopItemSpawner.GetComponentsInChildren<ItemPickupView>(true)
                    .FirstOrDefault(p => p != null && p.Item == item);

                if (pickup != null)
                {
                    _rig.ShopItemSpawner.AnimateItemPurchase(pickup, targetPos);
                }
            }

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

            // Use item's own SellPriceMultiplier
            float sellMultiplier = item.SellPriceMultiplier;
            if (sellMultiplier <= 0f)
                sellMultiplier = _config.SellPriceMultiplier;

            int buyPrice = item.BuyPrice;
            if (buyPrice <= 0)
                buyPrice = 5; // Default fallback

            int sellPrice = Mathf.RoundToInt(buyPrice * sellMultiplier);
            if (sellPrice < 1) sellPrice = 1;

            // Sell!
            playerInv.Remove(item, 1, TurnSide.Player);
            playerInv.Coins += sellPrice;

            // Добавить физические монеты на стол
            CoinPileController.Instance?.AddCoinsInstant(sellPrice);

            // Visual feedback - remove from table
            RemovePlayerItemFromTable(item);

            // Play dialogue
            PlayDialogue(EnemyReactionContext.ShopSell);

            return true;
        }

        private void HighlightPlayerItemsForSale(bool highlight)
        {
            if (_rig?.PlayerItemSpawner == null) return;

            var indicator = SellModeIndicator.Instance;
            if (indicator == null) return;

            var pickups = _rig.PlayerItemSpawner.GetComponentsInChildren<ItemPickupView>(true);
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
            if (_rig?.PlayerItemSpawner == null) return;

            var pickups = _rig.PlayerItemSpawner.GetComponentsInChildren<ItemPickupView>(true);
            foreach (var pickup in pickups)
            {
                if (pickup.Item == item && pickup.Owner == TurnSide.Player)
                {
                    pickup.transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack).OnComplete(() =>
                    {
                        _rig.PlayerItemSpawner.RemoveItem(pickup);
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

            // Clear shop items (not player items)
            if (_rig?.ShopItemSpawner != null)
            {
                _rig.ShopItemSpawner.ClearShopItems();
            }

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
        public List<ItemDefinition> CurrentShopItems => _currentShopItems;
        public ShopConfig Config => _config;
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