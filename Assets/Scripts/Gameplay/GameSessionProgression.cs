using UnityEngine;

namespace ShellGame.Gameplay
{
    public sealed class GameSessionProgression : MonoBehaviour
    {
        public static GameSessionProgression Instance { get; private set; }

        public int CompletedRoundsInSession { get; private set; }
        public int CurrentLevelIndex { get; private set; }
        public float CurrentDifficultyIndex { get; private set; }
        public int MaxShellsPenalty { get; private set; }

        /// <summary>
        /// Одноразовый флаг: "следующая загружаемая игровая сцена должна
        /// восстановить состояние из RunCheckpointStorage вместо обычного
        /// свежего старта". Выставляется MainMenuController перед загрузкой
        /// сцены по кнопке "Продолжить попытку", потребляется (сбрасывается)
        /// в GameManager.Start() сразу же — поэтому обычная внутриигровая
        /// смена уровня никогда его не видит и никогда не восстанавливается
        /// вместо честной генерации нового раунда.
        /// </summary>
        public bool PendingContinueFromCheckpoint { get; set; }

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

        public void SetCompletedRounds(int count)
        {
            CompletedRoundsInSession = Mathf.Max(0, count);
        }

        public void SetCurrentLevelIndex(int levelIndex)
        {
            CurrentLevelIndex = Mathf.Max(0, levelIndex);
        }

        public void AddMaxShellsPenalty(int amount = 1)
        {
            MaxShellsPenalty += amount;
        }

        public void SetMaxShellsPenalty(int amount)
        {
            MaxShellsPenalty = Mathf.Max(0, amount);
        }

        public void AdvanceToNextLevel()
        {
            SetCurrentLevelIndex(Mathf.Max(1, CurrentLevelIndex + 1));
            CurrentDifficultyIndex += 1f;
            MaxShellsPenalty = 0; // Сбрасываем штраф на новом уровне
        }

        public void AdvanceDifficultyForRound()
        {
            CurrentDifficultyIndex += 0.45f;
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
            MaxShellsPenalty = 0; // Сбрасываем при рестарте забега
            PendingContinueFromCheckpoint = false;
        }
    }
}