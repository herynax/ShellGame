using System;
using System.Collections.Generic;
using ShellGame.AI;
using ShellGame.Core;
using ShellGame.Health;
using ShellGame.Shells;

namespace ShellGame.Items
{
    /// <summary>
    /// Всё, что может понадобиться предмету, чтобы применить свой эффект.
    /// Собирается вызывающим кодом (сейчас — GameManager.CreateItemContext)
    /// и передаётся в ItemDefinition.Apply/CanUse — сам предмет не знает
    /// про GameManager напрямую.
    /// </summary>
    public sealed class ItemEffectContext
    {
        /// <summary>Кто использует предмет — на эту сторону и распространяется эффект (хилка/двойной урон/монокль).</summary>
        public TurnSide UserSide;

        public HealthController Health;

        /// <summary>Наперстки текущего раунда — нужны Монокли, чтобы найти помеченные.</summary>
        public IReadOnlyList<Shell> ActiveShells;

        /// <summary>Может быть null, если предмет использует игрок и AI ни при чём.</summary>
        public EnemyAIController EnemyAI;

        /// <summary>Делегат в GameManager — предмет "Двойной урон" ставит множитель на следующий удачный удар этой стороны.</summary>
        public Action<TurnSide, int> SetNextHitDamageMultiplier;

        /// <summary>
        /// "Наркотический" эффект (игрок) — замедляет общий темп игры
        /// (Time.timeScale) до ближайшего реального выбора наперстка.
        /// </summary>
        public Action<TurnSide, float> SlowGamePaceUntilNextChoice;

        /// <summary>Не даёт "наркотическому" эффекту стакаться поверх уже активного замедления игрока.</summary>
        public Func<bool> CanSlowGamePace;

        /// <summary>Снижает шанс потери отслеживания метки на СЛЕДУЮЩЕМ перемешивании этой стороны — имеет смысл только для противника (см. SlowShuffleItemDefinition, EnemyAIController.ReduceTrackingLossNextShuffle). Аргумент — множитель на Plose (0..1).</summary>
        public Action<float> ReduceEnemyTrackingLossNextShuffle;

        /// <summary>
        /// Монокль (сторона игрока): переводит наперстки в режим "подглядеть" —
        /// следующий клик по наперстку не считается финальным выбором раунда,
        /// а просто ненадолго показывает, есть ли под ним метка. holdDuration —
        /// сколько наперсток держится приподнятым; onPeeked — опциональный
        /// колбэк, срабатывает, когда peek состоялся.
        /// </summary>
        public Action<float, Action<Shell>> BeginShellPeek;

        /// <summary>
        /// Возвращает ПОЛНУЮ длительность анимации Shell.RevealMarker(holdDuration)
        /// (подъём+пауза+спуск, а не просто саму паузу). Нужно предметам вроде
        /// Монокля у противника, чтобы понять, сколько реально ждать после
        /// визуальной "проверки", прежде чем продолжать.
        /// </summary>
        public Func<float, float> ResolveShellRevealDuration;

        /// <summary>
        /// Заполняется САМИМ предметом внутри Apply(), если применение
        /// визуально занимает какое-то время (например, Монокль у
        /// противника поднимает один наперсток для "проверки") — вызывающий
        /// код (ItemSpawner/GameManager) обязан подождать это время, прежде
        /// чем продолжать (например, прежде чем враг перейдёт к реальному
        /// выбору). 0 по умолчанию — ждать не нужно.
        /// </summary>
        public float ConsumedExtraDelay;

        /// <summary>Помечает текущий ход врага пропущенным.</summary>
        public Action SkipCurrentTurn;

        /// <summary>Запускает дополнительный полный ход игрока.</summary>
        public Action RequestExtraTurn;

        /// <summary>Проверяет, доступен ли предмет дополнительного хода.</summary>
        public Func<bool> CanRequestExtraTurn;
    }
}