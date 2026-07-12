using System.Collections.Generic;
using ShellGame.AI;
using ShellGame.Core;
using ShellGame.Health;
using ShellGame.Shells;
using UnityEngine;

namespace ShellGame.Gameplay
{
    public sealed class ShellsTableController : MonoBehaviour
    {
        [SerializeField] private Shell _shellPrefab;
        [SerializeField] private ShellConfig _shellConfig;
        [SerializeField] private List<ShellSlot> _slots = new List<ShellSlot>();
        [SerializeField] private Camera _interactionCamera;
        [SerializeField] private LayerMask _shellLayerMask;
        [SerializeField] private Marker _markerPrefab;
        [SerializeField] private RoundProgressionConfig _progressionConfig;
        [SerializeField] private int _maxPrewarmCount = 8;

        [Header("Здоровье и противник")]
        [SerializeField] private HealthProgressionConfig _healthProgressionConfig;
        [SerializeField] private EnemyAIConfig _enemyAIConfig;
        [SerializeField] private TurnSide _startingSide = TurnSide.Player;

        [SerializeField] private RoundGenerator _roundGenerator;
        [SerializeField] private RoundInputSystem _inputSystem;
        [SerializeField] private ShuffleSystem _shuffleSystem;
        [SerializeField] private GameManager _gameManager;
        private HealthController _healthController;
        private EnemyAIController _enemyAI;

        private void Awake()
        {
            if (_roundGenerator == null)
                _roundGenerator = GetComponentInChildren<RoundGenerator>();
            if (_inputSystem == null)
                _inputSystem = GetComponentInChildren<RoundInputSystem>();
            if (_shuffleSystem == null)
                _shuffleSystem = GetComponentInChildren<ShuffleSystem>();
            if (_gameManager == null)
                _gameManager = GetComponent<GameManager>();
            if (_healthController == null)
                _healthController = GetComponentInChildren<HealthController>();
            if (_enemyAI == null)
                _enemyAI = GetComponentInChildren<EnemyAIController>();

            if (_gameManager == null)
                _gameManager = gameObject.AddComponent<GameManager>();
            if (_roundGenerator == null)
                _roundGenerator = gameObject.AddComponent<RoundGenerator>();
            if (_inputSystem == null)
                _inputSystem = gameObject.AddComponent<RoundInputSystem>();
            if (_shuffleSystem == null)
                _shuffleSystem = gameObject.AddComponent<ShuffleSystem>();
            if (_healthController == null)
                _healthController = gameObject.AddComponent<HealthController>();
            if (_enemyAI == null)
                _enemyAI = gameObject.AddComponent<EnemyAIController>();

            _roundGenerator.Initialize(_shellPrefab, _shellConfig, _slots, _markerPrefab, _progressionConfig, _maxPrewarmCount);
            _inputSystem.Initialize(_interactionCamera, _shellLayerMask);
            _enemyAI.Initialize(_enemyAIConfig);
            _gameManager.Initialize(
                _roundGenerator,
                _inputSystem,
                _shuffleSystem,
                _healthController,
                _enemyAI,
                _healthProgressionConfig,
                _startingSide);
        }

        public void SetupRound(int shellCount, int markerCount)
        {
            _gameManager?.StartRound();
        }

        public void ClearRound()
        {
            _roundGenerator?.ClearRound();
        }
    }
}
