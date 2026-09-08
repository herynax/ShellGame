using System.Collections.Generic;
using ShellGame.AI;
using ShellGame.Core;
using ShellGame.Feedback;
using ShellGame.Health;
using ShellGame.Shells;
using UnityEngine;

namespace ShellGame.Gameplay
{
    public sealed class ShellsTableController : MonoBehaviour
    {
        [SerializeField] private Shell _shellPrefab;
        [SerializeField] private ShellConfig _shellConfig;
        [SerializeField] private Camera _interactionCamera;
        [SerializeField] private LayerMask _shellLayerMask;
        [SerializeField] private Marker _markerPrefab;
        [SerializeField] private RoundProgressionConfig _progressionConfig;
        [SerializeField] private int _maxPrewarmCount = 8;

        [Header("Здоровье и противник")]
        [SerializeField] private HealthProgressionConfig _healthProgressionConfig;
        [SerializeField] private EnemyAIConfig _enemyAIConfig;
        [SerializeField] private TurnSide _startingSide = TurnSide.Player;

        [Header("Указатель хода (настраивается вручную — модель стрелки + цели)")]
        [HideInInspector] private TurnIndicatorController _turnIndicator;

        [HideInInspector] private RoundGenerator _roundGenerator;
        [HideInInspector] private RoundInputSystem _inputSystem;
        [HideInInspector] private RoundStartButton _roundStartButton;
        [HideInInspector] private ShuffleSystem _shuffleSystem;
        [HideInInspector] private GameManager _gameManager;
        [HideInInspector] private HealthController _healthController;
        [HideInInspector] private EnemyAIController _enemyAI;

        private void Awake()
        {
            ResolveReferences();

            if (_roundGenerator == null)
                _roundGenerator = gameObject.AddComponent<RoundGenerator>();
            if (_roundStartButton == null)
                _roundStartButton = gameObject.AddComponent<RoundStartButton>();
            if (_shuffleSystem == null)
                _shuffleSystem = gameObject.AddComponent<ShuffleSystem>();
            if (_healthController == null)
                _healthController = gameObject.AddComponent<HealthController>();
            if (_enemyAI == null)
                _enemyAI = gameObject.AddComponent<EnemyAIController>();

            // TurnIndicatorController сознательно НЕ авто-создаётся: ему
            // нужна модель стрелки + цели (_playerTarget/_enemyTarget),
            // настроенные вручную в сцене. Если его нет — GameManager
            // просто пропускает анимацию (проверка на null), ничего не упадёт.

            _roundGenerator.Initialize(_shellPrefab, _shellConfig, _markerPrefab, _progressionConfig, _maxPrewarmCount);
            _shuffleSystem.Initialize(_shellConfig);
            _inputSystem?.Initialize(_interactionCamera, _shellLayerMask, _roundStartButton);
            _enemyAI.Initialize(_enemyAIConfig);
            _roundStartButton?.Hide();
            if (_gameManager != null)
            {
                _gameManager.Initialize(
                    _roundGenerator,
                    _inputSystem,
                    _shuffleSystem,
                    _healthController,
                    _enemyAI,
                    _roundStartButton,
                    _healthProgressionConfig,
                    _startingSide,
                    _turnIndicator);
            }
        }

        private void ResolveReferences()
        {
            if (_roundGenerator == null)
                _roundGenerator = GetComponentInChildren<RoundGenerator>(true);
            if (_inputSystem == null)
                _inputSystem = GetComponentInChildren<RoundInputSystem>(true);
            if (_roundStartButton == null)
                _roundStartButton = GetComponentInChildren<RoundStartButton>(true);
            if (_shuffleSystem == null)
                _shuffleSystem = GetComponentInChildren<ShuffleSystem>(true);
            if (_gameManager == null)
                _gameManager = GetComponent<GameManager>();
            if (_healthController == null)
                _healthController = GetComponentInChildren<HealthController>(true);
            if (_enemyAI == null)
                _enemyAI = GetComponentInChildren<EnemyAIController>(true);
            if (_turnIndicator == null)
                _turnIndicator = GetComponentInChildren<TurnIndicatorController>(true);
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
