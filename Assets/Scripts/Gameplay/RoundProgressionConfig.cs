using System.Collections.Generic;
using UnityEngine;

namespace ShellGame.Gameplay
{
    [CreateAssetMenu(fileName = "RoundProgressionConfig", menuName = "ShellGame/Gameplay/Round Progression Config")]
    public sealed class RoundProgressionConfig : ScriptableObject
    {
        [SerializeField] private List<RoundProgressionEntry> _entries = new List<RoundProgressionEntry>();

        public RoundParameters GetRoundParameters(
            int levelIndex,
            int roundIndex,
            int completedRoundsBeforeCurrentRound = 0,
            float difficultyOverride = -1f)
        {
            int effectiveCompletedRounds = Mathf.Max(0, completedRoundsBeforeCurrentRound);
            float difficultyIndex = difficultyOverride >= 0f
                ? difficultyOverride
                : ComputeDifficultyIndex(levelIndex, roundIndex, effectiveCompletedRounds);

            if (difficultyOverride < 0f && _entries != null)
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

        /// <summary>
        /// Индекс сложности накапливается по всем раундам текущего игрового сеанса.
        /// Это даёт плавный рост сложности между уровнями и сброс при новом запуске игры.
        /// </summary>
        private static float ComputeDifficultyIndex(int levelIndex, int roundIndex, int completedRoundsBeforeCurrentRound)
        {
            int progressionOffset = completedRoundsBeforeCurrentRound + roundIndex;
            return levelIndex + 0.45f * progressionOffset;
        }

        private RoundParameters CalculateParameters(int levelIndex, int roundIndex, float difficultyIndex)
        {
            int cupCount = EvaluateCupCount(difficultyIndex);
            int maxMarkers = EvaluateMaxMarkers(cupCount);
            float maxMarkerProbability = EvaluateMaxMarkerProbability(difficultyIndex);
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

        // ============================================================
        //  Публичные расчёты раскладки — единый источник истины для
        //  игры и окна баланса (Assets/Editor/Balance/BalanceWindow.cs).
        // ============================================================

        /// <summary>Число чашек на столе: C = clamp(3 + floor(D / 2.2), 3, 8).</summary>
        public static int EvaluateCupCount(float difficultyIndex) =>
            Mathf.Clamp(3 + Mathf.FloorToInt(difficultyIndex / 2.2f), 3, 8);

        /// <summary>Максимум меток для C чашек: 1 + floor((C - 2) / 2).</summary>
        public static int EvaluateMaxMarkers(int cupCount) =>
            1 + Mathf.FloorToInt((cupCount - 2) / 2f);

        /// <summary>Шанс, что выпадет максимальное число меток: min(0.15 * D, 0.85).</summary>
        public static float EvaluateMaxMarkerProbability(float difficultyIndex) =>
            Mathf.Min(0.15f * difficultyIndex, 0.85f);

        /// <summary>
        /// Ожидаемое число меток (матожидание случайного выбора максимума).
        /// Используется окном баланса и таблицей — в игре метки выбираются
        /// рулеткой Random.value.
        /// </summary>
        public static float EvaluateExpectedMarkerCount(float difficultyIndex)
        {
            int cupCount = EvaluateCupCount(difficultyIndex);
            int maxMarkers = EvaluateMaxMarkers(cupCount);
            float p = EvaluateMaxMarkerProbability(difficultyIndex);
            return p * maxMarkers + (1f - p) * Mathf.Max(1, maxMarkers - 1);
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
