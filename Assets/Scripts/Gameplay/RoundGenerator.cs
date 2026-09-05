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
        public float LayoutTransitionDuration { get; private set; }

        public void SetSide(TurnSide side)
        {
            foreach (var shell in _activeShells)
                shell?.SetSide(side);
        }

        public void Initialize(Shell shellPrefab, ShellConfig shellConfig, Marker markerPrefab, RoundProgressionConfig progressionConfig, int maxPrewarmCount)
        {
            _shellPrefab = shellPrefab;
            _shellConfig = shellConfig;
            _markerPrefab = markerPrefab;
            _progressionConfig = progressionConfig;
            _maxPrewarmCount = maxPrewarmCount;

            ResolveSlots();
        }

        private void Awake()
        {
            ResolveSlots();

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

        private void ResolveSlots()
        {
            if (_slots != null && _slots.Count > 0)
                return;

            _slots.Clear();
            var discoveredSlots = GetComponentsInChildren<ShellSlot>(true);
            if (discoveredSlots != null && discoveredSlots.Length > 0)
            {
                foreach (var slot in discoveredSlots)
                    _slots.Add(slot);
            }
        }

        public RoundParameters GenerateRound(int levelIndex, int roundIndex, int completedRoundsBeforeCurrentRound = 0)
        {
            ResolveSlots();
            GameEvents.RaiseRoundSetupStarted();

            var parameters = _progressionConfig != null
                ? _progressionConfig.GetRoundParameters(levelIndex, roundIndex, completedRoundsBeforeCurrentRound)
                : new RoundParameters { LevelIndex = levelIndex, RoundIndex = roundIndex, CupCount = 3, MarkerCount = 1 };

            parameters.CupCount = Mathf.Clamp(parameters.CupCount, 1, _slots.Count);
            parameters.MarkerCount = Mathf.Clamp(parameters.MarkerCount, 0, parameters.CupCount);

            _spawnedSlots.Clear();
            _spawnedSlots.AddRange(PickRandomSlots(parameters.CupCount));

            foreach (var slot in _slots)
                slot.OccupyingShell = null;

            foreach (var marker in _activeMarkers)
                Destroy(marker.gameObject);
            _activeMarkers.Clear();

            var previousShells = new List<Shell>(_activeShells);
            var nextShells = new List<Shell>(_spawnedSlots.Count);
            LayoutTransitionDuration = previousShells.Count > 0 ? ResolveLayoutMoveDuration(levelIndex, roundIndex) : 0f;

            var markerIndices = PickRandomMarkerIndices(parameters.CupCount, parameters.MarkerCount);
            for (int i = 0; i < _spawnedSlots.Count; i++)
            {
                var slot = _spawnedSlots[i];
                Shell shell;
                if (i < previousShells.Count)
                {
                    shell = previousShells[i];
                }
                else
                {
                    shell = _pool.Spawn(slot.SpawnPosition, slot.Rotation);
                }

                if (_shellPrefab != null)
                    shell.transform.localScale = _shellPrefab.transform.localScale;
                shell.Initialize(_shellConfig, _audio);
                shell.AssignToSlot(slot);
                if (i >= previousShells.Count)
                {
                    shell.PlaceAtSurface(slot.SpawnPosition);
                    shell.PlaySpawnIn();
                }
                else
                    shell.MoveToSlot(slot, null, LayoutTransitionDuration);

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

                slot.OccupyingShell = shell;
                nextShells.Add(shell);
                if (marker != null)
                    _activeMarkers.Add(marker);
            }

            for (int i = _spawnedSlots.Count; i < previousShells.Count; i++)
                _pool.Despawn(previousShells[i]);

            _activeShells.Clear();
            _activeShells.AddRange(nextShells);

            _audio.PlayOneShot(_shellConfig.AudioEvents.Deal);
            return parameters;
        }

        private float ResolveLayoutMoveDuration(int levelIndex, int roundIndex)
        {
            if (_shellConfig == null)
                return 0.22f;

            float reducedDuration = _shellConfig.ShuffleMoveDurationBase
                - _shellConfig.ShuffleRoundReduction * Mathf.Max(0, roundIndex)
                - _shellConfig.ShuffleLevelReduction * Mathf.Max(0, levelIndex);
            return Mathf.Max(_shellConfig.ShuffleMoveDurationMin, reducedDuration);
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
