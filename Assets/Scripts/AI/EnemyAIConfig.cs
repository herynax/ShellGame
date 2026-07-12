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

        [Header("Ошибка на финальном выборе — Perror(D) = max(Pmin, Pbase - k*D)")]
        public float DecisionErrorPbase = 0.3f;
        public float DecisionErrorPmin = 0.05f;
        public float DecisionErrorK = 0.05f;

        [Header("\"Раздумье\" перед атакой, сек — DecisionDelay(D) = max(Min, Base - k*D)")]
        public float DecisionDelayBase = 1.2f;
        public float DecisionDelayMin = 0.3f;
        public float DecisionDelayK = 0.08f;

        public float EvaluateTrackingLossProbability(float difficultyIndex) =>
            Mathf.Max(TrackingPmin, TrackingPbase - TrackingK * difficultyIndex);

        public float EvaluateDecisionErrorProbability(float difficultyIndex) =>
            Mathf.Max(DecisionErrorPmin, DecisionErrorPbase - DecisionErrorK * difficultyIndex);

        public float EvaluateDecisionDelay(float difficultyIndex) =>
            Mathf.Max(DecisionDelayMin, DecisionDelayBase - DecisionDelayK * difficultyIndex);
    }
}
