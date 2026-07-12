namespace ShellGame.AI
{
    /// <summary>Idle → ObserveMarkers → TrackShuffle → Decision → Attack → EndTurn (см. раздел "Система работы врагов" в ГДД).</summary>
    public enum EnemyAIState
    {
        Idle,
        ObserveMarkers,
        TrackShuffle,
        Decision,
        Attack,
        EndTurn,
    }
}
