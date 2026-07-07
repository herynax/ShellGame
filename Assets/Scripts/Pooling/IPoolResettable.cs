namespace ShellGame.Pooling
{
    /// <summary>
    /// Реализуется объектами, которые нужно приводить в чистое состояние
    /// при выдаче из пула / возврате в пул (сброс полей, остановка твинов,
    /// отписка от событий конкретного раунда и т.д.).
    /// </summary>
    public interface IPoolResettable
    {
        /// <summary>Вызывается сразу после LeanPool.Spawn, перед тем как объект станет видимым игроку.</summary>
        void OnSpawnFromPool();

        /// <summary>Вызывается перед LeanPool.Despawn — здесь нужно убить твины, отписаться от событий раунда.</summary>
        void OnReturnToPool();
    }
}
