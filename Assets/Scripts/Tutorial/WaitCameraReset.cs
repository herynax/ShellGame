using System.Collections;
using UnityEngine;

namespace ShellGame.Tutorial
{
    /// <summary>
    /// Шаг сценария, который ждёт, пока DialogueCameraFeedback полностью 
    /// вернёт FOV камеры в исходное состояние после закрытия реплики.
    /// </summary>
    public sealed class WaitCameraReset : TutorialStep
    {
        private readonly float _extraPause;

        /// <param name="extraPause">Дополнительная задержка в секундах перед следующей репликой (опционально)</param>
        public WaitCameraReset(float extraPause = 0f)
        {
            _extraPause = extraPause;
        }

        public override IEnumerator Run(MonoBehaviour runner)
        {
            // Ждём, пока DialogueCameraFeedback закроет зум
            while (DialogueCameraFeedback.IsZoomActive)
            {
                yield return null;
            }

            if (_extraPause > 0f)
            {
                yield return new WaitForSeconds(_extraPause);
            }
        }
    }
}