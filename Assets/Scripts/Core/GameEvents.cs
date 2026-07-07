using System;
using ShellGame.Shells;

namespace ShellGame.Core
{
    /// <summary>
    /// Централизованная событийная шина. Позволяет UI, аудио, AI и геймплейным
    /// системам подписываться на игровые события, не зная друг о друге напрямую.
    ///
    /// Важно: подписчики обязаны отписываться в OnDisable/OnDestroy, чтобы не
    /// плодить утечки (особенно критично для пулингуемых объектов — наперсток
    /// может быть возвращён в пул, пока событие ещё "живо").
    /// </summary>
    public static class GameEvents
    {
        public static event Action<Shell> ShellHoverEnter;
        public static event Action<Shell> ShellHoverExit;
        public static event Action<Shell> ShellSelected;

        /// <summary>Наперсток поднят и показал, есть ли под ним метка.</summary>
        public static event Action<Shell, bool /*hasMarker*/> ShellRevealed;

        public static event Action RoundSetupStarted;
        public static event Action RoundShuffleStarted;
        public static event Action RoundShuffleCompleted;

        public static void RaiseShellHoverEnter(Shell shell) => ShellHoverEnter?.Invoke(shell);
        public static void RaiseShellHoverExit(Shell shell) => ShellHoverExit?.Invoke(shell);
        public static void RaiseShellSelected(Shell shell) => ShellSelected?.Invoke(shell);
        public static void RaiseShellRevealed(Shell shell, bool hasMarker) => ShellRevealed?.Invoke(shell, hasMarker);
        public static void RaiseRoundSetupStarted() => RoundSetupStarted?.Invoke();
        public static void RaiseRoundShuffleStarted() => RoundShuffleStarted?.Invoke();
        public static void RaiseRoundShuffleCompleted() => RoundShuffleCompleted?.Invoke();
    }
}
