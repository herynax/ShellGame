using UnityEngine;

namespace ShellGame.Gameplay
{
    /// <summary>
    /// Формат рана и ось сложности для инкрементальной структуры
    /// «карта → бои → минибосс → новая карта → … → финальный босс».
    ///
    /// Раньше сложность считалась как L + 0.45·(CompletedRounds + t), где
    /// CompletedRounds копился на протяжении ВСЕГО рана и никогда не
    /// сбрасывался. Для рана из ~55 энкаунтеров это давало D > 45 уже в
    /// первой карте, и все кривые EnemyAIConfig/ShellConfig выгорали задолго
    /// до конца игры.
    ///
    /// Теперь сложность — функция позиции в ране:
    ///     p   = EncountersClearedInRun / EncountersTotal      ∈ [0,1]
    ///     D   = DifficultyAtRunEnd · p^RunRampGamma
    ///         + IntraEncounterRamp · (RoundsInEncounter / NominalRoundsPerEncounter)
    ///
    /// Плюсы: детерминировано (не зависит от того, сколько раундов длился
    /// бой), ровно одна линия на графике, и на неё накладываются спайки
    /// минибоссов/босса (см. EnemyAIConfig.DifficultyMultiplier).
    ///
    /// CurrentDifficultyIndex остаётся единственным числом, которое читает
    /// весь остальной код — поэтому потребителям (RoundProgressionConfig,
    /// ShellConfig) менять формулы не пришлось, перенастроены константы.
    /// </summary>
    [CreateAssetMenu(fileName = "RunDifficultyConfig", menuName = "ShellGame/Gameplay/Run Difficulty Config")]
    public sealed class RunDifficultyConfig : ScriptableObject
    {
        public const string ResourcesPath = "Configs/RunDifficultyConfig";

        [Header("--- Структура рана ---")]
        [Tooltip("Сколько карт в одном забеге.")]
        [Min(1)] public int MapsPerRun = 5;

        [Tooltip("Сколько боёв-узлов приходится на одну карту. В карте 10-15 узлов, часть из них магазин/ивент, поэтому берём меньше.")]
        [Min(1)] public int EncountersPerMap = 10;

        [Tooltip("Сколько минибоссов за ран (= MapsPerRun - 1).")]
        [Min(0)] public int MiniBossesPerRun = 4;

        [Header("--- Ось сложности ---")]
        [Tooltip("Индекс сложности в финале рана (p = 1). Все кривые EnemyAIConfig/ShellConfig выходят в пол примерно на этом значении.")]
        [Min(0f)] public float DifficultyAtRunEnd = 45f;

        [Tooltip("Показатель наклона всей линии. 1 = ровная линия. >1 = долгое плато в начале и резкий подъём к финалу.")]
        [Range(0.2f, 3f)] public float RunRampGamma = 1f;

        [Tooltip("Насколько сложность подрастает ВНУТРИ одного боя (прибавка к D к концу боя).")]
        [Min(0f)] public float IntraEncounterRamp = 3f;

        [Tooltip("Номинальное число раундов в бою — знаменатель для IntraEncounterRamp.")]
        [Range(1f, 30f)] public float NominalRoundsPerEncounter = 6f;

        [Header("--- Спайки по типу энкаунтера ---")]
        [Tooltip("Множитель сложности для элитного боя. Накладывается на базовую линию.")]
        [Min(0.1f)] public float EliteDifficultyMultiplier = 1.08f;
        [Tooltip("Плоская прибавка к D для элитного боя.")]
        public float EliteDifficultyFlatBonus = 1f;

        [Tooltip("Множитель сложности для минибосса. Накладывается на базовую линию.")]
        [Min(0.1f)] public float MiniBossDifficultyMultiplier = 1.12f;
        [Tooltip("Плоская прибавка к D для минибосса.")]
        public float MiniBossDifficultyFlatBonus = 2f;

        [Tooltip("Множитель сложности для финального босса.")]
        [Min(0.1f)] public float BossDifficultyMultiplier = 1.2f;
        [Tooltip("Плоская прибавка к D для финального босса.")]
        public float BossDifficultyFlatBonus = 3f;

        [Header("--- Служебное ---")]
        [Tooltip("Максимальный D, при котором все кривые уже вышли в пол. Используется окном баланса как правая граница графиков.")]
        [Min(1f)] public float DifficultySaturation = 60f;

        /// <summary>Сколько всего боёв в забеге: бои карт + минибоссы + финальный босс.</summary>
        public int EncountersTotal => Mathf.Max(1, MapsPerRun * EncountersPerMap + MiniBossesPerRun + 1);

        /// <summary>Доля пройденного рана ∈ [0,1].</summary>
        public float EvaluateRunProgress01(int encountersCleared) =>
            Mathf.Clamp01(encountersCleared / (float)EncountersTotal);

        /// <summary>Базовая линия сложности по доле пройденного рана (без внутрибоевого наклона и спайков).</summary>
        public float EvaluateBaseDifficulty(float runProgress01)
        {
            float gamma = Mathf.Max(0.0001f, RunRampGamma);
            return DifficultyAtRunEnd * Mathf.Pow(Mathf.Clamp01(runProgress01), gamma);
        }

        /// <summary>Базовая линия + наклон внутри текущего боя.</summary>
        public float EvaluateBaseDifficulty(int encountersCleared, float roundsInEncounter)
        {
            float ramp = 0f;
            if (NominalRoundsPerEncounter > 0.0001f && IntraEncounterRamp > 0f)
                ramp = IntraEncounterRamp * Mathf.Clamp01(roundsInEncounter / NominalRoundsPerEncounter);

            return EvaluateBaseDifficulty(EvaluateRunProgress01(encountersCleared)) + ramp;
        }

        /// <summary>
        /// То же, но конец оси D задаётся извне — используется пресетами
        /// сложности (DifficultyAtRunEnd берётся из пресета, а не из ассета).
        /// </summary>
        public float EvaluateBaseDifficulty(int encountersCleared, float roundsInEncounter, float difficultyAtRunEnd)
        {
            float ramp = 0f;
            if (NominalRoundsPerEncounter > 0.0001f && IntraEncounterRamp > 0f)
                ramp = IntraEncounterRamp * Mathf.Clamp01(roundsInEncounter / NominalRoundsPerEncounter);

            return EvaluateBaseDifficulty(EvaluateRunProgress01(encountersCleared), difficultyAtRunEnd) + ramp;
        }

        /// <summary>Базовая линия по доле рана с внешним концом оси D.</summary>
        public float EvaluateBaseDifficulty(float runProgress01, float difficultyAtRunEnd)
        {
            float gamma = Mathf.Max(0.0001f, RunRampGamma);
            return difficultyAtRunEnd * Mathf.Pow(Mathf.Clamp01(runProgress01), gamma);
        }

        /// <summary>
        /// Спайк сложности для минибосса/босса. Фактически значения живут в
        /// EnemyAIConfig.DifficultyMultiplier (у каждого энкаунтера свой
        /// конфиг), а этот метод нужен окну баланса и таблице, чтобы
        /// показать, где именно окажется D у босса.
        /// </summary>
        public float ApplyBossSpike(float baseDifficulty, bool isMiniBoss, bool isFinalBoss)
        {
            if (isFinalBoss)
                return baseDifficulty * BossDifficultyMultiplier + BossDifficultyFlatBonus;
            if (isMiniBoss)
                return baseDifficulty * MiniBossDifficultyMultiplier + MiniBossDifficultyFlatBonus;
            return baseDifficulty * EliteDifficultyMultiplier + EliteDifficultyFlatBonus;
        }

        /// <summary>Загрузка конфига из Resources. Возвращает null, если ассета нет.</summary>
        public static RunDifficultyConfig Load()
        {
            var config = Resources.Load<RunDifficultyConfig>(ResourcesPath);
            if (config == null)
                Debug.LogWarning($"[RunDifficultyConfig] Не найден Resources/{ResourcesPath} — сложность рана считается дефолтами (DEnd=45, 5 карт, 4 минибосса).");
            return config;
        }
    }
}
