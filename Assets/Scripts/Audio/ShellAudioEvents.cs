using FMODUnity;
using UnityEngine;
using UnityEngine.Serialization;

namespace ShellGame.Audio
{
    /// <summary>
    /// Набор FMOD-событий, связанных с наперстком. Вынесено в ScriptableObject,
    /// чтобы звук назначался в инспекторе (дизайнером/саунд-дизайнером), а не
    /// хардкодился строковыми путями в коде.
    /// </summary>
    [CreateAssetMenu(fileName = "ShellAudioEvents", menuName = "ShellGame/Audio/Shell Audio Events")]
    public sealed class ShellAudioEvents : ScriptableObject
    {
        [Header("Раздача наперстков в начале раунда")]
        public EventReference Deal;

        [Header("Наведение курсором на наперсток")]
        public EventReference Hover;

        [Header("Выбор наперстка игроком")]
        public EventReference Select;

        [Header("Начало подъёма наперстка (звук лифта вверх) — играется сразу в момент старта анимации и фейдится при достижении пика")]
        [FormerlySerializedAs("Reveal")]
        [FormerlySerializedAs("RevealLift")]
        public EventReference RevealStart;

        [Header("Наперсток полностью опустился, показ содержимого закончен")]
        [FormerlySerializedAs("RevealDescription")]
        public EventReference RevealEnd;

        [Header("Перемещение наперстка во время шаффла")]
        public EventReference ShuffleMove;

        [Header("Под наперстком пусто")]
        public EventReference RevealEmpty;

        [Header("Под наперстком метка")]
        public EventReference RevealMarked;
    }
}