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

        /// <summary>
        /// Состояние TrackShuffle из ГДД: "игровое поле генерирует событие
        /// после каждого обмена двух наперстков — OnCupSwap(CupA, CupB)".
        /// Аргументы — индексы слотов (Shell.SlotIndex), которыми обменялись.
        /// </summary>
        public static event Action<int /*slotA*/, int /*slotB*/> CupSwapPerformed;

        /// <summary>Любое изменение здоровья стороны — включая инициализацию в начале уровня: (сторона, текущее, максимум).</summary>
        public static event Action<TurnSide, int, int> HealthChanged;

        /// <summary>
        /// Именно момент нанесения урона (в отличие от HealthChanged не
        /// стреляет при инициализации здоровья) — на это реагирует визуальный
        /// фидбек (тряска камеры/модели, виньетка и т.д.).
        /// Аргументы: сторона, сколько урона, здоровье после удара, максимум, умер ли от этого удара.
        /// </summary>
        public static event Action<TurnSide, int, int, int, bool> DamageTaken;

        /// <summary>Сторона получила урон при здоровье уже на нуле — гибель.</summary>
        public static event Action<TurnSide> SideDied;

        /// <summary>Инициатива перешла к новой стороне (после промаха).</summary>
        public static event Action<TurnSide> ActiveSideChanged;

        public static void RaiseShellHoverEnter(Shell shell) => ShellHoverEnter?.Invoke(shell);
        public static void RaiseShellHoverExit(Shell shell) => ShellHoverExit?.Invoke(shell);
        public static void RaiseShellSelected(Shell shell) => ShellSelected?.Invoke(shell);
        public static void RaiseShellRevealed(Shell shell, bool hasMarker) => ShellRevealed?.Invoke(shell, hasMarker);
        public static void RaiseRoundSetupStarted() => RoundSetupStarted?.Invoke();
        public static void RaiseRoundShuffleStarted() => RoundShuffleStarted?.Invoke();
        public static void RaiseRoundShuffleCompleted() => RoundShuffleCompleted?.Invoke();
        public static void RaiseCupSwapPerformed(int slotA, int slotB) => CupSwapPerformed?.Invoke(slotA, slotB);
        public static void RaiseHealthChanged(TurnSide side, int current, int max) => HealthChanged?.Invoke(side, current, max);

        public static void RaiseDamageTaken(TurnSide side, int amount, int currentHealth, int maxHealth, bool died) =>
            DamageTaken?.Invoke(side, amount, currentHealth, maxHealth, died);

        public static void RaiseSideDied(TurnSide side) => SideDied?.Invoke(side);
        public static void RaiseActiveSideChanged(TurnSide side) => ActiveSideChanged?.Invoke(side);
    }
}