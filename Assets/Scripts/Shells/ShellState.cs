namespace ShellGame.Shells
{
    /// <summary>Текущее состояние наперстка — используется, чтобы гасить лишние клики/твины (например, нельзя выбрать наперсток во время шаффла).</summary>
    public enum ShellState
    {
        PooledInactive,
        Idle,
        Revealing,
        Shuffling,
        Selected,
    }
}
