namespace ShellGame.Items
{
    /// <summary>
    /// Результат ItemSpawner.TryUseEnemyItemsRoutine — обычный out-параметр
    /// тут не подходит (метод стал корутиной ради вставки "раздумья"
    /// EnemyLookController между решениями), поэтому результат собирается
    /// в этот простой изменяемый объект, который вызывающий код (GameManager)
    /// создаёт и передаёт до yield return.
    /// </summary>
    public sealed class EnemyItemUseResult
    {
        public bool UsedAnything;
        public bool SkippedTurn;
        public float ExtraDelaySeconds;
    }
}