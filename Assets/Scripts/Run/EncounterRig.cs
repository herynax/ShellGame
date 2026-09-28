using ShellGame.Feedback;
using Unity.Cinemachine;
using UnityEngine;

namespace ShellGame.Run
{
    public sealed class EncounterRig : MonoBehaviour
    {
        public EnemyDamageFeedback EnemyFeedback;
        public EnemyLookController EnemyLook;

        [Tooltip("Камера, которая смотрит на умирающего врага (раньше enemyCamera в PlayerDamageFeedback)")]
        public CinemachineCamera EnemyCamera;
    }
}