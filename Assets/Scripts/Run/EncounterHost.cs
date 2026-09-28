using System.Collections;
using ShellGame.AI;
using ShellGame.Core;
using ShellGame.Dialogue;
using ShellGame.Feedback;
using ShellGame.Gameplay;
using ShellGame.Health;
using ShellGame.Items;
using ShellGame.Tutorial;
using UnityEngine;
using Zenject;

namespace ShellGame.Run
{
    public sealed class EncounterHost : MonoBehaviour
    {
        [SerializeField] private Transform _rigParent;

        [Inject] private DiContainer _container = null;
        [Inject] private GameManager _gameManager = null;
        [Inject] private HealthController _health = null;
        [Inject] private ItemSpawner _items = null;
        [Inject] private EnemyAIController _enemyAI = null;
        [Inject] private RoundGenerator _roundGenerator = null;
        [Inject] private PlayerDamageFeedback _playerFeedback = null;

        private EncounterRig _rig;
        private EnemyReactionDirector _director;

        private void Awake()
        {
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
            EnemyReactionGate.SetBusy(null, false);
            TutorialSceneTransitionGate.HoldEnemyDeathTransition = false;

            _rig = _container.InstantiatePrefabForComponent<EncounterRig>(def.RigPrefab, _rigParent);
            yield return null; // Awake/OnEnable рига

            _health.BindEnemyFeedback(_rig.EnemyFeedback);
            _playerFeedback.BindRig(_rig);
            _items.ResetForNewEncounter(_rig.EnemyLook);
            _enemyAI.ResetForNewEncounter(def.EnemyAIConfig);

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
            _roundGenerator.ClearRound();
            _items.ResetForNewEncounter(null);

            Destroy(_rig.gameObject);
            _rig = null;

            // Destroy отложенный: без кадра новый HealthSoundProvider/ItemVisualAnchors
            // увидят старый Instance и уничтожат сами себя. Заодно директор
            // успевает отработать OnDisable и отпустить свои гейты.
            yield return null;
        }
    }
}