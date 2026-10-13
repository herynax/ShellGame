using ShellGame.AI;
using ShellGame.Core;
using ShellGame.Feedback;
using ShellGame.Gameplay;
using ShellGame.Health;
using ShellGame.Items;
using ShellGame.Shells;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShellGame.Run
{
    public sealed class EncounterRig : MonoBehaviour
    {
        public EnemyDamageFeedback EnemyFeedback;
        public EnemyLookController EnemyLook;

        [Tooltip("Указатель хода (компонент TurnIndicatorController на объекте Pointer этого рига)")]
        public TurnIndicatorController TurnIndicator;

        [Tooltip("Камера, которая смотрит на умирающего врага")]
        public CinemachineCamera EnemyCamera;

        [SerializeField] private ShellsTableController _shellTable;
        [SerializeField] private EnemyAIController _enemyAI;
        [SerializeField] private HealthController _health;

        [Header("Системы стола (пусто = ищется среди детей рига)")]
        [SerializeField] private RoundGenerator _roundGenerator;
        [SerializeField] private ShuffleSystem _shuffleSystem;
        [SerializeField] private RoundStartButton _roundStartButton;
        [SerializeField] private PlayerItemSpawner _playerItemSpawner;
        [SerializeField] private EnemyItemSpawner _enemyItemSpawner;

        [SerializeField] private GameObject _enemyPos;

        [Header("Атмосфера и Переходы")]
        [Tooltip("Глобальный Volume этого энкаунтера (если пусто — ищется в детях)")]
        [SerializeField] private Volume _postProcessVolume;

        [Tooltip("Система частиц пыли при приземлении комнаты")]
        [SerializeField] private ParticleSystem _landingParticles;

        public EncounterHost _encounterHost;
        public ShellsTableController ShellTable => _shellTable;
        public EnemyAIController EnemyAI => _enemyAI;
        public HealthController Health => _health;
        public RoundGenerator RoundGenerator => _roundGenerator;
        public ShuffleSystem ShuffleSystem => _shuffleSystem;
        public RoundStartButton RoundStartButton => _roundStartButton;
        public PlayerItemSpawner PlayerItemSpawner => _playerItemSpawner;
        public EnemyItemSpawner EnemyItemSpawner => _enemyItemSpawner;
        public GameObject EnemyPos => _enemyPos;
        public Volume PostProcessVolume => _postProcessVolume;
        public ParticleSystem LandingParticles => _landingParticles;

        public void Awake()
        {
            _encounterHost = GetComponentInParent<EncounterHost>();

            if (_encounterHost != null && _encounterHost.HealthSoundProvider != null && EnemyPos != null)
                _encounterHost.HealthSoundProvider.enemyTransform = EnemyPos.transform;

            if (_enemyAI == null) _enemyAI = GetComponentInChildren<EnemyAIController>(true);
            if (_health == null) _health = GetComponentInChildren<HealthController>(true);
            if (_roundGenerator == null) _roundGenerator = GetComponentInChildren<RoundGenerator>(true);
            if (_shuffleSystem == null) _shuffleSystem = GetComponentInChildren<ShuffleSystem>(true);
            if (_roundStartButton == null) _roundStartButton = GetComponentInChildren<RoundStartButton>(true);
            if (_playerItemSpawner == null) _playerItemSpawner = GetComponentInChildren<PlayerItemSpawner>(true);
            if (_enemyItemSpawner == null) _enemyItemSpawner = GetComponentInChildren<EnemyItemSpawner>(true);

            if (_postProcessVolume == null) _postProcessVolume = GetComponentInChildren<Volume>(true);
            if (_landingParticles == null) _landingParticles = GetComponentInChildren<ParticleSystem>(true);
        }
    }
}