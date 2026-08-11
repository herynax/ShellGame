using UnityEngine;

namespace ShellGame.Gameplay
{
    public sealed class GameSessionProgression : MonoBehaviour
    {
        public static GameSessionProgression Instance { get; private set; }

        public int CompletedRoundsInSession { get; private set; }
        public int CurrentLevelIndex { get; private set; }
        public float CurrentDifficultyIndex { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                return;
            }

            Destroy(gameObject);
        }

        public void IncrementCompletedRounds()
        {
            CompletedRoundsInSession++;
        }

        public void SetCurrentLevelIndex(int levelIndex)
        {
            CurrentLevelIndex = Mathf.Max(0, levelIndex);
        }

        public void AdvanceToNextLevel()
        {
            CurrentLevelIndex = Mathf.Max(1, CurrentLevelIndex + 1);
        }

        public float GetDifficultyForRound(int levelIndex, int roundIndex, int completedRoundsBeforeCurrentRound)
        {
            float formulaDifficulty = levelIndex + 0.45f * (completedRoundsBeforeCurrentRound + roundIndex);
            return Mathf.Max(CurrentDifficultyIndex, formulaDifficulty);
        }

        public void SetDifficultyIndex(float difficultyIndex)
        {
            CurrentDifficultyIndex = Mathf.Max(0f, difficultyIndex);
        }

        public void Reset()
        {
            CompletedRoundsInSession = 0;
            CurrentLevelIndex = 0;
            CurrentDifficultyIndex = 0f;
        }
    }
}
