using ShellGame.AI;
using ShellGame.Core;
using ShellGame.Feedback;
using ShellGame.Gameplay;
using ShellGame.Health;
using ShellGame.Items;
using ShellGame.Shells;
using Unity.Cinemachine;
using UnityEngine;

namespace ShellGame.Run
{
    /// <summary>
    /// Риг для энкаунтера магазина. Минимальный набор компонентов:
    /// - ItemSpawner для отображения товаров на столе
    /// - ShopEncounterController для логики магазина
    /// - CoinPileController для монет
    /// - Зоны для монет, продажи и выхода
    /// </summary>
    public sealed class ShopEncounterRig : MonoBehaviour
    {
        [Header("Core Systems")]
        [SerializeField] private ItemSpawner _itemSpawner;
        [SerializeField] private ShopEncounterController _shopController;
        [SerializeField] private CoinPileController _coinPileController;

        [Header("Table Zones")]
        [SerializeField] private BoxCollider _coinZone;
        [SerializeField] private Transform _sellZonePosition;
        [SerializeField] private Transform _exitButtonPosition;

        [Header("Visual")]
        [SerializeField] private GameObject _merchantVisual;
        [SerializeField] private Transform _merchantLookTarget;

        public ItemSpawner ItemSpawner => _itemSpawner;
        public ShopEncounterController ShopController => _shopController;
        public CoinPileController CoinPileController => _coinPileController;
        public BoxCollider CoinZone => _coinZone;
        public Transform SellZonePosition => _sellZonePosition;
        public Transform ExitButtonPosition => _exitButtonPosition;
        public GameObject MerchantVisual => _merchantVisual;
        public Transform MerchantLookTarget => _merchantLookTarget;

        private void Awake()
        {
            // Auto-find components if not set
            if (_itemSpawner == null)
                _itemSpawner = GetComponentInChildren<ItemSpawner>(true);
            if (_shopController == null)
                _shopController = GetComponentInChildren<ShopEncounterController>(true);
            if (_coinPileController == null)
                _coinPileController = GetComponentInChildren<CoinPileController>(true);
            if (_coinZone == null)
                _coinZone = GetComponentInChildren<BoxCollider>(true);
        }
    }
}