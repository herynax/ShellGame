using System.Collections;

namespace ShellGame.Tutorial
{
    /// <summary>
    /// Интерфейс UI, который умеет показывать реплики обучения.
    /// Реализуется DialogueView и регистрируется в ServiceLocator — так шаг
    /// Say не зависит от конкретного UI-префаба (тот же принцип, что
    /// IAudioService / IShellPoolService в RoundGenerator).
    /// </summary>
    public interface IDialogueService
    {
        IEnumerator ShowLine(DialogueLine line);
    }
}
