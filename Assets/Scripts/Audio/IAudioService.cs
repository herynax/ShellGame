using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace ShellGame.Audio
{
    /// <summary>
    /// Абстракция над FMOD, чтобы геймплейный код не работал с FMODUnity API
    /// напрямую и его было легко замокать в тестах / заменить бэкенд звука.
    /// </summary>
    public interface IAudioService
    {
        /// <summary>Одноразовый звук в 3D-точке (шаги, вырывание зуба у конкретного наперстка и т.п.).</summary>
        void PlayOneShot(EventReference evt, Vector3 worldPosition);

        /// <summary>Одноразовый 2D-звук (UI, клики меню, дилер раздаёт наперстки "в воздух").</summary>
        void PlayOneShot(EventReference evt);

        /// <summary>
        /// Создать управляемый инстанс события (для звуков, которые нужно
        /// стартовать/останавливать вручную — например, зацикленный гул толпы
        /// или звук перемешивания, длящийся весь этап шаффла).
        /// Не забыть Release() у возвращённого инстанса, когда он больше не нужен.
        /// </summary>
        EventInstance CreateInstance(EventReference evt);

        void SetGlobalParameter(string name, float value);
    }
}
