/// <summary>
/// Реализует панель, которая хочет перехватить своё закрытие (показать confirm-диалог
/// вместо немедленного закрытия). MainMenuController/PauseController проверяют это
/// перед тем как реально закрыть верхнюю панель стека.
/// </summary>
public interface ICloseGuard
{
    /// <summary>
    /// Панель решает сама, закрываться ли сразу, или сначала что-то спросить у игрока.
    /// В любом случае, когда решение принято, нужно вызвать proceedClose().
    /// </summary>
    void RequestClose(System.Action proceedClose);
}