using UnityEngine;

namespace ShellGame.AI
{
    /// <summary>
    /// Параметры поведения противника. Все три величины считаются одной и
    /// той же формой формулы из ГДД: f(D) = max(Min, Base - k*D), где D —
    /// индекс сложности (RoundParameters.DifficultyIndex).
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyAIConfig", menuName = "ShellGame/AI/Enemy AI Config")]
    public sealed class EnemyAIConfig : ScriptableObject
    {
        [Header("Потеря отслеживания метки при обмене — Plose(D) = max(Pmin, Pbase - k*D)")]
        public float TrackingPbase = 0.5f;
        public float TrackingPmin = 0.05f;
        public float TrackingK = 0.0089f;

        [Header("Баланс точности врага: на старте рана ~25% верных выборов, к финалу ~75%")]
        public float MinCorrectChance = 0.25f;
        public float MaxCorrectChance = 0.75f;
        [Tooltip("При каком индексе сложности точность выходит на MaxCorrectChance. Должно совпадать с DifficultyAtRunEnd в RunDifficultyConfig, иначе кривая выгорает раньше конца рана.")]
        public float DifficultyForMaxCorrect = 45f;

        [Header("\"Раздумье\" перед атакой, сек — DecisionDelay(D) = max(Min, Base - k*D)")]
        public float DecisionDelayBase = 1.7f;
        public float DecisionDelayMin = 0.474f;
        public float DecisionDelayK = 0.02724f;

        [Header("Раздумье перед использованием предмета")]
        [Min(0f)]
        public float ItemUseThinkingDuration = 1f;

        [Header("Штраф точности от собственного HP врага (симметрично 'поплывшему' экрану игрока от дозы)")]
        [Tooltip("Доля ПОТЕРЯННОГО HP (0..1), начиная с которой враг начинает терять точность. По умолчанию 0.5 — как порог, с которого у игрока включается шумовой джиттер.")]
        public float HealthPenaltyStartLostFraction = 0.5f;
        [Tooltip("Максимум, на который снижается шанс верного выбора при HP = 0 (вычитается из шанса верного выбора, 0..1).")]
        public float HealthPenaltyMaxReduction = 0.3f;
        [Tooltip("Степень кривой нарастания штрафа после порога — как power curve хроматической аберрации у игрока. 1 = линейно, >1 = штраф резче нарастает ближе к нулю HP.")]
        public float HealthPenaltyCurvePower = 2f;
        [Tooltip("Нижняя граница итогового шанса верного выбора — не даём точности упасть до нуля даже при HP=0 и максимальном штрафе.")]
        public float MinCorrectChanceFloor = 0.05f;

        [Header("Дополнительное упрощение первого уровня")]
        [Tooltip("Множитель шанса правильного выбора врага на первом уровне. 0.5 делает врага примерно вдвое менее точным.")]
        [Range(0f, 1f)] public float FirstLevelCorrectChanceMultiplier = 0.5f;

        [Header("Спайк сложности для особых врагов")]
        [Tooltip("Множитель индекса сложности для этого врага. Накладывается на базовую линию рана в момент ObserveMarkers. 1 = враг идёт ровно по линии, >1 = сложнее линии. Поставь врагу-минибоссу 1.12, финальному боссу 1.2.")]
        [Range(0.1f, 3f)] public float DifficultyMultiplier = 1f;
        [Tooltip("Плоская прибавка к индексу сложности для этого врага — удобно для спайка, когда множителя мало.")]
        public float DifficultyFlatBonus = 0f;

        /// <summary>
        /// Накладывает спайк этого врага на базовый индекс сложности рана.
        /// Вызывается один раз при входе в ObserveMarkers, дальше все Evaluate*
        /// в EnemyAIController работают уже со спайкнутым значением.
        /// </summary>
        public float ApplyDifficultySpike(float baseDifficultyIndex) =>
            baseDifficultyIndex * DifficultyMultiplier + DifficultyFlatBonus;

        [Header("Здоровье этого энкаунтера")]
        [Tooltip("Множитель максимального HP врага. Обычный враг — 1. Минибосс — 1.6, финальный босс — 1.8. " +
                 "Отдельно от HP-кривой рана (HealthProgressionConfig): та задаёт базовое HP обычного врага, " +
                 "а этот множитель делает особый бой живучее без раздувания всей линии.")]
        [Min(1f)] public float HealthMultiplier = 1f;
        [Tooltip("Плоская прибавка к HP врага после умножения.")]
        public int HealthFlatBonus = 0;

        public int ApplyHealthMultiplier(int baseHealth) =>
            Mathf.Max(1, Mathf.RoundToInt(baseHealth * HealthMultiplier) + HealthFlatBonus);

        /// <summary>
        /// Рантайм-копия конфига для наложения пресета сложности. Оригинальный
        /// ассет на диске не мутируется — все правки идут в эту копию, которую
        /// EnemyAIController уничтожает вместе с ригом.
        /// </summary>
        public EnemyAIConfig CreateRuntimeClone()
        {
            var clone = CreateInstance<EnemyAIConfig>();
            clone.name = name + "_runtime";
            clone.hideFlags = HideFlags.HideAndDontSave;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(this), clone);
            return clone;
        }

        [Header("Использование предметов ИИ (по необходимости, без случайности)")]
        [Tooltip("Порог 'желательности' предмета (см. ItemDefinition.EvaluateEnemyDesire) на МИНИМАЛЬНОЙ сложности — выше него враг решает использовать предмет. Чем выше значение, тем терпеливее враг.")]
        public float ItemUseDesireThresholdEasy = 0.55f;
        [Tooltip("Порог на МАКСИМАЛЬНОЙ сложности — ниже, чем на лёгкой: опытный враг реагирует на возможность/угрозу раньше.")]
        public float ItemUseDesireThresholdHard = 0.15f;
        [Tooltip("При каком индексе сложности порог достигает 'сложного' значения.")]
        public float ItemUseDesireThresholdForHardAtDifficulty = 45f;

        public float EvaluateItemUseDesireThreshold(float difficultyIndex)
        {
            float normalized = Mathf.Clamp01(difficultyIndex / Mathf.Max(0.0001f, ItemUseDesireThresholdForHardAtDifficulty));
            return Mathf.Lerp(ItemUseDesireThresholdEasy, ItemUseDesireThresholdHard, normalized);
        }

        /// <summary>
        /// Штраф (0..HealthPenaltyMaxReduction), который нужно вычесть из
        /// шанса верного выбора при заданной доле HP врага. До порога
        /// HealthPenaltyStartLostFraction штраф = 0 — совпадает с тем, что
        /// у игрока шум/джиттер тоже включается только с ~50% дозы.
        /// </summary>
        public float EvaluateHealthAccuracyPenalty(float enemyHealthFraction01)
        {
            float lostFraction = 1f - Mathf.Clamp01(enemyHealthFraction01);
            if (lostFraction <= HealthPenaltyStartLostFraction)
                return 0f;

            float range = Mathf.Max(0.0001f, 1f - HealthPenaltyStartLostFraction);
            float t = Mathf.Clamp01((lostFraction - HealthPenaltyStartLostFraction) / range);
            t = Mathf.Pow(t, Mathf.Max(0.0001f, HealthPenaltyCurvePower));
            return t * HealthPenaltyMaxReduction;
        }

        public float EvaluateTrackingLossProbability(float difficultyIndex) =>
            Mathf.Max(TrackingPmin, TrackingPbase - TrackingK * difficultyIndex);

        public float EvaluateCorrectChoiceProbability(float difficultyIndex)
        {
            float normalized = Mathf.Clamp01(difficultyIndex / Mathf.Max(0.0001f, DifficultyForMaxCorrect));
            return Mathf.Lerp(MinCorrectChance, MaxCorrectChance, normalized);
        }

        public float EvaluateDecisionErrorProbability(float difficultyIndex, float enemyHealthFraction01 = 1f, bool isFirstLevel = false)
        {
            float correctChance = EvaluateCorrectChoiceProbability(difficultyIndex);
            if (isFirstLevel)
                correctChance *= FirstLevelCorrectChanceMultiplier;

            correctChance -= EvaluateHealthAccuracyPenalty(enemyHealthFraction01);
            correctChance = Mathf.Clamp(correctChance, MinCorrectChanceFloor, 1f);
            return 1f - correctChance;
        }

        public float EvaluateDecisionDelay(float difficultyIndex) =>
            Mathf.Max(DecisionDelayMin, DecisionDelayBase - DecisionDelayK * difficultyIndex);

        /// <summary>
        /// Итоговая пауза врага перед атакой с учётом множителей скорости,
        /// которые живут на EnemyAIController (_decisionSpeedMultiplier 0.9 и
        /// _noItemDecisionSpeedMultiplier 0.75). Раньше эти множители прятались
        /// в коде контроллера и в таблицу баланса не попадали.
        /// </summary>
        public float EvaluateDecisionDelay(float difficultyIndex, float decisionSpeedMultiplier, float noItemDecisionSpeedMultiplier)
        {
            float multiplier = Mathf.Max(0.01f, decisionSpeedMultiplier)
                             * Mathf.Max(0.01f, noItemDecisionSpeedMultiplier);
            return EvaluateDecisionDelay(difficultyIndex) * multiplier;
        }

        /// <summary>
        /// Доля меток, которые враг до сих пор отслеживает после n обменов:
        /// T = (1 - Plose)^n. Умножается на точность при подсчёте вероятности
        /// попадания — это и есть основной ограничитель силы врага.
        /// </summary>
        public float EvaluateTrackedKnowledgeFraction(float difficultyIndex, int swapCount)
        {
            if (swapCount <= 0) return 1f;
            return Mathf.Pow(1f - EvaluateTrackingLossProbability(difficultyIndex), swapCount);
        }
    }
}