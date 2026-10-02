using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShellGame.Core;
using ShellGame.Dialogue;
using ShellGame.Gameplay;
using ShellGame.Items;
using ShellGame.Meta;
using ShellGame.Shells;
using UnityEngine;

namespace ShellGame.Items
{
    public sealed class EnemyItemSpawner : BaseItemSpawner
    {
        public override TurnSide OwnerSide => TurnSide.Enemy;

        protected override List<ItemDefinition> GetAvailableItemPool()
        {
            // Use unlocks config to get all unlocked items
            if (_unlockManager != null && _unlocksConfig != null)
            {
                var unlocked = new List<ItemDefinition>();
                foreach (var entry in _unlocksConfig.Entries)
                {
                    if (entry.Item != null && _unlockManager.IsUnlocked(entry.Item))
                        unlocked.Add(entry.Item);
                }
                return unlocked;
            }

            return _fallbackAvailableItems;
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

                yield return EnemyReactionGate.WaitWhileBusy();

                if (itemSkippedTurn)
                {
                    result.SkippedTurn = true;
                    break;
                }

                if (bestContext.EnemyTurnResolvedByItem)
                {
                    result.TurnResolvedByItem = true;
                    break;
                }

                if (ShellHammerGate.IsPending || ShellKnifeGate.IsPending)
                    break;
            }

            _enemyLookController?.ResetLook();
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

        public void RestoreFromCheckpoint(List<ItemStackCheckpointData> enemyItems)
        {
            if (_hasSpawned) return;
            _hasSpawned = true;

            _spawnCounts.Clear();

            var points = GetPointsForSide(_itemCount);
            int pointIndex = 0;

            if (enemyItems != null)
            {
                foreach (var stack in enemyItems)
                {
                    var definition = ResolveItemByName(stack.ItemAssetName);
                    if (definition == null)
                    {
                        Debug.LogWarning($"[EnemyItemSpawner] Чекпоинт ссылается на неизвестный предмет '{stack.ItemAssetName}' — пропускаю.");
                        continue;
                    }

                    for (int i = 0; i < stack.Count; i++)
                    {
                        if (pointIndex >= points.Count)
                        {
                            Debug.LogWarning($"[EnemyItemSpawner] Не хватило точек спавна для восстановления всех предметов врага.");
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

            _enemyInventory.Add(definition);
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
    }
}