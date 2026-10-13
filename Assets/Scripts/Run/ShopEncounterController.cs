using System.Collections;
using System.Collections.Generic;
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
    public sealed class ShopEncounterController : MonoBehaviour
    {
        private const float SpawnWaitTimeout = 15f;
        private const float IntroWaitTimeout = 30f;
        private const float ExitLineTimeout = 10f;

        [Header("Config")]
        [SerializeField] private ShopConfig _config;
        [SerializeField] private ShopEncounterRig _rig;

        [Header("UI Prefabs")]
        [SerializeField] private GameObject _exitButtonPrefab;

        private EnemyReactionDirector _dialogueDirector;
        private ShopPickupInput _input;
        private GameObject _sellMarkerInstance;
        private GameObject _exitButtonInstance;
        private bool _isInSellMode;
        private bool _isExiting;
        private bool _isCleanedUp;
        private List<ItemDefinition> _currentShopItems = new List<ItemDefinition>();
        private IAudioService _audio;

        public System.Action OnShopExited;

        public bool IsInSellMode => _isInSellMode;
        public List<ItemDefinition> CurrentShopItems => _currentShopItems;
        public ShopConfig Config => _config;

        private ShopItemSpawner Spawner => _rig != null ? _rig.ShopItemSpawner : null;

        private void Awake()
        {
            if (_rig == null)
                _rig = GetComponentInParent<ShopEncounterRig>();

            if (!ServiceLocator.TryGet<IAudioService>(out _audio))
            {
                _audio = new FMODAudioService();
                ServiceLocator.Register(_audio);
            }

            var spawner = Spawner;
            if (spawner != null)
                spawner.Initialize(this);
        }

        private void Start() => InitializeShop();
        private void OnDestroy() => Cleanup();

        private void InitializeShop()
        {
            if (_config == null)
            {
                Debug.LogError("[ShopEncounterController] ShopConfig not assigned!");
                return;
            }

            if (_config.MerchantDialogue != null)
                _dialogueDirector = EnemyReactionBootstrap.CreateDirector(_config.MerchantDialogue);

            _input = gameObject.AddComponent<ShopPickupInput>();

            GenerateShopInventory();

            var spawner = Spawner;
            if (spawner != null)
            {
                spawner.Initialize(this);
                spawner.SetShopItems(_currentShopItems);
                StartCoroutine(spawner.SpawnItems());
            }

            SpawnSellMarker();
            SpawnExitButton();

            StartCoroutine(EntrySequenceRoutine());
        }

        private IEnumerator EntrySequenceRoutine()
        {
            var spawner = Spawner;
            if (spawner != null)
            {
                float waited = 0f;
                while (!spawner.HasFinishedSpawning && waited < SpawnWaitTimeout)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            yield return WaitForMerchantIntro();
            if (_isExiting) yield break;

            float delay = Mathf.Max(0f, _config.FreeItemDelayAfterIntro);
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (_isExiting) yield break;
            GrantFreeItem();
        }

        private IEnumerator WaitForMerchantIntro()
        {
            if (_dialogueDirector == null) yield break;

            float waited = 0f;
            while (_dialogueDirector != null && _dialogueDirector.IsPresenting && waited < IntroWaitTimeout)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void GrantFreeItem()
        {
            if (!_config.GrantFreeUnlockedItemOnEntry) return;

            var runManager = RunManager.Instance;
            if (runManager == null || runManager.CurrentRun == null || runManager.CurrentRun.PlayerInventory == null) return;

            var playerInv = runManager.CurrentRun.PlayerInventory;
            var spawner = Spawner;
            if (spawner == null) return;
            if (!playerInv.HasSpace(TurnSide.Player) || !spawner.HasFreePlayerSlot) return;

            var pool = GetUnlockedItems();
            pool.RemoveAll(candidate => candidate == null || candidate.WorldPrefab == null);
            if (pool.Count == 0) return;

            var freeItem = pool[UnityEngine.Random.Range(0, pool.Count)];
            playerInv.Add(freeItem, 1, TurnSide.Player);
            spawner.SpawnPlayerItem(freeItem);
        }

        private bool TryGetUnlocks(out IUnlockManager manager, out UnlocksConfig config)
        {
            manager = null;
            config = null;

            var playerSpawner = _rig != null ? _rig.PlayerItemSpawner : null;
            if (playerSpawner != null)
            {
                manager = playerSpawner.UnlockManager;
                config = playerSpawner.UnlocksConfigAsset;
            }

            if (manager == null || config == null)
            {
                var shopSpawner = Spawner;
                if (shopSpawner != null)
                {
                    if (manager == null) manager = shopSpawner.UnlockManager;
                    if (config == null) config = shopSpawner.UnlocksConfigAsset;
                }
            }

            return manager != null && config != null;
        }

        private List<ItemDefinition> GetUnlockedItems()
        {
            var result = new List<ItemDefinition>();
            if (!TryGetUnlocks(out var unlockManager, out var unlocksConfig))
                return result;

            foreach (var entry in unlocksConfig.Entries)
            {
                if (entry.Item != null && unlockManager.IsUnlocked(entry.Item))
                    result.Add(entry.Item);
            }

            return result;
        }

        private void GenerateShopInventory()
        {
            _currentShopItems.Clear();

            if (_config.UseUnlockedItems)
            {
                var candidates = GetUnlockedItems();
                if (_config.OnlySellableItems)
                    candidates.RemoveAll(candidate => candidate.BuyPrice <= 0);

                for (int i = candidates.Count - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    var temp = candidates[i];
                    candidates[i] = candidates[j];
                    candidates[j] = temp;
                }

                int count = Mathf.Min(_config.MaxShopItemsPerVisit, candidates.Count);
                _currentShopItems = candidates.GetRange(0, count);
            }

            var spawner = Spawner;
            if (spawner != null)
                spawner.SetShopItems(_currentShopItems);
        }

        private void SpawnSellMarker()
        {
            var markerItem = _config.SellMarkerItemAsset;
            var zone = _rig != null ? _rig.SellZonePosition : null;
            if (markerItem == null || zone == null || markerItem.WorldPrefab == null) return;

            _sellMarkerInstance = Instantiate(markerItem.WorldPrefab, zone.position, zone.rotation, transform);
            _sellMarkerInstance.name = "SellMarker";

            var pickup = _sellMarkerInstance.GetComponent<ItemPickupView>() ?? _sellMarkerInstance.AddComponent<ItemPickupView>();
            pickup.SetItem(markerItem);
            pickup.SetOwner(TurnSide.Player);
            pickup.SetAlwaysInteractive(true);
            pickup.SetSellExempt(true);

            var sellBehavior = _sellMarkerInstance.AddComponent<SellMarkerBehavior>();
            sellBehavior.Initialize(this);
        }

        private void SpawnExitButton()
        {
            if (_exitButtonPrefab == null || _rig == null || _rig.ExitButtonPosition == null) return;

            _exitButtonInstance = Instantiate(_exitButtonPrefab, _rig.ExitButtonPosition.position, _rig.ExitButtonPosition.rotation, transform);
            _exitButtonInstance.name = "ExitButton";

            var exitButton = _exitButtonInstance.GetComponent<ExitButtonView>() ?? _exitButtonInstance.AddComponent<ExitButtonView>();
            exitButton.Initialize(this);
        }

        public bool TryBuyItem(ItemDefinition item, ItemPickupView pickup = null)
        {
            if (item == null || _isExiting) return false;

            var runManager = RunManager.Instance;
            if (runManager == null || runManager.CurrentRun == null || runManager.CurrentRun.PlayerInventory == null) return false;

            var playerInv = runManager.CurrentRun.PlayerInventory;
            int buyPrice = item.BuyPrice;

            if (playerInv.Coins < buyPrice)
            {
                PlayDialogue(EnemyReactionContext.ShopNoMoney);
                PlayCannotBuyAnimation(pickup);
                return false;
            }

            var spawner = Spawner;
            if (!playerInv.HasSpace(TurnSide.Player) || (spawner != null && !spawner.HasFreePlayerSlot))
            {
                PlayDialogue(EnemyReactionContext.ShopNoSpace);
                PlayCannotBuyAnimation(pickup);
                return false;
            }

            playerInv.Coins -= buyPrice;
            playerInv.Add(item, 1, TurnSide.Player);

            CoinPileController.Instance?.RemoveCoins(buyPrice);
            AnimateCoinsFlyingToMerchant(buyPrice);

            if (spawner != null && pickup != null)
                spawner.AnimateItemPurchase(pickup);

            PlayDialogue(EnemyReactionContext.ShopPurchase);
            return true;
        }

        public Vector3 GetMerchantPosition()
        {
            if (_rig != null)
            {
                var enemyObj = _rig.transform.Find("Enemy") ?? _rig.transform.Find("Merchant") ?? _rig.transform.Find("NPC");
                if (enemyObj != null) return enemyObj.position + Vector3.up * 1f;
                return _rig.transform.position + _rig.transform.forward * 1.6f + Vector3.up * 1.2f;
            }
            return transform.position + Vector3.up * 1.5f;
        }

        private void PlayCannotBuyAnimation(ItemPickupView pickup)
        {
            if (pickup == null) return;

            Transform t = pickup.transform;
            t.DOKill(true);

            t.DOShakePosition(0.38f, strength: new Vector3(0.06f, 0f, 0.06f), vibrato: 24, randomness: 90f);
            t.DOShakeRotation(0.38f, strength: new Vector3(0f, 15f, 8f), vibrato: 18);

            StartCoroutine(FlashRedRoutine(pickup.gameObject));
        }

        private IEnumerator FlashRedRoutine(GameObject target)
        {
            if (target == null) yield break;

            var renderers = target.GetComponentsInChildren<Renderer>();
            var originalColors = new Dictionary<Material, Color>();
            var colorProps = new Dictionary<Material, string>();

            foreach (var r in renderers)
            {
                if (r == null) continue;
                foreach (var mat in r.materials)
                {
                    if (mat == null) continue;
                    string prop = mat.HasProperty("_BaseColor") ? "_BaseColor" : (mat.HasProperty("_Color") ? "_Color" : null);
                    if (prop != null && !originalColors.ContainsKey(mat))
                    {
                        originalColors[mat] = mat.GetColor(prop);
                        colorProps[mat] = prop;
                    }
                }
            }

            float duration = 0.35f;
            float elapsed = 0f;

            while (elapsed < duration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float pingPong = Mathf.PingPong(elapsed * 7f, 1f);

                foreach (var kvp in originalColors)
                {
                    if (kvp.Key != null)
                    {
                        Color flashed = Color.Lerp(kvp.Value, Color.red, pingPong);
                        kvp.Key.SetColor(colorProps[kvp.Key], flashed);
                    }
                }
                yield return null;
            }

            foreach (var kvp in originalColors)
            {
                if (kvp.Key != null)
                    kvp.Key.SetColor(colorProps[kvp.Key], kvp.Value);
            }
        }

        private void AnimateCoinsFlyingToMerchant(int amount)
        {
            if (amount <= 0) return;
            StartCoroutine(SpawnFlyingCoinsRoutine(amount));
        }

        private IEnumerator SpawnFlyingCoinsRoutine(int amount)
        {
            Vector3 startPos = CoinPileController.Instance != null
                ? CoinPileController.Instance.transform.position + Vector3.up * 0.1f
                : transform.position;

            Vector3 targetPos = GetMerchantPosition();
            int visualCoins = Mathf.Clamp(amount, 3, 7);

            GameObject template = null;
            Vector3 baseScale = new Vector3(0.035f, 0.007f, 0.035f); // Компактный дефолтный размер монетки

            if (CoinPileController.Instance != null)
            {
                // Ищем конкретно Rigidbody монетки (корень монеты), чтобы получить её честный размер
                var rb = CoinPileController.Instance.GetComponentInChildren<Rigidbody>();
                if (rb != null)
                {
                    template = rb.gameObject;
                    baseScale = template.transform.lossyScale;
                }
                else
                {
                    var mf = CoinPileController.Instance.GetComponentInChildren<MeshFilter>();
                    if (mf != null)
                    {
                        template = mf.gameObject;
                        baseScale = template.transform.lossyScale;
                    }
                }
            }

            // Защита: если мировой размер превышает 8 см (например, взялся масштаб стола), сжимаем до реалистичного
            if (baseScale.x > 0.08f || baseScale.y > 0.08f || baseScale.z > 0.08f)
            {
                baseScale = new Vector3(0.035f, 0.007f, 0.035f);
            }

            for (int i = 0; i < visualCoins; i++)
            {
                GameObject coin;
                if (template != null)
                {
                    coin = Instantiate(template, startPos + Random.insideUnitSphere * 0.08f, Random.rotation);
                    var rb = coin.GetComponent<Rigidbody>();
                    if (rb != null) Destroy(rb);
                    var col = coin.GetComponent<Collider>();
                    if (col != null) Destroy(col);
                }
                else
                {
                    coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    coin.transform.position = startPos + Random.insideUnitSphere * 0.08f;
                    var col = coin.GetComponent<Collider>();
                    if (col != null) Destroy(col);
                    var rend = coin.GetComponent<Renderer>();
                    if (rend != null) rend.material.color = new Color(1f, 0.84f, 0.1f);
                }

                // Жёстко выставляем реальный базовый скейл монетки перед началом полёта
                coin.transform.localScale = baseScale;

                float duration = Random.Range(0.42f, 0.55f);
                float jumpHeight = Random.Range(0.35f, 0.55f);

                coin.transform.DOJump(targetPos, jumpHeight, 1, duration).SetEase(Ease.InQuad);
                coin.transform.DOScale(Vector3.zero, duration).SetEase(Ease.InBack);
                coin.transform.DORotate(Random.insideUnitSphere * 720f, duration, RotateMode.FastBeyond360)
                    .OnComplete(() =>
                    {
                        if (coin != null) Destroy(coin);
                    });

                yield return new WaitForSeconds(0.04f);
            }
        }

        public void EnterSellMode()
        {
            if (_isInSellMode || _isExiting) return;
            _isInSellMode = true;

            ItemPickupView.SetSellMode(true);
            ItemPickupView.OnSellModeClick += OnPlayerItemSellClicked;

            HighlightPlayerItemsForSale(true);
            ShowSellModeHint();

            PlayDialogue(EnemyReactionContext.ShopSell);
        }

        public void ExitSellMode()
        {
            if (!_isInSellMode) return;
            _isInSellMode = false;

            ItemPickupView.SetSellMode(false);
            ItemPickupView.OnSellModeClick -= OnPlayerItemSellClicked;

            HighlightPlayerItemsForSale(false);
            HideSellModeHint();
        }

        private void OnPlayerItemSellClicked(ItemPickupView pickup) => TrySellPlayerItem(pickup);

        public bool TrySellPlayerItem(ItemPickupView pickup)
        {
            if (!_isInSellMode || _isExiting) return false;
            if (pickup == null || pickup.Item == null || pickup.IsSellExempt) return false;

            var runManager = RunManager.Instance;
            if (runManager == null || runManager.CurrentRun == null || runManager.CurrentRun.PlayerInventory == null) return false;

            var playerInv = runManager.CurrentRun.PlayerInventory;
            var item = pickup.Item;
            if (!playerInv.Has(item, TurnSide.Player)) return false;

            float sellMultiplier = item.SellPriceMultiplier <= 0f ? _config.SellPriceMultiplier : item.SellPriceMultiplier;
            int buyPrice = item.BuyPrice <= 0 ? 5 : item.BuyPrice;
            int sellPrice = Mathf.Max(1, Mathf.RoundToInt(buyPrice * sellMultiplier));

            playerInv.Remove(item, 1, TurnSide.Player);
            playerInv.Coins += sellPrice;

            CoinPileController.Instance?.DropCoins(sellPrice);

            SellModeIndicator.Instance?.HighlightItem(pickup, false);

            var spawner = Spawner;
            if (spawner != null)
                spawner.SellPickup(pickup, GetMerchantPosition());

            PlayDialogue(EnemyReactionContext.ShopSell);
            return true;
        }

        public bool TrySellPlayerItem(ItemDefinition item)
        {
            var spawner = Spawner;
            if (spawner == null || item == null) return false;

            foreach (var pickup in spawner.GetPlayerPickups())
            {
                if (pickup.Item == item)
                    return TrySellPlayerItem(pickup);
            }
            return false;
        }

        private void HighlightPlayerItemsForSale(bool highlight)
        {
            var spawner = Spawner;
            var indicator = SellModeIndicator.Instance;
            if (spawner == null || indicator == null) return;

            foreach (var pickup in spawner.GetPlayerPickups())
                indicator.HighlightItem(pickup, highlight);
        }

        private void ShowSellModeHint() => SellModeIndicator.Instance?.Activate();
        private void HideSellModeHint() => SellModeIndicator.Instance?.Deactivate();

        public void ExitShop()
        {
            if (_isExiting) return;
            _isExiting = true;
            StartCoroutine(ExitRoutine());
        }

        private IEnumerator ExitRoutine()
        {
            if (_isInSellMode)
                ExitSellMode();

            if (_dialogueDirector != null)
            {
                bool done = false;
                StartCoroutine(RunAndFlag(_dialogueDirector.ForceReactionRoutine(EnemyReactionContext.ShopExit), () => done = true));

                float waited = 0f;
                while (!done && waited < ExitLineTimeout)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            Cleanup();
            OnShopExited?.Invoke();
        }

        private static IEnumerator RunAndFlag(IEnumerator routine, System.Action onDone)
        {
            yield return routine;
            onDone?.Invoke();
        }

        private void Cleanup()
        {
            if (_isCleanedUp) return;
            _isCleanedUp = true;

            if (_isInSellMode)
                ExitSellMode();

            ItemPickupView.SetSellMode(false);
            ItemPickupView.OnSellModeClick -= OnPlayerItemSellClicked;

            if (_input != null)
            {
                Destroy(_input);
                _input = null;
            }

            if (_dialogueDirector != null)
            {
                Destroy(_dialogueDirector.gameObject);
                _dialogueDirector = null;
            }

            Spawner?.ClearShopItems();

            if (_sellMarkerInstance != null) Destroy(_sellMarkerInstance);
            if (_exitButtonInstance != null) Destroy(_exitButtonInstance);

            _isInSellMode = false;
        }

        private void PlayDialogue(EnemyReactionContext context)
        {
            _dialogueDirector?.ForceReaction(context);
        }
    }

    public sealed class SellMarkerBehavior : MonoBehaviour
    {
        private ShopEncounterController _controller;
        private ItemPickupView _pickupView;

        public void Initialize(ShopEncounterController controller)
        {
            _controller = controller;
            _pickupView = GetComponent<ItemPickupView>();

            if (_pickupView != null)
                _pickupView.Used += OnUsed;
        }

        private void OnUsed(ItemPickupView pickup)
        {
            if (_controller.IsInSellMode)
                _controller.ExitSellMode();
            else
                _controller.EnterSellMode();
        }

        private void OnDestroy()
        {
            if (_pickupView != null)
                _pickupView.Used -= OnUsed;
        }
    }
}