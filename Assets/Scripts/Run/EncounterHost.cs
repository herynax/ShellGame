using System.Collections;
using ShellGame.AI;
using ShellGame.Core;
using ShellGame.Dialogue;
using ShellGame.Feedback;
using ShellGame.Gameplay;
using ShellGame.Health;
using ShellGame.Items;
using ShellGame.Shells;
using ShellGame.Tutorial;
using UnityEngine;
using Zenject;

namespace ShellGame.Run
{
    public sealed class EncounterHost : MonoBehaviour
    {
        [SerializeField] private Transform _rigParent;

        [SerializeField] private HealthSoundProvider _healthSoundProvider;

        [SerializeField] private GameObject _playerPos;

        [Inject] private DiContainer _container = null;
        [Inject] private GameManager _gameManager = null;
        [Inject] private GameSessionProgression _sessionProgression = null;
        [Inject] private PlayerDamageFeedback _playerFeedback = null;
        [Inject] private TurnSpotlightController _spotlight = null;

        private HealthController _health;
        [SerializeField] private HealthController _playerHealth;
        private EnemyAIController _enemyAI;
        private PlayerItemSpawner _playerItemSpawner;
        private EnemyItemSpawner _enemyItemSpawner;
        private RoundGenerator _roundGenerator;

        public ItemUseMessageView _itemUseMessageView;

        public Camera _interactCamera;

        public HealthController PlayerHealth => _playerHealth;

        public HealthSoundProvider HealthSoundProvider => _healthSoundProvider;

        public CoinPileController CoinPileController => CoinPileController.Instance;

        /// <summary>Зона монет текущего энкаунтера: в магазине — зона стола, иначе null.</summary>
        public BoxCollider CoinZone => _shopRig != null ? _shopRig.CoinZone : null;
        public GameObject PlayerPos => _playerPos;

        private EncounterRig _rig;
        private ShopEncounterRig _shopRig;
        private EnemyReactionDirector _director;
        private FirstEncounterItemSelector _firstEncounterSelector;
        private ShopEncounterController _shopController;

        public event System.Action OnExitRequested;

        private void Awake()
        {
            if (_interactCamera == null)
                _interactCamera = Camera.main;

            RunManager.EnsureExists();
            RunManager.Instance.RegisterHost(this);
        }

        public IEnumerator EnterRoutine(EncounterDefinition def)
        {
            yield return ExitRoutine();

            // Статические шлюзы переживают энкаунтер — чистим.
            ShellPeekGate.Cancel();
            ShellKnifeGate.Cancel();
            ShellHammerGate.Cancel();
            GameplayGate.Clear();
            SceneTransitionGate.Clear();
            TutorialSceneTransitionGate.HoldEnemyDeathTransition = false;

            // Магазин — не бой: у него нет EncounterRig, спавнеров врага и раундов.
            // Идёт отдельной веткой, иначе InstantiatePrefabForComponent<EncounterRig>
            // вернул бы null и всё ниже развалилось бы.
            if (def.Kind == EncounterKind.Shop)
            {
                EnterShop(def);
                yield break;
            }

            _rig = _container.InstantiatePrefabForComponent<EncounterRig>(def.RigPrefab, _rigParent);
            yield return null; // Awake/OnEnable рига

            _enemyAI = _rig.EnemyAI;
            _health = _rig.Health;
            _playerItemSpawner = _rig.PlayerItemSpawner;
            _enemyItemSpawner = _rig.EnemyItemSpawner;
            _roundGenerator = _rig.RoundGenerator;

            _health.BindEnemyFeedback(_rig.EnemyFeedback);
            _playerFeedback.BindRig(_rig);
            _playerItemSpawner.ResetForNewEncounter(_rig.EnemyLook);
            _enemyItemSpawner.ResetForNewEncounter(_rig.EnemyLook);
            _enemyAI.ResetForNewEncounter(
                def.EnemyAIConfig,
                def.Kind,
                _sessionProgression != null ? _sessionProgression.SelectedPreset : null);

            // Всё, что живёт в риге, раздаём тем, кто остался в сцене,
            // ДО BeginEncounter (там вызывается SetImmediate и StartRound).
            _gameManager.BindRig(_rig);
            _spotlight.SetTurnIndicator(_rig.TurnIndicator);

            // Директор реплик нужен ДО BeginEncounter: в Awake он занимает
            // EnemyReactionGate, и GameManager не даст начать раунд, пока враг
            // не договорит вступление.
            if (def.ReactionConfig != null)
                _director = EnemyReactionBootstrap.CreateDirector(def.ReactionConfig);

            // Check for shop controller
            _shopController = _rig.GetComponentInChildren<ShopEncounterController>(true);
            if (_shopController != null)
            {
                _shopController.OnShopExited += RequestExit;
            }

            // Check for first encounter item selection
            bool isFirstEncounter = IsFirstEncounter(def);
            if (isFirstEncounter && def.FirstEncounterConfig != null)
            {
                _firstEncounterSelector = _rig.GetComponentInChildren<FirstEncounterItemSelector>(true);
                if (_firstEncounterSelector != null)
                {
                    _firstEncounterSelector.OnSelectionComplete += () => StartEncounter(def);
                    _firstEncounterSelector.StartSelection();
                    yield break; // Wait for selection to complete
                }
            }

            StartEncounter(def);
        }

        private bool IsFirstEncounter(EncounterDefinition def)
        {
            if (def.Kind != EncounterKind.Enemy) return false;

            var runManager = RunManager.Instance;
            if (runManager == null || runManager.CurrentRun == null) return false;

            // First encounter if no nodes completed yet (first enemy node)
            return runManager.CurrentRun.MapState.CompletedNodeIds.Count == 0;
        }

        private void StartEncounter(EncounterDefinition def)
        {
            _gameManager.BeginEncounter(def.Id, def.Kind == EncounterKind.Tutorial);
        }

        /// <summary>
        /// Вход в магазин. Боевых систем нет: спавнер врага, раунды и здоровье пропускаем.
        /// Куча монет переезжает на зону стола сама, в ShopEncounterController.
        /// </summary>
        private void EnterShop(EncounterDefinition def)
        {
            if (def.RigPrefab == null)
            {
                Debug.LogError($"[EncounterHost] У магазина '{def.Id}' не задан RigPrefab.");
                return;
            }

            _shopRig = _container.InstantiatePrefabForComponent<ShopEncounterRig>(def.RigPrefab, _rigParent);
            if (_shopRig == null)
            {
                Debug.LogError($"[EncounterHost] На префабе магазина '{def.RigPrefab.name}' нет компонента ShopEncounterRig.");
                return;
            }

            _shopController = _shopRig.ShopController;
            if (_shopController == null)
                _shopController = _shopRig.GetComponentInChildren<ShopEncounterController>(true);

            if (_shopController != null)
                _shopController.OnShopExited += RequestExit;

            StartEncounter(def);
        }

        public void RequestExit()
        {
            OnExitRequested?.Invoke();
        }

        public IEnumerator ExitRoutine()
        {
            if (_director != null)
            {
                Destroy(_director.gameObject);
                _director = null;
            }

            if (_shopController != null)
            {
                _shopController.OnShopExited -= RequestExit;
                _shopController = null;
            }

            if (_shopRig != null)
            {
                // Куча монет персистентная — возвращаем её на боевую зону сцены
                // (собственный коллайдер объекта CoinPile), иначе после выхода
                // из магазина монеты считали бы границы стола.
                if (CoinPileController.Instance != null)
                {
                    var combatZone = CoinPileController.Instance.GetComponent<BoxCollider>();
                    if (combatZone != null)
                        CoinPileController.Instance.CoinZone = combatZone;
                }

                Destroy(_shopRig.gameObject);
                _shopRig = null;
            }

            if (_rig == null) yield break;

            _gameManager.StopEncounter();
            _roundGenerator?.ClearRound();
            _playerItemSpawner?.ResetForNewEncounter(null);
            _enemyItemSpawner?.ResetForNewEncounter(null);
            _gameManager.BindRig(null);
            _spotlight.SetTurnIndicator(null);

            Destroy(_rig.gameObject);
            _rig = null;
            _enemyAI = null;
            _health = null;
            _playerItemSpawner = null;
            _enemyItemSpawner = null;
            _roundGenerator = null;

            // Destroy отложенный: без кадра новый HealthSoundProvider/ItemVisualAnchors
            // увидят старый Instance и уничтожат сами себя. Заодно директор
            // успевает отработать OnDisable и отпустить свои гейты.
            yield return null;
        }
    }
}