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
    }
}
