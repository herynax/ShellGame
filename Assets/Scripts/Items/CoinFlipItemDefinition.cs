using FMODUnity;
using ShellGame.Audio;
using ShellGame.Core;
using ShellGame.Run;
using UnityEngine;

namespace ShellGame.Items
{
    [CreateAssetMenu(fileName = "CoinFlipItem", menuName = "ShellGame/Items/Coin Flip Item")]
    public sealed class CoinFlipItemDefinition : ItemDefinition
    {
        [Header("Префаб монеты для подбрасывания")]
        [Tooltip("3D-модель монетки с TrailRenderer и скриптом CoinFlipVisual")]
        public GameObject CoinVisualPrefab;

        [Header("Настройки броска")]
        public float TossDuration = 1.35f;
        public float TossHeight = 1.6f;
        [Range(0f, 15f)] public float CameraZoomFov = 7f;

        [Header("Звуки")]
        public EventReference TossSound;
        public EventReference LandSound;
        public EventReference WinSound;
        public EventReference LoseSound;

        public override bool CanUse(ItemEffectContext context)
        {
            // Предмет предназначен для использования игроком
            return context != null && context.UserSide == TurnSide.Player;
        }

        public override bool Apply(ItemEffectContext context)
        {
            if (!CanUse(context))
                return false;

            // 1. Детерминированный исход броска: решаем заранее
            bool isHeads = Random.value >= 0.5f;

            var coinPile = CoinPileController.Instance;
            Vector3 tableCenter = coinPile != null
                ? coinPile.GetCoinTossPosition(context.ItemWorldPosition)
                : context.ItemWorldPosition;
            Vector3 startPos = tableCenter + Vector3.up * 0.05f;
            Vector3 landPos = startPos;
            context.ItemWorldPosition = tableCenter;

            // 3. Создаём визуальную монетку
            GameObject coinObj = CoinVisualPrefab != null
                ? Instantiate(CoinVisualPrefab, startPos, Quaternion.identity)
                : GameObject.CreatePrimitive(PrimitiveType.Cylinder);

            var visual = coinObj.GetComponent<CoinFlipVisual>();
            if (visual == null)
                visual = coinObj.AddComponent<CoinFlipVisual>();

            // 4. Запускаем бросок
            visual.Launch(
                startPos,
                landPos,
                isHeads,
                TossDuration,
                TossHeight,
                CameraZoomFov,
                TossSound,
                LandSound,
                () => ResolveOutcome(isHeads, context));

            return true;
        }

        private void ResolveOutcome(bool isHeads, ItemEffectContext context)
        {
            var spawner = Object.FindFirstObjectByType<PlayerItemSpawner>();
            var runManager = RunManager.Instance;
            var playerInv = runManager != null && runManager.CurrentRun != null ? runManager.CurrentRun.PlayerInventory : null;
            ServiceLocator.TryGet(out IAudioService audioService);

            var messageView = Object.FindFirstObjectByType<ItemUseMessageView>();

            if (isHeads)
            {
                // ОРЁЛ: Получение нового предмета
                if (!WinSound.IsNull)
                    audioService?.PlayOneShot(WinSound, context.ItemWorldPosition);

                ItemDefinition rewardedItem = null;

                if (spawner != null && spawner.HasFreeSlot)
                {
                    // Берём случайный открытый предмет
                    var pool = spawner.GetUnlockedItemsPool();
                    // Исключаем саму монетку, чтобы не крутить бесконечно (опционально)
                    pool.RemoveAll(item => item == null || item == this);

                    if (pool.Count > 0)
                    {
                        rewardedItem = pool[Random.Range(0, pool.Count)];
                        spawner.SpawnNewItem(rewardedItem);
                    }
                }

                if (rewardedItem != null)
                {
                    messageView?.ShowMessage($"Орёл! Вы получили: {rewardedItem.DisplayName}!");
                }
                else
                {
                    messageView?.ShowMessage("Орёл! Но в инвентаре не нашлось свободного места.");
                }
            }
            else
            {
                // РЕШКА: Потеря случайного предмета
                if (!LoseSound.IsNull)
                    audioService?.PlayOneShot(LoseSound, context.ItemWorldPosition);

                ItemDefinition lostItem = null;
                if (spawner != null)
                {
                    spawner.RemoveRandomItem(out lostItem);
                }

                if (lostItem != null)
                {
                    messageView?.ShowMessage($"Решка! Вы потеряли предмет: {lostItem.DisplayName}...");
                }
                else
                {
                    messageView?.ShowMessage("Решка! Но терять было уже нечего...");
                }
            }
        }

        public override void ShowPlayerUseFeedback(ItemUseMessageView messageView)
        {
            // Сообщение покажется в момент приземления монеты
        }
    }
}