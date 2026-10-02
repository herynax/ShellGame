using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using FMODUnity;
using ShellGame.Audio;
using ShellGame.Core;
using ShellGame.Dialogue;
using ShellGame.Gameplay;
using ShellGame.Meta;
using ShellGame.Run;
using UnityEngine;
using Zenject;

using ItemStackCheckpointData = ShellGame.Meta.ItemStackCheckpointData;

namespace ShellGame.Items
{
    public abstract class BaseItemSpawner : MonoBehaviour
    {
        [Header("Spawn Points")]
        [SerializeField] protected List<ItemSpawnPoint> _spawnPoints = new List<ItemSpawnPoint>();
        [SerializeField] protected int _itemCount = -1;
        [SerializeField] protected EventReference _spawnSound;

        [Tooltip("Максимум одинаковых предметов, которые может получить ОДНА сторона за раздачу.")]
        [SerializeField, Min(1)] protected int _maxDuplicatesPerSide = 2;

        [Header("Анимация появления")]
        [SerializeField] protected float _spawnAnimationDuration = 0.3f;
        [SerializeField] protected float _spawnDelay = 1.2f;
        [SerializeField] protected Ease _spawnEase = Ease.OutBack;
        [SerializeField] protected ItemUseMessageView _playerUseMessage;
        [SerializeField] protected ItemUseMessageView _enemyUseMessage;
        [SerializeField] protected ShellGame.Feedback.EnemyLookController _enemyLookController;

        [Header("Fallback Items")]
        [Tooltip("Fallback-список — используется если UnlocksConfig/IUnlockManager не заинжектированы (например, тестовая сцена без DI-контейнера анлоков).")]
        [SerializeField] protected List<ItemDefinition> _fallbackAvailableItems = new List<ItemDefinition>();

        protected readonly List<GameObject> _spawnedItems = new List<GameObject>();
        protected readonly Dictionary<ItemDefinition, int> _spawnCounts = new Dictionary<ItemDefinition, int>();
        protected ItemInventory _playerInventory;
        protected ItemInventory _enemyInventory;
        protected GameManager _gameManager;
        protected IAudioService _audio;
        protected bool _hasSpawned;

        [InjectOptional] protected IUnlockManager _unlockManager = null;
        [InjectOptional] protected UnlocksConfig _unlocksConfig = null;

        public IUnlockManager UnlockManager => _unlockManager;
        public UnlocksConfig UnlocksConfigAsset => _unlocksConfig;

        public bool HasFinishedSpawning { get; protected set; }

        public abstract TurnSide OwnerSide { get; }
        public IReadOnlyList<GameObject> SpawnedItems => _spawnedItems;

        public void SetItemsAvailable(bool available)
        {
            _itemsAvailable = available;
        }

        [Inject]
        private void InjectDependencies(GameManager gameManager)
        {
            _gameManager = gameManager;
        }

        protected virtual void Awake()
        {
            ResolveSpawnPoints();
            _playerInventory = new ItemInventory(TurnSide.Player);
            _enemyInventory = new ItemInventory(TurnSide.Enemy);

            if (_enemyLookController == null)
                _enemyLookController = FindFirstObjectByType<ShellGame.Feedback.EnemyLookController>();

            if (!ServiceLocator.TryGet<IAudioService>(out _audio))
            {
                _audio = new FMODAudioService();
                ServiceLocator.Register(_audio);
            }
        }

        public List<ItemDefinition> GetOwnedItemDefinitions()
        {
            var result = new List<ItemDefinition>();
            foreach (var itemObject in _spawnedItems)
            {
                if (itemObject == null) continue;
                var pickup = itemObject.GetComponent<ItemPickupView>();
                if (pickup != null && pickup.Owner == OwnerSide && pickup.Item != null)
                    result.Add(pickup.Item);
            }
            return result;
        }

        public virtual IEnumerator SpawnItems()
        {
            if (_hasSpawned || !_itemsAvailable)
            {
                HasFinishedSpawning = true;
                yield break;
            }

            _hasSpawned = true;
            yield return null;

            var availableItems = GetAvailableItemPool();
            if (availableItems.Count == 0)
            {
                HasFinishedSpawning = true;
                yield break;
            }

            _spawnCounts.Clear();

            var points = GetPointsForSide(_itemCount);
            int stepCount = points.Count;

            for (int i = 0; i < stepCount; i++)
            {
                int spawnedCount = 0;
                Vector3 soundPosition = Vector3.zero;

                if (i < points.Count && SpawnItem(points[i], availableItems, _spawnCounts, out var position))
                {
                    soundPosition += position;
                    spawnedCount++;
                }

                if (spawnedCount > 0)
                {
                    if (!_spawnSound.IsNull)
                        _audio.PlayOneShot(_spawnSound, soundPosition);
                }

                if (_spawnDelay > 0f)
                    yield return new WaitForSeconds(_spawnDelay);
            }

            if (_spawnAnimationDuration > 0f)
                yield return new WaitForSeconds(_spawnAnimationDuration);

            HasFinishedSpawning = true;
        }

        public List<ItemSpawnPoint> GetPointsForSide(int requestedCount)
        {
            var points = new List<ItemSpawnPoint>();
            foreach (var point in _spawnPoints)
            {
                if (point != null && point.Owner == OwnerSide)
                    points.Add(point);
            }

            points.Sort((left, right) => left.Index.CompareTo(right.Index));

            if (requestedCount < 0)
            {
                var runManager = ShellGame.Run.RunManager.Instance;
                if (runManager != null && runManager.CurrentRun != null && runManager.CurrentRun.PlayerInventory != null)
                {
                    var playerInv = runManager.CurrentRun.PlayerInventory;
                    requestedCount = OwnerSide == TurnSide.Player ? playerInv.MaxPlayerSlots : playerInv.MaxEnemySlots;
                }
            }

            int count = requestedCount < 0 ? points.Count : Mathf.Min(requestedCount, points.Count);
            if (count < points.Count)
                points.RemoveRange(count, points.Count - count);
            return points;
        }

        protected abstract List<ItemDefinition> GetAvailableItemPool();

        protected virtual bool SpawnItem(ItemSpawnPoint point, List<ItemDefinition> availableItems, Dictionary<ItemDefinition, int> sideCounts, out Vector3 spawnPosition)
        {
            spawnPosition = point.SpawnPosition;
            var definition = PickDefinitionForSide(availableItems, sideCounts);
            if (definition == null || definition.WorldPrefab == null)
                return false;

            var itemObject = Instantiate(definition.WorldPrefab, spawnPosition, point.Rotation, transform);
            if (itemObject == null)
            {
                Debug.LogError($"[BaseItemSpawner] Не удалось создать WorldPrefab для предмета '{definition.name}'.", this);
                return false;
            }

            RotateVisualRandomly(itemObject.transform);
            ShellGame.Core.TableSurfacePlacement.PlaceObjectOnSurface(itemObject.transform, spawnPosition);

            var pickup = itemObject.GetComponent<ItemPickupView>();
            if (pickup == null)
                pickup = itemObject.AddComponent<ItemPickupView>();

            pickup.SetItem(definition);
            pickup.SetOwner(OwnerSide);
            pickup.Used += HandleItemUsed;
            _spawnedItems.Add(itemObject);

            sideCounts.TryGetValue(definition, out var currentCount);
            sideCounts[definition] = currentCount + 1;

            if (OwnerSide == TurnSide.Enemy)
                _enemyInventory.Add(definition);

            var baseScale = itemObject.transform.localScale;
            itemObject.transform.localScale = Vector3.zero;
            itemObject.transform.DOScale(baseScale, Mathf.Max(0f, _spawnAnimationDuration)).SetEase(_spawnEase);
            return true;
        }

        protected virtual void RotateVisualRandomly(Transform itemTransform)
        {
            foreach (var child in itemTransform.GetComponentsInChildren<Transform>(true))
            {
                if (child == itemTransform || child.name != "Visual")
                    continue;

                var angles = child.localEulerAngles;
                angles.y = UnityEngine.Random.Range(0f, 360f);
                child.localEulerAngles = angles;
                return;
            }
        }

        protected virtual void HandleItemUsed(ItemPickupView pickup)
        {
            if (pickup == null || pickup.Item == null)
                return;

            if (pickup.Owner != TurnSide.Player || _gameManager == null)
                return;

            var item = pickup.Item;
            var context = _gameManager.CreateItemContext(TurnSide.Player);

            var baseBeginShellPeek = context.BeginShellPeek;
            context.BeginShellPeek = (holdDuration, onPeeked) =>
            {
                baseBeginShellPeek?.Invoke(holdDuration, peekedShell =>
                {
                    _playerUseMessage?.ClearMessage();
                    onPeeked?.Invoke(peekedShell);
                });
            };

            if (!item.CanUse(context) || !item.Apply(context))
            {
                _playerUseMessage?.ShowUnavailable();
                return;
            }

            item.PlayUseFeedback(context, _audio, pickup.transform.position);
            item.ShowPlayerUseFeedback(_playerUseMessage);

            pickup.Used -= HandleItemUsed;
            _spawnedItems.Remove(pickup.gameObject);
            Destroy(pickup.gameObject);
        }

        protected virtual ItemDefinition PickDefinitionForSide(List<ItemDefinition> availableItems, Dictionary<ItemDefinition, int> sideCounts)
        {
            var underCap = new List<ItemDefinition>();
            foreach (var candidate in availableItems)
            {
                if (candidate == null) continue;
                sideCounts.TryGetValue(candidate, out var count);
                if (count < _maxDuplicatesPerSide)
                    underCap.Add(candidate);
            }

            if (underCap.Count > 0)
                return underCap[UnityEngine.Random.Range(0, underCap.Count)];

            ItemDefinition leastUsed = null;
            int leastUsedCount = int.MaxValue;
            foreach (var candidate in availableItems)
            {
                if (candidate == null) continue;
                sideCounts.TryGetValue(candidate, out var count);
                if (count < leastUsedCount)
                {
                    leastUsedCount = count;
                    leastUsed = candidate;
                }
            }

            return leastUsed;
        }

        public virtual void ResetForNewEncounter(ShellGame.Feedback.EnemyLookController enemyLook)
        {
            foreach (var item in _spawnedItems)
            {
                if (item == null) continue;
                item.transform.DOKill();
                Destroy(item);
            }
            _spawnedItems.Clear();
            _spawnCounts.Clear();
            _playerInventory = new ItemInventory(TurnSide.Player);
            _enemyInventory = new ItemInventory(TurnSide.Enemy);
            _hasSpawned = false;
            HasFinishedSpawning = false;
            _enemyLookController = enemyLook;
        }

        protected virtual ItemDefinition ResolveItemByName(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return null;

            if (_unlocksConfig != null)
            {
                foreach (var entry in _unlocksConfig.Entries)
                    if (entry.Item != null && entry.Item.name == assetName)
                        return entry.Item;
            }

            foreach (var item in _fallbackAvailableItems)
                if (item != null && item.name == assetName)
                    return item;

            return null;
        }

        protected bool _itemsAvailable = true;

        private void ResolveSpawnPoints()
        {
            if (_spawnPoints != null && _spawnPoints.Count > 0)
                return;

            _spawnPoints.Clear();
            _spawnPoints.AddRange(GetComponentsInChildren<ItemSpawnPoint>(true));
        }

        private void OnDestroy()
        {
            foreach (var item in _spawnedItems)
            {
                if (item != null)
                    item.transform.DOKill();
            }
        }
    }
}