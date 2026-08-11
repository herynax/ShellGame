using DG.Tweening;
using ShellGame.Audio;
using UnityEngine;

namespace ShellGame.Shells
{
    /// <summary>
    /// Настройки поведения наперстка (тайминги, easing, звук), вынесенные из
    /// кода в ассет — позволяет геймдизайнеру подкручивать "ощущение" без
    /// пересборки скриптов, и даёт возможность позже сделать разные
    /// ShellConfig под разные скины/уровни сложности.
    /// </summary>
    [CreateAssetMenu(fileName = "ShellConfig", menuName = "ShellGame/Shells/Shell Config")]
    public sealed class ShellConfig : ScriptableObject
    {
        [Header("Подъём/показ метки")]
        public float LiftHeight = 0.35f;
        public float LiftDuration = 0.35f;
        public Ease LiftEase = Ease.OutBack;
        public float HoldRevealedDuration = 0.6f;

        [Header("Перемещение при перемешивании")]
        [Tooltip("Длительность одного перемещения наперстка в секундах. Это значение используется как базовое для первого раунда первого уровня.")]
        public float ShuffleMoveDurationBase = 0.22f;
        [Tooltip("Минимальная длительность одного перемещения. Ниже этого значения скорость уже не будет уменьшаться.")]
        public float ShuffleMoveDurationMin = 0.12f;
        [Tooltip("На сколько секунд сокращать длительность каждого следующего раунда.")]
        public float ShuffleRoundReduction = 0.01f;
        [Tooltip("На сколько секунд сокращать длительность при переходе на следующий уровень.")]
        public float ShuffleLevelReduction = 0.03f;
        public Ease ShuffleEase = Ease.InOutSine;

        [Header("Наведение курсора")]
        public float HoverScale = 1.05f;
        public float HoverTweenDuration = 0.12f;

        [Header("Спавн из пула")]
        public float SpawnScaleDuration = 0.25f;
        public Ease SpawnEase = Ease.OutBack;

        [Header("Звук")]
        public ShellAudioEvents AudioEvents;
    }
}
