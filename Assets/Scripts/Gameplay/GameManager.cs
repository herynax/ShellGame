using System.Collections;
using ShellGame.Core;
using ShellGame.Shells;
using UnityEngine;

namespace ShellGame.Gameplay
{
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private RoundGenerator _roundGenerator;
        [SerializeField] private RoundInputSystem _inputSystem;
        [SerializeField] private ShuffleSystem _shuffleSystem;
        [SerializeField] private int _levelIndex = 0;
        [SerializeField] private int _roundIndex = 0;
        [SerializeField] private float _spawnPauseDuration = 0.45f;
        [SerializeField] private float _revealHoldDuration = 0.75f;
        [SerializeField] private float _roundEndDelay = 0.5f;
        [SerializeField] private float _shuffleDelay = 0.15f;

        private RoundState _state = RoundState.Idle;
        private RoundParameters _currentParameters;
        private Shell _selectedShell;

        public RoundState State => _state;

        public void Initialize(RoundGenerator roundGenerator, RoundInputSystem inputSystem, ShuffleSystem shuffleSystem)
        {
            _roundGenerator = roundGenerator;
            _inputSystem = inputSystem;
            _shuffleSystem = shuffleSystem;
        }

        private void Start()
        {
            if (_roundGenerator == null)
                _roundGenerator = GetComponentInChildren<RoundGenerator>();
            if (_inputSystem == null)
                _inputSystem = GetComponentInChildren<RoundInputSystem>();
            if (_shuffleSystem == null)
                _shuffleSystem = GetComponentInChildren<ShuffleSystem>();

            StartRound();
        }

        public void StartRound()
        {
            if (_roundGenerator == null || _inputSystem == null || _shuffleSystem == null)
            {
                Debug.LogWarning("GameManager dependencies are not initialized yet. Waiting for initialization.");
                return;
            }

            _state = RoundState.Generate;
            StartCoroutine(RunRoundRoutine());
        }

        private IEnumerator RunRoundRoutine()
        {
            while (true)
            {
                switch (_state)
                {
                    case RoundState.Generate:
                        if (_roundGenerator == null)
                        {
                            yield break;
                        }

                        Debug.Log($"GameManager: stage=Generate level={_levelIndex} round={_roundIndex}");
                        _currentParameters = _roundGenerator.GenerateRound(_levelIndex, _roundIndex);
                        _state = RoundState.Reveal;
                        break;

                    case RoundState.Reveal:
                        if (_roundGenerator == null)
                        {
                            yield break;
                        }

                        Debug.Log("GameManager: stage=SpawnPause");
                        yield return new WaitForSeconds(Mathf.Max(0f, _spawnPauseDuration));

                        Debug.Log("GameManager: stage=RevealMarkers");
                        _roundGenerator.RevealMarkers(_revealHoldDuration);
                        yield return new WaitForSeconds(Mathf.Max(0f, _roundGenerator.GetRevealDuration(_revealHoldDuration)));
                        _roundGenerator.HideMarkers();
                        Debug.Log("GameManager: stage=Shuffle");
                        _state = RoundState.Shuffle;
                        break;

                    case RoundState.Shuffle:
                        if (_inputSystem == null || _shuffleSystem == null || _roundGenerator == null)
                        {
                            yield break;
                        }

                        _inputSystem.SetEnabled(false);
                        yield return new WaitForSeconds(_shuffleDelay);
                        _shuffleSystem.StartShuffling(_roundGenerator.GetShellsInPlayOrder(), () =>
                        {
                            _state = RoundState.PlayerTurn;
                        });
                        while (_state == RoundState.Shuffle)
                            yield return null;
                        break;

                    case RoundState.PlayerTurn:
                        if (_inputSystem == null)
                        {
                            yield break;
                        }

                        Debug.Log("GameManager: stage=PlayerTurn");
                        _inputSystem.SetEnabled(true);
                        _selectedShell = null;
                        while (_state == RoundState.PlayerTurn)
                            yield return null;
                        break;

                    case RoundState.RevealResult:
                        if (_selectedShell == null || _roundGenerator == null)
                        {
                            _state = RoundState.Cleanup;
                            break;
                        }

                        Debug.Log($"GameManager: stage=RevealResult selected={_selectedShell.name} hasMarker={_selectedShell.HasMarker}");
                        _selectedShell.RevealResult();
                        yield return new WaitForSeconds(Mathf.Max(_roundEndDelay, _roundGenerator.GetRevealDuration()));
                        Debug.Log(_selectedShell.HasMarker ? "Success" : "Fail");
                        _state = RoundState.Cleanup;
                        break;

                    case RoundState.Cleanup:
                        if (_roundGenerator == null || _inputSystem == null)
                        {
                            yield break;
                        }

                        Debug.Log("GameManager: stage=Cleanup");
                        _roundGenerator.ClearRound();
                        _inputSystem.SetEnabled(false);
                        _roundIndex++;
                        yield return new WaitForSeconds(0.1f);
                        _state = RoundState.Generate;
                        break;

                    default:
                        yield break;
                }
            }
        }

        private void OnEnable()
        {
            GameEvents.ShellSelected += OnShellSelected;
            GameEvents.RoundShuffleCompleted += OnShuffleCompleted;
            GameEvents.ShellRevealed += OnShellRevealed;
        }

        private void OnDisable()
        {
            GameEvents.ShellSelected -= OnShellSelected;
            GameEvents.RoundShuffleCompleted -= OnShuffleCompleted;
            GameEvents.ShellRevealed -= OnShellRevealed;
        }

        private void OnShellSelected(Shell shell)
        {
            if (_state != RoundState.PlayerTurn)
                return;

            _selectedShell = shell;
            _inputSystem.SetEnabled(false);
            _state = RoundState.RevealResult;
        }

        private void OnShellRevealed(Shell shell, bool hasMarker)
        {
            if (_state != RoundState.RevealResult || _selectedShell != shell)
                return;

            Debug.Log(hasMarker ? "Success" : "Fail");
        }

        private void OnShuffleCompleted()
        {
            if (_state == RoundState.Shuffle)
                _state = RoundState.PlayerTurn;
        }
    }
}
