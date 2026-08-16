using System.Collections;
using FMOD.Studio;
using FMODUnity;
using ShellGame.Audio;
using ShellGame.Core;
using UnityEngine;

namespace ShellGame.Tutorial
{
    /// <summary>
    /// Проигрывает разовый FMOD-звук — например, "вжух" на зум камеры или
    /// щелчок на подтверждение шага. Использует тот же IAudioService, что и
    /// весь остальной звук в игре (см. RoundGenerator._audio.PlayOneShot).
    /// </summary>
    public sealed class PlaySfx : TutorialStep
    {
        private readonly EventReference _evt;
        private readonly bool _wait;

        /// <param name="wait">
        /// Если true — шаг создаёт управляемый инстанс события и ждёт, пока
        /// он сам не остановится (PLAYBACK_STATE.STOPPED), прежде чем
        /// сценарий пойдёт дальше. Если false — просто "выстрелил и забыл".
        /// </param>
        public PlaySfx(EventReference evt, bool wait = false)
        {
            _evt = evt;
            _wait = wait;
        }

        public override IEnumerator Run(MonoBehaviour runner)
        {
            if (_evt.IsNull)
                yield break;

            if (!ServiceLocator.TryGet<IAudioService>(out var audio))
            {
                Debug.LogWarning("PlaySfx: IAudioService не зарегистрирован.");
                yield break;
            }

            if (!_wait)
            {
                audio.PlayOneShot(_evt);
                yield break;
            }

            var instance = audio.CreateInstance(_evt);
            instance.start();

            PLAYBACK_STATE state;
            do
            {
                yield return null;
                instance.getPlaybackState(out state);
            } while (state != PLAYBACK_STATE.STOPPED);

            instance.release();
        }
    }
}
