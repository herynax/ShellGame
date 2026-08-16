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

        public float EvaluateTrackingLossProbability(float difficultyIndex) =>
            Mathf.Max(TrackingPmin, TrackingPbase - TrackingK * difficultyIndex);

        public float EvaluateCorrectChoiceProbability(float difficultyIndex)
        {
            float normalized = Mathf.Clamp01(difficultyIndex / Mathf.Max(0.0001f, DifficultyForMaxCorrect));
            return Mathf.Lerp(MinCorrectChance, MaxCorrectChance, normalized);
        }

        public float EvaluateDecisionErrorProbability(float difficultyIndex)
        {
            float correctChance = EvaluateCorrectChoiceProbability(difficultyIndex);
            return 1f - correctChance;
        }

        public float EvaluateDecisionDelay(float difficultyIndex) =>
            Mathf.Max(DecisionDelayMin, DecisionDelayBase - DecisionDelayK * difficultyIndex);
    }
}
