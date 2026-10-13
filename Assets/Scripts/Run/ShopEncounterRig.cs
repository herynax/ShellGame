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
    /// - PlayerItemSpawner: доступ к анлокам (предметы игрока раскладывает ShopItemSpawner)
    /// - ShopItemSpawner: товары магазина и предметы игрока на столе
    /// - ShopEncounterController: логика магазина
    /// - CoinPileController: монеты (persistent, обычная зона кучи)
    /// - Позиции кнопки выхода и маркера продажи, точка магазинщика
    /// </summary>
    public sealed class ShopEncounterRig : MonoBehaviour
    {
        [Header("Core Systems")]
        [SerializeField] private PlayerItemSpawner _playerItemSpawner;
        [SerializeField] private ShopItemSpawner _shopItemSpawner;
        [SerializeField] private ShopEncounterController _shopController;
        [SerializeField] private EncounterHost _encounterHost;

        [Header("Table Zones")]
        [SerializeField] private Transform _exitButtonPosition;
        [SerializeField] private Transform _sellZonePosition;

        [Header("Visual")]
        [SerializeField] private GameObject _merchantVisual;
        [SerializeField] private Transform _merchantLookTarget;

        // Геттеры сами ищут компоненты: порядок Awake у рига и контроллера не гарантирован.
        public PlayerItemSpawner PlayerItemSpawner
        {
            get
            {
                if (_playerItemSpawner == null)
                    _playerItemSpawner = GetComponentInChildren<PlayerItemSpawner>(true);
                return _playerItemSpawner;
            }
        }

        public ShopItemSpawner ShopItemSpawner
        {
            get
            {
                if (_shopItemSpawner == null)
                    _shopItemSpawner = GetComponentInChildren<ShopItemSpawner>(true);
                return _shopItemSpawner;
            }
        }

        public ShopEncounterController ShopController
        {
            get
            {
                if (_shopController == null)
                    _shopController = GetComponentInChildren<ShopEncounterController>(true);
                return _shopController;
            }
        }

        public Transform ExitButtonPosition => _exitButtonPosition;
        public Transform SellZonePosition => _sellZonePosition;
        public GameObject MerchantVisual => _merchantVisual;
        public Transform MerchantLookTarget => _merchantLookTarget;

        private void Awake()
        {
            _encounterHost = GetComponentInParent<EncounterHost>();

            // Прогреваем авто-поиск.
            _ = PlayerItemSpawner;
            _ = ShopItemSpawner;
            _ = ShopController;
        }
    }
}