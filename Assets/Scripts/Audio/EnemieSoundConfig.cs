using FMODUnity;
using UnityEngine;

namespace ShellGame.Audio
{
    /// <summary>
    /// Централизованный конфиг звуков врага: инъекция, получение урона,
    /// смерть, реакция на урон. Назначается в инспекторе (дизайнером /
    /// саунд-дизайнером) без пересборки скриптов.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemySoundConfig", menuName = "ShellGame/Audio/Enemy Sound Config")]
    public sealed class EnemySoundConfig : ScriptableObject
    {
        [Header("Звук инъекции (2D)")]
        [Tooltip("Звук укола — проигрывается 2D при получении врагом иньекции.")]
        public EventReference injectionSound;

        [Header("Звук получения урона (3D)")]
        [Tooltip("Звук получения урона — проигрывается 3D от позиции врага при попадании.")]
        public EventReference damageSound;

        [Header("Звук смерти (3D)")]
        [Tooltip("Звук смерти врага — проигрывается 3D при уничтожении.")]
        public EventReference deathSound;
    }
}
