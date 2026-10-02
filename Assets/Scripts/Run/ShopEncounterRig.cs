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
    /// Риг для энкаунтера магазина.
    /// - PlayerItemSpawner для предметов игрока (постоянные)
    /// - ShopItemSpawner для товаров магазина
    /// - ShopEncounterController для логики магазина
    /// - CoinPileController для монет (persistent)
    /// - Зоны для монет, продажи и выхода
    /// </summary>
    public sealed class ShopEncounterRig : MonoBehaviour
    {
        [Header("Core Systems")]
        [SerializeField] private PlayerItemSpawner _playerItemSpawner;
        [SerializeField] private ShopItemSpawner _shopItemSpawner;
        [SerializeField] private ShopEncounterController _shopController;
        [SerializeField] private EncounterHost _encounterHost;

        [Header("Table Zones")]
        [Tooltip("Зона монет в магазине. На неё переезжает персистентная куча CoinPileController. Должна быть назначена явно.")]
        [SerializeField] private BoxCollider _coinZone;
        [SerializeField] private Transform _sellZonePosition;
        [SerializeField] private Transform _exitButtonPosition;

        [Header("Visual")]
        [SerializeField] private GameObject _merchantVisual;
        [SerializeField] private Transform _merchantLookTarget;

        public PlayerItemSpawner PlayerItemSpawner => _playerItemSpawner;
        public ShopItemSpawner ShopItemSpawner => _shopItemSpawner;
        public ShopEncounterController ShopController => _shopController;
        public BoxCollider CoinZone => _coinZone;
        public Transform SellZonePosition => _sellZonePosition;
        public Transform ExitButtonPosition => _exitButtonPosition;
        public GameObject MerchantVisual => _merchantVisual;
        public Transform MerchantLookTarget => _merchantLookTarget;

        private void Awake()
        {
            _encounterHost = GetComponentInParent<EncounterHost>();

            // Auto-find components if not set
            if (_playerItemSpawner == null)
                _playerItemSpawner = GetComponentInChildren<PlayerItemSpawner>(true);
            if (_shopItemSpawner == null)
                _shopItemSpawner = GetComponentInChildren<ShopItemSpawner>(true);
            if (_shopController == null)
                _shopController = GetComponentInChildren<ShopEncounterController>(true);

            // _coinZone намеренно НЕ ищется автоматически: GetComponentInChildren<BoxCollider>
            // может подхватить любой коллайдер стола (например зону предметов) и куча монет
            // уедет не туда. Зона назначается в инспекторе на объект с именем CoinZone.
        }
    }
}