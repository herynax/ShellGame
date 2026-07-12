using System;

namespace ShellGame.Gameplay
{
    [Serializable]
    public struct RoundParameters
    {
        public int LevelIndex;
        public int RoundIndex;
        public int CupCount;
        public int MarkerCount;

        /// <summary>
        /// Индекс сложности (L + 0.45*R из ГДД), используется формулами ИИ
        /// (Plose, Perror, скорость решений). Считается в RoundProgressionConfig
        /// той же формулой, что и параметры раунда, чтобы всё было согласовано.
        /// </summary>
        public float DifficultyIndex;
    }
}
