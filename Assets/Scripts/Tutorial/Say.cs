using System.Collections;
using ShellGame.Core;
using UnityEngine;

namespace ShellGame.Tutorial
{
    /// <summary>
    /// Шаг "показать реплику". Сам не знает про UI — берёт зарегистрированный
    /// IDialogueService из ServiceLocator (см. DialogueView) и ждёт, пока тот
    /// не закроет реплику (по клику/таймеру — см. DialogueLine).
    /// </summary>
    public sealed class Say : TutorialStep
    {
        private readonly DialogueLine _line;

        public Say(DialogueLine line) => _line = line;

        public override IEnumerator Run(MonoBehaviour runner)
        {
            if (!ServiceLocator.TryGet<IDialogueService>(out var dialogue))
            {
                Debug.LogWarning("Say: IDialogueService не зарегистрирован — добавьте DialogueView на сцену.");
                yield break;
            }

            yield return dialogue.ShowLine(_line);
        }
    }
}
