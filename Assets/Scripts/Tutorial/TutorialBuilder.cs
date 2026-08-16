using System;
using System.Collections.Generic;
using System.Linq;

namespace ShellGame.Tutorial
{
    /// <summary>
    /// Удобный синтаксис для написания сценария без вложенной "простыни" из
    /// new Sequence(new Say(...), new DoAction(...), ...).
    ///
    /// var script = TutorialBuilder.Create()
    ///     .Say(line1)
    ///     .Say(line2)
    ///     .Do(() => button.Show())
    ///     .Wait(new WaitForEvent(h => GameEvents.RoundStartConfirmed += h,
    ///                            h => GameEvents.RoundStartConfirmed -= h))
    ///     .Build();
    /// </summary>
    public sealed class TutorialBuilder
    {
        private readonly List<TutorialStep> _steps = new List<TutorialStep>();

        public static TutorialBuilder Create() => new TutorialBuilder();

        public TutorialBuilder Say(DialogueLine line) { _steps.Add(new Say(line)); return this; }
        public TutorialBuilder Do(Action action) { _steps.Add(new DoAction(action)); return this; }
        public TutorialBuilder WaitSeconds(float seconds) { _steps.Add(new global::ShellGame.Tutorial.WaitSeconds(seconds)); return this; }
        public TutorialBuilder WaitUntil(Func<bool> condition) { _steps.Add(new global::ShellGame.Tutorial.WaitUntil(condition)); return this; }

        /// <summary>Добавляет произвольный готовый шаг — WaitForEvent, WaitForShellSelected, PlaySfx, CameraFocus и т.д.</summary>
        public TutorialBuilder Wait(TutorialStep step) { _steps.Add(step); return this; }

        /// <summary>
        /// Параллельная группа: каждая ветка собирается своим под-билдером.
        /// Пример: .Parallel(b => b.Say(line), b => b.Wait(new WaitForShellSelected()))
        /// — реплика и ожидание идут одновременно, дальше сценарий пойдёт,
        /// когда закончатся ОБЕ ветки.
        /// </summary>
        public TutorialBuilder Parallel(params Func<TutorialBuilder, TutorialBuilder>[] branches)
        {
            var parallelSteps = branches
                .Select(branch => branch(Create()).Build())
                .ToArray();

            _steps.Add(new Parallel(parallelSteps));
            return this;
        }

        public TutorialStep Build() => new Sequence(_steps.ToArray());
    }
}
