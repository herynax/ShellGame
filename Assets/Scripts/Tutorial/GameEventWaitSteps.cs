using System;
using System.Collections;
using ShellGame.Core;
using ShellGame.Shells;
using UnityEngine;

namespace ShellGame.Tutorial
{
    /// <summary>
    /// Ждёт беспараметрическое событие GameEvents (RoundStartConfirmed,
    /// RoundShuffleStarted, RoundShuffleCompleted, RoundSetupStarted).
    /// Подписку/отписку передавайте явно:
    /// new WaitForEvent(h => GameEvents.RoundStartConfirmed += h,
    ///                   h => GameEvents.RoundStartConfirmed -= h)
    /// </summary>
    public sealed class WaitForEvent : TutorialStep
    {
        private readonly Action<Action> _subscribe;
        private readonly Action<Action> _unsubscribe;

        public WaitForEvent(Action<Action> subscribe, Action<Action> unsubscribe)
        {
            _subscribe = subscribe;
            _unsubscribe = unsubscribe;
        }

        public override IEnumerator Run(MonoBehaviour runner)
        {
            bool done = false;
            void Handler() => done = true;

            _subscribe(Handler);
            while (!done)
                yield return null;
            _unsubscribe(Handler);
        }
    }

    /// <summary>Ждёт выбор наперстка. После завершения доступен SelectedShell.</summary>
    public sealed class WaitForShellSelected : TutorialStep
    {
        public Shell SelectedShell { get; private set; }

        public override IEnumerator Run(MonoBehaviour runner)
        {
            Debug.Log("[Tutorial] WaitForShellSelected: начал ждать ShellSelected");
            bool done = false;
            Shell result = null;
            void Handler(Shell shell) { Debug.Log($"[Tutorial] WaitForShellSelected: поймал ShellSelected, slot={shell.SlotIndex}"); result = shell; done = true; }

            GameEvents.ShellSelected += Handler;
            while (!done)
                yield return null;
            GameEvents.ShellSelected -= Handler;

            SelectedShell = result;
        }
    }

    /// <summary>Ждёт когда наперсток поднят и результат показан.</summary>
    public sealed class WaitForShellRevealed : TutorialStep
    {
        public Shell RevealedShell { get; private set; }
        public bool HasMarker { get; private set; }

        public override IEnumerator Run(MonoBehaviour runner)
        {
            bool done = false;
            void Handler(Shell shell, bool hasMarker)
            {
                RevealedShell = shell;
                HasMarker = hasMarker;
                done = true;
            }

            GameEvents.ShellRevealed += Handler;
            while (!done)
                yield return null;
            GameEvents.ShellRevealed -= Handler;
        }
    }

    /// <summary>Ждёт момент нанесения урона. После завершения доступны параметры удара.</summary>
    public sealed class WaitForDamageTaken : TutorialStep
    {
        private readonly TurnSide? _filterSide;

        public TurnSide Side { get; private set; }
        public int Amount { get; private set; }
        public int HealthAfter { get; private set; }
        public int MaxHealth { get; private set; }
        public bool Died { get; private set; }

        /// <param name="filterSide">Если задано — шаг ждёт урон именно этой стороне, остальные удары игнорирует.</param>
        public WaitForDamageTaken(TurnSide? filterSide = null) => _filterSide = filterSide;

        public override IEnumerator Run(MonoBehaviour runner)
        {
            bool done = false;
            void Handler(TurnSide side, int amount, int currentHealth, int maxHealth, bool died)
            {
                if (_filterSide.HasValue && side != _filterSide.Value)
                    return;

                Side = side;
                Amount = amount;
                HealthAfter = currentHealth;
                MaxHealth = maxHealth;
                Died = died;
                done = true;
            }

            GameEvents.DamageTaken += Handler;
            while (!done)
                yield return null;
            GameEvents.DamageTaken -= Handler;
        }
    }

    /// <summary>Ждёт смену активной стороны (переход хода).</summary>
    public sealed class WaitForActiveSideChanged : TutorialStep
    {
        public TurnSide NewSide { get; private set; }

        public override IEnumerator Run(MonoBehaviour runner)
        {
            Debug.Log("[Tutorial] WaitForActiveSideChanged: начал ждать ActiveSideChanged");
            bool done = false;
            void Handler(TurnSide side)
            {
                Debug.Log($"[Tutorial] WaitForActiveSideChanged: поймал ActiveSideChanged, side={side}");
                NewSide = side;
                done = true;
            }

            GameEvents.ActiveSideChanged += Handler;
            while (!done)
                yield return null;
            GameEvents.ActiveSideChanged -= Handler;
            Debug.Log("[Tutorial] WaitForActiveSideChanged: завершён");
        }
    }

    /// <summary>
    /// Ждёт один обмен наперстков (OnCupSwap) во время перемешивания.
    /// ShuffleSystem кидает GameEvents.RaiseCupSwapPerformed(slotA, slotB)
    /// на каждый свап — предполагается, что событие называется
    /// GameEvents.CupSwapPerformed (Action&lt;int,int&gt;), по аналогии с
    /// остальными Raise*/событие-парами в проекте. Если у вас событие
    /// называется иначе — поправьте имя ниже, логика не изменится.
    ///
    /// Используется по одному экземпляру на каждый нужный свап — например,
    /// чтобы озвучить реплику именно после ПЕРВОГО обмена, поставьте
    /// .Wait(new WaitForCupSwap()) один раз в сценарии; для второго обмена —
    /// ещё один новый экземпляр и т.д. (см. TutorialScript_Level0, сцена 3).
    /// </summary>
    public sealed class WaitForCupSwap : TutorialStep
    {
        public int SlotA { get; private set; }
        public int SlotB { get; private set; }

        public override IEnumerator Run(MonoBehaviour runner)
        {
            bool done = false;
            void Handler(int slotA, int slotB)
            {
                SlotA = slotA;
                SlotB = slotB;
                done = true;
            }

            GameEvents.CupSwapPerformed += Handler;
            while (!done)
                yield return null;
            GameEvents.CupSwapPerformed -= Handler;
        }
    }
}