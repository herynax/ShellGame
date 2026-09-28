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

        [Inject] private DiContainer _container = null;
        [Inject] private GameManager _gameManager = null;
        [Inject] private PlayerDamageFeedback _playerFeedback = null;
        [Inject] private TurnSpotlightController _spotlight = null;

        private HealthController _health;
        [SerializeField] private HealthController _playerHealth;
        private EnemyAIController _enemyAI;
        private ItemSpawner _items;
        private RoundGenerator _roundGenerator;

        public ItemUseMessageView _itemUseMessageView;

        public Camera _interactCamera;

        public HealthController PlayerHealth => _playerHealth;

        public HealthSoundProvider HealthSoundProvider => _healthSoundProvider;

        private EncounterRig _rig;
        private EnemyReactionDirector _director;

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

            _rig = _container.InstantiatePrefabForComponent<EncounterRig>(def.RigPrefab, _rigParent);
            yield return null; // Awake/OnEnable рига

            _enemyAI = _rig.EnemyAI;
            _health = _rig.Health;
            _items = _rig.ItemSpawner;
            _roundGenerator = _rig.RoundGenerator;

            _health.BindEnemyFeedback(_rig.EnemyFeedback);
            _playerFeedback.BindRig(_rig);
            _items.ResetForNewEncounter(_rig.EnemyLook);
            _enemyAI.ResetForNewEncounter(def.EnemyAIConfig);

            // Всё, что живёт в риге, раздаём тем, кто остался в сцене,
            // ДО BeginEncounter (там вызывается SetImmediate и StartRound).
            _gameManager.BindRig(_rig);
            _spotlight.SetTurnIndicator(_rig.TurnIndicator);

            // Директор реплик нужен ДО BeginEncounter: в Awake он занимает
            // EnemyReactionGate, и GameManager не даст начать раунд, пока враг
            // не договорит вступление.
            if (def.ReactionConfig != null)
                _director = EnemyReactionBootstrap.CreateDirector(def.ReactionConfig);

            _gameManager.BeginEncounter(def.Id, def.Kind == EncounterKind.Tutorial);
        }

        public IEnumerator ExitRoutine()
        {
            if (_director != null)
            {
                Destroy(_director.gameObject);
                _director = null;
            }

            if (_rig == null) yield break;

            _gameManager.StopEncounter();
            _roundGenerator?.ClearRound();
            _items?.ResetForNewEncounter(null);
            _gameManager.BindRig(null);
            _spotlight.SetTurnIndicator(null);

            Destroy(_rig.gameObject);
            _rig = null;
            _enemyAI = null;
            _health = null;
            _items = null;
            _roundGenerator = null;

            // Destroy отложенный: без кадра новый HealthSoundProvider/ItemVisualAnchors
            // увидят старый Instance и уничтожат сами себя. Заодно директор
            // успевает отработать OnDisable и отпустить свои гейты.
            yield return null;
        }
    }
}