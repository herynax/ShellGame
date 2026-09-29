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
        [Tooltip("Мастер-список предметов для продажи. При каждом посещении выбирается случайное подмножество.")]
        public List<ShopItemEntry> BuyItemMasterList = new List<ShopItemEntry>();

        [Tooltip("Количество предметов для продажи при каждом посещении")]
        public int BuyItemsPerVisit = 4;

        [Header("Free Item on Entry")]
        [Tooltip("Пул предметов, из которых выдается один бесплатно при входе (если есть место)")]
        public List<ItemDefinition> FreeItemPool = new List<ItemDefinition>();

        [Header("Pricing")]
        [Range(0.1f, 1f), Tooltip("Множитель цены продажи: цена продажи = цена покупки * этот множитель")]
        public float SellPriceMultiplier = 0.5f;

        [Header("Table Zones")]
        [Tooltip("Зона (BoxCollider) где лежат монеты. Монеты констрейнятся внутри этой зоны.")]
        public BoxCollider CoinZone;

        [Tooltip("Позиция маркера продажи (крестик)")]
        public Transform SellZonePosition;

        [Tooltip("Позиция кнопки выхода 'Продолжить путь'")]
        public Transform ExitButtonPosition;
    }

    [System.Serializable]
    public class ShopItemEntry
    {
        [Tooltip("Предмет для продажи")]
        public ItemDefinition Item;

        [Tooltip("Цена покупки")]
        public int BuyPrice;

        [Tooltip("Макс. количество в наличии за одно посещение (-1 = безлимит)")]
        public int MaxStock = -1;
    }
}