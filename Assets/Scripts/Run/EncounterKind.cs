using ShellGame.AI;
using ShellGame.Dialogue;
using UnityEngine;

namespace ShellGame.Run
{
    public enum EncounterKind { Tutorial, Enemy, Boss }

    [CreateAssetMenu(fileName = "Encounter", menuName = "ShellGame/Run/Encounter Definition")]
    public sealed class EncounterDefinition : ScriptableObject
    {
        [Tooltip("Тот же Id, что в MapNode.ForcedEncounterId (Fish, Wrath...)")]
        public string Id;
        public EncounterKind Kind = EncounterKind.Enemy;

        [Tooltip("Префаб окружения/врага с компонентом EncounterRig на корне")]
        public GameObject RigPrefab;

        [Tooltip("Поведение этого врага")]
        public EnemyAIConfig EnemyAIConfig;

        [Tooltip("Реплики этого врага. Пусто — враг молчит (например, в обучении).")]
        public EnemyReactionConfig ReactionConfig;

        [Tooltip("Группа лоадинг-типов. Пусто = берётся Id.")]
        public string LoadingTipsGroup;

        public string TipsGroup => string.IsNullOrEmpty(LoadingTipsGroup) ? Id : LoadingTipsGroup;
    }
}