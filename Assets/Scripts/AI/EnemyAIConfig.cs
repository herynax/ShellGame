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
        public float TrackingPbase = 0.35f;
        public float TrackingPmin = 0.05f;
        public float TrackingK = 0.05f;

        [Header("Баланс точности врага: на низкой сложности ~30% верных выборов, к 5 уровню ~75%")]
        public float MinCorrectChance = 0.30f;
        public float MaxCorrectChance = 0.75f;
        public float DifficultyForMaxCorrect = 5f;

        [Header("\"Раздумье\" перед атакой, сек — DecisionDelay(D) = max(Min, Base - k*D)")]
        public float DecisionDelayBase = 1.2f;
        public float DecisionDelayMin = 0.3f;
        public float DecisionDelayK = 0.08f;

        [Header("Штраф точности от собственного HP врага (симметрично 'поплывшему' экрану игрока от дозы)")]
        [Tooltip("Доля ПОТЕРЯННОГО HP (0..1), начиная с которой враг начинает терять точность. По умолчанию 0.5 — как порог, с которого у игрока включается шумовой джиттер.")]
        public float HealthPenaltyStartLostFraction = 0.5f;
        [Tooltip("Максимум, на который снижается шанс верного выбора при HP = 0 (вычитается из шанса верного выбора, 0..1).")]
        public float HealthPenaltyMaxReduction = 0.4f;
        [Tooltip("Степень кривой нарастания штрафа после порога — как power curve хроматической аберрации у игрока. 1 = линейно, >1 = штраф резче нарастает ближе к нулю HP.")]
        public float HealthPenaltyCurvePower = 2f;
        [Tooltip("Нижняя граница итогового шанса верного выбора — не даём точности упасть до нуля даже при HP=0 и максимальном штрафе.")]
        public float MinCorrectChanceFloor = 0.05f;

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

        public float EvaluateDecisionErrorProbability(float difficultyIndex, float enemyHealthFraction01 = 1f)
        {
            float correctChance = EvaluateCorrectChoiceProbability(difficultyIndex);
            correctChance -= EvaluateHealthAccuracyPenalty(enemyHealthFraction01);
            correctChance = Mathf.Clamp(correctChance, MinCorrectChanceFloor, 1f);
            return 1f - correctChance;
        }

        public float EvaluateDecisionDelay(float difficultyIndex) =>
            Mathf.Max(DecisionDelayMin, DecisionDelayBase - DecisionDelayK * difficultyIndex);
    }
}