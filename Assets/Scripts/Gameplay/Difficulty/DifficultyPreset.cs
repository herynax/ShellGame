using ShellGame.AI;
using ShellGame.Run;
using UnityEngine;

namespace ShellGame.Gameplay
{
    /// <summary>
    /// Пресет сложности рана. Задаёт рамки, в которых идёт весь забег:
    /// здоровье игрока/врага, точность врага, скорость забывания метки и
    /// ось сложности, а также бонусы особых боёв (минибосс/босс).
    ///
    /// Значения не трогают ассеты на диске: рантайм-копия EnemyAIConfig
    /// (см. EnemyAIConfig.CreateRuntimeClone) мутируется через ApplyTo.
    /// «Средний» повторяет текущий баланс игры.
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultyPreset", menuName = "ShellGame/Gameplay/Difficulty Preset")]
    public sealed class DifficultyPreset : ScriptableObject
    {
        [Header("Идентификация")]
        [Tooltip("Стабильный Id (Easy/Medium/Hard/Martyr). Хранится в чекпоинте.")]
        public string Id = "Medium";
        public string DisplayName = "Средний";
        [TextArea(2, 4)] public string Description;

        [Header("Здоровье (статично внутри рана)")]
        [Min(1)] public int PlayerHealth = 5;
        [Min(1)] public int EnemyHealth = 5;

        [Header("Точность врага: Min на старте рана -> Max к концу")]
        [Range(0f, 1f)] public float MinCorrectChance = 0.25f;
        [Range(0f, 1f)] public float MaxCorrectChance = 0.75f;

        [Header("Забывание метки — Plose(D) = max(Pmin, Pbase - k*D). Больше k = враг сильнее")]
        public float TrackingK = 0.0089f;

        [Header("Ось сложности: D в финале рана (p=1). Совпадает с точкой насыщения точности")]
        [Min(0f)] public float DifficultyAtRunEnd = 45f;

        [Header("Особые бои — HP врага")]
        [Min(1f)] public float MiniBossHealthMultiplier = 1.6f;
        [Min(1f)] public float BossHealthMultiplier = 1.8f;

        [Header("Особые бои — спайк индекса сложности D")]
        [Min(0.1f)] public float MiniBossSpikeMultiplier = 1.12f;
        public float MiniBossSpikeFlatBonus = 2f;
        [Min(0.1f)] public float BossSpikeMultiplier = 1.2f;
        public float BossSpikeFlatBonus = 3f;

        /// <summary>
        /// Накладывает пресет на рантайм-копию конфига врага. Для особых
        /// боёв подставляются HP-множитель и спайк из пресета, для обычного
        /// боя — нейтральные значения.
        /// </summary>
        public void ApplyTo(EnemyAIConfig config, EncounterKind kind)
        {
            if (config == null)
                return;

            config.MinCorrectChance = MinCorrectChance;
            config.MaxCorrectChance = MaxCorrectChance;
            config.TrackingK = TrackingK;
            config.DifficultyForMaxCorrect = DifficultyAtRunEnd;

            switch (kind)
            {
                case EncounterKind.Boss:
                    config.HealthMultiplier = BossHealthMultiplier;
                    config.HealthFlatBonus = 0;
                    config.DifficultyMultiplier = BossSpikeMultiplier;
                    config.DifficultyFlatBonus = BossSpikeFlatBonus;
                    break;

                case EncounterKind.MiniBoss:
                    config.HealthMultiplier = MiniBossHealthMultiplier;
                    config.HealthFlatBonus = 0;
                    config.DifficultyMultiplier = MiniBossSpikeMultiplier;
                    config.DifficultyFlatBonus = MiniBossSpikeFlatBonus;
                    break;

                default:
                    config.HealthMultiplier = 1f;
                    config.HealthFlatBonus = 0;
                    config.DifficultyMultiplier = 1f;
                    config.DifficultyFlatBonus = 0f;
                    break;
            }
        }
    }
}
