using System.Collections.Generic;
using UnityEngine;
using ShellGame.Dialogue;
using ShellGame.Items;

namespace ShellGame.Run
{
    [CreateAssetMenu(fileName = "ShopConfig", menuName = "ShellGame/Run/Shop Config")]
    public class ShopConfig : ScriptableObject
    {
        [Header("Merchant Dialogue")]
        [Tooltip("Реплики торговца (использует систему EnemyReactionConfig)")]
        public EnemyReactionConfig MerchantDialogue;

        [Header("Shop Inventory")]
        [Tooltip("Использовать разблокированные предметы из UnlocksConfig вместо ручного списка")]
        public bool UseUnlockedItems = true;

        [Tooltip("Макс. предметов в магазине за посещение (если UseUnlockedItems)")]
        public int MaxShopItemsPerVisit = 4;

        [Tooltip("Только предметы с BuyPrice > 0 в магазине")]
        public bool OnlySellableItems = true;

        [Header("Free Item on Entry")]
        [Tooltip("Выдавать бесплатный предмет при входе из разблокированных")]
        public bool GrantFreeUnlockedItemOnEntry = true;

        [Header("Pricing")]
        [Tooltip("Множитель цены продажи (от цены покупки). 0.5 = продажа за половину цены. Переопределяется ItemDefinition.SellPriceMultiplier")]
        public float SellPriceMultiplier = 0.5f;

        [Header("Sell Marker")]
        [Tooltip("Префаб UIMarkerItemDefinition для маркера продажи. Позиции зон берутся из ShopEncounterRig.")]
        public UIMarkerItemDefinition SellMarkerItemAsset;
    }
}