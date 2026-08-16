using System.Collections;
using UnityEngine;

namespace ShellGame.Tutorial
{
    /// <summary>
    /// Базовый строительный блок сценария обучения. Каждый шаг — это
    /// корутина, которая делает своё дело и завершается, когда шаг "исполнен"
    /// (реплика показана и закрыта, событие поймано, тайминг истёк и т.д.).
    ///
    /// runner нужен только для того, чтобы составные шаги (см. Parallel)
    /// могли запускать вложенные корутины через StartCoroutine — без него
    /// пришлось бы городить синглтон.
    /// </summary>
    public abstract class TutorialStep
    {
        public abstract IEnumerator Run(MonoBehaviour runner);
    }
}
