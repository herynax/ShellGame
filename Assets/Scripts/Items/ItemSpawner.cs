// ItemSpawner.cs
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using FMODUnity;
using ShellGame.Audio;
using ShellGame.Core;
using ShellGame.Gameplay;
using ShellGame.Meta;
using ShellGame.Shells;
using UnityEngine;
using Zenject;

// Явный алиас, чтобы типы внутри ShellGame.Items не перекрывали тип из ShellGame.Meta
using ItemStackCheckpointData = ShellGame.Meta.ItemStackCheckpointData;

namespace ShellGame.Items
{
    public sealed class ItemSpawner : MonoBehaviour
    {
        [Header("Предметы")]
        [Tooltip("Если выключено, предметы игрока и врага не будут появляться на сцене.")]
        [SerializeField] private bool _itemsAvailable = true;

        [Tooltip("Fallback-список — используется ТОЛЬКО если UnlocksConfig/IUnlockManager не заинжектированы (например, тестовая сцена без DI-контейнера анлоков). В обычной игре пул предметов берётся из UnlocksConfig через IUnlockManager — см. ResolveAvailableItems().")]
        [SerializeField] private List<ItemDefinition> _fallbackAvailableItems = new List<ItemDefinition>();

        [SerializeField] private List<ItemSpawnPoint> _spawnPoints = new List<ItemSpawnPoint>();
        [SerializeField] private int _playerItemCount = -1;
        [SerializeField] private int _enemyItemCount = -1;
        [SerializeField] private EventReference _spawnSound;

        [Tooltip("Максимум одинаковых предметов, которые может получить ОДНА сторона за раздачу.")]
        [SerializeField, Min(1)] private int _maxDuplicatesPerSide = 2;

        [Header("Анимация появления")]
        [SerializeField] private float _spawnAnimationDuration = 0.3f;
        [SerializeField] private float _spawnDelay = 1.2f;
        [SerializeField] private Ease _spawnEase = Ease.OutBack;
        [SerializeField] private ItemUseMessageView _playerUseMessage;
        [SerializeField] private ItemUseMessageView _enemyUseMessage;
        [SerializeField] private ShellGame.Feedback.EnemyLookController _enemyLookController;

        private readonly List<GameObject> _spawnedItems = new List<GameObject>();
        private readonly Dictionary<ItemDefinition, int> _playerSpawnCounts = new Dictionary<ItemDefinition, int>();
        private readonly Dictionary<ItemDefinition, int> _enemySpawnCounts = new Dictionary<ItemDefinition, int>();
        private ItemInventory _playerInventory;
        private ItemInventory _enemyInventory;
        private GameManager _gameManager;
        private IAudioService _audio;
        private bool _hasSpawned;

        [InjectOptional] private IUnlockManager _unlockManager;
        [InjectOptional] private UnlocksConfig _unlocksConfig;

        public bool HasFinishedSpawning { get; private set; }


        /// <summary>
        /// Принудительно включает/выключает спавн предметов на сцене — например,
        /// чтобы включить предметы на первом уровне после того, как игрок хотя
        /// бы раз загрузился на втором уровне (см. GameManager.AreFirstLevelItemsUnlocked).
        /// Вызывать ДО SpawnItems()/RestoreFromCheckpoint() — после того как
        /// _hasSpawned станет true, вызов уже ни на что не повлияет.
        /// </summary>
        public void SetItemsAvailable(bool available)
        {
            _itemsAvailable = available;
        }

        [Inject]
        private void InjectDependencies(GameManager gameManager)
        {
            _gameManager = gameManager;
        }

        public ItemInventory PlayerInventory => _playerInventory;
        public ItemInventory EnemyInventory => _enemyInventory;

        private void Awake()
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

        /// <summary>
        /// Пул предметов, из которого сейчас можно спавнить. Если анлоки
        /// подключены (UnlocksConfig + IUnlockManager заинжектированы) —
        /// берёт только РАЗБЛОКИРОВАННЫЕ на данный момент предметы из
        /// конфига (UnlocksConfig — единственный источник правды о том,
        /// какие предметы вообще существуют в игре). Fallback-список нужен
        /// только для сцен/тестов без DI-контейнера анлоков.
        /// </summary>
        private List<ItemDefinition> ResolveAvailableItems()
        {
            if (_unlockManager != null && _unlocksConfig != null)
            {
                var unlocked = new List<ItemDefinition>();
                foreach (var entry in _unlocksConfig.Entries)
                {
                    if (entry.Item != null && _unlockManager.IsUnlocked(entry.Item))
                        unlocked.Add(entry.Item);
                }

                if (unlocked.Count > 0)
                    return unlocked;

                Debug.LogWarning("[ItemSpawner] UnlocksConfig подключён, но ни один предмет не разблокирован — проверь UnlockedByDefault на стартовых предметах.", this);
            }

            return _fallbackAvailableItems;
        }


        private List<ItemDefinition> GetFinalItemPool()
        {
            var finalPool = new List<ItemDefinition>();
            
            // ВАЖНО: используем UnlocksConfig как единственный источник всех предметов
            foreach (var entry in _unlocksConfig.Entries)
            {
                ItemDefinition item = entry.Item;
                if (item == null) continue;

                bool unlocked = _unlockManager.IsUnlocked(item);

                if (unlocked)
                {
                    finalPool.Add(item);
                }
            }
            return finalPool;
        }

        public IEnumerator SpawnItems()
        {
            if (_hasSpawned || !_itemsAvailable)
            {
                HasFinishedSpawning = true;
                yield break;
            }

            _hasSpawned = true;

            yield return null; 

            // Получаем актуальный пул предметов прямо сейчас
            var availableItems = GetFinalItemPool();
            
            
            if (availableItems.Count == 0)
            {
                HasFinishedSpawning = true;
                yield break;
            }

            _playerSpawnCounts.Clear();
            _enemySpawnCounts.Clear();

            var playerPoints = GetPointsForSide(TurnSide.Player, _playerItemCount);
            var enemyPoints = GetPointsForSide(TurnSide.Enemy, _enemyItemCount);
            int stepCount = Mathf.Max(playerPoints.Count, enemyPoints.Count);

            for (int i = 0; i < stepCount; i++)
            {
                int spawnedCount = 0;
                Vector3 soundPosition = Vector3.zero;

                if (i < playerPoints.Count && SpawnItem(playerPoints[i], TurnSide.Player, availableItems, _playerSpawnCounts, out var playerPosition))
                {
                    soundPosition += playerPosition;
                    spawnedCount++;
                }

                if (i < enemyPoints.Count && SpawnItem(enemyPoints[i], TurnSide.Enemy, availableItems, _enemySpawnCounts, out var enemyPosition))
                {
                    soundPosition += enemyPosition;
                    spawnedCount++;
                }

                if (spawnedCount > 0)
                {
                    soundPosition /= spawnedCount;
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

        private List<ItemSpawnPoint> GetPointsForSide(TurnSide side, int requestedCount)
        {
            var points = new List<ItemSpawnPoint>();
            foreach (var point in _spawnPoints)
            {
                if (point != null && point.Owner == side)
                    points.Add(point);
            }

            points.Sort((left, right) => left.Index.CompareTo(right.Index));
            int count = requestedCount < 0 ? points.Count : Mathf.Min(requestedCount, points.Count);
            if (count < points.Count)
                points.RemoveRange(count, points.Count - count);
            return points;
        }

        /// <summary>Все ItemDefinition, которые СЕЙЧАС физически лежат на столе у этой стороны (по заспавненным ItemPickupView, не по _playerInventory/_enemyInventory — у игрока эти счётчики не ведутся, предметы читаются прямо из мира).</summary>
        public List<ItemDefinition> GetOwnedItemDefinitions(TurnSide side)
        {
            var result = new List<ItemDefinition>();
            foreach (var itemObject in _spawnedItems)
            {
                if (itemObject == null) continue;
                var pickup = itemObject.GetComponent<ItemPickupView>();
                if (pickup != null && pickup.Owner == side && pickup.Item != null)
                    result.Add(pickup.Item);
            }
            return result;
        }

        /// <summary>
        /// Восстанавливает предметы на столе из чекпоинта вместо обычного
        /// случайного спавна — вызывается ДО того, как WaitForStart дойдёт до
        /// SpawnItems() (которая после этого просто увидит _hasSpawned=true и
        /// молча ничего не сделает).
        /// </summary>
        public void RestoreFromCheckpoint(List<ShellGame.Meta.ItemStackCheckpointData> playerItems, List<ShellGame.Meta.ItemStackCheckpointData> enemyItems)
        {
            if (_hasSpawned) return;
            _hasSpawned = true;

            _playerSpawnCounts.Clear();
            _enemySpawnCounts.Clear();

            var playerPoints = GetPointsForSide(TurnSide.Player, _playerItemCount);
            var enemyPoints = GetPointsForSide(TurnSide.Enemy, _enemyItemCount);

            SpawnRestoredStacks(playerItems, playerPoints, TurnSide.Player, _playerSpawnCounts);
            SpawnRestoredStacks(enemyItems, enemyPoints, TurnSide.Enemy, _enemySpawnCounts);

            HasFinishedSpawning = true;
        }

        private void SpawnRestoredStacks(List<ShellGame.Meta.ItemStackCheckpointData> stacks, List<ItemSpawnPoint> points, TurnSide side, Dictionary<ItemDefinition, int> sideCounts)
        {
            if (stacks == null) return;

            int pointIndex = 0;
            foreach (var stack in stacks)
            {
                var definition = ResolveItemByName(stack.ItemAssetName);
                if (definition == null)
                {
                    Debug.LogWarning($"[ItemSpawner] Чекпоинт ссылается на неизвестный предмет '{stack.ItemAssetName}' — пропускаю.");
                    continue;
                }

                for (int i = 0; i < stack.Count; i++)
                {
                    if (pointIndex >= points.Count)
                    {
                        Debug.LogWarning($"[ItemSpawner] Не хватило точек спавна для восстановления всех предметов стороны {side}.");
                        return;
                    }

                    SpawnSpecificItem(points[pointIndex], side, definition, sideCounts);
                    pointIndex++;
                }
            }
        }

        private ItemDefinition ResolveItemByName(string assetName)
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

        private void SpawnSpecificItem(ItemSpawnPoint point, TurnSide owner, ItemDefinition definition, Dictionary<ItemDefinition, int> sideCounts)
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
            pickup.SetOwner(owner);
            pickup.Used += HandleItemUsed;
            _spawnedItems.Add(itemObject);

            sideCounts.TryGetValue(definition, out var count);
            sideCounts[definition] = count + 1;

            if (owner == TurnSide.Enemy)
                _enemyInventory.Add(definition);

            // Без анимации "появления из ниоткуда" — это восстановление, предмет
            // просто уже лежит на столе, а не появляется впервые.
        }

        /// <summary>
        /// Случайный предмет для точки спавна среди тех, которых на ЭТОЙ
        /// стороне ещё меньше _maxDuplicatesPerSide. Если пул настолько
        /// мал, что лимит физически не соблюсти (например, разблокировано
        /// всего 1-2 предмета на много точек спавна) — берёт наименее
        /// заспавненный, чтобы не оставить точку пустой.
        /// </summary>
        private ItemDefinition PickDefinitionForSide(List<ItemDefinition> availableItems, Dictionary<ItemDefinition, int> sideCounts)
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
                return underCap[Random.Range(0, underCap.Count)];

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

        private bool SpawnItem(ItemSpawnPoint point, TurnSide owner, List<ItemDefinition> availableItems, Dictionary<ItemDefinition, int> sideCounts, out Vector3 spawnPosition)
        {
            spawnPosition = point.SpawnPosition;
            var definition = PickDefinitionForSide(availableItems, sideCounts);
            if (definition == null || definition.WorldPrefab == null)
                return false;

            var itemObject = Instantiate(definition.WorldPrefab, spawnPosition, point.Rotation, transform);
            if (itemObject == null)
            {
                Debug.LogError($"[ItemSpawner] Не удалось создать WorldPrefab для предмета '{definition.name}'.", this);
                return false;
            }

            RotateVisualRandomly(itemObject.transform);
            ShellGame.Core.TableSurfacePlacement.PlaceObjectOnSurface(itemObject.transform, spawnPosition);

            var pickup = itemObject.GetComponent<ItemPickupView>();
            if (pickup == null)
                pickup = itemObject.AddComponent<ItemPickupView>();

            pickup.SetItem(definition);
            pickup.SetOwner(owner);
            pickup.Used += HandleItemUsed;
            _spawnedItems.Add(itemObject);

            sideCounts.TryGetValue(definition, out var currentCount);
            sideCounts[definition] = currentCount + 1;

            if (owner == TurnSide.Enemy)
                _enemyInventory.Add(definition);

            var baseScale = itemObject.transform.localScale;
            itemObject.transform.localScale = Vector3.zero;
            itemObject.transform.DOScale(baseScale, Mathf.Max(0f, _spawnAnimationDuration)).SetEase(_spawnEase);
            return true;
        }

        private static void RotateVisualRandomly(Transform itemTransform)
        {
            foreach (var child in itemTransform.GetComponentsInChildren<Transform>(true))
            {
                if (child == itemTransform || child.name != "Visual")
                    continue;

                var angles = child.localEulerAngles;
                angles.y = Random.Range(0f, 360f);
                child.localEulerAngles = angles;
                return;
            }
        }

        private void HandleItemUsed(ItemPickupView pickup)
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

        public IEnumerator TryUseEnemyItemsRoutine(GameManager gameManager, float difficultyIndex, EnemyItemUseResult result)
        {
            if (_enemyInventory == null || gameManager == null || _enemyInventory.Snapshot.Count == 0)
                yield break;

            int safetyGuard = 8;

            while (safetyGuard-- > 0 && _enemyInventory.Snapshot.Count > 0)
            {
                var probeContext = gameManager.CreateItemContext(TurnSide.Enemy);
                var enemyAI = probeContext.EnemyAI;
                float desireThreshold = enemyAI != null ? enemyAI.GetItemUseDesireThreshold(difficultyIndex) : 0.5f;

                ItemDefinition bestItem = null;
                ItemEffectContext bestContext = null;
                GameObject bestItemObject = null;
                float bestDesire = desireThreshold;
                var candidatePositions = new List<Vector3>();

                foreach (var itemObject in _spawnedItems)
                {
                    if (itemObject == null) continue;

                    var pickup = itemObject.GetComponent<ItemPickupView>();
                    if (pickup == null || pickup.Owner != TurnSide.Enemy || pickup.Item == null)
                        continue;

                    candidatePositions.Add(itemObject.transform.position);

                    var context = gameManager.CreateItemContext(TurnSide.Enemy);
                    if (!pickup.Item.CanUse(context))
                        continue;

                    float desire = pickup.Item.EvaluateEnemyDesire(context);
                    if (desire < bestDesire)
                        continue;

                    bestDesire = desire;
                    bestItem = pickup.Item;
                    bestContext = context;
                    bestItemObject = itemObject;
                }

                if (bestItem == null)
                    break;

                float thinkingDuration = probeContext.EnemyAI != null
                    ? probeContext.EnemyAI.GetItemUseThinkingDuration()
                    : 0f;
                if (thinkingDuration > 0f)
                    yield return new WaitForSeconds(thinkingDuration);

                if (_enemyLookController != null && bestItemObject != null)
                    yield return _enemyLookController.PlayConsidering(candidatePositions, bestItemObject.transform.position);

                bool itemSkippedTurn = false;
                bestContext.SkipCurrentTurn = () => itemSkippedTurn = true;

                if (!_enemyInventory.TryUse(bestItem, bestContext))
                    break;

                result.UsedAnything = true;
                var worldPosition = RemoveSpawnedEnemyItem(bestItem);
                bestItem.PlayUseFeedback(bestContext, _audio, worldPosition);
                _enemyUseMessage?.ShowMessage(bestItem.GetEnemyUseAnnouncement());
                result.ExtraDelaySeconds += Mathf.Max(0f, bestContext.ConsumedExtraDelay);

                if (itemSkippedTurn)
                {
                    result.SkippedTurn = true;
                    break;
                }

                // НОВОЕ: если предмет сам разрешил ход (сейчас — Нож), дальше в этом ходу
                // использовать предметы нельзя — ход уже "потрачен" на реальный выбор.
                if (bestContext.EnemyTurnResolvedByItem)
                {
                    result.TurnResolvedByItem = true;
                    break;
                }
            }

            _enemyLookController?.ResetLook();
        }

        public IEnumerator PlayEnemyLookAtShells(IReadOnlyList<Shell> shells)
        {
            if (_enemyLookController == null || shells == null)
                yield break;

            var shellPositions = new List<Vector3>();
            foreach (var shell in shells)
            {
                if (shell != null)
                    shellPositions.Add(shell.transform.position);
            }

            yield return _enemyLookController.PlayAtTargets(shellPositions);
        }

        private Vector3 RemoveSpawnedEnemyItem(ItemDefinition item)
        {
            foreach (var itemObject in new List<GameObject>(_spawnedItems))
            {
                if (itemObject == null)
                    continue;

                var pickup = itemObject.GetComponent<ItemPickupView>();
                if (pickup == null || pickup.Owner != TurnSide.Enemy || pickup.Item != item)
                    continue;

                var position = itemObject.transform.position;
                pickup.Used -= HandleItemUsed;
                _spawnedItems.Remove(itemObject);
                Destroy(itemObject);
                return position;
            }

            return transform.position;
        }

        private ItemInventory GetInventory(TurnSide side) => side == TurnSide.Player ? _playerInventory : _enemyInventory;

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