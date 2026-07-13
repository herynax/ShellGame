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

        /// <summary>Анимация смены/подтверждения инициативы (указатель поворачивается к активной стороне) — идёт после Cleanup, перед следующим Generate.</summary>
        InitiativeAnimation,

        /// <summary>Одна из сторон получила добивающий удар при нулевом здоровье — раунд-луп останавливается.</summary>
        GameOver,
    }
}
