using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace ShellGame.Audio
{
    /// <summary>
    /// Реализация IAudioService на FMOD Studio API.
    /// Сознательно НЕ используем Unity AudioSource: FMOD даёт миксер,
    /// параметры (для "тревожно-активной" музыки, снейпшоты на смерть/крит.
    /// здоровье и т.д.) и лучше ложится на WebGL по управлению памятью/потоками
    /// звука, чем множество разрозненных AudioSource-компонентов.
    /// </summary>
    public sealed class FMODAudioService : IAudioService
    {
        public void PlayOneShot(EventReference evt, Vector3 worldPosition)
        {
            if (evt.IsNull) return;
            RuntimeManager.PlayOneShot(evt, worldPosition);
        }

        public void PlayOneShot(EventReference evt)
        {
            if (evt.IsNull) return;
            RuntimeManager.PlayOneShot(evt);
        }

        public EventInstance CreateInstance(EventReference evt)
        {
            return RuntimeManager.CreateInstance(evt);
        }

        public void SetGlobalParameter(string name, float value)
        {
            RuntimeManager.StudioSystem.setParameterByName(name, value);
        }
    }
}
