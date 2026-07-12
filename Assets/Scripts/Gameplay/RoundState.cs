namespace ShellGame.Gameplay
{
    public enum RoundState
    {
        Idle,
        Generate,
        Reveal,
        Shuffle,

        /// <summary>Ход активной стороны — игрока или противника (см. GameManager._activeSide).</summary>
        PlayerTurn,

        RevealResult,
        Cleanup,

        /// <summary>Одна из сторон получила добивающий удар при нулевом здоровье — раунд-луп останавливается.</summary>
        GameOver,
    }
}
