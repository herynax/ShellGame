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
        public float ShuffleMoveDurationBase = 0.6f;
        [Tooltip("Минимальная длительность одного перемещения. Ниже этого значения скорость уже не будет уменьшаться.")]
        public float ShuffleMoveDurationMin = 0.24f;
        [Tooltip("На сколько секунд сокращать длительность каждого следующего раунда.")]
        public float ShuffleRoundReduction = 0.0043f;
        [Tooltip("На сколько секунд сокращать длительность при переходе на следующий уровень.")]
        public float ShuffleLevelReduction = 0.0028f;
        public Ease ShuffleEase = Ease.InOutSine;
        [Tooltip("Множитель длительности перемещения наперстка ДЛЯ ХОДА ВРАГА (применяется поверх обычной длительности). 0.8 = враг перемешивает на 20% быстрее игрока. 1 = без разницы.")]
        [Range(0.05f, 1f)]
        public float EnemyShuffleSpeedMultiplier = 0.8f;
        [Tooltip("Минимальная длительность одного перемещения ДЛЯ ХОДА ВРАГА — отдельный порог, не тот же, что у игрока. Враг всегда быстрее игрока, но на высокой сложности перемешивание не должно вырождаться в мгновенные телепорты: длительность упрётся в этот порог и больше не упадёт.")]
        [Range(0.05f, 1f)]
        public float EnemyShuffleMoveDurationMin = 0.16f;

        [Header("Число обменов при перемешивании")]
        [Tooltip("Сколько обменов делает ИГРОК в начале рана.")]
        [Min(0)] public int SwapCountBase = 4;
        [Tooltip("На сколько обменов растёт число обменов игрока на единицу индекса сложности.")]
        [Min(0f)] public float SwapCountPerDifficulty = 0.111f;
        [Tooltip("Нижняя граница числа обменов игрока.")]
        [Min(0)] public int SwapCountMin = 3;
        [Tooltip("Верхняя граница числа обменов игрока.")]
        [Min(1)] public int SwapCountMax = 9;
        [Tooltip("Сколько обменов делает ВРАГ. Держим фиксированным намеренно: если растить обмены и врагу, он начнёт терять нужную метку (T = (1-Plose)^n) и станет СЛАБЕЕ — две оси сложности сражались бы друг с другом. Сила врага ведётся через Plose и точность, число обменов игрока — это нагрузка на память.")]
        [Min(0)] public int EnemySwapCount = 5;

        [Header("Пауза между обменами")]
        [Tooltip("Пауза между соседними обменами во время перемешивания, сек.")]
        [Min(0f)] public float BetweenSwapDelay = 0.1f;

        [Header("Наведение курсора")]
        public float HoverScale = 1.05f;
        public float HoverTweenDuration = 0.12f;
        [Tooltip("Префаб ауры, который появляется под наперстком при наведении курсора.")]
        public GameObject HoverAuraPrefab;

        [Header("Спавн из пула")]
        public float SpawnScaleDuration = 0.25f;
        public Ease SpawnEase = Ease.OutBack;

        [Header("Звук")]
        public ShellAudioEvents AudioEvents;

        // ============================================================
        //  Единый источник истины для окна баланса и таблицы.
        //  ShuffleSystem вызывает ровно эти методы, поэтому графики в
        //  Docs/BalanceModel.tsv и в Assets/Editor/Balance/BalanceWindow.cs
        //  считаются той же формулой, что и игра.
        // ============================================================

        /// <summary>
        /// Суммарный коэффициент ускорения перемешивания от сложности.
        /// Сложность может вычесть больше базы — не даём уйти в минус.
        /// </summary>
        public float EvaluateShuffleReducedDuration(float difficultyIndex)
        {
            float difficultyReduction = (ShuffleRoundReduction + ShuffleLevelReduction)
                * Mathf.Max(0f, difficultyIndex);
            return Mathf.Max(0f, ShuffleMoveDurationBase - difficultyReduction);
        }

        /// <summary>
        /// Длительность одного перемещения наперстка, сек.
        /// У игрока и врага разные нижние пороги, и враг дополнительно
        /// перемешивает быстрее.
        /// </summary>
        public float EvaluateShuffleMoveDuration(float difficultyIndex, bool isEnemyTurn, float moveDurationMultiplier = 1f)
        {
            float duration = EvaluateShuffleReducedDuration(difficultyIndex) * Mathf.Max(0f, moveDurationMultiplier);

            if (isEnemyTurn)
            {
                duration *= Mathf.Clamp(EnemyShuffleSpeedMultiplier, 0.05f, 1f);
                return Mathf.Max(Mathf.Max(0.01f, EnemyShuffleMoveDurationMin), duration);
            }

            return Mathf.Max(ShuffleMoveDurationMin, duration);
        }

        /// <summary>
        /// Число обменов при перемешивании: растёт вместе со сложностью
        /// только у игрока (нагрузка на память), у врага фиксировано.
        /// </summary>
        public int EvaluateShuffleSwapCount(float difficultyIndex, bool isEnemyTurn)
        {
            if (isEnemyTurn)
                return Mathf.Max(0, EnemySwapCount);

            float raw = SwapCountBase + SwapCountPerDifficulty * Mathf.Max(0f, difficultyIndex);
            return Mathf.Clamp(Mathf.RoundToInt(raw), SwapCountMin, SwapCountMax);
        }
    }
}