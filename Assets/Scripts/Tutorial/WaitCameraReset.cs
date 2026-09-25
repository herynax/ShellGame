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
        private readonly float _maxWaitSeconds;

        /// <param name="extraPause">Дополнительная задержка в секундах перед следующей репликой (опционально)</param>
        /// <param name="maxWaitSeconds">Страховка: сколько максимум ждать отзум (0 = ждать бесконечно, как раньше)</param>
        public WaitCameraReset(float extraPause = 0f, float maxWaitSeconds = 0f)
        {
            _extraPause = extraPause;
            _maxWaitSeconds = maxWaitSeconds;
        }

        public override IEnumerator Run(MonoBehaviour runner)
        {
            // Ждём, пока DialogueCameraFeedback полностью вернёт FOV камеры
            // в исходное состояние после закрытия реплики — это и есть отзум
            // между предложениями врага.
            float waited = 0f;
            while (DialogueCameraFeedback.IsZoomActive)
            {
                if (_maxWaitSeconds > 0f && waited >= _maxWaitSeconds)
                    break;

                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_extraPause > 0f)
            {
                yield return new WaitForSeconds(_extraPause);
            }
        }
    }
}