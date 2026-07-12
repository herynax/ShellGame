using System.Collections.Generic;
using UnityEngine;

namespace ShellGame.Gameplay
{
    [CreateAssetMenu(fileName = "RoundProgressionConfig", menuName = "ShellGame/Gameplay/Round Progression Config")]
    public sealed class RoundProgressionConfig : ScriptableObject
    {
        [SerializeField] private List<RoundProgressionEntry> _entries = new List<RoundProgressionEntry>();

        public RoundParameters GetRoundParameters(int levelIndex, int roundIndex)
        {
            float difficultyIndex = ComputeDifficultyIndex(levelIndex, roundIndex);

            if (_entries != null)
            {
                foreach (var entry in _entries)
                {
                    if (entry.LevelIndex == levelIndex && entry.RoundIndex == roundIndex)
                    {
                        return new RoundParameters
                        {
                            LevelIndex = levelIndex,
                            RoundIndex = roundIndex,
                            CupCount = entry.CupCount,
                            MarkerCount = entry.MarkerCount,
                            DifficultyIndex = difficultyIndex,
                        };
                    }
                }
            }

            return CalculateParameters(levelIndex, roundIndex, difficultyIndex);
        }

        /// <summary>Индекс сложности из ГДД: D = L + 0.45 * R.</summary>
        private static float ComputeDifficultyIndex(int levelIndex, int roundIndex)
        {
            return levelIndex + 0.45f * roundIndex;
        }

        private RoundParameters CalculateParameters(int levelIndex, int roundIndex, float difficultyIndex)
        {
            int cupCount = Mathf.Clamp(3 + Mathf.FloorToInt(difficultyIndex / 2.2f), 3, 8);
            int maxMarkers = 1 + Mathf.FloorToInt((cupCount - 2) / 2f);
            float maxMarkerProbability = Mathf.Min(0.15f * difficultyIndex, 0.85f);
            int markerCount = Random.value < maxMarkerProbability ? maxMarkers : Mathf.Max(1, maxMarkers - 1);

            return new RoundParameters
            {
                LevelIndex = levelIndex,
                RoundIndex = roundIndex,
                CupCount = cupCount,
                MarkerCount = markerCount,
                DifficultyIndex = difficultyIndex,
            };
        }
    }

    [System.Serializable]
    public sealed class RoundProgressionEntry
    {
        public int LevelIndex;
        public int RoundIndex;
        public int CupCount;
        public int MarkerCount;
    }
}
