using System.Collections.Generic;
using ShellGame.Audio;
using ShellGame.Core;
using ShellGame.Pooling;
using ShellGame.Shells;
using UnityEngine;

namespace ShellGame.Gameplay
{
    public sealed class RoundGenerator : MonoBehaviour
    {
        [SerializeField] private Shell _shellPrefab;
        [SerializeField] private ShellConfig _shellConfig;
        [SerializeField] private List<ShellSlot> _slots = new List<ShellSlot>();
        [SerializeField] private Marker _markerPrefab;
        [SerializeField] private RoundProgressionConfig _progressionConfig;
        [SerializeField] private int _maxPrewarmCount = 8;

        private IShellPoolService _pool;
        private IAudioService _audio;
        private readonly List<Shell> _activeShells = new List<Shell>();
        private readonly List<Marker> _activeMarkers = new List<Marker>();
        private readonly List<ShellSlot> _spawnedSlots = new List<ShellSlot>();

        public IReadOnlyList<Shell> ActiveShells => _activeShells;

        public void Initialize(Shell shellPrefab, ShellConfig shellConfig, List<ShellSlot> slots, Marker markerPrefab, RoundProgressionConfig progressionConfig, int maxPrewarmCount)
        {
            _shellPrefab = shellPrefab;
            _shellConfig = shellConfig;
            _slots = slots ?? new List<ShellSlot>();
            _markerPrefab = markerPrefab;
            _progressionConfig = progressionConfig;
            _maxPrewarmCount = maxPrewarmCount;
        }

        private void Awake()
        {
            if (!ServiceLocator.TryGet<IAudioService>(out _audio))
            {
                _audio = new FMODAudioService();
                ServiceLocator.Register(_audio);
            }

            if (!ServiceLocator.TryGet<IShellPoolService>(out _pool))
            {
                _pool = new ShellPoolService(_shellPrefab, transform);
                ServiceLocator.Register(_pool);
            }

            _pool.Prewarm(_maxPrewarmCount);
        }

        public RoundParameters GenerateRound(int levelIndex, int roundIndex)
        {
            ClearRound();
            GameEvents.RaiseRoundSetupStarted();

            var parameters = _progressionConfig != null
                ? _progressionConfig.GetRoundParameters(levelIndex, roundIndex)
                : new RoundParameters { LevelIndex = levelIndex, RoundIndex = roundIndex, CupCount = 3, MarkerCount = 1 };

            parameters.CupCount = Mathf.Clamp(parameters.CupCount, 1, _slots.Count);
            parameters.MarkerCount = Mathf.Clamp(parameters.MarkerCount, 0, parameters.CupCount);
            Debug.Log($"RoundGenerator: level={levelIndex}, round={roundIndex}, cups={parameters.CupCount}, markers={parameters.MarkerCount}");

            _spawnedSlots.Clear();
            _spawnedSlots.AddRange(PickRandomSlots(parameters.CupCount));

            var markerIndices = PickRandomMarkerIndices(parameters.CupCount, parameters.MarkerCount);
            for (int i = 0; i < _spawnedSlots.Count; i++)
            {
                var slot = _spawnedSlots[i];
                var shell = _pool.Spawn(slot.SpawnPosition, slot.Rotation);
                if (_shellPrefab != null)
                    shell.transform.localScale = _shellPrefab.transform.localScale;
                shell.Initialize(_shellConfig, _audio);
                shell.AssignToSlot(slot);
                shell.PlaceAtSurface(slot.SpawnPosition);

                var hasMarker = markerIndices.Contains(i);
                Marker marker = null;
                if (hasMarker)
                {
                    if (_markerPrefab != null)
                    {
                        marker = Instantiate(_markerPrefab, transform, true);
                        marker.PlaceAtSurface(slot.SpawnPosition);
                    }
                    else
                    {
                        var markerObject = new GameObject("Marker");
                        markerObject.transform.SetParent(transform, false);
                        marker = markerObject.AddComponent<Marker>();
                        marker.PlaceAtSurface(slot.SpawnPosition);
                    }
                }

                shell.AttachMarker(marker);
                shell.SetMarker(hasMarker);
                shell.PlaySpawnIn();
                Debug.Log($"RoundGenerator: spawned shell #{i} slot={slot.Index} hasMarker={hasMarker}");

                slot.OccupyingShell = shell;
                _activeShells.Add(shell);
                if (marker != null)
                    _activeMarkers.Add(marker);
            }

            _audio.PlayOneShot(_shellConfig.AudioEvents.Deal);
            return parameters;
        }

        public void ClearRound()
        {
            foreach (var slot in _slots)
                slot.OccupyingShell = null;

            foreach (var shell in _activeShells)
                _pool.Despawn(shell);

            foreach (var marker in _activeMarkers)
                Destroy(marker.gameObject);

            _activeShells.Clear();
            _activeMarkers.Clear();
            _spawnedSlots.Clear();
        }

        public IReadOnlyList<Shell> GetShellsInPlayOrder() => _activeShells;

        public float GetRevealDuration(float holdDuration = -1f)
        {
            if (_shellConfig == null)
                return 0.75f;

            var resolvedHoldDuration = holdDuration >= 0f ? holdDuration : _shellConfig.HoldRevealedDuration;
            return _shellConfig.LiftDuration * 2f + resolvedHoldDuration;
        }

        public void RevealMarkers(float holdDuration)
        {
            foreach (var shell in _activeShells)
            {
                shell.RevealMarker(holdDuration);
            }
        }

        public void HideMarkers()
        {
            foreach (var shell in _activeShells)
            {
                shell.HideMarker();
            }
        }

        public List<ShellSlot> PickRandomSlots(int count)
        {
            var shuffled = new List<ShellSlot>(_slots);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            return shuffled.GetRange(0, count);
        }

        public HashSet<int> PickRandomMarkerIndices(int shellCount, int markerCount)
        {
            var indices = new List<int>();
            for (int i = 0; i < shellCount; i++)
                indices.Add(i);

            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            return new HashSet<int>(indices.GetRange(0, markerCount));
        }
    }
}
